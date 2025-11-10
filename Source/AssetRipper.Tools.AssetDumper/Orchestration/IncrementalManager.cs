using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Writers;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Manages incremental export by loading and reusing existing manifest data.
/// </summary>
public sealed class IncrementalManager
{
	private readonly Options _options;

	public IncrementalManager(Options options)
	{
		_options = options;
	}

	/// <summary>
	/// Loads existing manifest if incremental processing is enabled.
	/// </summary>
	public Manifest? TryLoadExistingManifest()
	{
		if (!_options.IncrementalProcessing)
		{
			return null;
		}

		string manifestPath = Path.Combine(_options.OutputPath, "manifest.json");
		if (!File.Exists(manifestPath))
		{
			return null;
		}

		try
		{
			string json = File.ReadAllText(manifestPath);
			Manifest? manifest = JsonConvert.DeserializeObject<Manifest>(json);
			if (manifest == null)
			{
				return null;
			}

			bool hasV2Tables = manifest.Tables.Keys.Any(static key =>
				key.StartsWith("facts/", StringComparison.OrdinalIgnoreCase) ||
				key.StartsWith("relations/", StringComparison.OrdinalIgnoreCase));

			return hasV2Tables ? manifest : null;
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Failed to read existing manifest for incremental reuse: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Checks if all specified tables exist in the manifest with valid data and on disk.
	/// </summary>
	public bool ManifestContainsTables(Manifest manifest, params string[] tableIds)
	{
		foreach (string tableId in tableIds)
		{
			if (!manifest.Tables.TryGetValue(tableId, out ManifestTable? table))
			{
				return false;
			}

			if (!IsTableMaterialized(table))
			{
				Logger.Verbose(LogCategory.Export, $"Manifest table '{tableId}' missing required data for incremental reuse.");
				return false;
			}
		}

		return true;
	}

	private bool IsTableMaterialized(ManifestTable table)
	{
		if (string.IsNullOrWhiteSpace(table.Schema))
		{
			return false;
		}

		bool hasFile = !string.IsNullOrWhiteSpace(table.File);
		bool hasShards = table.Shards is { Count: > 0 };

		if (!hasFile && !hasShards)
		{
			return false;
		}

		if (hasFile && !EntryExists(table.File!))
		{
			return false;
		}

		if (hasShards && !ShardsExist(table.Shards!))
		{
			return false;
		}

		return true;
	}

	private bool EntryExists(string relativePath)
	{
		string normalized = OutputPathHelper.NormalizeRelativePath(relativePath);
		string absolute = OutputPathHelper.ResolveAbsolutePath(_options.OutputPath, normalized);
		return File.Exists(absolute);
	}

	private bool ShardsExist(IEnumerable<ManifestTableShard> shards)
	{
		foreach (ManifestTableShard shard in shards)
		{
			if (string.IsNullOrWhiteSpace(shard.Path))
			{
				return false;
			}

			if (!EntryExists(shard.Path))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Creates a DomainExportResult from manifest table data for reuse.
	/// </summary>
	public DomainExportResult? CreateResultFromManifest(Manifest manifest, string tableId)
	{
		if (!manifest.Tables.TryGetValue(tableId, out ManifestTable? table))
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(table.Schema))
		{
			return null;
		}

		string domain = ExtractDomainFromTableId(tableId);
		string format = string.IsNullOrWhiteSpace(table.Format) ? "ndjson" : table.Format!;
		DomainExportResult result = new DomainExportResult(domain, tableId, table.Schema, format)
		{
			EntryFile = table.File,
			Checksum = table.Checksum
		};

		if (table.RecordCount.HasValue)
		{
			result.RecordCountOverride = table.RecordCount.Value;
		}

		if (table.ByteCount.HasValue)
		{
			result.ByteCountOverride = table.ByteCount.Value;
		}

		if (table.Shards != null)
		{
			foreach (ManifestTableShard shard in table.Shards)
			{
				result.Shards.Add(ConvertShardDescriptor(tableId, shard));
			}
		}

		return result;
	}

	/// <summary>
	/// Loads indexes from existing manifest into the export context.
	/// </summary>
	public void LoadExistingIndexes(Manifest manifest, ExportContext context)
	{
		if (!_options.ExportIndexes || manifest.Indexes == null)
		{
			return;
		}

		foreach ((string domain, ManifestIndex manifestIndex) in manifest.Indexes)
		{
			context.IndexRefs[domain] = manifestIndex;
		}
	}

	private static ShardDescriptor ConvertShardDescriptor(string tableId, ManifestTableShard shard)
	{
		return new ShardDescriptor
		{
			Domain = tableId,
			Shard = shard.Path ?? string.Empty,
			Records = shard.Records,
			Bytes = shard.Bytes,
			Compression = shard.Compression ?? "none",
			FirstKey = shard.FirstKey,
			LastKey = shard.LastKey,
			UncompressedBytes = shard.UncompressedBytes,
			FrameSize = shard.FrameSize,
			Sha256 = shard.Sha256
		};
	}

	private static string ExtractDomainFromTableId(string tableId)
	{
		int separatorIndex = tableId.LastIndexOf('/');
		if (separatorIndex >= 0 && separatorIndex < tableId.Length - 1)
		{
			return tableId.Substring(separatorIndex + 1);
		}

		return tableId;
	}
}
