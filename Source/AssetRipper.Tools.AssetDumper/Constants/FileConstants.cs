namespace AssetRipper.Tools.AssetDumper.Constants;

/// <summary>
/// Constants for file names, extensions, and path components.
/// </summary>
public static class FileConstants
{
	// Directory names
	public const string FactsDirectoryName = "facts";
	public const string RelationsDirectoryName = "relations";
	public const string IndexesDirectoryName = "indexes";
	public const string MetricsDirectoryName = "metrics";
	public const string SchemaDirectoryName = "schema";

	// File extensions
	public const string NdjsonExtension = ".ndjson";
	public const string ZstdExtension = ".zst";
	public const string JsonExtension = ".json";
	public const string CompressedNdjsonExtension = ".ndjson.zst";

	// Standard file names
	public const string ManifestFileName = "manifest.json";
	public const string ReadmeFileName = "README.md";

	// Shard naming
	public const string ShardFilePrefix = "part-";
	public const string ShardFileNumberFormat = "D5"; // 5 digits with leading zeros

	// Collection identifiers
	public const string MissingCollectionId = "MISSING";
	public const string BuiltinExtraCollectionName = "BUILTIN-EXTRA";
	public const string BuiltinDefaultCollectionName = "BUILTIN-DEFAULT";
	public const string BuiltinEditorCollectionName = "BUILTIN-EDITOR";
}
