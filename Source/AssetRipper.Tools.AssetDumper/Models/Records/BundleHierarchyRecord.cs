using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Records;

/// <summary>
/// Bundle hierarchy edge record for relations/bundle_hierarchy.ndjson.
/// Represents parent-child relationships between bundles with enhanced metadata.
/// </summary>
public sealed class BundleHierarchyRecord
{
	/// <summary>
	/// Domain identifier, always "bundle_hierarchy".
	/// </summary>
	[JsonProperty("domain")]
	public string Domain { get; set; } = "bundle_hierarchy";

	/// <summary>
	/// Stable PK of the parent bundle (8-character hex string).
	/// </summary>
	[JsonProperty("parentPk")]
	public string ParentPk { get; set; } = string.Empty;

	/// <summary>
	/// Name of the parent bundle for readability and symmetric design.
	/// </summary>
	[JsonProperty("parentName", NullValueHandling = NullValueHandling.Ignore)]
	public string? ParentName { get; set; }

	/// <summary>
	/// Stable PK of the child bundle (8-character hex string).
	/// </summary>
	[JsonProperty("childPk")]
	public string ChildPk { get; set; } = string.Empty;

	/// <summary>
	/// Index of the child in the parent's child list.
	/// Preserves original order for BundlePath reconstruction.
	/// </summary>
	[JsonProperty("childIndex")]
	public int ChildIndex { get; set; }

	/// <summary>
	/// Name of the child bundle for readability.
	/// </summary>
	[JsonProperty("childName", NullValueHandling = NullValueHandling.Ignore)]
	public string? ChildName { get; set; }

	/// <summary>
	/// Type of the child bundle (GameBundle, SerializedBundle, etc.).
	/// Enables quick type identification without additional lookups.
	/// </summary>
	[JsonProperty("childBundleType", NullValueHandling = NullValueHandling.Ignore)]
	public string? ChildBundleType { get; set; }

	/// <summary>
	/// Depth of the child bundle in the hierarchy tree (root = 0).
	/// Enables depth-based queries and hierarchy analysis.
	/// </summary>
	[JsonProperty("childDepth", NullValueHandling = NullValueHandling.Ignore)]
	public int? ChildDepth { get; set; }
}
