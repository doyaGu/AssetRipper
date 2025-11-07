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
			tableId: "primary/scripts",
			schemaPath: "Schemas/v2/primary/scripts.schema.json");

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
			CollectionId = collectionId,
			Collection = collection.Name,
			BundleName = collection.Bundle?.Name ?? string.Empty,
			Platform = collection.Platform.ToString(),
			Version = collection.Version.ToString(),
			Flags = collection.Flags.ToString(),
			File = collection.FilePath,

			PathId = script.PathID,
			ClassId = script.ClassID,
			ClassName = script.ClassName,
			Namespace = script.Namespace.String,
			FullName = script.GetFullName(),
			AssemblyName = script.GetAssemblyNameFixed(),
			ExecutionOrder = script.ExecutionOrder
		};

		// Add script identifiers (safe computation with fallbacks)
		record.ScriptGuid = SafeCompute(script, "script guid", () => ScriptHashing.CalculateScriptGuid(script).ToString(), string.Empty);
		record.AssemblyGuid = SafeCompute(script, "assembly guid", () => ScriptHashing.CalculateAssemblyGuid(script).ToString(), (string?)null);
		record.ScriptFileId = SafeCompute(script, "script file id", () => (int?)ScriptHashing.CalculateScriptFileID(script), (int?)null);

		TryAssignPropertiesHash(script, record);

		// TODO: Add behaviour resolution info if available
		// This would require integration with the behaviour resolution system
		// record.Behaviour = ResolveBehaviourInfo(script);

		return record;
	}

	private T SafeCompute<T>(IMonoScript script, string context, Func<T> computation, T fallback)
	{
		try
		{
			return computation();
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
