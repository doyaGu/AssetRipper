using System.Text.Json.Serialization;

namespace AssetRipper.Tools.AssetDumper.Facts;

/// <summary>
/// Model for types domain.
/// Represents Unity type information dictionary to avoid repeating type metadata in every asset record.
/// </summary>
public sealed class TypeRecord
{
	/// <summary>Fixed domain identifier for types table.</summary>
	[JsonPropertyName("domain")]
	public string Domain { get; set; } = "types";

	/// <summary>Stable integer identifier assigned by the exporter.</summary>
	[JsonPropertyName("classKey")]
	public required int ClassKey { get; set; }

	/// <summary>Unity ClassID (114 = MonoBehaviour, etc.).</summary>
	[JsonPropertyName("classId")]
	public required int ClassId { get; set; }

	/// <summary>Unity type name.</summary>
	[JsonPropertyName("className")]
	public required string ClassName { get; set; }

	/// <summary>Type ID of the object. For non-MonoBehaviour types, equals classId.</summary>
	[JsonPropertyName("typeId")]
	public int? TypeId { get; set; }

	/// <summary>Index in the SerializedFile.Types array (Unity 5+). -1 if not applicable.</summary>
	[JsonPropertyName("serializedTypeIndex")]
	public int? SerializedTypeIndex { get; set; }

	/// <summary>Script type index for MonoBehaviour types. -1 if not a MonoBehaviour.</summary>
	[JsonPropertyName("scriptTypeIndex")]
	public int? ScriptTypeIndex { get; set; }

	/// <summary>Whether the type definition was stripped from the build.</summary>
	[JsonPropertyName("isStripped")]
	public bool? IsStripped { get; set; }

	/// <summary>Original Unity type name before any processing.</summary>
	[JsonPropertyName("originalClassName")]
	public string? OriginalClassName { get; set; }

	/// <summary>Name of the base class if it exists.</summary>
	[JsonPropertyName("baseClassName")]
	public string? BaseClassName { get; set; }

	/// <summary>Whether the class is abstract.</summary>
	[JsonPropertyName("isAbstract")]
	public bool? IsAbstract { get; set; }

	/// <summary>Whether the class only appears in editor builds.</summary>
	[JsonPropertyName("isEditorOnly")]
	public bool? IsEditorOnly { get; set; }

	/// <summary>Whether the class only appears in game builds.</summary>
	[JsonPropertyName("isReleaseOnly")]
	public bool? IsReleaseOnly { get; set; }

	/// <summary>MonoScript information for MonoBehaviour types.</summary>
	[JsonPropertyName("monoScript")]
	public MonoScriptInfo? MonoScript { get; set; }

	/// <summary>Additional notes or comments about this type.</summary>
	[JsonPropertyName("notes")]
	public string? Notes { get; set; }
}

/// <summary>
/// MonoScript information for MonoBehaviour types (ClassID 114).
/// </summary>
public sealed class MonoScriptInfo
{
	/// <summary>Assembly name containing the script.</summary>
	[JsonPropertyName("assemblyName")]
	public string? AssemblyName { get; set; }

	/// <summary>Namespace of the script class.</summary>
	[JsonPropertyName("namespace")]
	public string? Namespace { get; set; }

	/// <summary>Class name of the script.</summary>
	[JsonPropertyName("className")]
	public string? ClassName { get; set; }

	/// <summary>GUID of the MonoScript asset.</summary>
	[JsonPropertyName("scriptGuid")]
	public string? ScriptGuid { get; set; }
}
