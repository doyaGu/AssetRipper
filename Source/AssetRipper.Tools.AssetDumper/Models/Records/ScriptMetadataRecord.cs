using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// MonoScript fact record combining collection linkage, identifiers, and scene provenance.
/// </summary>
public sealed class ScriptMetadataRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "script_metadata";

	[JsonProperty("pk")]
	public string Pk { get; set; } = string.Empty;

	[JsonProperty("collectionId")]
	public string CollectionId { get; set; } = string.Empty;

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

	[JsonProperty("assemblyNameRaw", NullValueHandling = NullValueHandling.Ignore)]
	public string? AssemblyNameRaw { get; set; }

	[JsonProperty("assemblyGuid", NullValueHandling = NullValueHandling.Ignore)]
	public string? AssemblyGuid { get; set; }

	[JsonProperty("scriptGuid", NullValueHandling = NullValueHandling.Ignore)]
	public string? ScriptGuid { get; set; }

	[JsonProperty("scriptFileId", NullValueHandling = NullValueHandling.Ignore)]
	public int? ScriptFileId { get; set; }

	[JsonProperty("executionOrder")]
	public int ExecutionOrder { get; set; }

	[JsonProperty("propertiesHash", NullValueHandling = NullValueHandling.Ignore)]
	public string? PropertiesHash { get; set; }

	[JsonProperty("isPresent")]
	public bool IsPresent { get; set; }

	[JsonProperty("isGeneric", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsGeneric { get; set; }

	[JsonProperty("genericParameterCount", NullValueHandling = NullValueHandling.Ignore)]
	public int? GenericParameterCount { get; set; }

	[JsonProperty("scene", NullValueHandling = NullValueHandling.Ignore)]
	public ScriptSceneInfo? Scene { get; set; }
}

/// <summary>
/// Scene metadata associated with a script when sourced from a scene collection.
/// </summary>
public sealed class ScriptSceneInfo
{
	[JsonProperty("name")]
	public string Name { get; set; } = string.Empty;

	[JsonProperty("path")]
	public string Path { get; set; } = string.Empty;

	[JsonProperty("guid")]
	public string Guid { get; set; } = string.Empty;
}
