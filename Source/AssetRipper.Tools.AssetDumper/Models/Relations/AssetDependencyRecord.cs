using Newtonsoft.Json;
using AssetRipper.Tools.AssetDumper.Models.Common;
using AssetRipper.Tools.AssetDumper.Models.Facts;

namespace AssetRipper.Tools.AssetDumper.Models.Relations;

/// <summary>
/// Schema-aligned relation linking the source asset to a referenced target asset.
/// Represents a single dependency relationship extracted from FetchDependencies().
/// </summary>
public sealed class AssetDependencyRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "asset_dependencies";

	[JsonProperty("from")]
	public AssetPrimaryKey From { get; set; } = new();

	[JsonProperty("to")]
	public AssetPrimaryKey To { get; set; } = new();

	[JsonProperty("edge")]
	public AssetDependencyEdge Edge { get; set; } = new();

	[JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
	public string? Status { get; set; }

	[JsonProperty("targetType", NullValueHandling = NullValueHandling.Ignore)]
	public string? TargetType { get; set; }

	[JsonProperty("notes", NullValueHandling = NullValueHandling.Ignore)]
	public string? Notes { get; set; }
}

/// <summary>
/// Additional metadata describing the dependency edge.
/// Corresponds to the (string, PPtr) tuple returned by FetchDependencies().
/// </summary>
public sealed class AssetDependencyEdge
{
	/// <summary>
	/// Type of dependency relationship.
	/// - pptr: Standard PPtr serialized reference
	/// - external: Cross-file reference (FileID > 0)
	/// - internal: Same-file reference (FileID == 0)
	/// - array_element: Reference in array or list
	/// - dictionary_key: Dictionary key reference
	/// - dictionary_value: Dictionary value reference
	/// </summary>
	[JsonProperty("kind")]
	public string Kind { get; set; } = "pptr";

	/// <summary>
	/// Full field path from FetchDependencies() (e.g., "m_Materials[2]", "components[0].m_GameObject").
	/// This is the first element of the (string, PPtr) tuple.
	/// Required field per schema.
	/// </summary>
	[JsonProperty("field")]
	public string Field { get; set; } = string.Empty;

	/// <summary>
	/// Type of the field holding the reference (e.g., "PPtr&lt;Material&gt;", "PPtr&lt;GameObject&gt;[]").
	/// Useful for type-based dependency analysis.
	/// </summary>
	[JsonProperty("fieldType", NullValueHandling = NullValueHandling.Ignore)]
	public string? FieldType { get; set; }

	/// <summary>
	/// Original FileID from PPtr structure.
	/// - 0: Same-file reference
	/// - &gt; 0: Index into dependency list
	/// - &lt; 0: Built-in resource (-1=BuiltinExtra, -2=DefaultResource, -3=EditorResource)
	/// </summary>
	[JsonProperty("fileId", NullValueHandling = NullValueHandling.Ignore)]
	public int? FileId { get; set; }

	/// <summary>
	/// Zero-based index if the reference is in an array or list.
	/// Extracted from field path (e.g., "m_Materials[2]" -> arrayIndex=2).
	/// </summary>
	[JsonProperty("arrayIndex", NullValueHandling = NullValueHandling.Ignore)]
	public int? ArrayIndex { get; set; }

	/// <summary>
	/// Whether the reference field can legally be null (PathID == 0).
	/// Useful for distinguishing intentional null references from missing assets.
	/// </summary>
	[JsonProperty("isNullable", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsNullable { get; set; }

	/// <summary>
	/// Legacy field for backward compatibility. Prefer using IsNullable.
	/// </summary>
	[JsonProperty("optional", NullValueHandling = NullValueHandling.Ignore)]
	public bool? Optional { get; set; }
}
