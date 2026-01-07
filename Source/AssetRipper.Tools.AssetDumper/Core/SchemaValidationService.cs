using AssetRipper.Import.Logging;
using Json.Schema;
using System.Text.Json;
using ZstdSharp;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Writers;

namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Validates exported NDJSON tables against Draft 2020-12 JSON Schemas.
/// </summary>
internal sealed class SchemaValidationService
{
	private const int MaxReportedErrors = 50;
	private readonly Options _options;
	private readonly EvaluationOptions _evaluationOptions;

	public SchemaValidationService(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_evaluationOptions = new EvaluationOptions
		{
			OutputFormat = OutputFormat.List,
			RequireFormatValidation = false
		};
	}

	public bool Validate(IEnumerable<DomainExportResult> domainResults)
	{
		if (domainResults is null)
		{
			throw new ArgumentNullException(nameof(domainResults));
		}

		bool allValid = true;
		int reportedErrors = 0;

		foreach (DomainExportResult result in domainResults)
		{
			if (!string.Equals(result.Format, "ndjson", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string? schemaPath = ResolveSchemaPath(result.SchemaPath);
			if (schemaPath is null)
			{
				Logger.Warning(LogCategory.Export, $"Skipping schema validation for {result.TableId}: schema '{result.SchemaPath}' not found.");
				allValid = false;
				continue;
			}

			JsonSchema schema;
			try
			{
				schema = JsonSchema.FromFile(schemaPath);
			}
			catch (Exception ex)
			{
				Logger.Error(LogCategory.Export, $"Failed to load schema '{schemaPath}': {ex.Message}");
				allValid = false;
				continue;
			}

			bool resultValid = ValidateDomain(result, schema, ref reportedErrors);
			if (!resultValid)
			{
				allValid = false;
				if (reportedErrors >= MaxReportedErrors)
				{
					Logger.Warning(LogCategory.Export, "Maximum schema validation error limit reached; remaining issues suppressed.");
					break;
				}
			}
		}

		if (allValid)
		{
			if (!_options.Silent)
			{
				Logger.Info("Schema validation completed with no violations detected.");
			}
		}
		else
		{
			Logger.Error(LogCategory.Export, "Schema validation reported one or more issues.");
		}

		return allValid;
	}

	private bool ValidateDomain(DomainExportResult result, JsonSchema schema, ref int reportedErrors)
	{
		bool domainValid = true;

		if (result.HasShards)
		{
			foreach (ShardDescriptor shard in result.Shards)
			{
				if (reportedErrors >= MaxReportedErrors)
				{
					break;
				}

				string absolutePath = OutputPathHelper.ResolveAbsolutePath(_options.OutputPath, shard.Shard);
				bool shardValid = ValidateFile(schema, absolutePath, result.TableId, shard.Shard, shard.Compression, ref reportedErrors);
				if (!shardValid)
				{
					domainValid = false;
				}
			}
		}
		else if (!string.IsNullOrWhiteSpace(result.EntryFile))
		{
			string absolutePath = OutputPathHelper.ResolveAbsolutePath(_options.OutputPath, result.EntryFile);
			bool fileValid = ValidateFile(schema, absolutePath, result.TableId, result.EntryFile, "none", ref reportedErrors);
			if (!fileValid)
			{
				domainValid = false;
			}
		}
		else
		{
			Logger.Warning(LogCategory.Export, $"No output file recorded for {result.TableId}; skipping schema validation.");
		}

		return domainValid;
	}

	private bool ValidateFile(JsonSchema schema, string filePath, string tableId, string displayPath, string? compression, ref int reportedErrors)
	{
		if (!File.Exists(filePath))
		{
			Logger.Error(LogCategory.Export, $"Schema validation input missing for {tableId}: {filePath}");
			return false;
		}

		bool fileValid = true;
		long lineNumber = 0;

		try
		{
			if (!TryOpenReader(filePath, compression, tableId, displayPath, out StreamReader? reader))
			{
				return false;
			}

			using (reader)
			{
				string? line;
				while ((line = reader.ReadLine()) != null)
				{
					lineNumber++;
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}

					JsonDocument? doc;
					try
					{
						doc = JsonDocument.Parse(line);
					}
					catch (Exception ex)
					{
						ReportValidationError(tableId, displayPath, lineNumber, $"Invalid JSON: {ex.Message}", ref reportedErrors);
						fileValid = false;
						if (reportedErrors >= MaxReportedErrors)
						{
							break;
						}
						continue;
					}

					using (doc)
					{
						EvaluationResults evaluation = schema.Evaluate(doc.RootElement, _evaluationOptions);
						if (!evaluation.IsValid)
						{
							ReportValidationError(tableId, displayPath, lineNumber, "Schema validation failed.", ref reportedErrors);
							fileValid = false;
							if (reportedErrors >= MaxReportedErrors)
							{
								break;
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed schema validation on {filePath}: {ex.Message}");
			return false;
		}

		return fileValid;
	}

	private static bool TryOpenReader(string filePath, string? compression, string tableId, string displayPath, [NotNullWhen(true)] out StreamReader? reader)
	{
		reader = null;
		Stream baseStream;

		try
		{
			baseStream = File.OpenRead(filePath);
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to open shard for validation ({tableId} -> {displayPath}): {ex.Message}");
			return false;
		}

		string normalizedCompression = string.IsNullOrWhiteSpace(compression)
			? "none"
			: compression.Trim().ToLowerInvariant();

		try
		{
			Stream effectiveStream = normalizedCompression switch
			{
				"none" => baseStream,
				"zstd" or "zstd-seekable" => new DecompressionStream(baseStream),
				_ => throw new NotSupportedException($"Unsupported compression '{compression}'.")
			};

			reader = new StreamReader(effectiveStream);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to initialise validation stream ({tableId} -> {displayPath}): {ex.Message}");
			baseStream.Dispose();
			return false;
		}
	}

	private static void ReportValidationError(string tableId, string path, long lineNumber, string message, ref int reportedErrors)
	{
		Logger.Error(LogCategory.Export, $"[{tableId}] {path}:{lineNumber}: {message}");
		reportedErrors++;
	}

	private static string? ResolveSchemaPath(string schemaRelativePath)
	{
		if (string.IsNullOrWhiteSpace(schemaRelativePath))
		{
			return null;
		}

		string normalized = schemaRelativePath.Replace('/', Path.DirectorySeparatorChar);
		string[] segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length == 0)
		{
			return null;
		}

		foreach (string baseDirectory in EnumerateCandidateBaseDirectories())
		{
			string candidate = CombinePath(baseDirectory, segments);
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	private static IEnumerable<string> EnumerateCandidateBaseDirectories()
	{
		HashSet<string> yielded = new(StringComparer.OrdinalIgnoreCase);

		foreach (string directory in AscendDirectories(AppContext.BaseDirectory, 4))
		{
			if (yielded.Add(directory))
			{
				yield return directory;
			}
		}

		foreach (string directory in AscendDirectories(Environment.CurrentDirectory, 4))
		{
			if (yielded.Add(directory))
			{
				yield return directory;
			}
		}
	}

	private static IEnumerable<string> AscendDirectories(string? startDirectory, int limit)
	{
		string? current = startDirectory;
		for (int i = 0; i <= limit && !string.IsNullOrEmpty(current); i++)
		{
			yield return current;
			current = Path.GetDirectoryName(current);
		}
	}

	private static string CombinePath(string baseDirectory, string[] segments)
	{
		string path = baseDirectory;
		foreach (string segment in segments)
		{
			path = Path.Combine(path, segment);
		}
		return path;
	}
}
