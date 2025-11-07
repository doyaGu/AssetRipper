namespace AssetRipper.Tools.AssetDumper.Constants;

/// <summary>
/// Constants for JSON Schema URIs and related identifiers.
/// </summary>
public static class SchemaConstants
{
	/// <summary>
	/// Base URI for schema definitions.
	/// </summary>
	public const string SchemaBaseUri = "https://assetripper.github.io/AssetDumper/schema/";

	/// <summary>
	/// JSON Schema Draft version identifier.
	/// </summary>
	public const string SchemaDraft = "https://json-schema.org/draft/2020-12/schema";

	// Core schema identifiers
	public const string CoreSchemaId = "core.schema.json";
	public const string AssetPKAnchor = "AssetPK";
	public const string CollectionIDAnchor = "CollectionID";

	// Facts schema identifiers
	public const string AssetsSchemaId = "facts/assets.schema.json";
	public const string CollectionsSchemaId = "facts/collections.schema.json";
	public const string TypesSchemaId = "facts/types.schema.json";
	public const string BundlesSchemaId = "facts/bundles.schema.json";
	public const string ScenesSchemaId = "facts/scenes.schema.json";
	public const string ScriptsSchemaId = "facts/scripts.schema.json";

	// Relations schema identifiers
	public const string DependenciesSchemaId = "relations/asset_dependencies.schema.json";

	// Indexes schema identifiers
	public const string ByClassIndexSchemaId = "indexes/by_class.schema.json";
	public const string ByCollectionIndexSchemaId = "indexes/by_collection.schema.json";

	// Metrics schema identifiers
	public const string SceneStatsSchemaId = "metrics/scene_stats.schema.json";
	public const string AssetDistributionSchemaId = "metrics/asset_distribution.schema.json";
	public const string DependencyStatsSchemaId = "metrics/dependency_stats.schema.json";

	// Manifest schema identifier
	public const string ManifestSchemaId = "manifest.schema.json";
}
