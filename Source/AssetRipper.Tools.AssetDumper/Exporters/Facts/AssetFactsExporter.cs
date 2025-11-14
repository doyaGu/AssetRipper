using System.Globalization;
using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.Export.PrimaryContent;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Models.Common;
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

		List<AssetCollection> collections = gameData.GameBundle
			.FetchAssetCollections()
			.Where(static collection => collection is SerializedAssetCollection or ProcessedAssetCollection)
			.ToList();

		if (_options.Verbose)
		{
			int serializedCount = collections.Count(static c => c is SerializedAssetCollection);
			int processedCount = collections.Count(static c => c is ProcessedAssetCollection);
			Logger.Info(LogCategory.Export, $"Processing {serializedCount} serialized collections and {processedCount} processed collections");
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

		// Track total exported assets across all collections
		long totalExportedAssets = 0;

		try
		{
			// Sort collections by name for consistent output ordering
			var sortedCollections = collections
				.OrderBy(c => c.Name ?? string.Empty, StringComparer.Ordinal)
				.ToList();

			foreach (AssetCollection collection in sortedCollections)
			{
				string collectionId = ExportHelper.ComputeCollectionId(collection);
				CollectionJsonWalker walker = new(collection);

				// MEMORY OPTIMIZATION: Buffer only one collection at a time instead of all assets
				// This reduces peak memory from O(total assets) to O(max assets per collection)
				List<(string key, AssetRecord record)> collectionBatch = new();

				// Process assets in this collection (already ordered by PathID)
				foreach (IUnityObjectBase asset in collection.Assets.Values.OrderBy(static asset => asset.PathID))
				{
					string assetName = asset.GetBestName() ?? $"PathID:{asset.PathID}";

					try
					{
						// Implement timeout protection to prevent malicious/corrupted assets from blocking
						if (_options.TimeoutSeconds > 0)
						{
							using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));

							var processingTask = Task.Run(() =>
							{
								SerializedObjectMetadata metadata = SerializedObjectMetadata.FromAsset(asset);
								string stableKey = StableKeyHelper.Create(collectionId, asset.PathID);
								JToken payload = walker.Serialize(asset);
								AssetRecord record = CreateAssetFactRecord(collectionId, asset, metadata, payload);
								return (stableKey, record);
							}, cts.Token);

							try
							{
								var (stableKey, record) = processingTask.Wait(_options.TimeoutSeconds * 1000)
									? processingTask.Result
									: throw new TimeoutException($"Asset processing timed out after {_options.TimeoutSeconds}s");

								collectionBatch.Add((stableKey, record));
							}
							catch (AggregateException aex) when (aex.InnerException is OperationCanceledException)
							{
								Logger.Warning(LogCategory.Export, $"Asset '{assetName}' cancelled due to timeout ({_options.TimeoutSeconds}s) in collection '{collectionId}'");
								continue;
							}
						}
						else
						{
							// No timeout - process normally
							SerializedObjectMetadata metadata = SerializedObjectMetadata.FromAsset(asset);
							string stableKey = StableKeyHelper.Create(collectionId, asset.PathID);
							JToken payload = walker.Serialize(asset);
							AssetRecord record = CreateAssetFactRecord(collectionId, asset, metadata, payload);
							collectionBatch.Add((stableKey, record));
						}
					}
					catch (TimeoutException)
					{
						Logger.Warning(LogCategory.Export, $"Asset '{assetName}' timed out after {_options.TimeoutSeconds}s in collection '{collectionId}'");
						continue;
					}
					catch (Exception ex)
					{
						// Log and skip problematic assets instead of failing entire export
						if (_options.Verbose)
						{
							Logger.Warning(LogCategory.Export, $"Failed to serialize asset '{assetName}' in collection '{collectionId}': {ex.Message}");
						}
						// Continue processing other assets
						continue;
					}
				}

				// Sort assets within this collection by stable key
				// This ensures consistent ordering within each collection
				collectionBatch.Sort(static (left, right) => string.CompareOrdinal(left.key, right.key));

				// Write all assets from this collection to output
				foreach ((string key, AssetRecord record) entry in collectionBatch)
				{
					string? indexKey = _enableIndex ? entry.key : null;
					writer.WriteRecord(entry.record, entry.key, indexKey);
				}

				int batchCount = collectionBatch.Count;
				totalExportedAssets += batchCount;

				if (_options.Verbose)
				{
					Logger.Verbose(LogCategory.Export, $"Exported {batchCount} assets from collection '{collection.Name ?? collectionId}'");
				}

				// Release memory for this collection's batch
				collectionBatch.Clear();
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
			Logger.Info(LogCategory.Export, $"Exported {totalExportedAssets} asset facts across {writer.ShardCount} shards");
		}
		else
		{
			Logger.Info(LogCategory.Export, $"Exported {totalExportedAssets} asset facts");
		}

		return result;
	}

	private AssetRecord CreateAssetFactRecord(string collectionId, IUnityObjectBase asset, SerializedObjectMetadata metadata, JToken payload)
	{
		int classKey = _typeDictionary.GetOrAdd(asset, metadata);
		string? bestName = asset.GetBestName();
		AssetCollection collection = asset.Collection;

		// New: Build hierarchy path
		HierarchyPath? hierarchy = BuildHierarchyPath(collection.Bundle);

		// New: Get redundant name fields for readability
		string? collectionName = collection.Name;
		string? bundleName = collection.Bundle?.Name;
		string? sceneName = collection.IsScene ? collection.Scene?.Name : null;

		// New: Get path-related properties
		string? originalPath = string.IsNullOrWhiteSpace(asset.OriginalPath) ? null : asset.OriginalPath;
		string? originalDirectory = string.IsNullOrWhiteSpace(asset.OriginalDirectory) ? null : asset.OriginalDirectory;
		string? originalName = string.IsNullOrWhiteSpace(asset.OriginalName) ? null : asset.OriginalName;
		string? originalExtension = string.IsNullOrWhiteSpace(asset.OriginalExtension) ? null : asset.OriginalExtension;
		string? assetBundleName = string.IsNullOrWhiteSpace(asset.AssetBundleName) ? null : asset.AssetBundleName;

		AssetRecord fact = new AssetRecord
		{
			PrimaryKey = new AssetPrimaryKey
			{
				CollectionId = collectionId,
				PathId = asset.PathID
			},
			PathId = asset.PathID,
			ClassKey = classKey,
			ClassName = asset.ClassName,
			Name = string.IsNullOrWhiteSpace(bestName) ? null : bestName,
			OriginalPath = originalPath,
			OriginalDirectory = originalDirectory,
			OriginalName = originalName,
			OriginalExtension = originalExtension,
			AssetBundleName = assetBundleName,
			Hierarchy = hierarchy,
			CollectionName = collectionName,
			BundleName = bundleName,
			SceneName = sceneName,
			Unity = BuildUnityMetadata(metadata),
			ByteStart = metadata.ByteStart >= 0 ? metadata.ByteStart : null,
			ByteSize = metadata.ByteSize >= 0 ? metadata.ByteSize : null,
			Data = payload
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
			// Note: SerializedVersion would need to be extracted from the actual asset type,
			// not available in SerializedObjectMetadata
		};

		return unity;
	}

	private static HierarchyPath? BuildHierarchyPath(Bundle bundle)
	{
		if (bundle == null)
		{
			return null;
		}

		List<Bundle> lineage = new List<Bundle>();
		Bundle? current = bundle;
		while (current != null)
		{
			lineage.Insert(0, current);
			current = current.Parent;
		}

		if (lineage.Count == 0)
		{
			return null;
		}

		List<string> bundlePath = new List<string>(lineage.Count);
		List<string> bundleNames = new List<string>(lineage.Count);

		foreach (Bundle b in lineage)
		{
			string bundlePk = ExportHelper.ComputeBundlePk(b);
			bundlePath.Add(bundlePk);
			bundleNames.Add(b.Name ?? string.Empty);
		}

		return new HierarchyPath
		{
			BundlePath = bundlePath,
			BundleNames = bundleNames,
			Depth = lineage.Count - 1
		};
	}

	private static string ComputeBundleStableKey(List<Bundle> lineage)
	{
		string composite = string.Join("|", lineage.Select(static b => $"{b.GetType().FullName}:{b.Name}"));
		return ExportHelper.ComputeStableHash(composite);
	}

	private sealed class CollectionJsonWalker : DefaultJsonWalker
	{
		private readonly AssetCollection _collection;

		public CollectionJsonWalker(AssetCollection collection)
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
			AssetCollection? targetCollection = pptr.FileID switch
			{
				0 => _collection,
				> 0 when pptr.FileID < _collection.Dependencies.Count => _collection.Dependencies[pptr.FileID],
				_ => null
			};

			if (targetCollection is not null)
			{
				string targetId = ExportHelper.ComputeCollectionId(targetCollection);
				Writer.Write("{ \"m_Collection\": ");
				Writer.Write(JsonConvert.ToString(targetCollection.Name ?? string.Empty));
				Writer.Write(", \"m_CollectionId\": ");
				Writer.Write(JsonConvert.ToString(targetId));
				Writer.Write(", \"m_PathID\": ");
				Writer.Write(pptr.PathID.ToString(CultureInfo.InvariantCulture));
				if (pptr.FileID != 0)
				{
					Writer.Write(", \"m_FileID\": ");
					Writer.Write(pptr.FileID.ToString(CultureInfo.InvariantCulture));
				}
				Writer.Write(" }");
				return;
			}

			base.VisitPPtr(pptr);
		}
	}
}
