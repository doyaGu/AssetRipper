using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Writers;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetRipper.Tools.AssetDumper.Exporters.Records;

/// <summary>
/// Exports detailed type member information (fields, properties, methods).
/// Phase C exporter that provides deep introspection for code analysis.
/// </summary>
internal sealed class TypeMemberExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public TypeMemberExporter(Options options, CompressionKind compressionKind, bool enableIndex)
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

	public DomainExportResult ExportMembers(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting type members (fields, properties, methods)...");

		// Validate assembly manager
		if (gameData.AssemblyManager?.IsSet != true)
		{
			Logger.Warning(LogCategory.Export, "Assembly manager not available. Skipping member export.");
			return CreateEmptyResult();
		}

		// Collect all type members
		List<TypeMemberRecord> memberRecords = new List<TypeMemberRecord>();

		foreach (AssemblyDefinition assembly in gameData.AssemblyManager.GetAssemblies())
		{
			string assemblyName = GetAssemblyName(assembly);

			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (TypeDefinition type in module.TopLevelTypes)
				{
					if (ShouldExportType(type))
					{
						List<TypeMemberRecord> typeMembers = ExtractMembersForType(type, assemblyName);
						memberRecords.AddRange(typeMembers);
					}
				}
			}
		}

		Logger.Info(LogCategory.Export, $"Collected {memberRecords.Count} type members");

		if (memberRecords.Count == 0)
		{
			return CreateEmptyResult();
		}

		// Setup sharded writer
		DomainExportResult result = new DomainExportResult(
			domain: "type_members",
			tableId: "facts/type_members",
			schemaPath: "Schemas/v2/facts/type_members.schema.json");

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
			foreach (TypeMemberRecord record in memberRecords)
			{
				string pk = record.Pk;
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

		Logger.Info(LogCategory.Export, $"Exported {memberRecords.Count} type members across {writer.ShardCount} shards");
		return result;
	}

	private DomainExportResult CreateEmptyResult()
	{
		return new DomainExportResult(
			domain: "type_members",
			tableId: "facts/type_members",
			schemaPath: "Schemas/v2/facts/type_members.schema.json");
	}

	/// <summary>
	/// Extracts all members (fields, properties, methods) for a single type.
	/// </summary>
	private List<TypeMemberRecord> ExtractMembersForType(TypeDefinition type, string assemblyName)
	{
		List<TypeMemberRecord> records = new List<TypeMemberRecord>();

		string typeFullName = type.FullName ?? $"{type.Namespace}.{type.Name}";
		string namespaceName = type.Namespace?.Value ?? string.Empty;
		string typeName = type.Name?.Value ?? "Unknown";

		// Extract fields
		foreach (FieldDefinition field in type.Fields)
		{
			TypeMemberRecord? record = CreateFieldRecord(field, assemblyName, namespaceName, typeName, typeFullName);
			if (record != null)
			{
				records.Add(record);
			}
		}

		// Extract properties
		foreach (PropertyDefinition property in type.Properties)
		{
			TypeMemberRecord? record = CreatePropertyRecord(property, assemblyName, namespaceName, typeName, typeFullName);
			if (record != null)
			{
				records.Add(record);
			}
		}

		// Extract methods
		foreach (MethodDefinition method in type.Methods)
		{
			TypeMemberRecord? record = CreateMethodRecord(method, assemblyName, namespaceName, typeName, typeFullName);
			if (record != null)
			{
				records.Add(record);
			}
		}

		return records;
	}

	/// <summary>
	/// Creates a member record for a field.
	/// </summary>
	private TypeMemberRecord? CreateFieldRecord(
		FieldDefinition field,
		string assemblyName,
		string namespaceName,
		string typeName,
		string typeFullName)
	{
		string? fieldName = field.Name?.Value;
		if (string.IsNullOrEmpty(fieldName))
		{
			return null;
		}

		// Skip compiler-generated backing fields
		if (fieldName.StartsWith("<", StringComparison.Ordinal) || fieldName.Contains("k__BackingField"))
		{
			return null;
		}

		string pk = CreateMemberKey(assemblyName, namespaceName, typeName, fieldName);
		string visibility = GetFieldVisibility(field.Attributes);
		string? fieldType = field.Signature?.FieldType?.FullName;

		// Check for serialization attributes
		bool serialized = HasSerializeFieldAttribute(field);

		TypeMemberRecord record = new TypeMemberRecord
		{
			Pk = pk,
			TypeFullName = typeFullName,
			MemberName = fieldName,
			MemberKind = "Field",
			MemberType = fieldType ?? "unknown",
			Visibility = visibility,
			IsStatic = field.IsStatic,
			Serialized = serialized,
			Attributes = ExtractAttributeNames(field.CustomAttributes)
		};

		return record;
	}

	/// <summary>
	/// Creates a member record for a property.
	/// </summary>
	private TypeMemberRecord? CreatePropertyRecord(
		PropertyDefinition property,
		string assemblyName,
		string namespaceName,
		string typeName,
		string typeFullName)
	{
		string? propertyName = property.Name?.Value;
		if (string.IsNullOrEmpty(propertyName))
		{
			return null;
		}

		// Skip compiler-generated properties
		if (propertyName.StartsWith("<", StringComparison.Ordinal))
		{
			return null;
		}

		string pk = CreateMemberKey(assemblyName, namespaceName, typeName, propertyName);

		// Determine visibility and static from accessors
		string visibility = "private";
		bool isStatic = false;
		bool? isVirtual = null;
		bool? isOverride = null;

		MethodDefinition? accessor = property.GetMethod ?? property.SetMethod;
		if (accessor != null)
		{
			visibility = GetMethodVisibility(accessor.Attributes);
			isStatic = accessor.IsStatic;
			if (accessor.IsVirtual && !accessor.IsAbstract)
			{
				isVirtual = true;
			}
			if (accessor.Attributes.HasFlag(MethodAttributes.ReuseSlot))
			{
				isOverride = true;
			}
		}

		string? propertyType = property.Signature?.ReturnType?.FullName;

		TypeMemberRecord record = new TypeMemberRecord
		{
			Pk = pk,
			TypeFullName = typeFullName,
			MemberName = propertyName,
			MemberKind = "Property",
			MemberType = propertyType ?? "unknown",
			Visibility = visibility,
			IsStatic = isStatic,
			IsVirtual = isVirtual,
			IsOverride = isOverride,
			Serialized = false,
			Attributes = ExtractAttributeNames(property.CustomAttributes)
		};

		return record;
	}

	/// <summary>
	/// Creates a member record for a method.
	/// </summary>
	private TypeMemberRecord? CreateMethodRecord(
		MethodDefinition method,
		string assemblyName,
		string namespaceName,
		string typeName,
		string typeFullName)
	{
		string? methodName = method.Name?.Value;
		if (string.IsNullOrEmpty(methodName))
		{
			return null;
		}

		// Skip compiler-generated methods and property accessors
		if (methodName.StartsWith("<", StringComparison.Ordinal) ||
			methodName.StartsWith("get_", StringComparison.Ordinal) ||
			methodName.StartsWith("set_", StringComparison.Ordinal) ||
			methodName.StartsWith("add_", StringComparison.Ordinal) ||
			methodName.StartsWith("remove_", StringComparison.Ordinal))
		{
			return null;
		}

		string pk = CreateMemberKey(assemblyName, namespaceName, typeName, methodName);
		string visibility = GetMethodVisibility(method.Attributes);
		string? returnType = method.Signature?.ReturnType?.FullName ?? "void";

		bool? isVirtual = null;
		bool? isOverride = null;
		bool? isSealed = null;

		if (method.IsVirtual && !method.IsAbstract)
		{
			isVirtual = true;
		}
		if (method.Attributes.HasFlag(MethodAttributes.ReuseSlot))
		{
			isOverride = true;
		}
		if (method.IsFinal)
		{
			isSealed = true;
		}

		TypeMemberRecord record = new TypeMemberRecord
		{
			Pk = pk,
			TypeFullName = typeFullName,
			MemberName = methodName,
			MemberKind = "Method",
			MemberType = returnType,
			Visibility = visibility,
			IsStatic = method.IsStatic,
			IsVirtual = isVirtual,
			IsOverride = isOverride,
			IsSealed = isSealed,
			Serialized = false,
			Attributes = ExtractAttributeNames(method.CustomAttributes)
		};

		return record;
	}

	/// <summary>
	/// Checks if a field has SerializeField attribute (Unity serialization).
	/// </summary>
	private bool HasSerializeFieldAttribute(FieldDefinition field)
	{
		foreach (CustomAttribute attr in field.CustomAttributes)
		{
			string? attrName = attr.Constructor?.DeclaringType?.Name?.Value;
			if (attrName == "SerializeField" || attrName == "SerializeFieldAttribute")
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Extracts attribute names from custom attributes.
	/// </summary>
	private string[]? ExtractAttributeNames(IList<CustomAttribute> attributes)
	{
		if (attributes == null || attributes.Count == 0)
		{
			return null;
		}

		List<string> attrNames = new List<string>();
		foreach (CustomAttribute attr in attributes)
		{
			string? attrName = attr.Constructor?.DeclaringType?.Name?.Value;
			if (!string.IsNullOrEmpty(attrName))
			{
				// Remove "Attribute" suffix if present
				if (attrName.EndsWith("Attribute", StringComparison.Ordinal) && attrName.Length > 9)
				{
					attrName = attrName.Substring(0, attrName.Length - 9);
				}
				attrNames.Add(attrName);
			}
		}

		return attrNames.Count > 0 ? attrNames.ToArray() : null;
	}

	/// <summary>
	/// Gets visibility string from field attributes.
	/// </summary>
	private string GetFieldVisibility(FieldAttributes attributes)
	{
		FieldAttributes accessMask = attributes & FieldAttributes.FieldAccessMask;

		return accessMask switch
		{
			FieldAttributes.Public => "public",
			FieldAttributes.Private => "private",
			FieldAttributes.Family => "protected",
			FieldAttributes.Assembly => "internal",
			FieldAttributes.FamilyOrAssembly => "protected internal",
			FieldAttributes.FamilyAndAssembly => "private protected",
			_ => "private"
		};
	}

	/// <summary>
	/// Gets visibility string from method attributes.
	/// </summary>
	private string GetMethodVisibility(MethodAttributes attributes)
	{
		MethodAttributes accessMask = attributes & MethodAttributes.MemberAccessMask;

		return accessMask switch
		{
			MethodAttributes.Public => "public",
			MethodAttributes.Private => "private",
			MethodAttributes.Family => "protected",
			MethodAttributes.Assembly => "internal",
			MethodAttributes.FamilyOrAssembly => "protected internal",
			MethodAttributes.FamilyAndAssembly => "private protected",
			_ => "private"
		};
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

	private static string CreateMemberKey(string assemblyName, string namespaceName, string typeName, string memberName)
	{
		// Format: ASSEMBLY:NAMESPACE:TYPENAME.MEMBERNAME
		return $"{assemblyName}:{namespaceName}:{typeName}.{memberName}";
	}

	private static string GetAssemblyName(AssemblyDefinition assembly)
	{
		return assembly.Name ?? "Unknown";
	}
}
