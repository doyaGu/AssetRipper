using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Writers;
using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Import.Structure.Assembly.Serializable;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace AssetRipper.Tools.AssetDumper.Exporters.Records;

/// <summary>
/// Exports script-type mapping relations to NDJSON shards for the script_type_mapping domain.
/// </summary>
internal sealed class ScriptTypeMappingExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public ScriptTypeMappingExporter(Options options, CompressionKind compressionKind, bool enableIndex)
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
	/// Exports all script-type mappings to NDJSON shards.
	/// Returns shard descriptors for manifest generation.
	/// </summary>
	public DomainExportResult ExportMappings(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting script-type mappings...");

		// Validate assembly manager availability
		if (gameData.AssemblyManager?.IsSet != true)
		{
			Logger.Warning(LogCategory.Export, "Assembly manager not available. Skipping script-type mapping export.");
			return CreateEmptyResult();
		}

		// Collect all MonoScripts
		List<ScriptWithCollection> scriptsToMap = new();
		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			string collectionId = ExportHelper.ComputeCollectionId(collection);

			foreach (IMonoScript script in collection.OfType<IMonoScript>())
			{
				scriptsToMap.Add(new ScriptWithCollection(script, collection, collectionId));
			}
		}

		int totalScripts = scriptsToMap.Count;
		Logger.Info(LogCategory.Export, $"Found {totalScripts} MonoScripts to map");

		if (totalScripts == 0)
		{
			return CreateEmptyResult();
		}

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 20000;
		long maxBytesPerShard = 50 * 1024 * 1024; // 50MB per shard

		DomainExportResult result = new DomainExportResult(
			domain: "script_type_mapping",
			tableId: "relations/script_type_mapping",
			schemaPath: "Schemas/v2/relations/script_type_mapping.schema.json");

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
		int validMappings = 0;
		int invalidMappings = 0;
		try
		{
			List<ScriptTypeMappingRecordWithKey> recordsWithKeys = ParallelProcessor.ProcessInParallelWithNulls(
				scriptsToMap,
				scriptInfo =>
				{
					try
					{
						ScriptTypeMappingRecord record = CreateMappingRecord(scriptInfo, gameData);
						return new ScriptTypeMappingRecordWithKey(record, record.ScriptPk);
					}
					catch (Exception ex)
					{
						Logger.Warning(LogCategory.Export, $"Failed to map script {scriptInfo.Script.GetFullName()}: {ex.Message}");
						return null;
					}
				},
				maxParallelism: 0); // 0 = auto-detect based on CPU cores

			// Sequential write phase
			foreach (ScriptTypeMappingRecordWithKey item in recordsWithKeys)
			{
				if (item.Record.IsValid)
					validMappings++;
				else
					invalidMappings++;

				string? indexKey = _enableIndex ? item.ScriptPk : null;
				writer.WriteRecord(item.Record, item.ScriptPk, indexKey);
				totalExported++;
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

		Logger.Info(LogCategory.Export, $"Exported {totalExported} script-type mappings across {writer.ShardCount} shards");
		Logger.Info(LogCategory.Export, $"Valid mappings: {validMappings}, Invalid mappings: {invalidMappings}");

		return result;
	}

	private ScriptTypeMappingRecord CreateMappingRecord(ScriptWithCollection scriptInfo, GameData gameData)
	{
		IMonoScript script = scriptInfo.Script;
		string scriptPk = StableKeyHelper.Create(scriptInfo.CollectionId, script.PathID);
		string scriptGuid = ScriptHashing.CalculateScriptGuid(script).ToString();
		string assemblyName = script.GetAssemblyNameFixed();
		string namespaceName = script.Namespace.String ?? string.Empty;
		string className = script.ClassName_R.String ?? script.ClassName;
		string fullName = script.GetFullName();
		string assemblyGuid = ComputeAssemblyGuid(assemblyName);

		ScriptTypeMappingRecord record = new ScriptTypeMappingRecord
		{
			Domain = "script_type_mapping",
			ScriptPk = scriptPk,
			ScriptGuid = scriptGuid,
			TypeFullName = fullName,
			AssemblyGuid = assemblyGuid,
			AssemblyName = assemblyName,
			Namespace = string.IsNullOrEmpty(namespaceName) ? null : namespaceName,
			ClassName = className,
			IsValid = false,
			FailureReason = null
		};

		// Attempt to resolve TypeDefinition
		try
		{
			ScriptIdentifier scriptId = gameData.AssemblyManager.GetScriptID(assemblyName, namespaceName, className);
			
			// Store ScriptIdentifier for debugging
			record.ScriptIdentifier = $"{assemblyName}::{namespaceName}::{className}";
			
			if (gameData.AssemblyManager.IsPresent(scriptId))
			{
				if (gameData.AssemblyManager.IsValid(scriptId))
				{
					// Try to get type definition to confirm resolution and extract generic info
					try
					{
						AsmResolver.DotNet.TypeDefinition? typeDef = gameData.AssemblyManager.GetTypeDefinition(scriptId);
						if (typeDef != null)
						{
							record.IsValid = true;
							record.FailureReason = null;
							
							// Extract generic information
							if (typeDef.GenericParameters.Count > 0)
							{
								record.IsGeneric = true;
								record.GenericParameterCount = typeDef.GenericParameters.Count;
							}
						}
						else
						{
							record.IsValid = false;
							record.FailureReason = "TypeDefinition is null";
						}
					}
					catch (Exception ex)
					{
						record.IsValid = false;
						record.FailureReason = $"Failed to get TypeDefinition: {ex.Message}";
					}
				}
				else
				{
					record.IsValid = false;
					record.FailureReason = "ScriptIdentifier is invalid";
				}
			}
			else
			{
				record.IsValid = false;
				record.FailureReason = $"Can't find type: {scriptId.UniqueName}";
			}
		}
		catch (Exception ex)
		{
			record.IsValid = false;
			record.FailureReason = $"Failed to create ScriptIdentifier: {ex.Message}";
		}

		return record;
	}

	private static string ComputeAssemblyGuid(string assemblyName)
	{
		// Use same logic as AssemblyFactsExporter for consistency
		using SHA256 hash = SHA256.Create();
		byte[] hashBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(assemblyName));
		return new Guid(hashBytes.Take(16).ToArray()).ToString("N").ToUpperInvariant();
	}

	private DomainExportResult CreateEmptyResult()
	{
		return new DomainExportResult(
			domain: "script_type_mapping",
			tableId: "relations/script_type_mapping",
			schemaPath: "Schemas/v2/relations/script_type_mapping.schema.json");
	}
}

/// <summary>
/// Helper class to hold script with its collection context.
/// </summary>
internal sealed class ScriptWithCollection
{
	public IMonoScript Script { get; }
	public AssetCollection Collection { get; }
	public string CollectionId { get; }

	public ScriptWithCollection(IMonoScript script, AssetCollection collection, string collectionId)
	{
		Script = script;
		Collection = collection;
		CollectionId = collectionId;
	}
}

/// <summary>
/// Helper class to hold mapping record with its script PK for parallel processing.
/// </summary>
internal sealed class ScriptTypeMappingRecordWithKey
{
	public ScriptTypeMappingRecord Record { get; }
	public string ScriptPk { get; }

	public ScriptTypeMappingRecordWithKey(ScriptTypeMappingRecord record, string scriptPk)
	{
		Record = record;
		ScriptPk = scriptPk;
	}
}
