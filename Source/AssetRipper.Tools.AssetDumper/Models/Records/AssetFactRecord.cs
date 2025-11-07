using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Schema-aligned asset fact record for facts/assets.ndjson.
/// </summary>
public sealed class AssetFactRecord
{
	[JsonProperty("pk")]
	public AssetPrimaryKey PrimaryKey { get; set; } = new();

	[JsonProperty("classKey")]
	public int ClassKey { get; set; }

	[JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
	public string? Name { get; set; }

	[JsonProperty("unity", NullValueHandling = NullValueHandling.Ignore)]
	public AssetUnityMetadata? Unity { get; set; }

	[JsonProperty("data")]
	public AssetDataContainer Data { get; set; } = new();

	[JsonProperty("hash", NullValueHandling = NullValueHandling.Ignore)]
	public string? Hash { get; set; }
}

/// <summary>
/// Container for asset data including byte offset information and serialized content.
/// </summary>
public sealed class AssetDataContainer
{
	/// <summary>
	/// Byte offset in the serialized file (relative to data section start).
	/// -1 if unavailable.
	/// </summary>
	[JsonProperty("byteStart", NullValueHandling = NullValueHandling.Ignore)]
	public long? ByteStart { get; set; }

	/// <summary>
	/// Size of the serialized asset data in bytes.
	/// -1 if unavailable.
	/// </summary>
	[JsonProperty("byteSize", NullValueHandling = NullValueHandling.Ignore)]
	public int? ByteSize { get; set; }

	/// <summary>
	/// Serialized asset content as JSON.
	/// </summary>
	[JsonProperty("content")]
	public JToken Content { get; set; } = JValue.CreateNull();
}

public sealed class AssetPrimaryKey
{
	[JsonProperty("collectionId")]
	public string CollectionId { get; set; } = string.Empty;

	[JsonProperty("pathId")]
	public long PathId { get; set; }
}

public sealed class AssetUnityMetadata
{
	[JsonProperty("classId", NullValueHandling = NullValueHandling.Ignore)]
	public int? ClassId { get; set; }

	[JsonProperty("typeId", NullValueHandling = NullValueHandling.Ignore)]
	public int? TypeId { get; set; }

	[JsonProperty("serializedTypeIndex", NullValueHandling = NullValueHandling.Ignore)]
	public int? SerializedTypeIndex { get; set; }

	[JsonProperty("scriptTypeIndex", NullValueHandling = NullValueHandling.Ignore)]
	public int? ScriptTypeIndex { get; set; }

	[JsonProperty("isStripped", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsStripped { get; set; }
}
