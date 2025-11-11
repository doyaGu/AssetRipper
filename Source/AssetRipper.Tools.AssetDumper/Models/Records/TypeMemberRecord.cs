using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Type member fact record (fields, properties, methods) for detailed code analysis.
/// </summary>
public sealed class TypeMemberRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "type_members";

	[JsonProperty("pk")]
	public string Pk { get; set; } = string.Empty;

	[JsonProperty("typeFullName")]
	public string TypeFullName { get; set; } = string.Empty;

	[JsonProperty("memberName")]
	public string MemberName { get; set; } = string.Empty;

	[JsonProperty("memberKind")]
	public string MemberKind { get; set; } = string.Empty;

	[JsonProperty("memberType")]
	public string MemberType { get; set; } = string.Empty;

	[JsonProperty("visibility")]
	public string Visibility { get; set; } = "public";

	[JsonProperty("isStatic")]
	public bool IsStatic { get; set; }

	[JsonProperty("isVirtual", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsVirtual { get; set; }

	[JsonProperty("isOverride", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsOverride { get; set; }

	[JsonProperty("isSealed", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsSealed { get; set; }

	[JsonProperty("serialized")]
	public bool Serialized { get; set; }

	[JsonProperty("attributes", NullValueHandling = NullValueHandling.Ignore)]
	public string[]? Attributes { get; set; }
}
