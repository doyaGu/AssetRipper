using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Minimal NDJSON record for exported Unity assets.
/// Matches the v2-lean "assets" domain specification (one asset per line).
/// </summary>
public class AssetRecord
{
	[JsonProperty("k")]
	public string StableKey { get; set; } = string.Empty;

	[JsonProperty("c")]
	public string CollectionId { get; set; } = string.Empty;

	[JsonProperty("p")]
	public long PathId { get; set; }

	[JsonProperty("classID")]
	public int ClassId { get; set; }

	[JsonProperty("className")]
	public string ClassName { get; set; } = string.Empty;

	[JsonProperty("bestName", NullValueHandling = NullValueHandling.Ignore)]
	public string? BestName { get; set; }

	[JsonProperty("hierarchy", NullValueHandling = NullValueHandling.Ignore)]
	public HierarchyPath? Hierarchy { get; set; }

	[JsonIgnore]
	public string? BundleName { get; set; }

	[JsonIgnore]
	public string? CollectionName { get; set; }
}
