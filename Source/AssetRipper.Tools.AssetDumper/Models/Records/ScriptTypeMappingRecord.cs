using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Script-type mapping relation with validation status.
/// </summary>
public sealed class ScriptTypeMappingRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "script_type_mapping";

	[JsonProperty("scriptPk")]
	public string ScriptPk { get; set; } = string.Empty;

	[JsonProperty("scriptGuid")]
	public string ScriptGuid { get; set; } = string.Empty;

	[JsonProperty("typeFullName")]
	public string TypeFullName { get; set; } = string.Empty;

	[JsonProperty("assemblyGuid")]
	public string AssemblyGuid { get; set; } = string.Empty;

	[JsonProperty("isValid")]
	public bool IsValid { get; set; }

	[JsonProperty("failureReason", NullValueHandling = NullValueHandling.Ignore)]
	public string? FailureReason { get; set; }
}
