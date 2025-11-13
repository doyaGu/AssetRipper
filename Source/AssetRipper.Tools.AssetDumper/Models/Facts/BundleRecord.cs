using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Facts;

/// <summary>
/// Metadata describing bundle nodes emitted in the facts/bundles domain.
/// </summary>
public sealed class BundleRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "bundles";

	[JsonProperty("pk")]
	public string Pk { get; set; } = string.Empty;

	[JsonProperty("name")]
	public string Name { get; set; } = string.Empty;

	[JsonProperty("bundleType")]
	public string BundleType { get; set; } = string.Empty;

	[JsonProperty("parentPk", NullValueHandling = NullValueHandling.Ignore)]
	public string? ParentPk { get; set; }

	[JsonProperty("isRoot")]
	public bool IsRoot { get; set; }

	[JsonProperty("hierarchyDepth")]
	public int HierarchyDepth { get; set; }

	[JsonProperty("hierarchyPath")]
	public string HierarchyPath { get; set; } = string.Empty;

	[JsonProperty("childBundlePks")]
	public List<string> ChildBundlePks { get; set; } = new();

	[JsonProperty("childBundleNames", NullValueHandling = NullValueHandling.Ignore)]
	public List<string>? ChildBundleNames { get; set; }

	[JsonProperty("bundleIndex", NullValueHandling = NullValueHandling.Ignore)]
	public int? BundleIndex { get; set; }

	[JsonProperty("ancestorPath", NullValueHandling = NullValueHandling.Ignore)]
	public List<string>? AncestorPath { get; set; }

	[JsonProperty("collectionIds", NullValueHandling = NullValueHandling.Ignore)]
	public List<string>? CollectionIds { get; set; }

	[JsonProperty("resources", NullValueHandling = NullValueHandling.Ignore)]
	public List<BundleResourceRecord>? Resources { get; set; }

	[JsonProperty("failedFiles", NullValueHandling = NullValueHandling.Ignore)]
	public List<BundleFailedFileRecord>? FailedFiles { get; set; }

	[JsonProperty("scenes", NullValueHandling = NullValueHandling.Ignore)]
	public List<SceneRefRecord>? Scenes { get; set; }

	[JsonProperty("directCollectionCount")]
	public int DirectCollectionCount { get; set; }

	[JsonProperty("totalCollectionCount")]
	public int TotalCollectionCount { get; set; }

	[JsonProperty("directSceneCollectionCount")]
	public int DirectSceneCollectionCount { get; set; }

	[JsonProperty("totalSceneCollectionCount")]
	public int TotalSceneCollectionCount { get; set; }

	[JsonProperty("directChildBundleCount")]
	public int DirectChildBundleCount { get; set; }

	[JsonProperty("totalChildBundleCount")]
	public int TotalChildBundleCount { get; set; }

	[JsonProperty("directResourceCount")]
	public int DirectResourceCount { get; set; }

	[JsonProperty("totalResourceCount")]
	public int TotalResourceCount { get; set; }

	[JsonProperty("directFailedFileCount")]
	public int DirectFailedFileCount { get; set; }

	[JsonProperty("totalFailedFileCount")]
	public int TotalFailedFileCount { get; set; }

	[JsonProperty("directAssetCount")]
	public int DirectAssetCount { get; set; }

	[JsonProperty("totalAssetCount")]
	public int TotalAssetCount { get; set; }
}

/// <summary>
/// Resource payload information associated with a bundle.
/// </summary>
public sealed class BundleResourceRecord
{
	[JsonProperty("name")]
	public string Name { get; set; } = string.Empty;

	[JsonProperty("filePath", NullValueHandling = NullValueHandling.Ignore)]
	public string? FilePath { get; set; }
}

/// <summary>
/// Failed file information associated with a bundle.
/// </summary>
public sealed class BundleFailedFileRecord
{
	[JsonProperty("name")]
	public string Name { get; set; } = string.Empty;

	[JsonProperty("filePath", NullValueHandling = NullValueHandling.Ignore)]
	public string? FilePath { get; set; }

	[JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
	public string? Error { get; set; }
}

/// <summary>
/// Scene reference in a bundle.
/// </summary>
public sealed class SceneRefRecord
{
	[JsonProperty("sceneGuid")]
	public string SceneGuid { get; set; } = string.Empty;

	[JsonProperty("sceneName", NullValueHandling = NullValueHandling.Ignore)]
	public string? SceneName { get; set; }

	[JsonProperty("scenePath", NullValueHandling = NullValueHandling.Ignore)]
	public string? ScenePath { get; set; }
}