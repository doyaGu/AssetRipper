using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Validation.Models;
using System.Diagnostics;
using System.Text.Json;

namespace AssetRipper.Tools.AssetDumper.Validation;

/// <summary>
/// Orchestrates multi-tier validation strategy for AssetDumper exports.
/// Coordinates domain-level and comprehensive validation with configurable error handling.
/// </summary>
public sealed class ValidationOrchestrator
{
	private readonly Options _options;
	private readonly List<DomainValidationSummary> _domainSummaries;
	private readonly List<ValidationError> _allErrors;
	private readonly List<ValidationWarning> _allWarnings;
	private readonly Stopwatch _globalStopwatch;
	private int _errorCount;

	public ValidationOrchestrator(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_domainSummaries = new List<DomainValidationSummary>();
		_allErrors = new List<ValidationError>();
		_allWarnings = new List<ValidationWarning>();
		_globalStopwatch = new Stopwatch();
		_errorCount = 0;
	}

	/// <summary>
	/// Performs domain-level validation for a single exported domain.
	/// This is a lightweight validation that can be called immediately after each domain export.
	/// </summary>
	/// <param name="domainResult">The domain export result to validate</param>
	/// <returns>Domain validation summary with errors and warnings</returns>
	public async Task<DomainValidationSummary> ValidateDomainAsync(DomainExportResult domainResult)
	{
		if (domainResult is null)
			throw new ArgumentNullException(nameof(domainResult));

		if (!_options.ValidateSchemas)
		{
			// Validation disabled, return empty summary
			return CreateEmptySummary(domainResult);
		}

		var stopwatch = Stopwatch.StartNew();

		try
		{
			Logger.Info(LogCategory.Export, $"Validating domain: {domainResult.TableId}");

			// Use DomainValidator for lightweight validation
			var domainValidator = new DomainValidator(_options);
			var summary = await domainValidator.ValidateAsync(domainResult);

			// Aggregate results
			_domainSummaries.Add(summary);
			_errorCount += summary.ErrorCount;

			// Check if we've exceeded the error threshold
			if (_options.MaxErrors > 0 && _errorCount > _options.MaxErrors)
			{
				var errorMsg = $"Exceeded maximum validation errors ({_options.MaxErrors}). Total errors: {_errorCount}";
				Logger.Error(LogCategory.Export, errorMsg);

				if (!_options.ContinueOnError)
				{
					throw new ValidationException(errorMsg);
				}
			}

			stopwatch.Stop();

			if (summary.ErrorCount > 0)
			{
				Logger.Warning(LogCategory.Export,
					$"Domain validation found {summary.ErrorCount} errors in {domainResult.TableId} ({stopwatch.Elapsed.TotalSeconds:F2}s)");
			}
			else
			{
				Logger.Info(LogCategory.Export,
					$"Domain validation passed for {domainResult.TableId} ({stopwatch.Elapsed.TotalSeconds:F2}s)");
			}

			return summary;
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Domain validation failed for {domainResult.TableId}: {ex.Message}");

			if (!_options.ContinueOnError)
			{
				throw;
			}

			// Return failed summary
			return new DomainValidationSummary
			{
				Domain = domainResult.Domain,
				TableId = domainResult.TableId,
				Result = ValidationResult.Failed,
				ErrorCount = 1,
				SchemaPath = domainResult.SchemaPath
			};
		}
	}

	/// <summary>
	/// Performs comprehensive validation across all domains with cross-table checks.
	/// This should be called after all domains have been exported.
	/// </summary>
	/// <param name="domainResults">All domain export results</param>
	/// <returns>Comprehensive validation report</returns>
	public async Task<ValidationReport> ValidateComprehensiveAsync(IEnumerable<DomainExportResult> domainResults)
	{
		if (domainResults is null)
			throw new ArgumentNullException(nameof(domainResults));

		var resultsList = domainResults.ToList();

		if (!_options.ValidateComprehensively)
		{
			// Comprehensive validation disabled, return aggregated domain results
			return GenerateAggregatedReport(resultsList);
		}

		_globalStopwatch.Restart();

		try
		{
			Logger.Info(LogCategory.Export, "Starting comprehensive validation with cross-table checks...");

			// Use SchemaValidator for comprehensive validation
			var schemaValidator = new SchemaValidator(_options);
			var comprehensiveReport = await schemaValidator.ValidateAllAsync(resultsList);

			_globalStopwatch.Stop();

			// Merge with existing domain summaries
			MergeDomainSummaries(comprehensiveReport);

			// Log results
			LogComprehensiveResults(comprehensiveReport);

			return comprehensiveReport;
		}
		catch (Exception ex)
		{
			_globalStopwatch.Stop();
			Logger.Error(LogCategory.Export, $"Comprehensive validation failed: {ex.Message}");

			if (!_options.ContinueOnError)
			{
				throw;
			}

			return new ValidationReport
			{
				OverallResult = ValidationResult.Failed,
				ValidationTime = _globalStopwatch.Elapsed,
				ErrorMessage = ex.Message,
				DomainSummaries = _domainSummaries,
				Errors = _allErrors,
				Warnings = _allWarnings
			};
		}
	}

	/// <summary>
	/// Saves the validation report to a JSON file.
	/// </summary>
	/// <param name="report">The validation report to save</param>
	/// <returns>Task representing the asynchronous operation</returns>
	public async Task SaveReportAsync(ValidationReport report)
	{
		if (report is null)
			throw new ArgumentNullException(nameof(report));

		var reportPath = _options.ValidationReportFilePath;

		try
		{
			// Ensure directory exists
			var directory = Path.GetDirectoryName(reportPath);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			// Serialize with pretty formatting
			var options = new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			};

			var json = JsonSerializer.Serialize(report, options);
			await File.WriteAllTextAsync(reportPath, json);

			Logger.Info(LogCategory.Export, $"Validation report saved to: {reportPath}");
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to save validation report: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Gets the current error count across all validated domains.
	/// </summary>
	public int ErrorCount => _errorCount;

	/// <summary>
	/// Gets whether the validation has failed based on error count.
	/// </summary>
	public bool HasFailed => _errorCount > 0;

	/// <summary>
	/// Generates a human-readable summary of the validation results.
	/// </summary>
	/// <param name="report">The validation report</param>
	/// <returns>Formatted summary string</returns>
	public static string GenerateSummary(ValidationReport report)
	{
		if (report is null)
			throw new ArgumentNullException(nameof(report));

		var summary = new System.Text.StringBuilder();
		summary.AppendLine("╔═══════════════════════════════════════════════════════╗");
		summary.AppendLine("║         VALIDATION SUMMARY                            ║");
		summary.AppendLine("╚═══════════════════════════════════════════════════════╝");
		summary.AppendLine();

		// Overall result
		summary.AppendLine($"Overall Result: {report.OverallResult}");
		summary.AppendLine($"Validation Time: {report.ValidationTime.TotalSeconds:F2}s");
		summary.AppendLine($"Total Records: {report.TotalRecordsValidated:N0}");
		summary.AppendLine($"Data Files: {report.DataFilesProcessed}");
		summary.AppendLine($"Schemas Loaded: {report.SchemasLoaded}");
		summary.AppendLine();

		// Error/Warning counts
		summary.AppendLine($"Errors: {report.Errors.Count}");
		summary.AppendLine($"Warnings: {report.Warnings.Count}");
		summary.AppendLine();

		// Domain summaries
		if (report.DomainSummaries.Any())
		{
			summary.AppendLine("Domain Results:");
			summary.AppendLine("─────────────────────────────────────────────────────");

			foreach (var domain in report.DomainSummaries.OrderBy(d => d.TableId))
			{
				var icon = domain.ErrorCount == 0 ? "✓" : "✗";
				summary.AppendLine($"  {icon} {domain.TableId,-40} Errors: {domain.ErrorCount,5}  Records: {domain.RecordsValidated,10:N0}");
			}

			summary.AppendLine();
		}

		// Performance metrics
		if (report.Metadata.Performance.RecordsPerSecond > 0)
		{
			summary.AppendLine("Performance:");
			summary.AppendLine($"  Records/sec: {report.Metadata.Performance.RecordsPerSecond:N0}");
			summary.AppendLine($"  Peak Memory: {report.Metadata.Performance.PeakMemoryUsageMB:F2} MB");
			summary.AppendLine();
		}

		// Top errors
		if (report.Errors.Any())
		{
			summary.AppendLine("Top Errors:");
			summary.AppendLine("─────────────────────────────────────────────────────");

			var topErrors = report.Errors
				.GroupBy(e => e.ErrorType)
				.OrderByDescending(g => g.Count())
				.Take(5);

			foreach (var group in topErrors)
			{
				summary.AppendLine($"  {group.Key,-30} Count: {group.Count(),5}");
			}
		}

		return summary.ToString();
	}

	private DomainValidationSummary CreateEmptySummary(DomainExportResult domainResult)
	{
		return new DomainValidationSummary
		{
			Domain = domainResult.Domain,
			TableId = domainResult.TableId,
			Result = ValidationResult.Passed,
			RecordsValidated = 0,
			ErrorCount = 0,
			WarningCount = 0,
			SchemaPath = domainResult.SchemaPath
		};
	}

	private ValidationReport GenerateAggregatedReport(List<DomainExportResult> domainResults)
	{
		_globalStopwatch.Stop();

		return new ValidationReport
		{
			OverallResult = _errorCount == 0 ? ValidationResult.Passed : ValidationResult.Failed,
			ValidationTime = _globalStopwatch.Elapsed,
			DomainSummaries = _domainSummaries,
			Errors = _allErrors,
			Warnings = _allWarnings,
			TotalRecordsValidated = _domainSummaries.Sum(s => s.RecordsValidated),
			DataFilesProcessed = domainResults.Sum(r => r.Shards.Count),
			SchemasLoaded = _domainSummaries.Select(s => s.SchemaPath).Distinct().Count()
		};
	}

	private void MergeDomainSummaries(ValidationReport comprehensiveReport)
	{
		// Update domain summaries with comprehensive results
		foreach (var existingSummary in _domainSummaries)
		{
			var comprehensiveSummary = comprehensiveReport.DomainSummaries
				.FirstOrDefault(s => s.TableId == existingSummary.TableId);

			if (comprehensiveSummary != null)
			{
				// Merge error counts (comprehensive validation may find additional errors)
				existingSummary.ErrorCount = Math.Max(existingSummary.ErrorCount, comprehensiveSummary.ErrorCount);
				existingSummary.WarningCount = Math.Max(existingSummary.WarningCount, comprehensiveSummary.WarningCount);
				existingSummary.Result = comprehensiveSummary.Result;
			}
		}

		// Add any new summaries from comprehensive validation
		foreach (var comprehensiveSummary in comprehensiveReport.DomainSummaries)
		{
			if (!_domainSummaries.Any(s => s.TableId == comprehensiveSummary.TableId))
			{
				_domainSummaries.Add(comprehensiveSummary);
			}
		}
	}

	private void LogComprehensiveResults(ValidationReport report)
	{
		if (report.OverallResult == ValidationResult.Passed || report.OverallResult == ValidationResult.PassedWithWarnings)
		{
			Logger.Info(LogCategory.Export,
				$"✓ Comprehensive validation PASSED ({report.ValidationTime.TotalSeconds:F2}s, {report.TotalRecordsValidated:N0} records)");
		}
		else
		{
			Logger.Error(LogCategory.Export,
				$"✗ Comprehensive validation FAILED with {report.Errors.Count} errors ({report.ValidationTime.TotalSeconds:F2}s)");
		}

		if (report.Warnings.Count > 0)
		{
			Logger.Warning(LogCategory.Export, $"Found {report.Warnings.Count} warnings");
		}
	}
}

/// <summary>
/// Exception thrown when validation fails and fail-fast mode is enabled.
/// </summary>
public class ValidationException : Exception
{
	public ValidationException(string message) : base(message)
	{
	}

	public ValidationException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
