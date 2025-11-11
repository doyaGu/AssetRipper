namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Configuration presets for common use cases.
/// Provides pre-configured settings optimized for different scenarios.
/// </summary>
public enum ConfigPreset
{
	/// <summary>
	/// Fast preset - minimal export for quick iteration/testing.
	/// - Facts only (assets, scripts)
	/// - No code analysis
	/// - Verbose logging
	/// - Incremental mode
	/// - No compression
	/// </summary>
	Fast,

	/// <summary>
	/// Full preset - complete export with all features.
	/// - All facts, relations, and code analysis
	/// - Decompilation and AST generation
	/// - Compression (zstd)
	/// - Schema validation
	/// - Indexes enabled
	/// </summary>
	Full,

	/// <summary>
	/// <summary>
	/// Analysis preset - optimized for code analysis.
	/// - Script facts and full code analysis
	/// - Decompilation and AST
	/// - Unity project code only
	/// - Indexes enabled
	/// - Compression (gzip)
	/// </summary>
	Analysis,

	/// <summary>
	/// Minimal preset - smallest output size.
	/// - Facts only (assets, collections)
	/// - No code analysis
	/// - Silent mode
	/// - Maximum compression (zstd)
	/// - Skip built-in resources
	/// </summary>
	Minimal,

	/// <summary>
	/// Debug preset - detailed debugging information.
	/// - All domains exported
	/// - Verbose logging and dependency tracing
	/// - Sequential processing
	/// - Extended timeouts
	/// - No compression
	/// - Schema validation
	/// </summary>
	Debug
}
