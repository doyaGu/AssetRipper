namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Configuration presets for common use cases.
/// Provides pre-configured settings optimized for different scenarios.
/// </summary>
public enum ConfigPreset
{
	/// <summary>
	/// Development preset - fast iteration with verbose output.
	/// - Verbose logging enabled
	/// - Incremental processing enabled
	/// - No compression
	/// - No schema validation
	/// - Metrics enabled
	/// </summary>
	Development,

	/// <summary>
	/// Production preset - optimized for production exports.
	/// - Compressed output (zstd)
	/// - Schema validation enabled
	/// - Indexes enabled
	/// - Compact JSON
	/// - Full export (no incremental)
	/// </summary>
	Production,

	/// <summary>
	/// Debug preset - detailed debugging information.
	/// - Verbose logging and dependency tracing
	/// - Sequential processing (easier debugging)
	/// - Extended timeouts
	/// - All metadata included
	/// - No compression
	/// </summary>
	Debug,

	/// <summary>
	/// Minimal preset - smallest output size.
	/// - Silent mode
	/// - Minimal exports
	/// - Maximum compression
	/// - Compact JSON
	/// - Skip optional metadata
	/// </summary>
	Minimal,

	/// <summary>
	/// Analysis preset - optimized for static analysis tools.
	/// - Scripts and AST export enabled
	/// - Full metadata
	/// - Indexes enabled
	/// - Relations and metrics included
	/// - Good compression (gzip for compatibility)
	/// </summary>
	Analysis
}
