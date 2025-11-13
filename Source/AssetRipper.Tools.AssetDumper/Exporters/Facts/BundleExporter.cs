using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Models.Common;
using Newtonsoft.Json;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Writers;

namespace AssetRipper.Tools.AssetDumper.Exporters.Facts;

/// <summary>
/// Emits bundle metadata records describing the bundle hierarchy and aggregate statistics.
/// </summary>
internal sealed class BundleExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public BundleExporter(Options options, CompressionKind compressionKind, bool enableIndex)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_compressionKind = compressionKind;
		_enableIndex = enableIndex;
		_jsonSettings = new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
	}

	/// <summary>
	/// Exports bundle metadata into NDJSON shards and returns manifest descriptors.
	/// </summary>
	public DomainExportResult Export(GameData gameData)
	{
		if (gameData is null)
		{
			throw new ArgumentNullException(nameof(gameData));
		}

		Bundle? rootBundle = gameData.GameBundle;
		if (rootBundle is null)
		{
			if (!_options.Silent)
			{
				Logger.Warning(LogCategory.Export, "Game bundle graph is unavailable; skipping bundle metadata export.");
			}

			return new DomainExportResult(
				domain: "bundleMetadata",
				tableId: "facts/bundles",
				schemaPath: "Schemas/v2/facts/bundles.schema.json");
		}

		List<BundleRecord> records = new();
		List<Bundle> lineage = new();
		TraverseBundle(rootBundle, parentPk: null, depth: 0, lineage, records);

		if (!_options.Silent)
		{
			Logger.Info(LogCategory.Export, $"Exporting bundle metadata for {records.Count} bundle nodes...");
		}

		records.Sort(static (left, right) => string.CompareOrdinal(left.HierarchyPath, right.HierarchyPath));

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 5_000;
		long maxBytesPerShard = 25 * 1024 * 1024;

		DomainExportResult result = new(
			domain: "bundleMetadata",
			tableId: "facts/bundles",
			schemaPath: "Schemas/v2/facts/bundles.schema.json");

		ShardedNdjsonWriter writer = new(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard,
			maxBytesPerShard,
			_compressionKind,
			collectIndexEntries: _enableIndex,
			descriptorDomain: result.TableId);

		try
		{
			foreach (BundleRecord record in records)
			{
				string stableKey = record.Pk;
				string? indexKey = _enableIndex ? stableKey : null;
				writer.WriteRecord(record, stableKey, indexKey);
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

		if (!_options.Silent)
		{
			Logger.Info(LogCategory.Export, $"Exported {records.Count} bundle metadata records across {writer.ShardCount} shard(s).");
		}

		return result;
	}

	private BundleAggregate TraverseBundle(Bundle bundle, string? parentPk, int depth, List<Bundle> lineage, List<BundleRecord> records)
	{
		lineage.Add(bundle);

		string pk = ComputeBundleStableKey(lineage);
		int directChildCount = bundle.Bundles.Count;
		int directCollectionCount = bundle.Collections.Count;
		int directSceneCount = 0;
		int directAssetCount = 0;
		foreach (AssetCollection collection in bundle.Collections)
		{
			directAssetCount += collection.Count;
			if (collection.IsScene)
			{
				directSceneCount++;
			}
		}

		int directResourceCount = bundle.Resources.Count;
		int directFailedFileCount = bundle.FailedFiles.Count;

		List<string>? collectionIds = BuildCollectionIdList(bundle);
		List<BundleResourceRecord>? resourceRecords = BuildResourceRecords(bundle.Resources);
		List<BundleFailedFileRecord>? failedFileRecords = BuildFailedFileRecords(bundle);
		List<SceneRefRecord>? sceneRecords = BuildSceneRecords(bundle);

		// New: Collect child bundle information
		List<string>? childBundlePks = null;
		List<string>? childBundleNames = null;
		if (bundle.Bundles.Count > 0)
		{
			childBundlePks = new List<string>(bundle.Bundles.Count);
			childBundleNames = new List<string>(bundle.Bundles.Count);
			foreach (Bundle child in bundle.Bundles)
			{
				List<Bundle> childLineage = new(lineage) { child };
				string childPk = ComputeBundleStableKey(childLineage);
				childBundlePks.Add(childPk);
				childBundleNames.Add(child.Name);
			}
		}

		// New: Build ancestor path
		List<string>? ancestorPath = null;
		if (lineage.Count > 1)
		{
			ancestorPath = new List<string>(lineage.Count - 1);
			for (int i = 0; i < lineage.Count - 1; i++)
			{
				List<Bundle> ancestorLineage = lineage.Take(i + 1).ToList();
				ancestorPath.Add(ComputeBundleStableKey(ancestorLineage));
			}
		}

		// New: Calculate bundle index
		int? bundleIndex = null;
		if (parentPk != null && lineage.Count > 1)
		{
			Bundle parent = lineage[lineage.Count - 2];
			for (int i = 0; i < parent.Bundles.Count; i++)
			{
				if (parent.Bundles[i] == bundle)
				{
					bundleIndex = i;
					break;
				}
			}
		}

		BundleAggregate totals = new()
		{
			CollectionCount = directCollectionCount,
			SceneCollectionCount = directSceneCount,
			ResourceCount = directResourceCount,
			FailedFileCount = directFailedFileCount,
			AssetCount = directAssetCount,
			BundleCount = 0
		};

		foreach (Bundle child in bundle.Bundles)
		{
			BundleAggregate childTotals = TraverseBundle(child, pk, depth + 1, lineage, records);
			totals.CollectionCount += childTotals.CollectionCount;
			totals.SceneCollectionCount += childTotals.SceneCollectionCount;
			totals.ResourceCount += childTotals.ResourceCount;
			totals.FailedFileCount += childTotals.FailedFileCount;
			totals.AssetCount += childTotals.AssetCount;
			totals.BundleCount += 1 + childTotals.BundleCount;
		}

		BundleRecord record = new()
		{
			Pk = pk,
			Name = bundle.Name,
			BundleType = bundle.GetType().Name,
			ParentPk = parentPk,
			IsRoot = parentPk is null,
			HierarchyDepth = depth,
			HierarchyPath = string.Join("/", lineage.Select(static b => b.Name)),
			ChildBundlePks = childBundlePks,
			ChildBundleNames = childBundleNames,
			BundleIndex = bundleIndex,
			AncestorPath = ancestorPath,
			CollectionIds = collectionIds,
			Resources = resourceRecords,
			FailedFiles = failedFileRecords,
			Scenes = sceneRecords,
			DirectCollectionCount = directCollectionCount,
			TotalCollectionCount = totals.CollectionCount,
			DirectSceneCollectionCount = directSceneCount,
			TotalSceneCollectionCount = totals.SceneCollectionCount,
			DirectChildBundleCount = directChildCount,
			TotalChildBundleCount = totals.BundleCount,
			DirectResourceCount = directResourceCount,
			TotalResourceCount = totals.ResourceCount,
			DirectFailedFileCount = directFailedFileCount,
			TotalFailedFileCount = totals.FailedFileCount,
			DirectAssetCount = directAssetCount,
			TotalAssetCount = totals.AssetCount
		};

		records.Add(record);
		lineage.RemoveAt(lineage.Count - 1);
		return totals;
	}

	private static string ComputeBundleStableKey(List<Bundle> lineage)
	{
		string composite = string.Join("|", lineage.Select(static b => $"{b.GetType().FullName}:{b.Name}"));
		return ExportHelper.ComputeStableHash(composite);
	}

	private static List<string>? BuildCollectionIdList(Bundle bundle)
	{
		if (bundle.Collections.Count == 0)
		{
			return null;
		}

		HashSet<string> ids = new(StringComparer.Ordinal);
		foreach (AssetCollection collection in bundle.Collections)
		{
			ids.Add(ExportHelper.ComputeCollectionId(collection));
		}

		return ids.OrderBy(static id => id, StringComparer.Ordinal).ToList();
	}

	private static List<BundleResourceRecord>? BuildResourceRecords(IReadOnlyList<ResourceFile> resources)
	{
		if (resources.Count == 0)
		{
			return null;
		}

		List<BundleResourceRecord> records = new(resources.Count);
		foreach (ResourceFile resource in resources)
		{
			string? filePath = string.IsNullOrWhiteSpace(resource.FilePath) ? null : resource.FilePath;
			records.Add(new BundleResourceRecord
			{
				Name = resource.Name,
				FilePath = filePath
			});
		}

		return records;
	}

	private static List<BundleFailedFileRecord>? BuildFailedFileRecords(Bundle bundle)
	{
		if (bundle.FailedFiles.Count == 0)
		{
			return null;
		}

		List<BundleFailedFileRecord> records = new(bundle.FailedFiles.Count);
		foreach (FailedFile failedFile in bundle.FailedFiles)
		{
			string? filePath = string.IsNullOrWhiteSpace(failedFile.FilePath) ? null : failedFile.FilePath;
			string? error = string.IsNullOrWhiteSpace(failedFile.StackTrace) ? null : failedFile.StackTrace;
			records.Add(new BundleFailedFileRecord
			{
				Name = failedFile.Name,
				FilePath = filePath,
				Error = error
			});
		}

		return records;
	}

	private static List<SceneRefRecord>? BuildSceneRecords(Bundle bundle)
	{
		var scenes = bundle.Scenes.ToList();
		if (scenes.Count == 0)
		{
			return null;
		}

		List<SceneRefRecord> records = new(scenes.Count);
		foreach (SceneDefinition scene in scenes)
		{
			string sceneGuid = scene.GUID.ToString();
			string? sceneName = string.IsNullOrWhiteSpace(scene.Name) ? null : scene.Name;
			string? scenePath = string.IsNullOrWhiteSpace(scene.Path) ? null : scene.Path;
			
			records.Add(new SceneRefRecord
			{
				SceneGuid = sceneGuid,
				SceneName = sceneName,
				ScenePath = scenePath
			});
		}

		return records;
	}

	private sealed class BundleAggregate
	{
		public int CollectionCount { get; set; }
		public int SceneCollectionCount { get; set; }
		public int ResourceCount { get; set; }
		public int FailedFileCount { get; set; }
		public int AssetCount { get; set; }
		public int BundleCount { get; set; }
	}
}