using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Schema-aligned type dictionary record for facts/types.ndjson.
/// </summary>
public sealed class TypeFactRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "types";

	[JsonProperty("classKey")]
	public int ClassKey { get; set; }

	[JsonProperty("classId")]
	public int ClassId { get; set; }

	[JsonProperty("className")]
	public string ClassName { get; set; } = string.Empty;

	[JsonProperty("typeId", NullValueHandling = NullValueHandling.Ignore)]
	public int? TypeId { get; set; }

	[JsonProperty("serializedTypeIndex", NullValueHandling = NullValueHandling.Ignore)]
	public int? SerializedTypeIndex { get; set; }

	[JsonProperty("scriptTypeIndex", NullValueHandling = NullValueHandling.Ignore)]
	public int? ScriptTypeIndex { get; set; }

	[JsonProperty("isStripped", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsStripped { get; set; }

	[JsonProperty("originalClassName", NullValueHandling = NullValueHandling.Ignore)]
	public string? OriginalClassName { get; set; }

	[JsonProperty("baseClassName", NullValueHandling = NullValueHandling.Ignore)]
	public string? BaseClassName { get; set; }

	[JsonProperty("isAbstract", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsAbstract { get; set; }

	[JsonProperty("isEditorOnly", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsEditorOnly { get; set; }

	[JsonProperty("isReleaseOnly", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsReleaseOnly { get; set; }

	[JsonProperty("monoScript", NullValueHandling = NullValueHandling.Ignore)]
	public TypeMonoScriptInfo? MonoScript { get; set; }

	[JsonProperty("notes", NullValueHandling = NullValueHandling.Ignore)]
	public string? Notes { get; set; }
}

/// <summary>
/// MonoScript information for MonoBehaviour types.
/// </summary>
public sealed class TypeMonoScriptInfo
{
	[JsonProperty("assemblyName", NullValueHandling = NullValueHandling.Ignore)]
	public string? AssemblyName { get; set; }

	[JsonProperty("namespace", NullValueHandling = NullValueHandling.Ignore)]
	public string? Namespace { get; set; }

	[JsonProperty("className", NullValueHandling = NullValueHandling.Ignore)]
	public string? ClassName { get; set; }

	[JsonProperty("scriptGuid", NullValueHandling = NullValueHandling.Ignore)]
	public string? ScriptGuid { get; set; }
}
