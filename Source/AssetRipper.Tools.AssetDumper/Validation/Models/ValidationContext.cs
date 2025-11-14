using AssetRipper.Tools.AssetDumper.Models.Common;
using System.Text.Json.Nodes;

namespace AssetRipper.Tools.AssetDumper.Validation.Models;

/// <summary>
/// Context for validation operations containing cross-references and semantic rules.
/// </summary>
public class ValidationContext
{
    /// <summary>
    /// Maps asset primary keys to their locations across all tables.
    /// </summary>
    public Dictionary<string, List<AssetReference>> AssetReferences { get; set; } = new();

    /// <summary>
    /// Maps class keys to type information.
    /// </summary>
    public Dictionary<int, TypeReference> TypeReferences { get; set; } = new();

    /// <summary>
    /// Maps collection IDs to their metadata.
    /// </summary>
    public Dictionary<string, CollectionReference> CollectionReferences { get; set; } = new();

    /// <summary>
    /// Maps bundle PKs to their hierarchy information.
    /// </summary>
    public Dictionary<string, BundleReference> BundleReferences { get; set; } = new();

    /// <summary>
    /// Maps scene GUIDs to their metadata.
    /// </summary>
    public Dictionary<string, SceneReference> SceneReferences { get; set; } = new();

    /// <summary>
    /// Dependency graph between assets.
    /// </summary>
    public Dictionary<string, List<DependencyReference>> AssetDependencies { get; set; } = new();

    /// <summary>
    /// Index mappings for fast lookups.
    /// </summary>
    public IndexMappings IndexMappings { get; set; } = new();

    /// <summary>
    /// Unity-specific validation rules and constraints.
    /// </summary>
    public UnityValidationRules UnityRules { get; set; } = new();

    /// <summary>
    /// Cross-table consistency checks.
    /// </summary>
    public ConsistencyChecks ConsistencyChecks { get; set; } = new();
}

/// <summary>
/// Reference to an asset location.
/// </summary>
public class AssetReference
{
    /// <summary>
    /// Asset primary key (collectionId:pathId).
    /// </summary>
    public string AssetPk { get; set; } = string.Empty;

    /// <summary>
    /// Collection identifier.
    /// </summary>
    public string CollectionId { get; set; } = string.Empty;

    /// <summary>
    /// Path ID within collection.
    /// </summary>
    public long PathId { get; set; }

    /// <summary>
    /// Table where asset is referenced.
    /// </summary>
    public string TableId { get; set; } = string.Empty;

    /// <summary>
    /// Line number in table.
    /// </summary>
    public long LineNumber { get; set; }

    /// <summary>
    /// Class key of the asset.
    /// </summary>
    public int ClassKey { get; set; }

    /// <summary>
    /// Class name of the asset.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Asset name if available.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Bundle hierarchy information.
    /// </summary>
    public HierarchyPath? Hierarchy { get; set; }
}

/// <summary>
/// Reference to type information.
/// </summary>
public class TypeReference
{
    /// <summary>
    /// Dense class key.
    /// </summary>
    public int ClassKey { get; set; }

    /// <summary>
    /// Unity ClassID.
    /// </summary>
    public int ClassId { get; set; }

    /// <summary>
    /// Class name.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Type ID if available.
    /// </summary>
    public int? TypeId { get; set; }

    /// <summary>
    /// Script type index for MonoBehaviour.
    /// </summary>
    public int? ScriptTypeIndex { get; set; }

    /// <summary>
    /// Whether type is stripped.
    /// </summary>
    public bool IsStripped { get; set; }

    /// <summary>
    /// Base class name if available.
    /// </summary>
    public string? BaseClassName { get; set; }

    /// <summary>
    /// Whether class is abstract.
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// MonoScript information for MonoBehaviour.
    /// </summary>
    public MonoScriptInfo? MonoScript { get; set; }
}

/// <summary>
/// Reference to collection information.
/// </summary>
public class CollectionReference
{
    /// <summary>
    /// Collection identifier.
    /// </summary>
    public string CollectionId { get; set; } = string.Empty;

    /// <summary>
    /// Collection name if available.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether this is a scene collection.
    /// </summary>
    public bool IsScene { get; set; }

    /// <summary>
    /// Scene GUID if this is a scene collection.
    /// </summary>
    public string? SceneGuid { get; set; }

    /// <summary>
    /// Number of assets in this collection.
    /// </summary>
    public int AssetCount { get; set; }

    /// <summary>
    /// Bundle containing this collection.
    /// </summary>
    public string? BundlePk { get; set; }
}

/// <summary>
/// Reference to bundle information.
/// </summary>
public class BundleReference
{
    /// <summary>
    /// Bundle primary key.
    /// </summary>
    public string BundlePk { get; set; } = string.Empty;

    /// <summary>
    /// Bundle name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Bundle type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Parent bundle PK if nested.
    /// </summary>
    public string? ParentBundlePk { get; set; }

    /// <summary>
    /// Child bundle PKs.
    /// </summary>
    public List<string> ChildBundlePks { get; set; } = new();

    /// <summary>
    /// Collections in this bundle.
    /// </summary>
    public List<string> CollectionIds { get; set; } = new();

    /// <summary>
    /// Hierarchy depth.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Full hierarchy path.
    /// </summary>
    public List<string> HierarchyPath { get; set; } = new();
}

/// <summary>
/// Reference to scene information.
/// </summary>
public class SceneReference
{
    /// <summary>
    /// Scene GUID.
    /// </summary>
    public string SceneGuid { get; set; } = string.Empty;

    /// <summary>
    /// Scene name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Scene path.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Collection ID for this scene.
    /// </summary>
    public string? CollectionId { get; set; }

    /// <summary>
    /// Number of assets in this scene.
    /// </summary>
    public int AssetCount { get; set; }

    /// <summary>
    /// Root GameObjects in this scene.
    /// </summary>
    public List<string> RootGameObjects { get; set; } = new();
}

/// <summary>
/// Reference to dependency information.
/// </summary>
public class DependencyReference
{
    /// <summary>
    /// Source asset PK.
    /// </summary>
    public string FromAssetPk { get; set; } = string.Empty;

    /// <summary>
    /// Target asset PK.
    /// </summary>
    public string ToAssetPk { get; set; } = string.Empty;

    /// <summary>
    /// Dependency kind.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Field path containing the reference.
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Field type.
    /// </summary>
    public string? FieldType { get; set; }

    /// <summary>
    /// File ID from PPtr structure.
    /// </summary>
    public int? FileId { get; set; }

    /// <summary>
    /// Array index if applicable.
    /// </summary>
    public int? ArrayIndex { get; set; }

    /// <summary>
    /// Whether reference can be null.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Resolution status.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Expected target type.
    /// </summary>
    public string? TargetType { get; set; }
}

/// <summary>
/// Index mappings for fast lookups.
/// </summary>
public class IndexMappings
{
    /// <summary>
    /// Maps class keys to asset lists.
    /// </summary>
    public Dictionary<int, List<string>> ByClass { get; set; } = new();

    /// <summary>
    /// Maps collection IDs to asset lists.
    /// </summary>
    public Dictionary<string, List<string>> ByCollection { get; set; } = new();

    /// <summary>
    /// Maps asset names to asset PKs.
    /// </summary>
    public Dictionary<string, List<string>> ByName { get; set; } = new();

    /// <summary>
    /// Maps bundle PKs to asset lists.
    /// </summary>
    public Dictionary<string, List<string>> ByBundle { get; set; } = new();
}

/// <summary>
/// Unity-specific validation rules.
/// </summary>
public class UnityValidationRules
{
    /// <summary>
    /// Required fields for each Unity class.
    /// </summary>
    public Dictionary<int, List<string>> RequiredFields { get; set; } = new();

    /// <summary>
    /// Valid enum values for Unity enums.
    /// </summary>
    public Dictionary<string, List<string>> EnumValues { get; set; } = new();

    /// <summary>
    /// Field type constraints.
    /// </summary>
    public Dictionary<string, List<string>> FieldTypeConstraints { get; set; } = new();

    /// <summary>
    /// Reference integrity rules.
    /// </summary>
    public Dictionary<string, List<ReferenceRule>> ReferenceRules { get; set; } = new();
}

/// <summary>
/// Reference validation rule.
/// </summary>
public class ReferenceRule
{
    /// <summary>
    /// Source field path.
    /// </summary>
    public string SourceField { get; set; } = string.Empty;

    /// <summary>
    /// Target class requirement.
    /// </summary>
    public List<int> AllowedClassIds { get; set; } = new();

    /// <summary>
    /// Whether reference is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Whether reference can be null.
    /// </summary>
    public bool Nullable { get; set; }
}

/// <summary>
/// Cross-table consistency checks.
/// </summary>
public class ConsistencyChecks
{
    /// <summary>
    /// Checks to perform between facts and relations.
    /// </summary>
    public List<ConsistencyCheck> FactsRelations { get; set; } = new();

    /// <summary>
    /// Checks to perform between facts and indexes.
    /// </summary>
    public List<ConsistencyCheck> FactsIndexes { get; set; } = new();

    /// <summary>
    /// Checks to perform between relations and indexes.
    /// </summary>
    public List<ConsistencyCheck> RelationsIndexes { get; set; } = new();
}

/// <summary>
/// Individual consistency check definition.
/// </summary>
public class ConsistencyCheck
{
    /// <summary>
    /// Check name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Check description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Source table and field.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Target table and field.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Check type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether check is critical.
    /// </summary>
    public bool Critical { get; set; }
}

/// <summary>
/// MonoScript information for MonoBehaviour types.
/// </summary>
public class MonoScriptInfo
{
    /// <summary>
    /// Assembly name.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Namespace.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Class name.
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Script GUID.
    /// </summary>
    public string? ScriptGuid { get; set; }
}