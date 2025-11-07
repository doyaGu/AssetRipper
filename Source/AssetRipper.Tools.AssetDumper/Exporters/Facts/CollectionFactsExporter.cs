using AssetRipper.Assets.Collections;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Models;
using Newtonsoft.Json;
using AssetRipper.Tools.AssetDumper.Writers;
using AssetRipper.Tools.AssetDumper.Helpers;

using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Exporters.Facts;

/// <summary>
/// Emits facts/collections.ndjson according to the AssetDump v2 schema.
/// </summary>
public sealed class CollectionFactsExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;

	public CollectionFactsExporter(Options options, CompressionKind compressionKind)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_compressionKind = compressionKind;
		_jsonSettings = new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
	}

	public DomainExportResult ExportCollections(GameData gameData)
	{
		if (gameData is null)
		{
			throw new ArgumentNullException(nameof(gameData));
		}

		IEnumerable<SerializedAssetCollection> serializedCollections = gameData.GameBundle
			.FetchAssetCollections()
			.OfType<SerializedAssetCollection>();

		List<CollectionFactRecord> records = serializedCollections
			.Select(CreateRecord)
			.Where(static record => record is not null)
			.Select(static record => record!)
			.ToList();

		records.Sort(static (left, right) => string.CompareOrdinal(left.CollectionId, right.CollectionId));

		DomainExportResult result = new DomainExportResult(
			domain: "collections",
			tableId: "facts/collections",
			schemaPath: "Schemas/v2/facts/collections.schema.json");

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 100_000;
		long maxBytesPerShard = 100 * 1024 * 1024;

		ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard,
			maxBytesPerShard,
			_compressionKind,
			seekableFrameSize: 2 * 1024 * 1024,
			collectIndexEntries: false,
			descriptorDomain: result.TableId);

		try
		{
			foreach (CollectionFactRecord record in records)
			{
				writer.WriteRecord(record, record.CollectionId);
			}
		}
		finally
		{
			writer.Dispose();
		}

		result.Shards.AddRange(writer.ShardDescriptors);
		return result;
	}

	private CollectionFactRecord? CreateRecord(SerializedAssetCollection collection)
	{
		if (collection is null)
		{
			return null;
		}

		string collectionId = ExportHelper.ComputeCollectionId(collection);
		List<string>? flags = BuildFlags(collection.Flags);
		CollectionSourceRecord? source = BuildSource(collection.FilePath);
		CollectionUnityRecord? unity = BuildUnityRecord(collection);

		return new CollectionFactRecord
		{
			CollectionId = collectionId,
			Name = collection.Name,
			BundleName = collection.Bundle?.Name,
			Platform = collection.Platform.ToString(),
			UnityVersion = collection.Version.ToString(),
			FormatVersion = 0,
			Endian = collection.EndianType.ToString(),
			Flags = flags,
			Source = source,
			Unity = unity
		};
	}

	private static List<string>? BuildFlags(TransferInstructionFlags flags)
	{
		if (flags == TransferInstructionFlags.NoTransferInstructionFlags)
		{
			return null;
		}

		HashSet<string> unique = new(StringComparer.Ordinal);
		foreach (TransferInstructionFlags value in Enum.GetValues<TransferInstructionFlags>())
		{
			if (value == TransferInstructionFlags.NoTransferInstructionFlags)
			{
				continue;
			}

			if (flags.HasFlag(value))
			{
				string name = value.ToString();
				if (!string.IsNullOrWhiteSpace(name))
				{
					unique.Add(name);
				}
			}
		}

		if (unique.Count == 0)
		{
			return null;
		}

		List<string> ordered = unique.ToList();
		ordered.Sort(StringComparer.Ordinal);
		return ordered;
	}

	private static CollectionSourceRecord? BuildSource(string? filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			return null;
		}

		return new CollectionSourceRecord
		{
			Uri = filePath
		};
	}

	private static CollectionUnityRecord? BuildUnityRecord(SerializedAssetCollection collection)
	{
		string? classification = ResolveBuiltInClassification(collection.Name);
		return classification is null
			? null
			: new CollectionUnityRecord { BuiltInClassification = classification };
	}

	private static string? ResolveBuiltInClassification(string? name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}

		string normalized = SpecialFileNames.FixFileIdentifier(name);
		if (SpecialFileNames.IsBuiltinExtra(normalized))
		{
			return "BUILTIN-EXTRA";
		}

		if (SpecialFileNames.IsDefaultResource(normalized))
		{
			return "BUILTIN-DEFAULT";
		}

		if (SpecialFileNames.IsEditorResource(normalized))
		{
			return "BUILTIN-EDITOR";
		}

		return null;
	}
}
