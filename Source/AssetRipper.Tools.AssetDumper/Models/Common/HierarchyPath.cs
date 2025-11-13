using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Common;

/// <summary>
/// Complete hierarchical path from root to target entity.
/// </summary>
public sealed class HierarchyPath
{
	[JsonProperty("bundlePath", NullValueHandling = NullValueHandling.Ignore)]
	public List<string>? BundlePath { get; set; }

	[JsonProperty("bundleNames", NullValueHandling = NullValueHandling.Ignore)]
	public List<string>? BundleNames { get; set; }

	[JsonProperty("depth", NullValueHandling = NullValueHandling.Ignore)]
	public int? Depth { get; set; }
}
