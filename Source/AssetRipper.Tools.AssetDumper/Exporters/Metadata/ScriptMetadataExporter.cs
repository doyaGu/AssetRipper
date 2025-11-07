using AssetRipper.Assets.Collections;
using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.Hash128;
using AssetRipper.Tools.AssetDumper.Models;
using Newtonsoft.Json;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Writers;

namespace AssetRipper.Tools.AssetDumper.Exporters.Metadata;

/// <summary>
/// Emits script metadata records to NDJSON shards following the v2 facts layout.
/// </summary>
public sealed class ScriptMetadataExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public ScriptMetadataExporter(Options options, CompressionKind compressionKind, bool enableIndex)
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
	/// Exports script metadata to NDJSON shards and returns manifest descriptors.
	/// </summary>
	public DomainExportResult Export(GameData gameData)
	{
		if (gameData is null)
		{
			throw new ArgumentNullException(nameof(gameData));
		}

		IEnumerable<AssetCollection> collections = gameData.GameBundle.FetchAssetCollections();
		Dictionary<AssetCollection, List<IMonoScript>> scriptsByCollection = new();

		foreach (AssetCollection collection in collections)
		{
			List<IMonoScript> scripts = collection.OfType<IMonoScript>()
				.OrderBy(static script => script.PathID)
				.ToList();

			if (scripts.Count > 0)
			{
				scriptsByCollection[collection] = scripts;
			}
		}

		int totalCollections = scriptsByCollection.Count;
		int totalScripts = scriptsByCollection.Sum(static kvp => kvp.Value.Count);

		if (!_options.Silent)
		{
			Logger.Info(LogCategory.Export, $"Exporting script metadata for {totalScripts} MonoScript assets across {totalCollections} collections...");
		}

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 20_000;
		long maxBytesPerShard = 50 * 1024 * 1024;

		DomainExportResult result = new DomainExportResult(
			domain: "scriptMetadata",
			tableId: "facts/script_metadata",
			schemaPath: "Schemas/v2/facts/script_metadata.schema.json");

		ShardedNdjsonWriter writer = new(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard,
			maxBytesPerShard,
			_compressionKind,
			collectIndexEntries: _enableIndex,
			descriptorDomain: result.TableId);

		int exported = 0;

		try
		{
			foreach ((AssetCollection collection, List<IMonoScript> scripts) in scriptsByCollection
				.OrderBy(static pair => ExportHelper.ComputeCollectionId(pair.Key), StringComparer.Ordinal))
			{
				string collectionId = ExportHelper.ComputeCollectionId(collection);

				foreach (IMonoScript script in scripts)
				{
					try
					{
						string stableKey = StableKeyHelper.Create(collectionId, script.PathID);
						ScriptMetadataRecord record = CreateRecord(script, collection, collectionId, stableKey);
						string? indexKey = _enableIndex ? stableKey : null;
						writer.WriteRecord(record, stableKey, indexKey);
						exported++;
					}
					catch (Exception ex)
					{
						Logger.Warning(LogCategory.Export, $"Failed to export metadata for script {script.GetFullName()}: {ex.Message}");
					}
				}
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
			Logger.Info(LogCategory.Export, $"Exported {exported} script metadata records across {writer.ShardCount} shard(s).");
		}

		return result;
	}

	private ScriptMetadataRecord CreateRecord(IMonoScript script, AssetCollection collection, string collectionId, string stableKey)
	{
		ScriptMetadataRecord record = new()
		{
			Pk = stableKey,
			CollectionId = collectionId,
			CollectionName = collection.Name,
			BundleName = collection.Bundle?.Name,
			CollectionFlags = collection.Flags == TransferInstructionFlags.NoTransferInstructionFlags ? null : collection.Flags.ToString(),
			CollectionPlatform = collection.Platform.ToString(),
			CollectionVersion = collection.Version.ToString(),
			CollectionFilePath = collection.FilePath,
			IsSceneCollection = collection.IsScene,
			PathId = script.PathID,
			ClassId = script.ClassID,
			ClassName = script.ClassName,
			FullName = script.GetFullName(),
			Namespace = string.IsNullOrWhiteSpace(script.Namespace.String) ? null : script.Namespace.String,
			AssemblyName = script.GetAssemblyNameFixed(),
			ExecutionOrder = script.ExecutionOrder,
			ScriptGuid = SafeCompute(script, "script guid", static s => ScriptHashing.CalculateScriptGuid(s).ToString(), (string?)null),
			AssemblyGuid = SafeCompute(script, "assembly guid", static s => ScriptHashing.CalculateAssemblyGuid(s).ToString(), (string?)null),
			ScriptFileId = SafeCompute(script, "script file id", static s => (int?)ScriptHashing.CalculateScriptFileID(s), (int?)null)
		};

		TryAssignPropertiesHash(script, record);
		AssignSceneMetadata(collection, record);

		return record;
	}

	private static T SafeCompute<T>(IMonoScript script, string context, Func<IMonoScript, T> computation, T fallback)
	{
		try
		{
			return computation(script);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Failed to compute {context} for script {script.GetFullName()}: {ex.Message}");
			return fallback;
		}
	}

	private static void TryAssignPropertiesHash(IMonoScript script, ScriptMetadataRecord record)
	{
		if (!script.Has_PropertiesHash_Hash128_5())
		{
			return;
		}

		try
		{
			Hash128_5 hash = script.GetPropertiesHash();
			record.PropertiesHash = Hash128Utilities.ToLowerHex(hash);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Failed to read properties hash for script {script.GetFullName()}: {ex.Message}");
		}
	}

	private static void AssignSceneMetadata(AssetCollection collection, ScriptMetadataRecord record)
	{
		if (!collection.IsScene || collection.Scene is null)
		{
			record.Scene = null;
			return;
		}

		record.Scene = new ScriptSceneMetadata
		{
			Name = collection.Scene.Name,
			Path = collection.Scene.Path,
			Guid = collection.Scene.GUID.ToString()
		};
	}
}
