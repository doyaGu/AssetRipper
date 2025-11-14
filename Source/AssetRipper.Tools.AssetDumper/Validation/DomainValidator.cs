using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Validation.Models;
using Json.Schema;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZstdSharp;

namespace AssetRipper.Tools.AssetDumper.Validation;

/// <summary>
/// Lightweight domain-level validator that performs basic schema validation.
/// Designed for streaming validation during export with minimal memory overhead.
/// Performs only structural, data type, and constraint validation (first 3 phases).
/// </summary>
public sealed class DomainValidator
{
	private readonly Options _options;
	private readonly EvaluationOptions _evaluationOptions;
	private readonly List<ValidationError> _errors;
	private readonly List<ValidationWarning> _warnings;

	public DomainValidator(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_errors = new List<ValidationError>();
		_warnings = new List<ValidationWarning>();

		_evaluationOptions = new EvaluationOptions
		{
			OutputFormat = OutputFormat.List,
			RequireFormatValidation = true
		};
	}

	/// <summary>
	/// Validates a single domain export result against its schema.
	/// Performs streaming validation to minimize memory usage.
	/// </summary>
	/// <param name="domainResult">The domain export result to validate</param>
	/// <returns>Domain validation summary</returns>
	public async Task<DomainValidationSummary> ValidateAsync(DomainExportResult domainResult)
	{
		if (domainResult is null)
			throw new ArgumentNullException(nameof(domainResult));

		var stopwatch = Stopwatch.StartNew();
		_errors.Clear();
		_warnings.Clear();

		try
		{
			// Load the schema for this domain
			var schema = await LoadSchemaAsync(domainResult.SchemaPath);
			if (schema is null)
			{
				return CreateFailedSummary(domainResult, "Failed to load schema");
			}

			// Validate all data files in this domain
			long totalRecords = 0;
			var filesProcessed = new List<string>();

			foreach (var shard in domainResult.Shards)
			{
				var filePath = Path.Combine(_options.OutputPath, shard.Shard);

				if (!File.Exists(filePath))
				{
					AddError(domainResult, "MissingDataFile", $"Data file not found: {filePath}", 0);
					continue;
				}

				var recordsValidated = await ValidateDataFileAsync(schema, filePath, domainResult);
				totalRecords += recordsValidated;
				filesProcessed.Add(shard.Shard);

				// Check error threshold
				if (_options.MaxErrors > 0 && _errors.Count >= _options.MaxErrors)
				{
					Logger.Warning(LogCategory.Export,
						$"Reached maximum error limit ({_options.MaxErrors}) for {domainResult.TableId}");
					break;
				}
			}

			stopwatch.Stop();

			// Create summary
			return new DomainValidationSummary
			{
				Domain = domainResult.Domain,
				TableId = domainResult.TableId,
				Result = _errors.Count == 0 ?
					(_warnings.Count == 0 ? ValidationResult.Passed : ValidationResult.PassedWithWarnings) :
					ValidationResult.Failed,
				RecordsValidated = (int)totalRecords,
				ErrorCount = _errors.Count,
				WarningCount = _warnings.Count,
				SchemaPath = domainResult.SchemaPath,
				FilesProcessed = filesProcessed
			};
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Domain validation failed for {domainResult.TableId}: {ex.Message}");
			return CreateFailedSummary(domainResult, ex.Message);
		}
	}

	/// <summary>
	/// Loads a JSON schema from the schema path.
	/// </summary>
	private async Task<JsonSchema?> LoadSchemaAsync(string schemaRelativePath)
	{
		try
		{
			// Schema paths are relative to the output directory
			var schemaPath = Path.Combine(_options.OutputPath, schemaRelativePath);

			if (!File.Exists(schemaPath))
			{
				Logger.Warning(LogCategory.Export, $"Schema file not found: {schemaPath}");
				return null;
			}

			var schemaJson = await File.ReadAllTextAsync(schemaPath);
			return JsonSchema.FromText(schemaJson);
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to load schema {schemaRelativePath}: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Validates all records in a data file against the schema.
	/// Uses streaming approach to minimize memory usage.
	/// </summary>
	private async Task<long> ValidateDataFileAsync(JsonSchema schema, string filePath, DomainExportResult domainResult)
	{
		long recordCount = 0;
		long lineNumber = 0;

		try
		{
			// Determine if file is compressed
			var isCompressed = filePath.EndsWith(".zst", StringComparison.OrdinalIgnoreCase);

			Stream fileStream = File.OpenRead(filePath);

			try
			{
				// Decompress if needed
				if (isCompressed)
				{
					var decompressStream = new DecompressionStream(fileStream);
					fileStream = decompressStream;
				}

				using var reader = new StreamReader(fileStream);
				string? line;

				while ((line = await reader.ReadLineAsync()) != null)
				{
					lineNumber++;

					if (string.IsNullOrWhiteSpace(line))
						continue;

					try
					{
						// Parse JSON
						var jsonNode = JsonNode.Parse(line);
						if (jsonNode is null)
						{
							AddError(domainResult, "InvalidJson", $"Failed to parse JSON at line {lineNumber}", lineNumber, filePath);
							continue;
						}

						// Validate against schema (Phase 1-3: Structural, DataType, Constraint)
						var result = schema.Evaluate(jsonNode, _evaluationOptions);

						if (!result.IsValid)
						{
							ProcessValidationResult(result, domainResult, lineNumber, filePath);
						}

						recordCount++;

						// Periodic progress logging for large files
						if (_options.Verbose && recordCount % 10000 == 0)
						{
							Logger.Info(LogCategory.Export,
								$"Validated {recordCount:N0} records in {Path.GetFileName(filePath)} ({_errors.Count} errors)");
						}
					}
					catch (JsonException jsonEx)
					{
						AddError(domainResult, "JsonParseError",
							$"JSON parse error at line {lineNumber}: {jsonEx.Message}", lineNumber, filePath);
					}
					catch (Exception ex)
					{
						AddError(domainResult, "ValidationError",
							$"Validation error at line {lineNumber}: {ex.Message}", lineNumber, filePath);
					}

					// Check error threshold
					if (_options.MaxErrors > 0 && _errors.Count >= _options.MaxErrors)
					{
						break;
					}
				}
			}
			finally
			{
				await fileStream.DisposeAsync();
			}
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to validate file {filePath}: {ex.Message}");
			AddError(domainResult, "FileReadError", $"Failed to read file: {ex.Message}", 0, filePath);
		}

		return recordCount;
	}

	/// <summary>
	/// Processes a schema validation result and extracts errors.
	/// </summary>
	private void ProcessValidationResult(EvaluationResults result, DomainExportResult domainResult,
		long lineNumber, string filePath)
	{
		if (result.IsValid)
			return;

		// Extract validation errors from the result
		foreach (var detail in result.Details)
		{
			if (!detail.IsValid)
			{
				var errorType = DetermineErrorType(detail);
				var message = detail.Errors?.FirstOrDefault().Value ?? "Schema validation failed";

				var error = new ValidationError
				{
					ErrorType = errorType,
					Severity = ValidationSeverity.Error,
					Domain = domainResult.Domain,
					TableId = domainResult.TableId,
					FilePath = filePath,
					LineNumber = lineNumber,
					JsonPath = detail.InstanceLocation.ToString(),
					Message = message,
					RuleDescription = detail.SchemaLocation.ToString(),
					Category = "Schema"
				};

				_errors.Add(error);
			}
		}
	}

	/// <summary>
	/// Determines the error type from validation detail.
	/// </summary>
	private ValidationErrorType DetermineErrorType(EvaluationResults detail)
	{
		var schemaLocation = detail.SchemaLocation.ToString().ToLowerInvariant();

		if (schemaLocation.Contains("required"))
			return ValidationErrorType.MissingRequired;
		if (schemaLocation.Contains("type"))
			return ValidationErrorType.DataType;
		if (schemaLocation.Contains("pattern"))
			return ValidationErrorType.Pattern;
		if (schemaLocation.Contains("enum"))
			return ValidationErrorType.InvalidEnum;
		if (schemaLocation.Contains("format"))
			return ValidationErrorType.Format;
		if (schemaLocation.Contains("minimum") || schemaLocation.Contains("maximum"))
			return ValidationErrorType.Range;
		if (schemaLocation.Contains("minlength") || schemaLocation.Contains("maxlength"))
			return ValidationErrorType.Length;
		if (schemaLocation.Contains("uniqueitems"))
			return ValidationErrorType.Unique;
		if (schemaLocation.Contains("if") || schemaLocation.Contains("then") || schemaLocation.Contains("else"))
			return ValidationErrorType.Conditional;

		return ValidationErrorType.Structural;
	}

	/// <summary>
	/// Adds a validation error to the collection.
	/// </summary>
	private void AddError(DomainExportResult domainResult, string category, string message,
		long lineNumber, string? filePath = null)
	{
		var error = new ValidationError
		{
			ErrorType = ValidationErrorType.Structural,
			Severity = ValidationSeverity.Error,
			Domain = domainResult.Domain,
			TableId = domainResult.TableId,
			FilePath = filePath ?? string.Empty,
			LineNumber = lineNumber,
			Message = message,
			Category = category
		};

		_errors.Add(error);
	}

	/// <summary>
	/// Creates a failed summary for errors that occur before validation.
	/// </summary>
	private DomainValidationSummary CreateFailedSummary(DomainExportResult domainResult, string errorMessage)
	{
		return new DomainValidationSummary
		{
			Domain = domainResult.Domain,
			TableId = domainResult.TableId,
			Result = ValidationResult.Failed,
			RecordsValidated = 0,
			ErrorCount = 1,
			WarningCount = 0,
			SchemaPath = domainResult.SchemaPath,
			FilesProcessed = new List<string>()
		};
	}

	/// <summary>
	/// Gets the list of validation errors found.
	/// </summary>
	public IReadOnlyList<ValidationError> Errors => _errors.AsReadOnly();

	/// <summary>
	/// Gets the list of validation warnings found.
	/// </summary>
	public IReadOnlyList<ValidationWarning> Warnings => _warnings.AsReadOnly();
}
