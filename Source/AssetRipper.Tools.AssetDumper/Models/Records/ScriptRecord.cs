using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Minimal MonoScript metadata line for the "scripts" NDJSON domain.
/// </summary>
public class ScriptRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "scripts";

	[JsonProperty("collectionId")]
	public string CollectionId { get; set; } = string.Empty;

	[JsonProperty("collection")]
	public string Collection { get; set; } = string.Empty;

	[JsonProperty("bundleName")]
	public string BundleName { get; set; } = string.Empty;

	[JsonProperty("platform")]
	public string Platform { get; set; } = string.Empty;

	[JsonProperty("version")]
	public string Version { get; set; } = string.Empty;

	[JsonProperty("flags", NullValueHandling = NullValueHandling.Ignore)]
	public string? Flags { get; set; }

	[JsonProperty("file", NullValueHandling = NullValueHandling.Ignore)]
	public string? File { get; set; }

	[JsonProperty("pathID")]
	public long PathId { get; set; }

	[JsonProperty("classID")]
	public int ClassId { get; set; }

	[JsonProperty("className")]
	public string ClassName { get; set; } = string.Empty;

	[JsonProperty("fullName")]
	public string FullName { get; set; } = string.Empty;

	[JsonProperty("namespace", NullValueHandling = NullValueHandling.Ignore)]
	public string? Namespace { get; set; }

	[JsonProperty("assemblyName")]
	public string AssemblyName { get; set; } = string.Empty;

	[JsonProperty("assemblyGuid", NullValueHandling = NullValueHandling.Ignore)]
	public string? AssemblyGuid { get; set; }

	[JsonProperty("scriptGuid")]
	public string ScriptGuid { get; set; } = string.Empty;

	[JsonProperty("scriptFileId", NullValueHandling = NullValueHandling.Ignore)]
	public int? ScriptFileId { get; set; }

	[JsonProperty("executionOrder")]
	public int ExecutionOrder { get; set; }

	[JsonProperty("propertiesHash", NullValueHandling = NullValueHandling.Ignore)]
	public string? PropertiesHash { get; set; }
}
