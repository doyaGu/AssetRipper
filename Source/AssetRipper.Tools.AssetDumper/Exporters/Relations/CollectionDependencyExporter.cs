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
			string dependencySource = DetermineDependencySource(collection);

			for (int i = 0; i < collection.Dependencies.Count; i++)
			{
				AssetCollection? target = collection.Dependencies[i];
				string? targetId = target != null ? ExportHelper.ComputeCollectionId(target) : null;
				bool resolved = target != null;

				FileIdentifierRecord? fileIdRecord = ExtractFileIdentifier(collection, i);

				edges.Add(new CollectionDependencyRecord
				{
					SourceCollection = sourceId,
					DependencyIndex = i,
					TargetCollection = targetId,
					Resolved = resolved,
					Source = dependencySource,
					FileIdentifier = fileIdRecord
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

	/// <summary>
	/// Determines the source of dependencies for a collection.
	/// </summary>
	/// <param name="collection">The asset collection.</param>
	/// <returns>Dependency source type: "serialized", "dynamic", or "builtin".</returns>
	private static string DetermineDependencySource(AssetCollection collection)
	{
		string collectionName = collection.Name.ToLowerInvariant();
		
		// Check for built-in collections
		if (collectionName.Contains("builtin") || 
		    collectionName.Contains("extra") || 
		    collectionName.Contains("resources/unity_builtin"))
		{
			return "builtin";
		}

		// SerializedAssetCollection has FileIdentifiers from file
		if (collection is Assets.Collections.SerializedAssetCollection)
		{
			return "serialized";
		}

		// Other collections (ProcessedAssetCollection, etc.) use dynamic dependencies
		return "dynamic";
	}

	/// <summary>
	/// Extracts FileIdentifier information from a SerializedAssetCollection.
	/// </summary>
	/// <param name="collection">The asset collection.</param>
	/// <param name="dependencyIndex">The dependency index.</param>
	/// <returns>FileIdentifierRecord if available, otherwise null.</returns>
	private static FileIdentifierRecord? ExtractFileIdentifier(AssetCollection collection, int dependencyIndex)
	{
		// FileIdentifier is only available for SerializedAssetCollection
		if (collection is not Assets.Collections.SerializedAssetCollection)
		{
			return null;
		}

		// Index 0 is self-reference, so FileIdentifiers are not available for it
		// For other indices, we need to access the underlying SerializedFile
		// However, SerializedAssetCollection.DependencyIdentifiers is private
		// and there's no public API to access FileIdentifiers after InitializeDependencyList()
		// 
		// The FileIdentifiers are cleared after dependency resolution in InitializeDependencyList(),
		// so we can't access them at export time.
		// 
		// TODO: Consider modifying SerializedAssetCollection to preserve FileIdentifiers
		// or expose them through a public API for export purposes.
		
		return null;
	}
}
