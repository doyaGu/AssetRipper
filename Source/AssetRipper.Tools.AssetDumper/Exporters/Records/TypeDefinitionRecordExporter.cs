using AsmResolver.DotNet;
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
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace AssetRipper.Tools.AssetDumper.Exporters.Records;

/// <summary>
/// Exports type definition facts to NDJSON shards for the type_definitions domain.
/// </summary>
internal sealed class TypeDefinitionRecordExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public TypeDefinitionRecordExporter(Options options, CompressionKind compressionKind, bool enableIndex)
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
	/// Exports all type definitions to NDJSON shards.
	/// Returns shard descriptors for manifest generation.
	/// </summary>
	public DomainExportResult ExportTypes(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting type definitions...");

		// Validate assembly manager availability
		if (gameData.AssemblyManager?.IsSet != true)
		{
			Logger.Warning(LogCategory.Export, "Assembly manager not available. Skipping type export.");
			return CreateEmptyResult();
		}

		// Build MonoScript lookup index first for efficient linking
		Dictionary<string, TypeScriptReference> scriptIndex = BuildScriptIndex(gameData);
		Logger.Info(LogCategory.Export, $"Built script index with {scriptIndex.Count} entries");

		// Collect all types from all assemblies
		List<TypeWithAssembly> typesToExport = new();
		foreach (AssemblyDefinition assembly in gameData.AssemblyManager.GetAssemblies())
		{
			string assemblyGuid = ComputeAssemblyGuid(assembly);
			string assemblyName = GetAssemblyName(assembly);

			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (TypeDefinition type in module.TopLevelTypes)
				{
					if (ShouldExportType(type))
					{
						typesToExport.Add(new TypeWithAssembly(type, assembly, assemblyGuid, assemblyName));
					}
				}
			}
		}

		int totalTypes = typesToExport.Count;
		Logger.Info(LogCategory.Export, $"Found {totalTypes} types to export");

		if (totalTypes == 0)
		{
			return CreateEmptyResult();
		}

		// Types can be numerous in large projects
		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 10000;
		long maxBytesPerShard = 50 * 1024 * 1024; // 50MB per shard

		DomainExportResult result = new DomainExportResult(
			domain: "type_definitions",
			tableId: "facts/type_definitions",
			schemaPath: "Schemas/v2/facts/type_definitions.schema.json");

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

		try
		{
			// Process types with parallel processing
			List<TypeDefinitionRecordWithKey> recordsWithKeys = ParallelProcessor.ProcessInParallelWithNulls(
				typesToExport,
				typeInfo =>
				{
					try
					{
						TypeDefinitionRecord record = CreateTypeRecord(typeInfo, scriptIndex);
						return new TypeDefinitionRecordWithKey(record, record.Pk);
					}
					catch (Exception ex)
					{
						Logger.Warning(LogCategory.Export, $"Failed to export type {typeInfo.Type.FullName}: {ex.Message}");
						return null;
					}
				},
				maxParallelism: 0); // 0 = auto-detect based on CPU cores

			// Sequential write phase
			foreach (TypeDefinitionRecordWithKey item in recordsWithKeys)
			{
				string? indexKey = _enableIndex ? item.Pk : null;
				writer.WriteRecord(item.Record, item.Pk, indexKey);
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

		Logger.Info(LogCategory.Export, $"Exported {totalExported} type definition records across {writer.ShardCount} shards");

		return result;
	}

	private Dictionary<string, TypeScriptReference> BuildScriptIndex(GameData gameData)
	{
		Dictionary<string, TypeScriptReference> index = new();

		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			string collectionId = ExportHelper.ComputeCollectionId(collection);

			foreach (IMonoScript script in collection.OfType<IMonoScript>())
			{
				try
				{
					string assemblyName = script.GetAssemblyNameFixed();
					string namespaceName = script.Namespace.String ?? string.Empty;
					string className = script.ClassName;

					// Create lookup key: ASSEMBLY:NAMESPACE:TYPENAME
					string key = CreateTypeKey(assemblyName, namespaceName, className);

					// Store reference (last wins if duplicates)
					index[key] = new TypeScriptReference
					{
						CollectionId = collectionId,
						PathId = script.PathID,
						ScriptGuid = ScriptHashing.CalculateScriptGuid(script).ToString()
					};
				}
				catch (Exception ex)
				{
					Logger.Verbose(LogCategory.Export, $"Failed to index script {script.ClassName}: {ex.Message}");
				}
			}
		}

		return index;
	}

	private TypeDefinitionRecord CreateTypeRecord(TypeWithAssembly typeInfo, Dictionary<string, TypeScriptReference> scriptIndex)
	{
		TypeDefinition type = typeInfo.Type;
		string fullName = type.FullName ?? $"{type.Namespace}.{type.Name}";
		string namespaceName = type.Namespace ?? string.Empty;
		string typeName = type.Name ?? "Unknown";

		// Create composite primary key
		string pk = CreateTypeKey(typeInfo.AssemblyName, namespaceName, typeName);

		TypeDefinitionRecord record = new TypeDefinitionRecord
		{
			Domain = "type_definitions",
			Pk = pk,
			AssemblyGuid = typeInfo.AssemblyGuid,
			AssemblyName = typeInfo.AssemblyName,
			Namespace = namespaceName,
			TypeName = typeName,
			FullName = fullName,
			IsClass = type.IsClass,
			IsStruct = type.IsValueType && !type.IsEnum,
			IsInterface = type.IsInterface,
			IsEnum = type.IsEnum,
			IsAbstract = type.IsAbstract,
			IsSealed = type.IsSealed,
			IsGeneric = type.GenericParameters.Count > 0,
			GenericParameterCount = type.GenericParameters.Count > 0 ? type.GenericParameters.Count : null,
			Visibility = GetVisibility(type)
		};

		// Set base type if exists
		if (type.BaseType != null)
		{
			record.BaseType = type.BaseType.FullName;
		}

		// Try to link to MonoScript
		string lookupKey = CreateTypeKey(typeInfo.AssemblyName, namespaceName, typeName);
		if (scriptIndex.TryGetValue(lookupKey, out TypeScriptReference? scriptRef))
		{
			record.ScriptRef = scriptRef;
		}

		return record;
	}

	private static bool ShouldExportType(TypeDefinition type)
	{
		// Skip compiler-generated types and nested types (export only top-level)
		string? typeName = type.Name?.Value;
		if (string.IsNullOrEmpty(typeName) || typeName.StartsWith("<", StringComparison.Ordinal))
		{
			return false;
		}

		// Skip special names (like module types)
		if (typeName == "<Module>")
		{
			return false;
		}

		return true;
	}

	private static string GetVisibility(TypeDefinition type)
	{
		if (type.IsPublic || type.IsNestedPublic)
			return "public";
		if (type.IsNestedPrivate)
			return "private";
		if (type.IsNestedFamily || type.IsNestedFamilyOrAssembly)
			return "protected";
		if (type.IsNotPublic || type.IsNestedAssembly)
			return "internal";

		return "internal"; // Default fallback
	}

	private static string CreateTypeKey(string assemblyName, string namespaceName, string typeName)
	{
		// Format: ASSEMBLY:NAMESPACE:TYPENAME
		// Empty namespace becomes empty string between colons
		return $"{assemblyName}:{namespaceName}:{typeName}";
	}

	private static string GetAssemblyName(AssemblyDefinition assembly)
	{
		return assembly.Name ?? "Unknown";
	}

	private static string ComputeAssemblyGuid(AssemblyDefinition assembly)
	{
		// Use same logic as AssemblyFactsExporter for consistency
		string name = GetAssemblyName(assembly);
		using SHA256 hash = SHA256.Create();
		byte[] hashBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(name));
		return new Guid(hashBytes.Take(16).ToArray()).ToString("N").ToUpperInvariant();
	}

	private DomainExportResult CreateEmptyResult()
	{
		return new DomainExportResult(
			domain: "type_definitions",
			tableId: "facts/type_definitions",
			schemaPath: "Schemas/v2/facts/type_definitions.schema.json");
	}
}

/// <summary>
/// Helper class to hold type with its assembly context.
/// </summary>
internal sealed class TypeWithAssembly
{
	public TypeDefinition Type { get; }
	public AssemblyDefinition Assembly { get; }
	public string AssemblyGuid { get; }
	public string AssemblyName { get; }

	public TypeWithAssembly(TypeDefinition type, AssemblyDefinition assembly, string assemblyGuid, string assemblyName)
	{
		Type = type;
		Assembly = assembly;
		AssemblyGuid = assemblyGuid;
		AssemblyName = assemblyName;
	}
}

/// <summary>
/// Helper class to hold type record with its primary key for parallel processing.
/// </summary>
internal sealed class TypeDefinitionRecordWithKey
{
	public TypeDefinitionRecord Record { get; }
	public string Pk { get; }

	public TypeDefinitionRecordWithKey(TypeDefinitionRecord record, string pk)
	{
		Record = record;
		Pk = pk;
	}
}
