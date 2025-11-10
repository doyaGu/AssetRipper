using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Records;

/// <summary>
/// Bundle hierarchy edge record for relations/bundle_hierarchy.ndjson.
/// </summary>
public sealed class BundleHierarchyRecord
{
	[JsonProperty("parentPk")]
	public string ParentPk { get; set; } = string.Empty;

	[JsonProperty("childPk")]
	public string ChildPk { get; set; } = string.Empty;

	[JsonProperty("childIndex")]
	public int ChildIndex { get; set; }

	[JsonProperty("childName", NullValueHandling = NullValueHandling.Ignore)]
	public string? ChildName { get; set; }
}
