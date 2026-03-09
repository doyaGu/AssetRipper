using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models.Common;

namespace AssetRipper.Tools.AssetDumper.Generators;

/// <summary>
/// Assembles manifest documents from newly emitted tables plus an optional baseline manifest.
/// </summary>
internal sealed class ManifestAssembler
{
	private readonly Options _options;

	public ManifestAssembler(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	public Manifest Assemble(
		ManifestProducer producer,
		IReadOnlyList<DomainExportResult> updatedResults,
		Dictionary<string, ManifestIndex>? updatedIndexes = null,
		Manifest? baseline = null)
	{
		if (producer is null)
		{
			throw new ArgumentNullException(nameof(producer));
		}

		if (updatedResults is null)
		{
			throw new ArgumentNullException(nameof(updatedResults));
		}

		Manifest manifest = CloneBaseline(baseline);
		manifest.CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
		manifest.Producer = CloneProducer(producer);

		MergeIndexes(manifest, updatedIndexes);
		foreach (DomainExportResult result in updatedResults.OrderBy(static result => result.TableId, StringComparer.OrdinalIgnoreCase))
		{
			manifest.Tables[result.TableId] = CreateManifestTable(result, manifest.Indexes);
		}

		manifest.Formats.Clear();
		PopulateFormats(manifest);
		manifest.Statistics = BuildStatistics(manifest);
		manifest.Metadata = BuildMetadata(manifest);
		if (manifest.Indexes is { Count: 0 })
		{
			manifest.Indexes = null;
		}

		return manifest;
	}

	private static Manifest CloneBaseline(Manifest? baseline)
	{
		Manifest manifest = new()
		{
			Version = baseline?.Version ?? "2.0"
		};

		if (baseline is null)
		{
			return manifest;
		}

		foreach ((string key, ManifestFormat format) in baseline.Formats)
		{
			manifest.Formats[key] = CloneFormat(format);
		}

		foreach ((string key, ManifestTable table) in baseline.Tables)
		{
			manifest.Tables[key] = CloneTable(table);
		}

		if (baseline.Indexes is { Count: > 0 })
		{
			manifest.Indexes = baseline.Indexes.ToDictionary(
				static pair => pair.Key,
				static pair => CloneIndex(pair.Value),
				StringComparer.OrdinalIgnoreCase);
		}

		return manifest;
	}

	private static ManifestProducer CloneProducer(ManifestProducer producer)
	{
		return new ManifestProducer
		{
			Name = producer.Name,
			Version = producer.Version,
			Commit = producer.Commit,
			AssetRipperVersion = producer.AssetRipperVersion,
			UnityVersion = producer.UnityVersion,
			ProjectName = producer.ProjectName
		};
	}

	private static ManifestFormat CloneFormat(ManifestFormat format)
	{
		return new ManifestFormat
		{
			Mime = format.Mime,
			Extension = format.Extension,
			Compression = format.Compression
		};
	}

	private static ManifestTable CloneTable(ManifestTable table)
	{
		return new ManifestTable
		{
			Schema = table.Schema,
			Format = table.Format,
			File = table.File,
			Sharded = table.Sharded,
			Shards = table.Shards?.Select(CloneShard).ToList(),
			Indexes = table.Indexes != null ? new List<string>(table.Indexes) : null,
			RecordCount = table.RecordCount,
			ByteCount = table.ByteCount,
			Checksum = table.Checksum != null
				? new ManifestChecksum { Algorithm = table.Checksum.Algorithm, Value = table.Checksum.Value }
				: null,
			Statistics = table.Statistics != null
				? new ManifestTableStatistics
				{
					Records = table.Statistics.Records,
					Bytes = table.Statistics.Bytes,
					Shards = table.Statistics.Shards
				}
				: null
		};
	}

	private static ManifestTableShard CloneShard(ManifestTableShard shard)
	{
		return new ManifestTableShard
		{
			Path = shard.Path,
			Records = shard.Records,
			Bytes = shard.Bytes,
			Compression = shard.Compression,
			UncompressedBytes = shard.UncompressedBytes,
			FrameSize = shard.FrameSize,
			FirstKey = shard.FirstKey,
			LastKey = shard.LastKey,
			Sha256 = shard.Sha256
		};
	}

	private static ManifestIndex CloneIndex(ManifestIndex index)
	{
		return new ManifestIndex
		{
			Type = index.Type,
			Path = index.Path,
			Domain = index.Domain,
			Format = index.Format,
			RecordCount = index.RecordCount,
			Checksum = index.Checksum != null
				? new ManifestChecksum { Algorithm = index.Checksum.Algorithm, Value = index.Checksum.Value }
				: null,
			CreatedAt = index.CreatedAt,
			Metadata = index.Metadata != null
				? new Dictionary<string, object>(index.Metadata, StringComparer.OrdinalIgnoreCase)
				: null
		};
	}

	private static void MergeIndexes(Manifest manifest, Dictionary<string, ManifestIndex>? updatedIndexes)
	{
		if (updatedIndexes is null || updatedIndexes.Count == 0)
		{
			return;
		}

		manifest.Indexes ??= new Dictionary<string, ManifestIndex>(StringComparer.OrdinalIgnoreCase);
		foreach ((string key, ManifestIndex index) in updatedIndexes)
		{
			manifest.Indexes[key] = CloneIndex(index);
		}
	}

	private static ManifestTable CreateManifestTable( DomainExportResult result, Dictionary<string, ManifestIndex>? indexes)
	{
		List<ManifestTableShard>? shardEntries = null;
		if (result.HasShards)
		{
			shardEntries = result.Shards
				.Select(static shard => new ManifestTableShard
				{
					Path = OutputPathHelper.NormalizeRelativePath(shard.Shard),
					Records = shard.Records,
					Bytes = shard.Bytes,
					Compression = shard.Compression,
					UncompressedBytes = shard.UncompressedBytes,
					FrameSize = shard.FrameSize,
					FirstKey = shard.FirstKey,
					LastKey = shard.LastKey,
					Sha256 = shard.Sha256
				})
				.OrderBy(static shard => shard.Path, StringComparer.Ordinal)
				.ToList();
		}

		long totalRecords = result.TotalRecords;
		long totalBytes = result.TotalBytes;
		bool shouldEmitCounts = result.HasShards || result.RecordCountOverride.HasValue || totalRecords > 0;
		bool shouldEmitBytes = result.HasShards || result.ByteCountOverride.HasValue || totalBytes > 0;

		ManifestTableStatistics? tableStatistics = null;
		if (result.HasShards || result.RecordCountOverride.HasValue || result.ByteCountOverride.HasValue)
		{
			tableStatistics = new ManifestTableStatistics
			{
				Records = totalRecords,
				Bytes = totalBytes,
				Shards = shardEntries?.Count ?? 0
			};
		}

		ManifestTable table = new()
		{
			Schema = result.SchemaPath,
			Format = result.Format,
			File = result.EntryFile != null ? OutputPathHelper.NormalizeRelativePath(result.EntryFile) : null,
			Sharded = result.HasShards,
			Shards = shardEntries,
			RecordCount = shouldEmitCounts ? totalRecords : null,
			ByteCount = shouldEmitBytes ? totalBytes : null,
			Checksum = result.Checksum,
			Statistics = tableStatistics
		};

		if (indexes != null && indexes.ContainsKey(result.Domain))
		{
			table.Indexes = new List<string> { result.Domain };
		}

		return table;
	}

	private static void PopulateFormats(Manifest manifest)
	{
		foreach (ManifestTable table in manifest.Tables.Values)
		{
			if (string.IsNullOrWhiteSpace(table.Format))
			{
				continue;
			}

			string formatKey = table.Format!;
			if (!manifest.Formats.TryGetValue(formatKey, out ManifestFormat? format))
			{
				format = CreateManifestFormat(formatKey);
				manifest.Formats[formatKey] = format;
			}

			string? compression = table.Shards?
				.Select(static shard => shard.Compression)
				.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase));

			if (!string.IsNullOrWhiteSpace(compression))
			{
				format.Compression = compression;
			}
		}

		if (manifest.Indexes is { Count: > 0 })
		{
			manifest.Formats["kindex"] = new ManifestFormat
			{
				Mime = "application/json",
				Extension = ".kindex"
			};
		}
	}

	private static ManifestFormat CreateManifestFormat(string format)
	{
		return format.ToLowerInvariant() switch
		{
			"ndjson" => new ManifestFormat { Mime = "application/x-ndjson", Extension = ".ndjson" },
			"json-metrics" => new ManifestFormat { Mime = "application/json", Extension = ".json" },
			_ => new ManifestFormat { Mime = "application/json" }
		};
	}

	private static ManifestStatistics? BuildStatistics(Manifest manifest)
	{
		if (manifest.Tables.Count == 0)
		{
			return null;
		}

		ManifestStatistics statistics = new()
		{
			TotalRecords = manifest.Tables.Values.Sum(static table => table.RecordCount ?? 0),
			TotalBytes = manifest.Tables.Values.Sum(static table => table.ByteCount ?? 0)
		};

		foreach ((string tableId, ManifestTable table) in manifest.Tables)
		{
			if (table.Statistics != null)
			{
				statistics.Tables[tableId] = new ManifestTableStatistics
				{
					Records = table.Statistics.Records,
					Bytes = table.Statistics.Bytes,
					Shards = table.Statistics.Shards
				};
			}
		}

		return statistics;
	}

	private Dictionary<string, object> BuildMetadata(Manifest manifest)
	{
		Dictionary<string, object> exportOptions = new()
		{
			["compression"] = _options.Compression ?? "none",
			["shardSize"] = _options.ShardSize,
			["indexEnabled"] = _options.EnableIndex,
			["minimalOutput"] = _options.MinimalOutput
		};

		Dictionary<string, object> tableSummary = manifest.Tables
			.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(
				static pair => pair.Key,
				static pair => (object)new Dictionary<string, object>
				{
					["records"] = pair.Value.RecordCount ?? 0,
					["bytes"] = pair.Value.ByteCount ?? 0,
					["shards"] = pair.Value.Shards?.Count ?? 0
				},
				StringComparer.OrdinalIgnoreCase);

		Dictionary<string, object> metadata = new()
		{
			["exportOptions"] = exportOptions,
			["tables"] = tableSummary
		};

		Dictionary<string, object>? indexSummary = BuildIndexMetadata(manifest.Indexes);
		if (indexSummary is not null)
		{
			metadata["indexes"] = indexSummary;
		}

		return metadata;
	}

	private static Dictionary<string, object>? BuildIndexMetadata(Dictionary<string, ManifestIndex>? indexes)
	{
		if (indexes is null || indexes.Count == 0)
		{
			return null;
		}

		Dictionary<string, object> domainSummaries = new(StringComparer.OrdinalIgnoreCase);
		foreach ((string domain, ManifestIndex index) in indexes)
		{
			Dictionary<string, object> summary = new(StringComparer.OrdinalIgnoreCase)
			{
				["path"] = index.Path,
				["type"] = index.Type
			};

			if (!string.IsNullOrWhiteSpace(index.Format))
			{
				summary["format"] = index.Format!;
			}

			if (index.RecordCount.HasValue)
			{
				summary["records"] = index.RecordCount.Value;
			}

			if (index.Checksum is not null)
			{
				summary["checksum"] = new Dictionary<string, object>
				{
					["algo"] = index.Checksum.Algorithm,
					["value"] = index.Checksum.Value
				};
			}

			if (!string.IsNullOrWhiteSpace(index.CreatedAt))
			{
				summary["createdAt"] = index.CreatedAt!;
			}

			if (index.Metadata is not null && index.Metadata.Count > 0)
			{
				summary["metadata"] = index.Metadata;
			}

			domainSummaries[domain] = summary;
		}

		return new Dictionary<string, object>
		{
			["count"] = indexes.Count,
			["domains"] = domainSummaries
		};
	}
}
