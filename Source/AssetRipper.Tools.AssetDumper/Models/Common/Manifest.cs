using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Common;

/// <summary>
/// Root manifest document describing an AssetDumper v2 export.
/// </summary>
public sealed class Manifest
{
	[JsonProperty("version")]
	public string Version { get; set; } = "2.0";

	[JsonProperty("createdAt")]
	public string CreatedAt { get; set; } = string.Empty;

	[JsonProperty("producer", NullValueHandling = NullValueHandling.Ignore)]
	public ManifestProducer? Producer { get; set; }

	[JsonProperty("formats")]
	public Dictionary<string, ManifestFormat> Formats { get; } = new(StringComparer.OrdinalIgnoreCase);

	[JsonProperty("tables")]
	public Dictionary<string, ManifestTable> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);

	[JsonProperty("indexes", NullValueHandling = NullValueHandling.Ignore)]
	public Dictionary<string, ManifestIndex>? Indexes { get; set; }

	[JsonProperty("statistics", NullValueHandling = NullValueHandling.Ignore)]
	public ManifestStatistics? Statistics { get; set; }

	[JsonProperty("metadata", NullValueHandling = NullValueHandling.Ignore)]
	public Dictionary<string, object>? Metadata { get; set; }
}

public sealed class ManifestProducer
{
	[JsonProperty("name")]
	public string Name { get; set; } = string.Empty;

	[JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
	public string? Version { get; set; }

	[JsonProperty("commit", NullValueHandling = NullValueHandling.Ignore)]
	public string? Commit { get; set; }

	[JsonProperty("assetRipperVersion", NullValueHandling = NullValueHandling.Ignore)]
	public string? AssetRipperVersion { get; set; }

	[JsonProperty("unityVersion", NullValueHandling = NullValueHandling.Ignore)]
	public string? UnityVersion { get; set; }

	[JsonProperty("projectName", NullValueHandling = NullValueHandling.Ignore)]
	public string? ProjectName { get; set; }
}

public sealed class ManifestFormat
{
	[JsonProperty("mime")]
	public string Mime { get; set; } = "application/x-ndjson";

	[JsonProperty("extension", NullValueHandling = NullValueHandling.Ignore)]
	public string? Extension { get; set; }

	[JsonProperty("compression", NullValueHandling = NullValueHandling.Ignore)]
	public string? Compression { get; set; }
}

public sealed class ManifestTable
{
	[JsonProperty("schema")]
	public string Schema { get; set; } = string.Empty;

	[JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
	public string? Format { get; set; }

	[JsonProperty("file", NullValueHandling = NullValueHandling.Ignore)]
	public string? File { get; set; }

	[JsonProperty("sharded")]
	public bool Sharded { get; set; }

	[JsonProperty("shards", NullValueHandling = NullValueHandling.Ignore)]
	public List<ManifestTableShard>? Shards { get; set; }

	[JsonProperty("indexes", NullValueHandling = NullValueHandling.Ignore)]
	public List<string>? Indexes { get; set; }

	[JsonProperty("recordCount", NullValueHandling = NullValueHandling.Ignore)]
	public long? RecordCount { get; set; }

	[JsonProperty("byteCount", NullValueHandling = NullValueHandling.Ignore)]
	public long? ByteCount { get; set; }

	[JsonProperty("checksum", NullValueHandling = NullValueHandling.Ignore)]
	public ManifestChecksum? Checksum { get; set; }

	[JsonProperty("statistics", NullValueHandling = NullValueHandling.Ignore)]
	public ManifestTableStatistics? Statistics { get; set; }
}

public sealed class ManifestTableShard
{
	[JsonProperty("path")]
	public string Path { get; set; } = string.Empty;

	[JsonProperty("records")]
	public long Records { get; set; }

	[JsonProperty("bytes")]
	public long Bytes { get; set; }

	[JsonProperty("compression", NullValueHandling = NullValueHandling.Ignore)]
	public string? Compression { get; set; }

	[JsonProperty("uncompressedBytes", NullValueHandling = NullValueHandling.Ignore)]
	public long? UncompressedBytes { get; set; }

	[JsonProperty("frameSize", NullValueHandling = NullValueHandling.Ignore)]
	public int? FrameSize { get; set; }

	[JsonProperty("firstKey", NullValueHandling = NullValueHandling.Ignore)]
	public string? FirstKey { get; set; }

	[JsonProperty("lastKey", NullValueHandling = NullValueHandling.Ignore)]
	public string? LastKey { get; set; }

	[JsonProperty("sha256", NullValueHandling = NullValueHandling.Ignore)]
	public string? Sha256 { get; set; }
}

public sealed class ManifestChecksum
{
	[JsonProperty("algo")]
	public string Algorithm { get; set; } = string.Empty;

	[JsonProperty("value")]
	public string Value { get; set; } = string.Empty;
}

public sealed class ManifestStatistics
{
	[JsonProperty("totalRecords")]
	public long TotalRecords { get; set; }

	[JsonProperty("totalBytes")]
	public long TotalBytes { get; set; }

	[JsonProperty("tables")]
	public Dictionary<string, ManifestTableStatistics> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ManifestTableStatistics
{
	[JsonProperty("records")]
	public long Records { get; set; }

	[JsonProperty("bytes")]
	public long Bytes { get; set; }

	[JsonProperty("shards")]
	public int Shards { get; set; }
}

public sealed class ManifestIndex
{
	[JsonProperty("type")]
	public string Type { get; set; } = string.Empty;

	[JsonProperty("path")]
	public string Path { get; set; } = string.Empty;

	[JsonProperty("domain", NullValueHandling = NullValueHandling.Ignore)]
	public string? Domain { get; set; }

	[JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
	public string? Format { get; set; }

	[JsonProperty("recordCount", NullValueHandling = NullValueHandling.Ignore)]
	public long? RecordCount { get; set; }

	[JsonProperty("checksum", NullValueHandling = NullValueHandling.Ignore)]
	public ManifestChecksum? Checksum { get; set; }

	[JsonProperty("createdAt", NullValueHandling = NullValueHandling.Ignore)]
	public string? CreatedAt { get; set; }

	[JsonProperty("metadata", NullValueHandling = NullValueHandling.Ignore)]
	public Dictionary<string, object>? Metadata { get; set; }
}
