using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Common;

/// <summary>
/// Single entry within a key index sidecar (key -> shard offset mapping).
/// </summary>
public class KeyIndexEntry
{
	[JsonProperty("key")]
	public string Key { get; set; } = string.Empty;

	[JsonProperty("shard")]
	public string Shard { get; set; } = string.Empty;

	[JsonProperty("offset")]
	public long Offset { get; set; }

	[JsonProperty("length")]
	public long Length { get; set; }

	[JsonProperty("line", NullValueHandling = NullValueHandling.Ignore)]
	public long? Line { get; set; }

	[JsonProperty("frame", NullValueHandling = NullValueHandling.Ignore)]
	public int? Frame { get; set; }
}

/// <summary>
/// JSON document persisted as <domain>.kindex describing byte offsets per key.
/// </summary>
public class KeyIndexDocument
{
	[JsonProperty("kind")]
	public string Kind { get; set; } = "kindex";

	[JsonProperty("domain")]
	public string Domain { get; set; } = string.Empty;

	[JsonProperty("createdAt")]
	public string CreatedAt { get; set; } = string.Empty;

	[JsonProperty("entries")]
	public List<KeyIndexEntry> Entries { get; set; } = new();

	[JsonProperty("metadata", NullValueHandling = NullValueHandling.Ignore)]
	public Dictionary<string, object>? Metadata { get; set; }
}
