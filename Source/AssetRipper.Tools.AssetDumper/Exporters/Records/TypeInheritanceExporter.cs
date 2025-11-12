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
/// Phase B exporter that tracks base classes and interface implementations with full hierarchy information.
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

		// Build descendant count map
		Dictionary<string, int> descendantCounts = CalculateDescendantCounts(gameData.AssemblyManager, typeIndex);
		Logger.Info(LogCategory.Export, $"Calculated descendant counts for {descendantCounts.Count} types");

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
							type, assemblyName, typeIndex, descendantCounts);
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
						string fullName = type.FullName ?? $"{type.Namespace?.Value ?? string.Empty}.{type.Name?.Value ?? "Unknown"}";

						TypeInfo typeInfo = new TypeInfo
						{
							Assembly = assemblyName,
							FullName = fullName
						};

						index[fullName] = typeInfo;
					}
				}
			}
		}

		return index;
	}

	/// <summary>
	/// Calculates descendant counts for all types.
	/// </summary>
	private Dictionary<string, int> CalculateDescendantCounts(
		IAssemblyManager assemblyManager,
		Dictionary<string, TypeInfo> typeIndex)
	{
		Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
		Dictionary<string, HashSet<string>> childrenMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

		// Build children map
		foreach (AssemblyDefinition assembly in assemblyManager.GetAssemblies())
		{
			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (TypeDefinition type in module.TopLevelTypes)
				{
					if (ShouldExportType(type))
					{
						string derivedFullName = type.FullName ?? $"{type.Namespace?.Value ?? string.Empty}.{type.Name?.Value ?? "Unknown"}";

						if (type.BaseType != null)
						{
							string? baseFullName = ExtractFullTypeName(type.BaseType);
							if (baseFullName != null)
							{
								if (!childrenMap.ContainsKey(baseFullName))
								{
									childrenMap[baseFullName] = new HashSet<string>(StringComparer.Ordinal);
								}
								childrenMap[baseFullName].Add(derivedFullName);
							}
						}
					}
				}
			}
		}

		// Calculate descendant counts recursively
		foreach (string typeName in typeIndex.Keys)
		{
			counts[typeName] = CalculateDescendantCountRecursive(typeName, childrenMap, new HashSet<string>(StringComparer.Ordinal));
		}

		return counts;
	}

	private int CalculateDescendantCountRecursive(
		string typeName,
		Dictionary<string, HashSet<string>> childrenMap,
		HashSet<string> visited)
	{
		if (visited.Contains(typeName))
		{
			return 0; // Circular reference guard
		}

		visited.Add(typeName);
		int count = 1; // Count the type itself

		if (childrenMap.TryGetValue(typeName, out HashSet<string>? children))
		{
			foreach (string child in children)
			{
				count += CalculateDescendantCountRecursive(child, childrenMap, visited);
			}
		}

		return count;
	}

	/// <summary>
	/// Extracts all inheritance relationships for a single type.
	/// </summary>
	private List<TypeInheritanceRecord> ExtractInheritanceForType(
		TypeDefinition type,
		string assemblyName,
		Dictionary<string, TypeInfo> typeIndex,
		Dictionary<string, int> descendantCounts)
	{
		List<TypeInheritanceRecord> records = new List<TypeInheritanceRecord>();

		string derivedFullName = type.FullName ?? $"{type.Namespace?.Value ?? string.Empty}.{type.Name?.Value ?? "Unknown"}";
		int inheritanceDepth = CalculateInheritanceDepth(type);
		int? descendantCount = descendantCounts.TryGetValue(derivedFullName, out int count) ? count : null;

		// Process base type (class inheritance)
		if (type.BaseType != null)
		{
			TypeInheritanceRecord? baseRecord = CreateInheritanceRecord(
				derivedFullName,
				assemblyName,
				type.BaseType,
				typeIndex,
				"class_inheritance",
				inheritanceDistance: 1,
				inheritanceDepth: inheritanceDepth,
				descendantCount: descendantCount);

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
					derivedFullName,
					assemblyName,
					iface.Interface,
					typeIndex,
					"interface_implementation",
					inheritanceDistance: 1,
					inheritanceDepth: inheritanceDepth,
					descendantCount: descendantCount);

				if (ifaceRecord != null)
				{
					records.Add(ifaceRecord);
				}
			}
		}

		return records;
	}

	/// <summary>
	/// Calculates inheritance depth by traversing the BaseType chain.
	/// </summary>
	private int CalculateInheritanceDepth(TypeDefinition type)
	{
		int depth = 0;
		TypeDefinition? current = type;

		while (current?.BaseType != null)
		{
			depth++;
			
			// Try to resolve the base type
			if (current.BaseType is TypeDefinition baseDef)
			{
				current = baseDef;
			}
			else if (current.BaseType is TypeReference typeRef)
			{
				current = typeRef.Resolve();
			}
			else
			{
				break;
			}

			// Safety check to prevent infinite loops
			if (depth > 100)
			{
				break;
			}
		}

		return depth;
	}

	/// <summary>
	/// Creates an inheritance record for a base type or interface.
	/// </summary>
	private TypeInheritanceRecord? CreateInheritanceRecord(
		string derivedFullName,
		string derivedAssembly,
		ITypeDescriptor baseTypeDescriptor,
		Dictionary<string, TypeInfo> typeIndex,
		string relationshipType,
		int inheritanceDistance,
		int inheritanceDepth,
		int? descendantCount)
	{
		string? baseFullName = ExtractFullTypeName(baseTypeDescriptor);
		if (baseFullName == null)
		{
			return null;
		}

		string? baseAssemblyName = ExtractAssemblyName(baseTypeDescriptor);
		string[]? baseTypeArguments = ExtractGenericTypeArguments(baseTypeDescriptor);

		TypeInheritanceRecord record = new TypeInheritanceRecord
		{
			DerivedType = derivedFullName,
			DerivedAssembly = derivedAssembly,
			BaseType = baseFullName,
			BaseAssembly = baseAssemblyName ?? string.Empty,
			RelationshipType = relationshipType,
			InheritanceDistance = inheritanceDistance,
			InheritanceDepth = inheritanceDepth,
			BaseTypeArguments = baseTypeArguments,
			DescendantCount = descendantCount
		};

		return record;
	}

	/// <summary>
	/// Extracts the full type name from a type descriptor.
	/// </summary>
	private string? ExtractFullTypeName(ITypeDescriptor typeDescriptor)
	{
		if (typeDescriptor is TypeDefinition typeDef)
		{
			return typeDef.FullName;
		}
		else if (typeDescriptor is TypeReference typeRef)
		{
			return typeRef.FullName;
		}
		else if (typeDescriptor is TypeSpecification typeSpec)
		{
			// For generic instances, get the base type name
			return typeSpec.Signature?.ToString();
		}

		return null;
	}

	/// <summary>
	/// Extracts the assembly name from a type descriptor.
	/// </summary>
	private string? ExtractAssemblyName(ITypeDescriptor typeDescriptor)
	{
		if (typeDescriptor is TypeDefinition typeDef)
		{
			// TypeDefinition doesn't directly expose Module in AsmResolver
			// We need to find the assembly through the declaring type or from context
			return null; // Will be resolved through type index lookup
		}
		else if (typeDescriptor is TypeReference typeRef)
		{
			if (typeRef.Scope is AssemblyReference asmRef)
			{
				return asmRef.Name;
			}
			else if (typeRef.Scope is ModuleReference modRef)
			{
				return modRef.Name;
			}
		}

		return null;
	}

	/// <summary>
	/// Extracts generic type arguments from a type descriptor.
	/// </summary>
	private string[]? ExtractGenericTypeArguments(ITypeDescriptor typeDescriptor)
	{
		// Check if it's a generic instance
		if (typeDescriptor is TypeSpecification typeSpec)
		{
			// Try to get generic arguments from the signature
			string? sigString = typeSpec.Signature?.ToString();
			if (!string.IsNullOrEmpty(sigString) && sigString.Contains("<") && sigString.Contains(">"))
			{
				// Simple parsing of generic arguments from signature string
				int startIdx = sigString.IndexOf('<');
				int endIdx = sigString.LastIndexOf('>');
				if (startIdx >= 0 && endIdx > startIdx)
				{
					string argsStr = sigString.Substring(startIdx + 1, endIdx - startIdx - 1);
					List<string> args = new List<string>();
					
					// Split by comma, handling nested generics
					int depth = 0;
					int lastIdx = 0;
					for (int i = 0; i < argsStr.Length; i++)
					{
						if (argsStr[i] == '<') depth++;
						else if (argsStr[i] == '>') depth--;
						else if (argsStr[i] == ',' && depth == 0)
						{
							args.Add(argsStr.Substring(lastIdx, i - lastIdx).Trim());
							lastIdx = i + 1;
						}
					}
					if (lastIdx < argsStr.Length)
					{
						args.Add(argsStr.Substring(lastIdx).Trim());
					}

					return args.Count > 0 ? args.ToArray() : null;
				}
			}
		}

		return null;
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
		public string FullName { get; set; } = string.Empty;
	}
}
