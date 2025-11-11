using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Type inheritance relation record for hierarchy analysis.
/// </summary>
public sealed class TypeInheritanceRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "type_inheritance";

	[JsonProperty("derivedType")]
	public string DerivedType { get; set; } = string.Empty;

	[JsonProperty("baseType")]
	public string BaseType { get; set; } = string.Empty;

	[JsonProperty("baseAssembly", NullValueHandling = NullValueHandling.Ignore)]
	public string? BaseAssembly { get; set; }

	[JsonProperty("isDirectBase")]
	public bool IsDirectBase { get; set; }

	[JsonProperty("inheritanceDepth")]
	public int InheritanceDepth { get; set; }
}
