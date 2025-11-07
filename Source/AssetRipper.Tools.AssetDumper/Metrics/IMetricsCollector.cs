using AssetRipper.Processing;

namespace AssetRipper.Tools.AssetDumper.Metrics;

/// <summary>
/// Interface for metrics collectors that gather statistics from game data.
/// Metrics are derived/recomputable artifacts separate from Facts and Relations.
/// </summary>
public interface IMetricsCollector
{
	/// <summary>
	/// Gets the unique identifier for this metrics type (e.g., "scene_stats", "asset_distribution").
	/// Used for file naming and manifest registration.
	/// </summary>
	string MetricsId { get; }

	/// <summary>
	/// Gets the schema URI for this metrics type, following Draft 2020-12 conventions.
	/// </summary>
	string SchemaUri { get; }

	/// <summary>
	/// Collect metrics from the provided game data.
	/// </summary>
	/// <param name="gameData">The loaded game data to analyze</param>
	void Collect(GameData gameData);

	/// <summary>
	/// Write collected metrics to the specified output directory.
	/// </summary>
	/// <param name="outputRoot">Root output directory (metrics will be written to metrics/ subdirectory)</param>
	/// <returns>Path to the written metrics file, or null if no output was generated</returns>
	string? WriteMetrics(string outputRoot);

	/// <summary>
	/// Gets whether this collector has gathered any metrics data.
	/// </summary>
	bool HasData { get; }
}
