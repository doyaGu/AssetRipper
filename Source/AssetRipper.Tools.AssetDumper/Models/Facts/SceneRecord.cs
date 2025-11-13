using Newtonsoft.Json;
using System.Collections.Generic;
using AssetRipper.Tools.AssetDumper.Models.Common;

namespace AssetRipper.Tools.AssetDumper.Models.Facts;

/// <summary>
/// Minimal scene line record (one scene per line).
/// </summary>
public class SceneRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "scenes";

	[JsonProperty("type")]
	public string Type { get; set; } = "Scene";

	[JsonProperty("name")]
	public string Name { get; set; } = string.Empty;

	[JsonProperty("sceneGuid")]
	public string SceneGuid { get; set; } = string.Empty;

	[JsonProperty("scenePath")]
	public string ScenePath { get; set; } = string.Empty;

	[JsonProperty("exportedAt")]
	public string ExportedAt { get; set; } = string.Empty;

	[JsonProperty("version")]
	public string Version { get; set; } = string.Empty;

	[JsonProperty("platform")]
	public string Platform { get; set; } = string.Empty;

	[JsonProperty("flags", NullValueHandling = NullValueHandling.Ignore)]
	public string? Flags { get; set; }

	[JsonProperty("endianType", NullValueHandling = NullValueHandling.Ignore)]
	public string? EndianType { get; set; }

	[JsonProperty("bundleName", NullValueHandling = NullValueHandling.Ignore)]
	public string? BundleName { get; set; }

	[JsonProperty("sceneCollectionCount")]
	public int SceneCollectionCount { get; set; }

	[JsonProperty("collectionIds")]
	public List<string> CollectionIds { get; set; } = new();

	[JsonProperty("collections", NullValueHandling = NullValueHandling.Ignore)]
	public List<SceneCollectionDescriptor>? Collections { get; set; }

	[JsonProperty("primaryCollectionId", NullValueHandling = NullValueHandling.Ignore)]
	public string? PrimaryCollectionId { get; set; }

	[JsonProperty("bundle", NullValueHandling = NullValueHandling.Ignore)]
	public BundleRef? Bundle { get; set; }

	[JsonProperty("collectionDetails", NullValueHandling = NullValueHandling.Ignore)]
	public List<SceneCollectionDetail>? CollectionDetails { get; set; }

	[JsonProperty("hierarchy", NullValueHandling = NullValueHandling.Ignore)]
	public AssetRef? Hierarchy { get; set; }

	[JsonProperty("hierarchyAssetId", NullValueHandling = NullValueHandling.Ignore)]
	public string? HierarchyAssetId { get; set; }

	[JsonProperty("pathID", NullValueHandling = NullValueHandling.Ignore)]
	public long? PathId { get; set; }

	[JsonProperty("classID", NullValueHandling = NullValueHandling.Ignore)]
	public int? ClassId { get; set; }

	[JsonProperty("className", NullValueHandling = NullValueHandling.Ignore)]
	public string? ClassName { get; set; }

	[JsonProperty("assetCount")]
	public int AssetCount { get; set; }

	[JsonProperty("gameObjectCount")]
	public int GameObjectCount { get; set; }

	[JsonProperty("componentCount")]
	public int ComponentCount { get; set; }

	[JsonProperty("managerCount")]
	public int ManagerCount { get; set; }

	[JsonProperty("prefabInstanceCount")]
	public int PrefabInstanceCount { get; set; }

	[JsonProperty("dependencyCount")]
	public int DependencyCount { get; set; }

	[JsonProperty("hasSceneRoots")]
	public bool HasSceneRoots { get; set; }

	[JsonProperty("rootGameObjectCount")]
	public int RootGameObjectCount { get; set; }

	[JsonProperty("strippedAssetCount")]
	public int StrippedAssetCount { get; set; }

	[JsonProperty("hiddenAssetCount")]
	public int HiddenAssetCount { get; set; }

	[JsonProperty("sceneRootsAsset", NullValueHandling = NullValueHandling.Ignore)]
	public AssetRef? SceneRootsAsset { get; set; }

	[JsonProperty("sceneRoots", NullValueHandling = NullValueHandling.Ignore)]
	public List<AssetRef>? SceneRoots { get; set; }

	[JsonProperty("rootGameObjects", NullValueHandling = NullValueHandling.Ignore)]
	public List<AssetRef>? RootGameObjects { get; set; }

	[JsonProperty("gameObjects", NullValueHandling = NullValueHandling.Ignore)]
	public List<AssetRef>? GameObjects { get; set; }

	[JsonProperty("components", NullValueHandling = NullValueHandling.Ignore)]
	public List<AssetRef>? Components { get; set; }

	[JsonProperty("managers", NullValueHandling = NullValueHandling.Ignore)]
	public List<AssetRef>? Managers { get; set; }

	[JsonProperty("prefabInstances", NullValueHandling = NullValueHandling.Ignore)]
	public List<AssetRef>? PrefabInstances { get; set; }

	[JsonProperty("strippedAssets", NullValueHandling = NullValueHandling.Ignore)]
	public List<AssetRef>? StrippedAssets { get; set; }

	[JsonProperty("hiddenAssets", NullValueHandling = NullValueHandling.Ignore)]
	public List<AssetRef>? HiddenAssets { get; set; }

	[JsonProperty("notes", NullValueHandling = NullValueHandling.Ignore)]
	public string? Notes { get; set; }
}

/// <summary>
/// Descriptor describing a collection participating in a scene export.
/// </summary>
public class SceneCollectionDescriptor
{
	[JsonProperty("collectionId")]
	public string CollectionId { get; set; } = string.Empty;

	[JsonProperty("name")]
	public string Name { get; set; } = string.Empty;

	[JsonProperty("bundleName", NullValueHandling = NullValueHandling.Ignore)]
	public string? BundleName { get; set; }

	[JsonProperty("bundlePk", NullValueHandling = NullValueHandling.Ignore)]
	public string? BundlePk { get; set; }

	[JsonProperty("collectionType", NullValueHandling = NullValueHandling.Ignore)]
	public string? CollectionType { get; set; }

	[JsonProperty("filePath", NullValueHandling = NullValueHandling.Ignore)]
	public string? FilePath { get; set; }

	[JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
	public string? Version { get; set; }

	[JsonProperty("platform", NullValueHandling = NullValueHandling.Ignore)]
	public string? Platform { get; set; }

	[JsonProperty("flags", NullValueHandling = NullValueHandling.Ignore)]
	public string? Flags { get; set; }

	[JsonProperty("isProcessed", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsProcessed { get; set; }

	[JsonProperty("isSceneCollection", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsSceneCollection { get; set; }

	[JsonProperty("sceneName", NullValueHandling = NullValueHandling.Ignore)]
	public string? SceneName { get; set; }

	[JsonProperty("isPrimary", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsPrimary { get; set; }

	[JsonProperty("assetCount", NullValueHandling = NullValueHandling.Ignore)]
	public int? AssetCount { get; set; }
}

/// <summary>
/// Detailed metadata for each collection composing a scene.
/// </summary>
public class SceneCollectionDetail
{
	[JsonProperty("collectionId")]
	public string CollectionId { get; set; } = string.Empty;

	[JsonProperty("bundle")]
	public BundleRef Bundle { get; set; } = new();

	[JsonProperty("isPrimary", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsPrimary { get; set; }

	[JsonProperty("assetCount", NullValueHandling = NullValueHandling.Ignore)]
	public int? AssetCount { get; set; }
}
