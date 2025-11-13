using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Models.Common;
using AssetRipper.Tools.AssetDumper.Writers;
using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Aggregated export result for a logical table/domain.
/// Captures shard descriptors, schema metadata and optional key-index entries for manifest generation.
/// </summary>
public sealed class DomainExportResult
{
	public DomainExportResult(string domain, string tableId, string schemaPath, string format = "ndjson")
	{
		if (string.IsNullOrWhiteSpace(domain))
		{
			throw new ArgumentException("Domain cannot be null or empty", nameof(domain));
		}

		if (string.IsNullOrWhiteSpace(tableId))
		{
			throw new ArgumentException("Table identifier cannot be null or empty", nameof(tableId));
		}

		if (string.IsNullOrWhiteSpace(schemaPath))
		{
			throw new ArgumentException("Schema path cannot be null or empty", nameof(schemaPath));
		}

		Domain = domain;
		TableId = tableId;
		SchemaPath = OutputPathHelper.NormalizeRelativePath(schemaPath);
		Format = string.IsNullOrWhiteSpace(format) ? "ndjson" : format;
	}

	/// <summary>
	/// Domain identifier used within the exporter (e.g. "assets").
	/// </summary>
	public string Domain { get; }

	/// <summary>
	/// Manifest table identifier (e.g. "facts/assets").
	/// </summary>
	public string TableId { get; }

	/// <summary>
	/// Relative schema path referenced by the manifest entry.
	/// </summary>
	public string SchemaPath { get; }

	/// <summary>
	/// Logical data format key (e.g. "ndjson").
	/// </summary>
	public string Format { get; }

	/// <summary>
	/// Indicates whether this result represents a metrics table (e.g. scene_stats, asset_stats).
	/// </summary>
	public bool IsMetrics { get; set; }

	/// <summary>
	/// Optional metrics type identifier for classification (e.g. "scene_stats", "asset_stats").
	/// Only meaningful when <see cref="IsMetrics"/> is true.
	/// </summary>
	public string? MetricsType { get; set; }

	/// <summary>
	/// Optional single-file output associated with this table.
	/// </summary>
	public string? EntryFile { get; set; }

	/// <summary>
	/// Optional checksum covering the table entry file.
	/// </summary>
	public ManifestChecksum? Checksum { get; set; }

	/// <summary>
	/// Optional override for aggregated record count.
	/// </summary>
	public long? RecordCountOverride { get; set; }

	/// <summary>
	/// Optional override for aggregated byte count.
	/// </summary>
	public long? ByteCountOverride { get; set; }

	/// <summary>
	/// Collection of shard descriptors generated for this table.
	/// </summary>
	public List<ShardDescriptor> Shards { get; } = new();

	public List<KeyIndexEntry> IndexEntries { get; } = new();

	public bool HasIndex => IndexEntries.Count > 0;

	public bool HasShards => Shards.Count > 0;

	/// <summary>
	/// Relative directory mirroring the manifest table identifier where this table writes data.
	/// </summary>
	public string ShardDirectory => OutputPathHelper.NormalizeRelativePath(TableId);

	public long TotalRecords => RecordCountOverride ?? Shards.Sum(static shard => shard.Records);

	public long TotalBytes => ByteCountOverride ?? Shards.Sum(static shard => shard.Bytes);
}
