using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Rich metadata describing a MonoScript asset as part of the script facts domain.
/// </summary>
public sealed class ScriptMetadataRecord
{
	[JsonProperty("pk")]
	public string Pk { get; set; } = string.Empty;

	[JsonProperty("collectionId")]
	public string CollectionId { get; set; } = string.Empty;

	[JsonProperty("collectionName")]
	public string CollectionName { get; set; } = string.Empty;

	[JsonProperty("bundleName", NullValueHandling = NullValueHandling.Ignore)]
	public string? BundleName { get; set; }

	[JsonProperty("collectionFlags", NullValueHandling = NullValueHandling.Ignore)]
	public string? CollectionFlags { get; set; }

	[JsonProperty("collectionPlatform", NullValueHandling = NullValueHandling.Ignore)]
	public string? CollectionPlatform { get; set; }

	[JsonProperty("collectionVersion", NullValueHandling = NullValueHandling.Ignore)]
	public string? CollectionVersion { get; set; }

	[JsonProperty("collectionFilePath", NullValueHandling = NullValueHandling.Ignore)]
	public string? CollectionFilePath { get; set; }

	[JsonProperty("isSceneCollection", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsSceneCollection { get; set; }

	[JsonProperty("pathId")]
	public long PathId { get; set; }

	[JsonProperty("classId")]
	public int ClassId { get; set; }

	[JsonProperty("className")]
	public string ClassName { get; set; } = string.Empty;

	[JsonProperty("fullName")]
	public string FullName { get; set; } = string.Empty;

	[JsonProperty("namespace", NullValueHandling = NullValueHandling.Ignore)]
	public string? Namespace { get; set; }

	[JsonProperty("assemblyName")]
	public string AssemblyName { get; set; } = string.Empty;

	[JsonProperty("executionOrder")]
	public int ExecutionOrder { get; set; }

	[JsonProperty("scriptGuid", NullValueHandling = NullValueHandling.Ignore)]
	public string? ScriptGuid { get; set; }

	[JsonProperty("assemblyGuid", NullValueHandling = NullValueHandling.Ignore)]
	public string? AssemblyGuid { get; set; }

	[JsonProperty("scriptFileId", NullValueHandling = NullValueHandling.Ignore)]
	public int? ScriptFileId { get; set; }

	[JsonProperty("propertiesHash", NullValueHandling = NullValueHandling.Ignore)]
	public string? PropertiesHash { get; set; }

	[JsonProperty("scene", NullValueHandling = NullValueHandling.Ignore)]
	public ScriptSceneMetadata? Scene { get; set; }
}

/// <summary>
/// Scene information for MonoScripts that originate from scene collections.
/// </summary>
public sealed class ScriptSceneMetadata
{
	[JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
	public string? Name { get; set; }

	[JsonProperty("path", NullValueHandling = NullValueHandling.Ignore)]
	public string? Path { get; set; }

	[JsonProperty("guid", NullValueHandling = NullValueHandling.Ignore)]
	public string? Guid { get; set; }
}
