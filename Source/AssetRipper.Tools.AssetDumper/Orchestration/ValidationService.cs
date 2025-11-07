using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Writers;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Provides validation and diagnostic logging for export results.
/// </summary>
public sealed class ValidationService
{
	private readonly Options _options;

	public ValidationService(Options options)
	{
		_options = options;
	}

	/// <summary>
	/// Logs export diagnostics including dataset summary.
	/// </summary>
	public void LogExportDiagnostics(List<DomainExportResult> domainResults)
	{
		if (_options.Silent || domainResults.Count == 0)
		{
			return;
		}

		Logger.Info("=== Export Dataset Summary ===");
		foreach (DomainExportResult result in domainResults
			.OrderBy(static r => r.TableId, StringComparer.OrdinalIgnoreCase))
		{
			long records = result.TotalRecords;
			long bytes = result.TotalBytes;
			int shards = result.Shards.Count;
			string location = shards > 0
				? $"{shards} shard(s)"
				: (!string.IsNullOrWhiteSpace(result.EntryFile)
					? OutputPathHelper.NormalizeRelativePath(result.EntryFile)
					: "no data");

			Logger.Info($"  {result.TableId}: {records:N0} records, {FormatBytes(bytes)} via {location}");
		}
		Logger.Info("=============================");
	}

	/// <summary>
	/// Validates that all shard files referenced in domain results exist on disk.
	/// </summary>
	public void ValidateShardOutputs(IEnumerable<DomainExportResult> domainResults)
	{
		if (_options.Silent)
		{
			return;
		}

		List<string> missing = new();
		foreach (DomainExportResult result in domainResults)
		{
			foreach (ShardDescriptor shard in result.Shards)
			{
				if (string.IsNullOrWhiteSpace(shard.Shard))
				{
					continue;
				}

				string absolutePath;
				try
				{
					absolutePath = OutputPathHelper.ResolveAbsolutePath(_options.OutputPath, shard.Shard);
				}
				catch
				{
					continue;
				}

				if (!File.Exists(absolutePath))
				{
					missing.Add(shard.Shard);
				}
			}
		}

		if (missing.Count == 0)
		{
			if (_options.Verbose)
			{
				Logger.Info("Validated all manifest-linked shard files on disk.");
			}
			return;
		}

		string detail = string.Join(", ", missing.Take(5));
		if (missing.Count > 5)
		{
			detail += $" ... (+{missing.Count - 5} more)";
		}
		Logger.Warning(LogCategory.Export, $"Missing shard files referenced by manifest: {detail}");
	}

	/// <summary>
	/// Performs JSON Schema validation if enabled.
	/// </summary>
	public bool ValidateSchemas(List<DomainExportResult> domainResults)
	{
		if (!_options.ValidateSchema || domainResults.Count == 0)
		{
			return true;
		}

		SchemaValidationService validator = new SchemaValidationService(_options);
		return validator.Validate(domainResults);
	}

	/// <summary>
	/// Logs detailed processing summary including directory statistics.
	/// </summary>
	public void LogProcessingSummary(TimeSpan totalTime)
	{
		if (!_options.Verbose)
		{
			return;
		}

		Logger.Info("=== Processing Summary ===");
		Logger.Info($"Total Time: {totalTime:mm\\:ss\\.fff}");
		Logger.Info($"Output Directory: {_options.OutputPath}");

		// Log output directory contents
		if (Directory.Exists(_options.OutputPath))
		{
			string[] directories = Directory.GetDirectories(_options.OutputPath);
			string[] files = Directory.GetFiles(_options.OutputPath);

			Logger.Info($"Output contains: {directories.Length} directories, {files.Length} files");

			foreach (string dir in directories)
			{
				string dirName = Path.GetFileName(dir);
				long dirSize = GetDirectorySize(dir);
				int fileCount = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
				Logger.Info($"  {dirName}/: {fileCount} files, {FormatBytes(dirSize)}");
			}
		}

		Logger.Info("===========================");
	}

	private static long GetDirectorySize(string directory)
	{
		try
		{
			return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
				.Sum(file => new FileInfo(file).Length);
		}
		catch
		{
			return 0;
		}
	}

	private static string FormatBytes(long bytes)
	{
		string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
		int counter = 0;
		double number = bytes;

		while (number >= 1024 && counter < suffixes.Length - 1)
		{
			number /= 1024;
			counter++;
		}

		return $"{number:N1} {suffixes[counter]}";
	}
}
