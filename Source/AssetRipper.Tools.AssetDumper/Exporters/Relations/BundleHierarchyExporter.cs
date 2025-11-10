using AssetRipper.Assets.Bundles;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models.Records;
using AssetRipper.Tools.AssetDumper.Writers;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Exporters.Relations;

/// <summary>
/// Exports bundle hierarchy edges to relations/bundle_hierarchy.ndjson.
/// </summary>
public sealed class BundleHierarchyExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;

	public BundleHierarchyExporter(Options options, CompressionKind compressionKind)
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

	public DomainExportResult Export(GameData gameData)
	{
		if (gameData is null)
		{
			throw new ArgumentNullException(nameof(gameData));
		}

		Bundle? rootBundle = gameData.GameBundle;
		if (rootBundle is null)
		{
			return new DomainExportResult(
				domain: "bundleHierarchy",
				tableId: "relations/bundle_hierarchy",
				schemaPath: "Schemas/v2/relations/bundle_hierarchy.schema.json");
		}

		List<BundleHierarchyRecord> edges = new();
		List<Bundle> lineage = new();
		TraverseForEdges(rootBundle, lineage, edges);

		DomainExportResult result = new(
			domain: "bundleHierarchy",
			tableId: "relations/bundle_hierarchy",
			schemaPath: "Schemas/v2/relations/bundle_hierarchy.schema.json");

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 50_000;
		long maxBytesPerShard = 25 * 1024 * 1024;

		ShardedNdjsonWriter writer = new(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard,
			maxBytesPerShard,
			_compressionKind,
			collectIndexEntries: false,
			descriptorDomain: result.TableId);

		try
		{
			foreach (BundleHierarchyRecord edge in edges)
			{
				string stableKey = $"{edge.ParentPk}:{edge.ChildIndex}";
				writer.WriteRecord(edge, stableKey);
			}
		}
		finally
		{
			writer.Dispose();
		}

		result.Shards.AddRange(writer.ShardDescriptors);
		return result;
	}

	private void TraverseForEdges(Bundle bundle, List<Bundle> lineage, List<BundleHierarchyRecord> edges)
	{
		lineage.Add(bundle);
		string parentPk = ComputeBundleStableKey(lineage);

		for (int i = 0; i < bundle.Bundles.Count; i++)
		{
			Bundle child = bundle.Bundles[i];
			List<Bundle> childLineage = new(lineage) { child };
			string childPk = ComputeBundleStableKey(childLineage);

			edges.Add(new BundleHierarchyRecord
			{
				ParentPk = parentPk,
				ChildPk = childPk,
				ChildIndex = i,
				ChildName = child.Name
			});

			TraverseForEdges(child, lineage, edges);
		}

		lineage.RemoveAt(lineage.Count - 1);
	}

	private static string ComputeBundleStableKey(List<Bundle> lineage)
	{
		string composite = string.Join("|", lineage.Select(static b => $"{b.GetType().FullName}:{b.Name}"));
		return ExportHelper.ComputeStableHash(composite);
	}
}
