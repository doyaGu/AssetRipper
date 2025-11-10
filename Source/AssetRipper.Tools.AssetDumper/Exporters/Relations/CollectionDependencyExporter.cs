using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models.Records;
using AssetRipper.Tools.AssetDumper.Writers;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Exporters.Relations;

/// <summary>
/// Exports collection-level dependency edges to relations/collection_dependencies.ndjson.
/// </summary>
public sealed class CollectionDependencyExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;

	public CollectionDependencyExporter(Options options, CompressionKind compressionKind)
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

		List<CollectionDependencyRecord> edges = new();

		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			string sourceId = ExportHelper.ComputeCollectionId(collection);

			for (int i = 0; i < collection.Dependencies.Count; i++)
			{
				AssetCollection? target = collection.Dependencies[i];
				string? targetId = target != null ? ExportHelper.ComputeCollectionId(target) : null;

				edges.Add(new CollectionDependencyRecord
				{
					SourceCollection = sourceId,
					DependencyIndex = i,
					TargetCollection = targetId,
					FileIdentifier = null // FileIdentifier information could be added here if available
				});
			}
		}

		DomainExportResult result = new(
			domain: "collectionDependencies",
			tableId: "relations/collection_dependencies",
			schemaPath: "Schemas/v2/relations/collection_dependencies.schema.json");

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 100_000;
		long maxBytesPerShard = 50 * 1024 * 1024;

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
			foreach (CollectionDependencyRecord edge in edges)
			{
				string stableKey = $"{edge.SourceCollection}:{edge.DependencyIndex}";
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
}
