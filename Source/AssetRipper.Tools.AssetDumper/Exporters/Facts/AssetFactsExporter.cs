using System.Globalization;
using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.Export.PrimaryContent;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AssetRipper.Tools.AssetDumper.Writers;
using AssetRipper.Tools.AssetDumper.Helpers;

using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Exporters.Facts;

/// <summary>
/// Emits facts/assets.ndjson records aligned with the v2 schema.
/// </summary>
public sealed class AssetFactsExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;
	private readonly TypeDictionaryBuilder _typeDictionary = new();

	public AssetFactsExporter(Options options, CompressionKind compressionKind, bool enableIndex)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
		_compressionKind = compressionKind;
		_enableIndex = enableIndex;
	}

	public TypeDictionaryBuilder TypeDictionary => _typeDictionary;

	/// <summary>
	/// Exports asset facts to NDJSON shards.
	/// Returns shard descriptors for manifest generation.
	/// </summary>
	public DomainExportResult ExportAssets(GameData gameData)
	{
		if (gameData is null)
		{
			throw new ArgumentNullException(nameof(gameData));
		}

		Logger.Info(LogCategory.Export, "Exporting asset facts...");

		Directory.CreateDirectory(_options.OutputPath);

		List<SerializedAssetCollection> collections = gameData.GameBundle
			.FetchAssetCollections()
			.OfType<SerializedAssetCollection>()
			.ToList();

		if (_options.Verbose)
		{
			Logger.Info(LogCategory.Export, $"Processing {collections.Count} serialized collections");
		}

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 100_000;
		long maxBytesPerShard = 100 * 1024 * 1024;

		DomainExportResult result = new DomainExportResult(
			"assets",
			"facts/assets",
			"Schemas/v2/facts/assets.schema.json");
		string shardDirectory = result.ShardDirectory;

		ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
			_options.OutputPath,
			shardDirectory,
			_jsonSettings,
			maxRecordsPerShard,
			maxBytesPerShard,
			_compressionKind,
			seekableFrameSize: 2 * 1024 * 1024,
			collectIndexEntries: _enableIndex,
			descriptorDomain: result.TableId);
		List<(string key, AssetFactRecord record)> bufferedRecords = new();

		try
		{
			foreach (SerializedAssetCollection collection in collections)
			{
				string collectionId = ExportHelper.ComputeCollectionId(collection);
				CollectionJsonWalker walker = new(collection);

				foreach (IUnityObjectBase asset in collection.Assets.Values.OrderBy(static asset => asset.PathID))
				{
					SerializedObjectMetadata metadata = SerializedObjectMetadata.FromAsset(asset);
					string stableKey = StableKeyHelper.Create(collectionId, asset.PathID);
					JToken payload = walker.Serialize(asset);
					AssetFactRecord record = CreateAssetFactRecord(collectionId, asset, metadata, payload);
					bufferedRecords.Add((stableKey, record));
				}
			}

			bufferedRecords.Sort(static (left, right) => string.CompareOrdinal(left.key, right.key));

			foreach ((string key, AssetFactRecord record) entry in bufferedRecords)
			{
				string? indexKey = _enableIndex ? entry.key : null;
				writer.WriteRecord(entry.record, entry.key, indexKey);
			}
		}
		finally
		{
			writer.Dispose();
		}

		result.Shards.AddRange(writer.ShardDescriptors);
		if (_enableIndex)
		{
			result.IndexEntries.AddRange(writer.IndexEntries);
		}

		if (_options.Verbose)
		{
			Logger.Info(LogCategory.Export, $"Exported {bufferedRecords.Count} asset facts across {writer.ShardCount} shards");
		}
		else
		{
			Logger.Info(LogCategory.Export, $"Exported {bufferedRecords.Count} asset facts");
		}

		return result;
	}

	private AssetFactRecord CreateAssetFactRecord(string collectionId, IUnityObjectBase asset, SerializedObjectMetadata metadata, JToken payload)
	{
		int classKey = _typeDictionary.GetOrAdd(asset, metadata);
		string? bestName = asset.GetBestName();

		AssetFactRecord fact = new AssetFactRecord
		{
			PrimaryKey = new AssetPrimaryKey
			{
				CollectionId = collectionId,
				PathId = asset.PathID
			},
			ClassKey = classKey,
			Name = string.IsNullOrWhiteSpace(bestName) ? null : bestName,
			Unity = BuildUnityMetadata(metadata),
			Data = new AssetDataContainer
			{
				ByteStart = metadata.ByteStart >= 0 ? metadata.ByteStart : null,
				ByteSize = metadata.ByteSize >= 0 ? metadata.ByteSize : null,
				Content = payload
			}
		};

		return fact;
	}

	private static AssetUnityMetadata BuildUnityMetadata(SerializedObjectMetadata metadata)
	{
		AssetUnityMetadata unity = new AssetUnityMetadata
		{
			ClassId = metadata.ClassId,
			TypeId = metadata.TypeId != 0 ? metadata.TypeId : null,
			SerializedTypeIndex = metadata.SerializedTypeIndex >= 0 ? metadata.SerializedTypeIndex : null,
			ScriptTypeIndex = metadata.ScriptTypeIndex >= 0 ? metadata.ScriptTypeIndex : null,
			IsStripped = metadata.IsStripped ? true : (bool?)null
		};

		return unity;
	}

	private sealed class CollectionJsonWalker : DefaultJsonWalker
	{
		private readonly SerializedAssetCollection _collection;

		public CollectionJsonWalker(SerializedAssetCollection collection)
		{
			_collection = collection ?? throw new ArgumentNullException(nameof(collection));
		}

		public JToken Serialize(IUnityObjectBase asset)
		{
			if (asset is null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			string raw = SerializeStandard(asset);
			string trimmed = raw.TrimEnd();
			return string.IsNullOrWhiteSpace(trimmed)
				? JValue.CreateNull()
				: JToken.Parse(trimmed);
		}

		public override void VisitPPtr<TAsset>(PPtr<TAsset> pptr)
		{
			AssetCollection? targetCollection = null;
			if (pptr.FileID == 0)
			{
				targetCollection = _collection;
			}
			else if (pptr.FileID > 0 && pptr.FileID < _collection.Dependencies.Count)
			{
				targetCollection = _collection.Dependencies[pptr.FileID];
			}

			if (targetCollection is not null)
			{
				string targetId = ExportHelper.ComputeCollectionId(targetCollection);
				Writer.Write("{ \"m_Collection\": ");
				Writer.Write(JsonConvert.ToString(targetCollection.Name ?? string.Empty));
				Writer.Write(", \"m_CollectionId\": ");
				Writer.Write(JsonConvert.ToString(targetId));
				Writer.Write(", \"m_PathID\": ");
				Writer.Write(pptr.PathID.ToString(CultureInfo.InvariantCulture));
				Writer.Write(" }");
				return;
			}

			base.VisitPPtr(pptr);
		}
	}
}
