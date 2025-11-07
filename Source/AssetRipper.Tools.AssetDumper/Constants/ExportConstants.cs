namespace AssetRipper.Tools.AssetDumper.Constants;

/// <summary>
/// Core export-related constants used throughout the AssetDumper.
/// </summary>
public static class ExportConstants
{
	/// <summary>
	/// Default maximum number of records per shard file.
	/// </summary>
	public const long DefaultMaxRecordsPerShard = 100_000;

	/// <summary>
	/// Default maximum bytes per shard file (100 MB).
	/// </summary>
	public const long DefaultMaxBytesPerShard = 100 * 1024 * 1024;

	/// <summary>
	/// Maximum bytes per shard for script-related data (50 MB).
	/// </summary>
	public const long ScriptShardMaxBytes = 50 * 1024 * 1024;

	/// <summary>
	/// Maximum bytes per shard for type facts (16 MB).
	/// </summary>
	public const long TypeFactsShardMaxBytes = 16 * 1024 * 1024;

	/// <summary>
	/// Default seekable frame size for Zstandard compression (2 MB).
	/// </summary>
	public const int DefaultSeekableFrameSize = 2 * 1024 * 1024;

	/// <summary>
	/// Buffer size for file I/O operations (80 KB).
	/// </summary>
	public const int FileBufferSize = 81920;

	/// <summary>
	/// Interval for logging dependency processing progress.
	/// </summary>
	public const int DependencyProgressLogInterval = 2_000;

	/// <summary>
	/// Interval for logging verbose asset processing progress.
	/// </summary>
	public const int VerboseAssetProgressInterval = 100_000;

	/// <summary>
	/// Default records per shard for scene data.
	/// </summary>
	public const long SceneShardMaxRecords = 10_000;
}
