using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Schema-aligned type dictionary record for facts/types.ndjson.
/// </summary>
public sealed class TypeFactRecord
{
	[JsonProperty("classKey")]
	public int ClassKey { get; set; }

	[JsonProperty("classId")]
	public int ClassId { get; set; }

	[JsonProperty("className")]
	public string ClassName { get; set; } = string.Empty;

	[JsonProperty("scriptTypeIndex", NullValueHandling = NullValueHandling.Ignore)]
	public int? ScriptTypeIndex { get; set; }

	[JsonProperty("isStripped", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsStripped { get; set; }

	[JsonProperty("notes", NullValueHandling = NullValueHandling.Ignore)]
	public string? Notes { get; set; }
}
