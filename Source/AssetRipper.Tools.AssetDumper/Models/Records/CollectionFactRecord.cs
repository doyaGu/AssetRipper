using Newtonsoft.Json;
using System.Collections.Generic;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Schema-aligned record representing a Unity SerializedFile collection.
/// </summary>
public sealed class CollectionFactRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "collections";

	[JsonProperty("collectionId")]
	public string CollectionId { get; set; } = string.Empty;

	[JsonProperty("name")]
	public string Name { get; set; } = string.Empty;

	[JsonProperty("collectionType", NullValueHandling = NullValueHandling.Ignore)]
	public string? CollectionType { get; set; }

	[JsonProperty("friendlyName", NullValueHandling = NullValueHandling.Ignore)]
	public string? FriendlyName { get; set; }

	[JsonProperty("filePath", NullValueHandling = NullValueHandling.Ignore)]
	public string? FilePath { get; set; }

	[JsonProperty("bundleName", NullValueHandling = NullValueHandling.Ignore)]
	public string? BundleName { get; set; }

	[JsonProperty("platform")]
	public string Platform { get; set; } = string.Empty;

	[JsonProperty("unityVersion")]
	public string UnityVersion { get; set; } = string.Empty;

	[JsonProperty("originalUnityVersion", NullValueHandling = NullValueHandling.Ignore)]
	public string? OriginalUnityVersion { get; set; }

	[JsonProperty("formatVersion", NullValueHandling = NullValueHandling.Ignore)]
	public int? FormatVersion { get; set; }

	[JsonProperty("endian")]
	public string Endian { get; set; } = string.Empty;

	[JsonProperty("flagsRaw", NullValueHandling = NullValueHandling.Ignore)]
	public string? FlagsRaw { get; set; }

	[JsonProperty("flags", NullValueHandling = NullValueHandling.Ignore)]
	public IReadOnlyList<string>? Flags { get; set; }

	[JsonProperty("isSceneCollection", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsSceneCollection { get; set; }

	[JsonProperty("bundle")]
	public BundleRef Bundle { get; set; } = new();

	[JsonProperty("scene", NullValueHandling = NullValueHandling.Ignore)]
	public SceneRef? Scene { get; set; }

	[JsonProperty("collectionIndex", NullValueHandling = NullValueHandling.Ignore)]
	public int? CollectionIndex { get; set; }

	[JsonProperty("dependencies")]
	public List<string> Dependencies { get; set; } = new();

	[JsonProperty("dependencyIndices", NullValueHandling = NullValueHandling.Ignore)]
	public Dictionary<string, int>? DependencyIndices { get; set; }

	[JsonProperty("assetCount")]
	public int AssetCount { get; set; }

	[JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)]
	public CollectionSourceRecord? Source { get; set; }

	[JsonProperty("unity", NullValueHandling = NullValueHandling.Ignore)]
	public CollectionUnityRecord? Unity { get; set; }
}

public sealed class CollectionSourceRecord
{
	[JsonProperty("uri", NullValueHandling = NullValueHandling.Ignore)]
	public string? Uri { get; set; }

	[JsonProperty("offset", NullValueHandling = NullValueHandling.Ignore)]
	public long? Offset { get; set; }

	[JsonProperty("size", NullValueHandling = NullValueHandling.Ignore)]
	public long? Size { get; set; }
}

public sealed class CollectionUnityRecord
{
	[JsonProperty("builtInClassification", NullValueHandling = NullValueHandling.Ignore)]
	public string? BuiltInClassification { get; set; }
}
