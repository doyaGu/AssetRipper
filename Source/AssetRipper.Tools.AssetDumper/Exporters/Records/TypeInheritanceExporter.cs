using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Writers;
using AsmResolver.DotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetRipper.Tools.AssetDumper.Exporters.Records;

/// <summary>
/// Exports type inheritance relationships for hierarchy and polymorphism analysis.
/// Phase B exporter that tracks base classes and interface implementations.
/// </summary>
internal sealed class TypeInheritanceExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public TypeInheritanceExporter(Options options, CompressionKind compressionKind, bool enableIndex)
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

	public DomainExportResult ExportInheritance(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting type inheritance relationships...");

		// Validate assembly manager
		if (gameData.AssemblyManager?.IsSet != true)
		{
			Logger.Warning(LogCategory.Export, "Assembly manager not available. Skipping inheritance export.");
			return CreateEmptyResult();
		}

		// Build type lookup index for resolving base types
		Dictionary<string, TypeInfo> typeIndex = BuildTypeIndex(gameData.AssemblyManager);
		Logger.Info(LogCategory.Export, $"Built type index with {typeIndex.Count} types");

		// Collect all inheritance relationships
		List<TypeInheritanceRecord> inheritanceRecords = new List<TypeInheritanceRecord>();

		foreach (AssemblyDefinition assembly in gameData.AssemblyManager.GetAssemblies())
		{
			string assemblyName = GetAssemblyName(assembly);
			
			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (TypeDefinition type in module.TopLevelTypes)
				{
					if (ShouldExportType(type))
					{
						List<TypeInheritanceRecord> typeRecords = ExtractInheritanceForType(
							type, assemblyName, typeIndex);
						inheritanceRecords.AddRange(typeRecords);
					}
				}
			}
		}

		Logger.Info(LogCategory.Export, $"Collected {inheritanceRecords.Count} inheritance relationships");

		if (inheritanceRecords.Count == 0)
		{
			return CreateEmptyResult();
		}

		// Setup sharded writer
		DomainExportResult result = new DomainExportResult(
			domain: "type_inheritance",
			tableId: "relations/type_inheritance",
			schemaPath: "Schemas/v2/relations/type_inheritance.schema.json");

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 10000;
		long maxBytesPerShard = 50 * 1024 * 1024;

		ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
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
			foreach (TypeInheritanceRecord record in inheritanceRecords)
			{
				string pk = $"{record.DerivedType}->{record.BaseType}";
				string? indexKey = _enableIndex ? pk : null;
				writer.WriteRecord(record, pk, indexKey);
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

		Logger.Info(LogCategory.Export, $"Exported {inheritanceRecords.Count} inheritance relationships across {writer.ShardCount} shards");
		return result;
	}

	private DomainExportResult CreateEmptyResult()
	{
		return new DomainExportResult(
			domain: "type_inheritance",
			tableId: "relations/type_inheritance",
			schemaPath: "Schemas/v2/relations/type_inheritance.schema.json");
	}

	/// <summary>
	/// Builds an index of all types for efficient lookup during inheritance resolution.
	/// </summary>
	private Dictionary<string, TypeInfo> BuildTypeIndex(IAssemblyManager assemblyManager)
	{
		Dictionary<string, TypeInfo> index = new Dictionary<string, TypeInfo>(StringComparer.Ordinal);

		foreach (AssemblyDefinition assembly in assemblyManager.GetAssemblies())
		{
			string assemblyName = GetAssemblyName(assembly);

			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (TypeDefinition type in module.TopLevelTypes)
				{
					if (ShouldExportType(type))
					{
						string namespaceName = type.Namespace?.Value ?? string.Empty;
						string typeName = type.Name?.Value ?? "Unknown";
						string typeKey = CreateTypeKey(assemblyName, namespaceName, typeName);

						index[typeKey] = new TypeInfo
						{
							Assembly = assemblyName,
							Namespace = namespaceName,
							TypeName = typeName,
							FullName = type.FullName ?? $"{namespaceName}.{typeName}"
						};
					}
				}
			}
		}

		return index;
	}

	/// <summary>
	/// Extracts all inheritance relationships for a single type.
	/// </summary>
	private List<TypeInheritanceRecord> ExtractInheritanceForType(
		TypeDefinition type,
		string assemblyName,
		Dictionary<string, TypeInfo> typeIndex)
	{
		List<TypeInheritanceRecord> records = new List<TypeInheritanceRecord>();

		string namespaceName = type.Namespace?.Value ?? string.Empty;
		string typeName = type.Name?.Value ?? "Unknown";
		string derivedTypeKey = CreateTypeKey(assemblyName, namespaceName, typeName);

		// Process base type (class inheritance)
		if (type.BaseType != null)
		{
			TypeInheritanceRecord? baseRecord = CreateInheritanceRecord(
				derivedTypeKey,
				type.BaseType,
				typeIndex,
				isDirectBase: true,
				depth: 1);

			if (baseRecord != null)
			{
				records.Add(baseRecord);
			}
		}

		// Process interface implementations
		foreach (InterfaceImplementation iface in type.Interfaces)
		{
			if (iface.Interface != null)
			{
				TypeInheritanceRecord? ifaceRecord = CreateInheritanceRecord(
					derivedTypeKey,
					iface.Interface,
					typeIndex,
					isDirectBase: true,
					depth: 1);

				if (ifaceRecord != null)
				{
					records.Add(ifaceRecord);
				}
			}
		}

		return records;
	}

	/// <summary>
	/// Creates an inheritance record for a base type or interface.
	/// </summary>
	private TypeInheritanceRecord? CreateInheritanceRecord(
		string derivedTypeKey,
		ITypeDescriptor baseTypeDescriptor,
		Dictionary<string, TypeInfo> typeIndex,
		bool isDirectBase,
		int depth)
	{
		// Try to extract base type information
		string? baseNamespace = null;
		string? baseTypeName = null;
		string? baseAssemblyName = null;

		if (baseTypeDescriptor is TypeDefinition baseDef)
		{
			baseNamespace = baseDef.Namespace?.Value ?? string.Empty;
			baseTypeName = baseDef.Name?.Value;
			
			// TypeDefinition is already part of an assembly - we'll need to track it differently
			// For now, we'll resolve it via the type index lookup
		}
		else if (baseTypeDescriptor is TypeReference typeRef)
		{
			baseNamespace = typeRef.Namespace?.Value ?? string.Empty;
			baseTypeName = typeRef.Name?.Value;

			// Try to resolve assembly from scope
			if (typeRef.Scope is AssemblyReference asmRef)
			{
				baseAssemblyName = asmRef.Name?.Value;
			}
			else if (typeRef.Scope is ModuleReference modRef)
			{
				baseAssemblyName = modRef.Name?.Value;
			}
		}

		if (string.IsNullOrEmpty(baseTypeName))
		{
			return null;
		}

		// Build base type key
		string baseTypeKey = !string.IsNullOrEmpty(baseAssemblyName)
			? CreateTypeKey(baseAssemblyName, baseNamespace ?? string.Empty, baseTypeName)
			: $"{baseNamespace}.{baseTypeName}";

		// Check if base type exists in our index
		string? resolvedBaseAssembly = null;
		if (!string.IsNullOrEmpty(baseAssemblyName) && typeIndex.ContainsKey(baseTypeKey))
		{
			resolvedBaseAssembly = baseAssemblyName;
		}

		TypeInheritanceRecord record = new TypeInheritanceRecord
		{
			DerivedType = derivedTypeKey,
			BaseType = baseTypeKey,
			BaseAssembly = resolvedBaseAssembly,
			IsDirectBase = isDirectBase,
			InheritanceDepth = depth
		};

		return record;
	}

	private static bool ShouldExportType(TypeDefinition type)
	{
		string? typeName = type.Name?.Value;
		if (string.IsNullOrEmpty(typeName) || typeName.StartsWith("<", StringComparison.Ordinal))
		{
			return false;
		}

		if (typeName == "<Module>")
		{
			return false;
		}

		return true;
	}

	private static string CreateTypeKey(string assemblyName, string namespaceName, string typeName)
	{
		return $"{assemblyName}:{namespaceName}:{typeName}";
	}

	private static string GetAssemblyName(AssemblyDefinition assembly)
	{
		return assembly.Name ?? "Unknown";
	}

	/// <summary>
	/// Helper class for type lookup index.
	/// </summary>
	private sealed class TypeInfo
	{
		public string Assembly { get; set; } = string.Empty;
		public string Namespace { get; set; } = string.Empty;
		public string TypeName { get; set; } = string.Empty;
		public string FullName { get; set; } = string.Empty;
	}
}
