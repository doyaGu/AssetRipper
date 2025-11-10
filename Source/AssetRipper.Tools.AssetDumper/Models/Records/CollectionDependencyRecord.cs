using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Records;

/// <summary>
/// Collection dependency edge record for relations/collection_dependencies.ndjson.
/// </summary>
public sealed class CollectionDependencyRecord
{
	[JsonProperty("sourceCollection")]
	public string SourceCollection { get; set; } = string.Empty;

	[JsonProperty("dependencyIndex")]
	public int DependencyIndex { get; set; }

	[JsonProperty("targetCollection")]
	public string? TargetCollection { get; set; }

	[JsonProperty("fileIdentifier", NullValueHandling = NullValueHandling.Ignore)]
	public FileIdentifierRecord? FileIdentifier { get; set; }
}

/// <summary>
/// Unity FileIdentifier information.
/// </summary>
public sealed class FileIdentifierRecord
{
	[JsonProperty("guid", NullValueHandling = NullValueHandling.Ignore)]
	public string? Guid { get; set; }

	[JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
	public int? Type { get; set; }

	[JsonProperty("pathName", NullValueHandling = NullValueHandling.Ignore)]
	public string? PathName { get; set; }
}
