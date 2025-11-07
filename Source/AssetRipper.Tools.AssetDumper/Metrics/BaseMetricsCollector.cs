using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using Newtonsoft.Json;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Metrics;

/// <summary>
/// Base class for metrics collectors providing common functionality.
/// </summary>
public abstract class BaseMetricsCollector : IMetricsCollector
{
	public abstract string MetricsId { get; }
	public abstract string SchemaUri { get; }
	public abstract bool HasData { get; }

	private readonly Options _options;
	protected Options Options => _options;

	internal BaseMetricsCollector(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	public abstract void Collect(GameData gameData);

	public virtual string? WriteMetrics(string outputRoot)
	{
		if (!HasData)
		{
			if (_options.Verbose)
			{
				Logger.Verbose($"No data collected for metrics '{MetricsId}', skipping output");
			}
			return null;
		}

		try
		{
			string metricsDir = OutputPathHelper.EnsureSubdirectory(outputRoot, OutputPathHelper.MetricsDirectoryName);
			string outputPath = Path.Combine(metricsDir, $"{MetricsId}.json");

			object? metricsData = GetMetricsData();
			if (metricsData == null)
			{
				if (_options.Verbose)
				{
					Logger.Verbose($"Metrics data for '{MetricsId}' is null, skipping output");
				}
				return null;
			}

			string json = JsonConvert.SerializeObject(metricsData, Formatting.Indented);
			File.WriteAllText(outputPath, json);

			if (!_options.Silent)
			{
				Logger.Info($"Wrote metrics: {MetricsId} to {outputPath}");
			}

			return outputPath;
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to write metrics '{MetricsId}': {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Get the collected metrics data as an object ready for JSON serialization.
	/// </summary>
	protected abstract object? GetMetricsData();
}
