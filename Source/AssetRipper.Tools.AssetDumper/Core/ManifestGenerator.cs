using AssetRipper.Assets.Collections;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Models;
using Newtonsoft.Json;
using System.Reflection;
using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Generates <c>manifest.json</c> for the AssetDumper v2 export layout.
/// </summary>
internal sealed class ManifestGenerator
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public ManifestGenerator(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = new JsonSerializerSettings
		{
			Formatting = _options.CompactJson ? Formatting.None : Formatting.Indented,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
	}

	public void GenerateManifest(
		GameData gameData,
		IReadOnlyList<DomainExportResult> domainResults,
		Dictionary<string, ManifestIndex>? indexes = null)
	{
		if (gameData is null)
		{
			throw new ArgumentNullException(nameof(gameData));
		}

		if (domainResults is null)
		{
			throw new ArgumentNullException(nameof(domainResults));
		}

		Manifest manifest = new()
		{
			CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
			Producer = CreateProducer(gameData)
		};

		PopulateFormats(manifest, domainResults, indexes);
		PopulateTables(manifest, domainResults, indexes);
		manifest.Statistics = BuildStatistics(manifest);
		manifest.Metadata = BuildMetadata(domainResults, indexes);
		manifest.Indexes = indexes != null && indexes.Count > 0
			? new Dictionary<string, ManifestIndex>(indexes, StringComparer.OrdinalIgnoreCase)
			: null;

		WriteManifest(manifest);
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

	private Dictionary<string, object> BuildMetadata(
		IEnumerable<DomainExportResult> domainResults,
		Dictionary<string, ManifestIndex>? indexes)
	{
		Dictionary<string, object> exportOptions = new()
		{
			["compression"] = _options.Compression ?? "none",
			["shardSize"] = _options.ShardSize,
			["indexEnabled"] = _options.EnableIndex,
			["minimalOutput"] = _options.MinimalOutput
		};

		Dictionary<string, object> tableSummary = domainResults
			.OrderBy(static result => result.TableId, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(
				static result => result.TableId,
				static result => (object)new Dictionary<string, object>
				{
					["records"] = result.TotalRecords,
					["bytes"] = result.TotalBytes,
					["shards"] = result.Shards.Count
				},
				StringComparer.OrdinalIgnoreCase);

		Dictionary<string, object> metadata = new()
		{
			["exportOptions"] = exportOptions,
			["tables"] = tableSummary
		};

		Dictionary<string, object>? indexSummary = BuildIndexMetadata(indexes);
		if (indexSummary is not null)
		{
			metadata["indexes"] = indexSummary;
		}

		return metadata;
	}

	private void PopulateFormats(Manifest manifest, IEnumerable<DomainExportResult> domainResults, Dictionary<string, ManifestIndex>? indexes)
	{
		foreach (IGrouping<string, DomainExportResult> group in domainResults
			.GroupBy(static result => result.Format, StringComparer.OrdinalIgnoreCase))
		{
			string format = group.Key;
			ManifestFormat manifestFormat;

			if (format.Equals("ndjson", StringComparison.OrdinalIgnoreCase))
			{
				manifestFormat = new ManifestFormat { Mime = "application/x-ndjson", Extension = ".ndjson" };
			}
			else if (format.Equals("json-metrics", StringComparison.OrdinalIgnoreCase))
			{
				manifestFormat = new ManifestFormat { Mime = "application/json", Extension = ".json" };
			}
			else
			{
				manifestFormat = new ManifestFormat { Mime = "application/json" };
			}

			string? compression = group
				.SelectMany(static result => result.Shards)
				.Select(static shard => shard.Compression)
				.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase));

			if (!string.IsNullOrWhiteSpace(compression))
			{
				manifestFormat.Compression = compression;
			}

			manifest.Formats[format] = manifestFormat;
		}

		if (indexes != null && indexes.Count > 0)
		{
			manifest.Formats["kindex"] = new ManifestFormat
			{
				Mime = "application/json",
				Extension = ".kindex"
			};
		}
	}

	private void PopulateTables(
		Manifest manifest,
		IEnumerable<DomainExportResult> domainResults,
		Dictionary<string, ManifestIndex>? indexes)
	{
		foreach (DomainExportResult result in domainResults.OrderBy(static r => r.TableId, StringComparer.OrdinalIgnoreCase))
		{
			List<ManifestTableShard>? shardEntries = null;
			if (result.HasShards)
			{
				shardEntries = result.Shards
					.Select(shard => new ManifestTableShard
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

			manifest.Tables[result.TableId] = table;
		}
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

	private ManifestProducer CreateProducer(GameData gameData)
	{
		ManifestProducer producer = new()
		{
			Name = "AssetDumper",
			Version = GetToolVersion(),
			AssetRipperVersion = GetAssetRipperVersion()
		};

		AssetCollection? firstCollection = gameData.GameBundle.FetchAssetCollections().FirstOrDefault();
		if (firstCollection != null)
		{
			producer.UnityVersion = firstCollection.Version.ToString();
			producer.ProjectName = gameData.GameBundle.Name;
		}

		return producer;
	}

	private void WriteManifest(Manifest manifest)
	{
		string manifestPath = Path.Combine(_options.OutputPath, "manifest.json");
		string json = JsonConvert.SerializeObject(manifest, _jsonSettings);
		File.WriteAllText(manifestPath, json);
	}

	private string GetToolVersion()
	{
		try
		{
			Assembly assembly = Assembly.GetExecutingAssembly();
			Version? version = assembly.GetName().Version;
			return version?.ToString() ?? "unknown";
		}
		catch
		{
			return "unknown";
		}
	}

	private string GetAssetRipperVersion()
	{
		try
		{
			Assembly? assetRipperAssembly = AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(static assembly => assembly.GetName().Name?.Contains("AssetRipper", StringComparison.OrdinalIgnoreCase) == true);

			if (assetRipperAssembly != null)
			{
				Version? version = assetRipperAssembly.GetName().Version;
				return version?.ToString() ?? "unknown";
			}

			return "unknown";
		}
		catch
		{
			return "unknown";
		}
	}
}
