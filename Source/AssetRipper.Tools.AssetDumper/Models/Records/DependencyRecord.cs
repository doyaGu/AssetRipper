using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Schema-aligned relation linking the source asset to a referenced target asset.
/// </summary>
public sealed class AssetDependencyRelation
{
	[JsonProperty("from")]
	public AssetPrimaryKey From { get; set; } = new();

	[JsonProperty("to")]
	public AssetPrimaryKey To { get; set; } = new();

	[JsonProperty("edge")]
	public AssetDependencyEdge Edge { get; set; } = new();

	[JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
	public string? Status { get; set; }

	[JsonProperty("notes", NullValueHandling = NullValueHandling.Ignore)]
	public string? Notes { get; set; }
}

/// <summary>
/// Additional metadata describing the dependency edge.
/// </summary>
public sealed class AssetDependencyEdge
{
	[JsonProperty("kind")]
	public string Kind { get; set; } = "serializedRef";

	[JsonProperty("field", NullValueHandling = NullValueHandling.Ignore)]
	public string? Field { get; set; }

	[JsonProperty("optional", NullValueHandling = NullValueHandling.Ignore)]
	public bool? Optional { get; set; }
}
