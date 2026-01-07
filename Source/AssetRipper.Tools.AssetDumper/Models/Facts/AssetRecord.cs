using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AssetRipper.Tools.AssetDumper.Models.Common;

namespace AssetRipper.Tools.AssetDumper.Models.Facts;

/// <summary>
/// Schema-aligned asset fact record for facts/assets.ndjson.
/// </summary>
public sealed class AssetRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "assets";

	[JsonProperty("pk")]
	public AssetPrimaryKey PrimaryKey { get; set; } = new();

	[JsonProperty("classKey")]
	public int ClassKey { get; set; }

	[JsonProperty("className", NullValueHandling = NullValueHandling.Ignore)]
	public string? ClassName { get; set; }

	[JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
	public string? Name { get; set; }

	[JsonProperty("originalPath", NullValueHandling = NullValueHandling.Ignore)]
	public string? OriginalPath { get; set; }

	[JsonProperty("originalDirectory", NullValueHandling = NullValueHandling.Ignore)]
	public string? OriginalDirectory { get; set; }

	[JsonProperty("originalName", NullValueHandling = NullValueHandling.Ignore)]
	public string? OriginalName { get; set; }

	[JsonProperty("originalExtension", NullValueHandling = NullValueHandling.Ignore)]
	public string? OriginalExtension { get; set; }

	[JsonProperty("assetBundleName", NullValueHandling = NullValueHandling.Ignore)]
	public string? AssetBundleName { get; set; }

	[JsonProperty("hierarchy", NullValueHandling = NullValueHandling.Ignore)]
	public HierarchyPath? Hierarchy { get; set; }

	[JsonProperty("collectionName", NullValueHandling = NullValueHandling.Ignore)]
	public string? CollectionName { get; set; }

	[JsonProperty("bundleName", NullValueHandling = NullValueHandling.Ignore)]
	public string? BundleName { get; set; }

	[JsonProperty("sceneName", NullValueHandling = NullValueHandling.Ignore)]
	public string? SceneName { get; set; }

	[JsonProperty("unity", NullValueHandling = NullValueHandling.Ignore)]
	public AssetUnityMetadata? Unity { get; set; }

	/// <summary>
	/// Byte offset in the serialized file (relative to data section start).
	/// Optional metadata field.
	/// </summary>
	[JsonProperty("byteStart", NullValueHandling = NullValueHandling.Ignore)]
	public long? ByteStart { get; set; }

	/// <summary>
	/// Size of the serialized asset data in bytes.
	/// Optional metadata field.
	/// </summary>
	[JsonProperty("byteSize", NullValueHandling = NullValueHandling.Ignore)]
	public int? ByteSize { get; set; }

	/// <summary>
	/// Serialized Unity object payload emitted inline as JSON.
	/// Null for unreadable assets (UnreadableObject, UnknownObject).
	/// </summary>
	[JsonProperty("data")]
	public JToken Data { get; set; } = JValue.CreateNull();

	[JsonProperty("hash", NullValueHandling = NullValueHandling.Ignore)]
	public string? Hash { get; set; }
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

	[JsonProperty("serializedVersion", NullValueHandling = NullValueHandling.Ignore)]
	public int? SerializedVersion { get; set; }
}
