using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Script-type mapping relation record with validation status.
/// Maps MonoScript assets to .NET TypeDefinitions, tracking resolution success/failure.
/// </summary>
public sealed class ScriptTypeMappingRecord
{
	/// <summary>
	/// Domain identifier, always "script_type_mapping".
	/// </summary>
	[JsonProperty("domain")]
	public string Domain { get; set; } = "script_type_mapping";

	/// <summary>
	/// MonoScript primary key (collectionId:pathId format).
	/// Links to assets with ClassID 115 (MonoScript).
	/// </summary>
	[JsonProperty("scriptPk")]
	public string ScriptPk { get; set; } = string.Empty;

	/// <summary>
	/// Script GUID computed via ScriptHashing.CalculateScriptGuid().
	/// Used for cross-reference with Unity's .meta files.
	/// </summary>
	[JsonProperty("scriptGuid")]
	public string ScriptGuid { get; set; } = string.Empty;

	/// <summary>
	/// Fully qualified .NET type name (Namespace.ClassName).
	/// Source: MonoScriptExtensions.GetFullName().
	/// Example: "UnityEngine.UI.Button"
	/// </summary>
	[JsonProperty("typeFullName")]
	public string TypeFullName { get; set; } = string.Empty;

	/// <summary>
	/// Assembly GUID (32-character hex string, uppercase).
	/// Links to assemblies.pk.
	/// Source: ScriptHashing.CalculateAssemblyGuid()
	/// </summary>
	[JsonProperty("assemblyGuid")]
	public string AssemblyGuid { get; set; } = string.Empty;

	/// <summary>
	/// Assembly name after FixAssemblyName() processing.
	/// Source: MonoScriptExtensions.GetAssemblyNameFixed().
	/// Example: "Assembly-CSharp", "UnityEngine.CoreModule"
	/// </summary>
	[JsonProperty("assemblyName")]
	public string AssemblyName { get; set; } = string.Empty;

	/// <summary>
	/// Type namespace (empty string for global namespace).
	/// Source: IMonoScript.Namespace.
	/// Example: "Game.Controllers"
	/// </summary>
	[JsonProperty("namespace", NullValueHandling = NullValueHandling.Ignore)]
	public string? Namespace { get; set; }

	/// <summary>
	/// Simple class name without namespace.
	/// Source: IMonoScript.ClassName_R.
	/// Example: "PlayerController"
	/// </summary>
	[JsonProperty("className")]
	public string ClassName { get; set; } = string.Empty;

	/// <summary>
	/// Whether TypeDefinition was successfully resolved via MonoScriptExtensions.GetBehaviourType().
	/// True if type exists in assembly, false if resolution failed.
	/// </summary>
	[JsonProperty("isValid")]
	public bool IsValid { get; set; }

	/// <summary>
	/// Human-readable reason for resolution failure (present only if IsValid=false).
	/// Source: GetBehaviourType() out parameter.
	/// Example: "Can't find type: Game.Controllers.PlayerController"
	/// </summary>
	[JsonProperty("failureReason", NullValueHandling = NullValueHandling.Ignore)]
	public string? FailureReason { get; set; }

	/// <summary>
	/// Whether the script represents a generic type (e.g., List&lt;T&gt;).
	/// Source: TypeDefinition.HasGenericParameters
	/// </summary>
	[JsonProperty("isGeneric", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsGeneric { get; set; }

	/// <summary>
	/// Number of generic type parameters (0 for non-generic types).
	/// Source: TypeDefinition.GenericParameters.Count
	/// </summary>
	[JsonProperty("genericParameterCount", NullValueHandling = NullValueHandling.Ignore)]
	public int? GenericParameterCount { get; set; }

	/// <summary>
	/// ScriptIdentifier.UniqueName for debugging (format: AssemblyName::Namespace::ClassName).
	/// Useful for diagnosing resolution failures.
	/// </summary>
	[JsonProperty("scriptIdentifier", NullValueHandling = NullValueHandling.Ignore)]
	public string? ScriptIdentifier { get; set; }
}
