namespace AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Constants;

/// <summary>
/// Shared constants for all AssetDumper tests.
/// </summary>
public static class TestConstants
{
    // Sample collection IDs
    public const string DefaultCollectionId = "sharedassets1.assets";
    public const string SecondaryCollectionId = "level1.unity";
    public const string InvalidCollectionId = "invalid@id#special";

    // Sample GUIDs
    public const string ValidGuid = "a1b2c3d4e5f67890a1b2c3d4e5f67890";
    public const string InvalidGuid = "invalid-guid";

    // Sample class IDs
    public const int GameObjectClassId = 1;
    public const int TransformClassId = 4;
    public const int MonoBehaviourClassId = 114;
    public const int TextureClassId = 28;

    // Sample class names
    public const string GameObjectClassName = "GameObject";
    public const string TransformClassName = "Transform";
    public const string MonoBehaviourClassName = "MonoBehaviour";

    // Sample paths
    public const long DefaultPathId = 1;
    public const long SecondaryPathId = 2;
    public const long NonExistentPathId = 999999;

    // Schema paths
    public const string AssetsSchemaPath = "Schemas/v2/facts/assets.schema.json";
    public const string TypesSchemaPath = "Schemas/v2/facts/types.schema.json";
    public const string DependenciesSchemaPath = "Schemas/v2/relations/asset_dependencies.schema.json";
    public const string CollectionDependenciesSchemaPath = "Schemas/v2/relations/collection_dependencies.schema.json";
    public const string BundleHierarchySchemaPath = "Schemas/v2/relations/bundle_hierarchy.schema.json";
    public const string AssemblyDependenciesSchemaPath = "Schemas/v2/relations/assembly_dependencies.schema.json";

    // File names
    public const string AssetsFileName = "assets.ndjson";
    public const string TypesFileName = "types.ndjson";
    public const string DependenciesFileName = "asset_dependencies.ndjson";
    public const string CollectionDependenciesFileName = "collection_dependencies.ndjson";
    public const string ManifestFileName = "manifest.json";

    // Validation error types (Schema v2.0)
    public const string ErrorTypeStructural = "Structural";
    public const string ErrorTypeReference = "Reference";
    public const string ErrorTypePattern = "Pattern";
    public const string ErrorTypeDataType = "DataType";
    public const string ErrorTypeConstraint = "Constraint";

    // Dependency kinds (Schema v2.0)
    public const string DependencyKindPPtr = "pptr";
    public const string DependencyKindExternal = "external";
    public const string DependencyKindInternal = "internal";
    public const string DependencyKindArrayElement = "array_element";
    public const string DependencyKindDictionaryKey = "dictionary_key";
    public const string DependencyKindDictionaryValue = "dictionary_value";

    // Collection dependency sources (Schema v2.0)
    public const string DependencySourceSerialized = "serialized";
    public const string DependencySourceDynamic = "dynamic";
    public const string DependencySourceBuiltin = "builtin";

    // Bundle types (Schema v2.0)
    public const string BundleTypeGameBundle = "GameBundle";
    public const string BundleTypeSerialized = "SerializedBundle";
    public const string BundleTypeProcessed = "ProcessedBundle";
    public const string BundleTypeResourceFile = "ResourceFile";
    public const string BundleTypeWebBundle = "WebBundle";
    public const string BundleTypeUnknown = "Unknown";

    // Assembly dependency types (Schema v2.0)
    public const string AssemblyDependencyTypeDirect = "direct";
    public const string AssemblyDependencyTypeFramework = "framework";
    public const string AssemblyDependencyTypePlugin = "plugin";
    public const string AssemblyDependencyTypeUnknown = "unknown";

    // Test data sizes
    public const int SmallDatasetSize = 10;
    public const int MediumDatasetSize = 100;
    public const int LargeDatasetSize = 1000;

    // Performance thresholds (milliseconds)
    public const int FastOperationThreshold = 100;
    public const int NormalOperationThreshold = 1000;
    public const int SlowOperationThreshold = 5000;
}
