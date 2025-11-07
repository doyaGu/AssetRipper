using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Metrics;

/// <summary>
/// Orchestrates collection and export of all metrics from game data.
/// Replaces the placeholder implementation with real metrics collection framework.
/// </summary>
public sealed class MetricsExporter
{
	private readonly Options _options;
	private readonly MetricsCollectorRegistry _registry;
	private readonly List<IMetricsCollector> _collectors;

	public MetricsExporter(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_registry = new MetricsCollectorRegistry();
		_collectors = new List<IMetricsCollector>();
		
		// Register built-in metrics collectors
		_registry.Register("scene_stats", opts => new SceneStatsCollector(opts));
		_registry.Register("asset_distribution", opts => new AssetDistributionCollector(opts));
		_registry.Register("dependency_stats", opts => new DependencyStatsCollector(opts));
	}

	/// <summary>
	/// Collect metrics from game data.
	/// </summary>
	public void CollectMetrics(GameData gameData)
	{
		if (gameData == null)
			throw new ArgumentNullException(nameof(gameData));

		_collectors.Clear();
		_collectors.AddRange(_registry.CreateAll(_options));

		if (_collectors.Count == 0)
		{
			if (_options.Verbose)
			{
				Logger.Verbose("No metrics collectors registered, skipping metrics collection");
			}
			return;
		}

		foreach (IMetricsCollector collector in _collectors)
		{
			try
			{
				if (_options.Verbose)
				{
					Logger.Verbose($"Collecting metrics: {collector.MetricsId}");
				}
				collector.Collect(gameData);
			}
			catch (Exception ex)
			{
				Logger.Error(LogCategory.Export, $"Failed to collect metrics '{collector.MetricsId}': {ex.Message}");
			}
		}
	}

	/// <summary>
	/// Write collected metrics to output directory.
	/// Returns list of written file paths for manifest registration.
	/// </summary>
	public List<string> WriteMetrics()
	{
		List<string> writtenPaths = new();

		foreach (IMetricsCollector collector in _collectors.Where(c => c.HasData))
		{
			try
			{
				string? path = collector.WriteMetrics(_options.OutputPath);
				if (path != null)
				{
					writtenPaths.Add(path);
				}
			}
			catch (Exception ex)
			{
				Logger.Error(LogCategory.Export, $"Failed to write metrics '{collector.MetricsId}': {ex.Message}");
			}
		}

		if (!_options.Silent && writtenPaths.Count > 0)
		{
			Logger.Info($"Wrote {writtenPaths.Count} metrics file(s)");
		}

		return writtenPaths;
	}

	/// <summary>
	/// Write collected metrics to output directory and return structured results for manifest registration.
	/// Each metric type becomes a separate DomainExportResult with proper schema and file information.
	/// </summary>
	public List<DomainExportResult> WriteMetricsWithResults()
	{
		List<DomainExportResult> results = new();

		foreach (IMetricsCollector collector in _collectors.Where(c => c.HasData))
		{
			try
			{
				string? path = collector.WriteMetrics(_options.OutputPath);
				if (path != null)
				{
					// Create DomainExportResult for this metrics file
					var result = new DomainExportResult(
						domain: $"metrics_{collector.MetricsId}",
						tableId: $"metrics/{collector.MetricsId}",
						schemaPath: $"schemas/metrics/{collector.MetricsId}.schema.json",
						format: "json-metrics"
					)
					{
						IsMetrics = true,
						MetricsType = collector.MetricsId,
						EntryFile = Path.GetRelativePath(_options.OutputPath, path)
					};

					// Set file metadata if available
					if (File.Exists(path))
					{
						FileInfo fileInfo = new FileInfo(path);
						result.ByteCountOverride = fileInfo.Length;
						result.RecordCountOverride = 1; // Metrics files are single documents
						
						// TODO: Add checksum calculation when needed
						// result.Checksum = ComputeFileChecksum(path);
					}

					results.Add(result);

					if (_options.Verbose)
					{
						Logger.Verbose($"Created metrics result for {collector.MetricsId}: {path}");
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Error(LogCategory.Export, $"Failed to write metrics '{collector.MetricsId}': {ex.Message}");
			}
		}

		if (!_options.Silent && results.Count > 0)
		{
			Logger.Info($"Wrote {results.Count} metrics file(s) with manifest results");
		}

		return results;
	}

	/// <summary>
	/// Get all collectors that have data for manifest registration.
	/// </summary>
	public IEnumerable<IMetricsCollector> GetCollectorsWithData()
	{
		return _collectors.Where(c => c.HasData);
	}
}
