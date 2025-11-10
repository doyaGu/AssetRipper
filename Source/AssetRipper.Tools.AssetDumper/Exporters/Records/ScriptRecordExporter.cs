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
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Writers;
using AssetRipper.IO.Files.SerializedFiles;

namespace AssetRipper.Tools.AssetDumper.Exporters.Records;

/// <summary>
/// Exports script records to NDJSON shards for the scripts domain.
/// </summary>
internal class ScriptRecordExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public ScriptRecordExporter(Options options, CompressionKind compressionKind, bool enableIndex)
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
	/// Exports all script records to NDJSON shards.
	/// Returns shard descriptors for manifest generation.
	/// </summary>
	public DomainExportResult ExportScripts(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting script records...");

		var allCollections = gameData.GameBundle.FetchAssetCollections().ToList();
		var scriptsByCollection = new Dictionary<AssetCollection, List<IMonoScript>>();

		foreach (AssetCollection collection in allCollections)
		{
			var scripts = collection.OfType<IMonoScript>().ToList();
			scripts.Sort((left, right) => left.PathID.CompareTo(right.PathID));
			if (scripts.Count > 0)
			{
				scriptsByCollection[collection] = scripts;
			}
		}

		int totalDiscovered = scriptsByCollection.Sum(kvp => kvp.Value.Count);
		Logger.Info(LogCategory.Export, $"Found {totalDiscovered} MonoScript assets across {scriptsByCollection.Count} collections");

		// Scripts are typically moderate in number
		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 20000;
		long maxBytesPerShard = 50 * 1024 * 1024; // 50MB per shard

		DomainExportResult result = new DomainExportResult(
			domain: "scripts",
			tableId: "facts/scripts",
			schemaPath: "Schemas/v2/facts/scripts.schema.json");

		ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard,
			maxBytesPerShard,
			_compressionKind,
			collectIndexEntries: _enableIndex,
			descriptorDomain: result.TableId);

		int totalExported = 0;

		List<KeyValuePair<AssetCollection, List<IMonoScript>>> orderedCollections = scriptsByCollection
			.OrderBy(pair => ExportHelper.ComputeCollectionId(pair.Key), StringComparer.Ordinal)
			.ToList();

		try
		{
			foreach (KeyValuePair<AssetCollection, List<IMonoScript>> entry in orderedCollections)
			{
				AssetCollection collection = entry.Key;
				List<IMonoScript> scripts = entry.Value;
				string collectionId = ExportHelper.ComputeCollectionId(collection);

				// Parallel processing of script records within each collection
				List<ScriptRecordWithKey> recordsWithKeys = ParallelProcessor.ProcessInParallelWithNulls(
					scripts,
					script =>
					{
						try
						{
							ScriptRecord record = CreateScriptRecord(script, collection, collectionId, out string stableKey);
							return new ScriptRecordWithKey(record, stableKey);
						}
						catch (Exception ex)
						{
							Logger.Warning(LogCategory.Export, $"Failed to export script {script.GetFullName()}: {ex.Message}");
							return null;
						}
					},
					maxParallelism: 0); // 0 = auto-detect based on CPU cores

				// Sequential write phase
				foreach (ScriptRecordWithKey item in recordsWithKeys)
				{
					string? indexKey = _enableIndex ? item.StableKey : null;
					writer.WriteRecord(item.Record, item.StableKey, indexKey);
					totalExported++;
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

		Logger.Info(LogCategory.Export, $"Exported {totalExported} script records across {writer.ShardCount} shards");

		return result;
	}

	private ScriptRecord CreateScriptRecord(IMonoScript script, AssetCollection collection, string collectionId, out string stableKey)
	{
		stableKey = StableKeyHelper.Create(collectionId, script.PathID);

		ScriptRecord record = new ScriptRecord
		{
			Domain = "scripts",
			Pk = stableKey,
			CollectionId = collectionId,

			PathId = script.PathID,
			ClassId = script.ClassID,
			ClassName = script.ClassName,
			Namespace = string.IsNullOrWhiteSpace(script.Namespace.String) ? null : script.Namespace.String,
			FullName = script.GetFullName(),
			AssemblyName = script.GetAssemblyNameFixed(),
			ExecutionOrder = script.ExecutionOrder
		};

		record.ScriptGuid = SafeCompute(script, "script guid", static s => ScriptHashing.CalculateScriptGuid(s).ToString(), (string?)null);
		record.AssemblyGuid = SafeCompute(script, "assembly guid", static s => ScriptHashing.CalculateAssemblyGuid(s).ToString(), (string?)null);
		record.ScriptFileId = SafeCompute(script, "script file id", static s => (int?)ScriptHashing.CalculateScriptFileID(s), (int?)null);

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

	private static void TryAssignPropertiesHash(IMonoScript script, ScriptRecord record)
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
			Logger.Warning(LogCategory.Export,
				$"Failed to read properties hash for script {script.GetFullName()}: {ex.Message}");
		}
	}

	private static void AssignSceneMetadata(AssetCollection collection, ScriptRecord record)
	{
		if (!collection.IsScene || collection.Scene is null)
		{
			record.Scene = null;
			return;
		}

		record.Scene = new ScriptSceneInfo
		{
			Name = collection.Scene.Name,
			Path = collection.Scene.Path,
			Guid = collection.Scene.GUID.ToString()
		};
	}
}

/// <summary>
/// Helper class to hold script record with its stable key for parallel processing.
/// </summary>
internal sealed class ScriptRecordWithKey
{
	public ScriptRecord Record { get; }
	public string StableKey { get; }

	public ScriptRecordWithKey(ScriptRecord record, string stableKey)
	{
		Record = record;
		StableKey = stableKey;
	}
}
