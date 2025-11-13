using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.Processing.Prefabs;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Models.Common;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Writers;

namespace AssetRipper.Tools.AssetDumper.Exporters.Facts;

/// <summary>
/// Exports scene records to NDJSON shards for the scenes domain.
/// </summary>
internal class SceneExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public SceneExporter(Options options, CompressionKind compressionKind, bool enableIndex)
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

	/// <summary>
	/// Exports all scene records to NDJSON shards.
	/// Returns shard descriptors for manifest generation.
	/// </summary>
	public DomainExportResult ExportScenes(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting scene records...");

		IEnumerable<SceneDefinition> scenesToProcess = gameData.GameBundle.Scenes.AsEnumerable();

		if (!string.IsNullOrEmpty(_options.SceneFilter))
		{
			Regex sceneRegex = new Regex(_options.SceneFilter, RegexOptions.IgnoreCase | RegexOptions.Compiled);
			scenesToProcess = scenesToProcess.Where(scene => sceneRegex.IsMatch(scene.Name));
		}

		List<SceneDefinition> sceneList = scenesToProcess.ToList();
		Logger.Info(LogCategory.Export, $"Processing {sceneList.Count} scenes");

		// Scenes are typically fewer, but still use sharding for consistency
		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 10000;
		long maxBytesPerShard = 50 * 1024 * 1024; // 50MB per shard

		DomainExportResult result = new DomainExportResult(
			domain: "scenes",
			tableId: "facts/scenes",
			schemaPath: "Schemas/v2/facts/scenes.schema.json");

		ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard,
			maxBytesPerShard,
			_compressionKind,
			collectIndexEntries: _enableIndex,
			descriptorDomain: result.TableId);

		int exportedCount = 0;

		try
		{
			// Parallel processing of scene records (CPU-bound work)
			// Record creation is parallelized; writes are serialized for thread safety
			List<SceneRecord> records = ParallelProcessor.ProcessInParallelWithNulls(
				sceneList,
				scene =>
				{
					try
					{
						return CreateSceneRecord(scene);
					}
					catch (Exception ex)
					{
						Logger.Error(LogCategory.Export, $"Error processing scene {scene.Name}: {ex.Message}");
						return null;
					}
				},
				maxParallelism: 0); // 0 = auto-detect based on CPU cores

			// Sequential write phase (thread-safe writer handles locking)
			foreach (SceneRecord record in records)
			{
				string stableKey = record.HierarchyAssetId ?? string.Empty;
				string? indexKey = _enableIndex ? stableKey : null;
				writer.WriteRecord(record, stableKey, indexKey);
				exportedCount++;
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

		Logger.Info(LogCategory.Export, $"Exported {exportedCount} scene records across {writer.ShardCount} shards");

		return result;
	}

	private SceneRecord? CreateSceneRecord(SceneDefinition scene)
	{
		SceneHierarchyObject? hierarchy = null;
		AssetCollection? primaryCollection = null;

		foreach (AssetCollection collection in scene.Collections)
		{
			hierarchy = collection.OfType<SceneHierarchyObject>().FirstOrDefault();
			if (hierarchy != null)
			{
				primaryCollection = collection;
				break;
			}
		}

		if (hierarchy == null || primaryCollection == null)
		{
			Logger.Warning(LogCategory.Export, $"No hierarchy found for scene: {scene.Name}");
			return null;
		}

		string primaryCollectionId = ExportHelper.ComputeCollectionId(primaryCollection);
		AssetRef hierarchyRef = new AssetRef(primaryCollectionId, hierarchy.PathID);

		SceneRecord record = new SceneRecord
		{
			Domain = "scenes",
			Type = "Scene",
			Name = scene.Name,
			SceneGuid = hierarchy.Scene.GUID.ToString(),
			ScenePath = hierarchy.Scene.Path,
			ExportedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),

			Version = primaryCollection.Version.ToString(),
			Platform = primaryCollection.Platform.ToString(),
			Flags = primaryCollection.Flags.ToString(),
			EndianType = primaryCollection.EndianType.ToString(),
			BundleName = primaryCollection.Bundle.Name,

			SceneCollectionCount = scene.Collections.Count,
			CollectionIds = scene.Collections
				.Select(c => ExportHelper.ComputeCollectionId(c))
				.ToList(),

			// New fields
			PrimaryCollectionId = primaryCollectionId,
			Bundle = primaryCollection.Bundle != null ? BuildBundleRef(primaryCollection.Bundle) : null,
			CollectionDetails = scene.Collections
				.Select((c, index) => CreateSceneCollectionDetail(c, index == 0))
				.ToList(),

			Hierarchy = hierarchyRef,
			HierarchyAssetId = StableKeyHelper.Create(hierarchyRef),
			PathId = hierarchy.PathID,
			ClassId = hierarchy.ClassID,
			ClassName = hierarchy.ClassName,

			AssetCount = hierarchy.Assets.Count(),
			GameObjectCount = hierarchy.GameObjects.Count,
			ComponentCount = hierarchy.Components.Count,
			ManagerCount = hierarchy.Managers.Count,
			PrefabInstanceCount = hierarchy.PrefabInstances.Count,
			DependencyCount = hierarchy.FetchDependencies()?.Count() ?? 0,

			HasSceneRoots = hierarchy.SceneRoots != null,
			RootGameObjectCount = hierarchy.GetRoots().Count(),
			StrippedAssetCount = hierarchy.StrippedAssets.Count,
			HiddenAssetCount = hierarchy.HiddenAssets.Count
		};

		// Add detailed collection descriptors
		record.Collections = scene.Collections.Select(c => CreateSceneCollectionDescriptor(c, primaryCollectionId)).ToList();

		// Add optional asset references (controlled by option to reduce output size)
		if (!_options.MinimalOutput)
		{
			// Note: SceneRoots is ISceneRoots interface - need to convert properly
			if (hierarchy.SceneRoots != null)
			{
				// SceneRoots is a special asset containing references
				// For now, just capture the asset itself
				IUnityObjectBase? sceneRootsAsset = hierarchy.SceneRoots as IUnityObjectBase;
				if (sceneRootsAsset != null)
				{
					AssetCollection sceneRootsCollection = sceneRootsAsset.Collection;
					string sceneRootsCollectionId = ExportHelper.ComputeCollectionId(sceneRootsCollection);
					record.SceneRootsAsset = new AssetRef(sceneRootsCollectionId, sceneRootsAsset.PathID);
				}
			}

			record.RootGameObjects = ConvertToAssetRefs(hierarchy.GetRoots());
			record.GameObjects = ConvertToAssetRefs(hierarchy.GameObjects);
			record.Components = ConvertToAssetRefs(hierarchy.Components);
			record.Managers = ConvertToAssetRefs(hierarchy.Managers);
			record.PrefabInstances = ConvertToAssetRefs(hierarchy.PrefabInstances);
			record.StrippedAssets = ConvertToAssetRefs(hierarchy.StrippedAssets);
			record.HiddenAssets = ConvertToAssetRefs(hierarchy.HiddenAssets);
		}

		return record;
	}

	private SceneCollectionDescriptor CreateSceneCollectionDescriptor(AssetCollection collection, string primaryCollectionId)
	{
		string collectionId = ExportHelper.ComputeCollectionId(collection);
		bool isPrimary = collectionId == primaryCollectionId;

		return new SceneCollectionDescriptor
		{
			CollectionId = collectionId,
			Name = collection.Name,
			BundleName = collection.Bundle?.Name,
			BundlePk = collection.Bundle != null ? ExportHelper.ComputeBundlePk(collection.Bundle) : null,
			CollectionType = GetCollectionType(collection),
			FilePath = collection.FilePath,
			Version = collection.Version.ToString(),
			Platform = collection.Platform.ToString(),
			Flags = collection.Flags.ToString(),
			IsProcessed = collection is ProcessedAssetCollection,
			IsSceneCollection = collection.IsScene,
			SceneName = collection.Scene?.Name,
			IsPrimary = isPrimary,
			AssetCount = collection.Count
		};
	}

	private SceneCollectionDetail CreateSceneCollectionDetail(AssetCollection collection, bool isPrimary)
	{
		string collectionId = ExportHelper.ComputeCollectionId(collection);
		BundleRef bundleRef = BuildBundleRef(collection.Bundle);

		return new SceneCollectionDetail
		{
			CollectionId = collectionId,
			Bundle = bundleRef,
			IsPrimary = isPrimary,
			AssetCount = collection.Count
		};
	}

	private BundleRef BuildBundleRef(AssetRipper.Assets.Bundles.Bundle bundle)
	{
		return new BundleRef
		{
			BundlePk = ExportHelper.ComputeBundlePk(bundle),
			BundleName = bundle.Name
		};
	}

	private List<AssetRef>? ConvertToAssetRefs(IEnumerable<IUnityObjectBase>? assets)
	{
		if (assets == null || !assets.Any())
			return null;

		return assets.Select(asset =>
		{
			string collectionId = ExportHelper.ComputeCollectionId(asset.Collection);
			return new AssetRef(collectionId, asset.PathID);
		}).ToList();
	}

	private string GetCollectionType(AssetCollection collection)
	{
		if (collection is SerializedAssetCollection)
			return "SerializedAssetCollection";
		if (collection is ProcessedAssetCollection)
			return "ProcessedAssetCollection";
		return "VirtualAssetCollection";
	}
}
