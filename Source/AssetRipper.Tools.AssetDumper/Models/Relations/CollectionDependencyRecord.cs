using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Relations;

/// <summary>
/// Collection dependency edge record for relations/collection_dependencies.ndjson.
/// Represents dependencies between AssetCollections with enhanced resolution status.
/// </summary>
public sealed class CollectionDependencyRecord
{
	/// <summary>
	/// Domain identifier, always "collection_dependencies".
	/// </summary>
	[JsonProperty("domain")]
	public string Domain { get; set; } = "collection_dependencies";

	/// <summary>
	/// The collection declaring the dependency.
	/// </summary>
	[JsonProperty("sourceCollection")]
	public string SourceCollection { get; set; } = string.Empty;

	/// <summary>
	/// Index in the source collection's dependency list.
	/// Index 0 is always the collection itself (self-reference).
	/// Corresponds to PPtr.FileID values.
	/// </summary>
	[JsonProperty("dependencyIndex")]
	public int DependencyIndex { get; set; }

	/// <summary>
	/// The target collection; null if unresolved or missing.
	/// </summary>
	[JsonProperty("targetCollection")]
	public string? TargetCollection { get; set; }

	/// <summary>
	/// Whether the dependency was successfully resolved (TargetCollection is not null).
	/// Enables quick filtering of missing dependencies.
	/// </summary>
	[JsonProperty("resolved", NullValueHandling = NullValueHandling.Ignore)]
	public bool? Resolved { get; set; }

	/// <summary>
	/// How the dependency was discovered.
	/// Values: "serialized" (from SerializedFile), "dynamic" (via AddDependency), "builtin" (Unity built-in).
	/// </summary>
	[JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)]
	public string? Source { get; set; }

	/// <summary>
	/// Original Unity FileIdentifier from SerializedFile.Dependencies array.
	/// Present only for serialized dependencies.
	/// </summary>
	[JsonProperty("fileIdentifier", NullValueHandling = NullValueHandling.Ignore)]
	public FileIdentifierRecord? FileIdentifier { get; set; }
}

/// <summary>
/// Unity FileIdentifier information from SerializedFile.Dependencies.
/// </summary>
public sealed class FileIdentifierRecord
{
	/// <summary>
	/// Unity GUID of the dependency file.
	/// </summary>
	[JsonProperty("guid", NullValueHandling = NullValueHandling.Ignore)]
	public string? Guid { get; set; }

	/// <summary>
	/// FileIdentifier type enumeration.
	/// 0 = Asset, 1 = Serialized, 2 = Meta, 3 = BuiltinExtra, 4 = Unknown.
	/// </summary>
	[JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
	public int? Type { get; set; }

	/// <summary>
	/// Original file path from Unity (may be relative or absolute).
	/// </summary>
	[JsonProperty("pathName", NullValueHandling = NullValueHandling.Ignore)]
	public string? PathName { get; set; }
}
