using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Records;
using AssetRipper.Tools.AssetDumper.Utils;
using AssetRipper.Tools.AssetDumper.Writers;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace AssetRipper.Tools.AssetDumper.Exporters.Records;

/// <summary>
/// Exports detailed type member information (fields, properties, methods) using V2 schema.
/// Includes comprehensive metadata: documentation, Unity attributes, parameters, etc.
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
			NullValueHandling = NullValueHandling.Ignore
		};
		_compressionKind = compressionKind;
		_enableIndex = enableIndex;
	}

	public DomainExportResult ExportMembers(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting type members (V2 schema with full metadata)...");

		// Validate assembly manager
		if (gameData.AssemblyManager?.IsSet != true)
		{
			Logger.Warning(LogCategory.Export, "Assembly manager not available. Skipping member export.");
			return CreateEmptyResult();
		}

		// Collect all type members
		List<TypeMemberRecord> memberRecords = new List<TypeMemberRecord>();
		int processedTypes = 0;
		int processedMembers = 0;

		foreach (AssemblyDefinition assembly in gameData.AssemblyManager.GetAssemblies())
		{
			string assemblyName = GetAssemblyName(assembly);
			string assemblyGuid = ComputeAssemblyGuid(assembly);

			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (TypeDefinition type in GetAllTypes(module))
				{
					if (ShouldExportType(type))
					{
						processedTypes++;
						List<TypeMemberRecord> typeMembers = ExtractMembersForType(type, assemblyName, assemblyGuid);
						processedMembers += typeMembers.Count;
						memberRecords.AddRange(typeMembers);
					}
				}
			}
		}

		Logger.Info(LogCategory.Export, $"Processed {processedTypes} types, collected {processedMembers} members");

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
	/// Gets all types including nested types recursively.
	/// </summary>
	private IEnumerable<TypeDefinition> GetAllTypes(ModuleDefinition module)
	{
		foreach (TypeDefinition topLevelType in module.TopLevelTypes)
		{
			yield return topLevelType;
			
			foreach (TypeDefinition nestedType in GetNestedTypesRecursive(topLevelType))
			{
				yield return nestedType;
			}
		}
	}

	private IEnumerable<TypeDefinition> GetNestedTypesRecursive(TypeDefinition type)
	{
		foreach (TypeDefinition nestedType in type.NestedTypes)
		{
			yield return nestedType;
			
			foreach (TypeDefinition deepNestedType in GetNestedTypesRecursive(nestedType))
			{
				yield return deepNestedType;
			}
		}
	}

	/// <summary>
	/// Extracts all members (fields, properties, methods) for a single type.
	/// </summary>
	private List<TypeMemberRecord> ExtractMembersForType(TypeDefinition type, string assemblyName, string assemblyGuid)
	{
		List<TypeMemberRecord> records = new List<TypeMemberRecord>();

		string typeFullName = type.FullName ?? $"{type.Namespace}.{type.Name}";
		string namespaceName = type.Namespace?.Value ?? string.Empty;
		string typeName = type.Name?.Value ?? "Unknown";

		// Extract fields (exclude compiler-generated if needed)
		foreach (FieldDefinition field in type.Fields)
		{
			if (IsCompilerGenerated(field))
				continue;

			TypeMemberRecord? record = CreateFieldRecord(field, assemblyName, assemblyGuid, namespaceName, typeName, typeFullName);
			if (record != null)
			{
				records.Add(record);
			}
		}

		// Extract properties
		foreach (PropertyDefinition property in type.Properties)
		{
			if (IsCompilerGenerated(property))
				continue;

			TypeMemberRecord? record = CreatePropertyRecord(property, assemblyName, assemblyGuid, namespaceName, typeName, typeFullName);
			if (record != null)
			{
				records.Add(record);
			}
		}

		// Extract methods
		foreach (MethodDefinition method in type.Methods)
		{
			if (IsCompilerGenerated(method) || IsPropertyAccessor(method) || IsEventAccessor(method))
				continue;

			TypeMemberRecord? record = CreateMethodRecord(method, assemblyName, assemblyGuid, namespaceName, typeName, typeFullName);
			if (record != null)
			{
				records.Add(record);
			}
		}

		// Extract events
		foreach (EventDefinition evt in type.Events)
		{
			TypeMemberRecord? record = CreateEventRecord(evt, assemblyName, assemblyGuid, namespaceName, typeName, typeFullName);
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
		string assemblyGuid,
		string namespaceName,
		string typeName,
		string typeFullName)
	{
		string? fieldName = field.Name?.Value;
		if (string.IsNullOrEmpty(fieldName))
		{
			return null;
		}

		string pk = CreateMemberKey(assemblyName, namespaceName, typeName, fieldName);
		string visibility = GetFieldVisibility(field.Attributes);
		string? fieldType = field.Signature?.FieldType?.FullName;

		// Check Unity serialization
		bool serialized = CheckUnitySerialization(field, visibility);

		// Extract attributes
		List<string>? attributes = ExtractAttributeNames(field.CustomAttributes);
		bool? serializeField = HasAttribute(field.CustomAttributes, "SerializeField");
		bool? hideInInspector = HasAttribute(field.CustomAttributes, "HideInInspector");

		TypeMemberRecord record = new TypeMemberRecord
		{
			Domain = "type_members",
			Pk = pk,
			AssemblyGuid = assemblyGuid,
			TypeFullName = typeFullName,
			MemberName = fieldName,
			MemberKind = "field",
			MemberType = fieldType ?? "unknown",
			Visibility = visibility,
			IsStatic = field.IsStatic,
			Serialized = serialized,
			
			// Field-specific
			IsConst = field.IsLiteral ? true : null,
			IsReadOnly = field.IsInitOnly ? true : null,
			ConstantValue = field.IsLiteral ? field.Constant?.Value : null,
			
			// Unity-specific
			SerializeField = serializeField,
			HideInInspector = hideInInspector,
			
			// Attributes
			Attributes = attributes
		};

		return record;
	}

	/// <summary>
	/// Creates a member record for a property.
	/// </summary>
	private TypeMemberRecord? CreatePropertyRecord(
		PropertyDefinition property,
		string assemblyName,
		string assemblyGuid,
		string namespaceName,
		string typeName,
		string typeFullName)
	{
		string? propertyName = property.Name?.Value;
		if (string.IsNullOrEmpty(propertyName))
		{
			return null;
		}

		string pk = CreateMemberKey(assemblyName, namespaceName, typeName, propertyName);

		// Determine visibility and modifiers from accessors
		string visibility = "private";
		bool isStatic = false;
		bool? isVirtual = null;
		bool? isOverride = null;
		bool? isSealed = null;
		bool? isAbstract = null;

		MethodDefinition? accessor = property.GetMethod ?? property.SetMethod;
		if (accessor != null)
		{
			visibility = GetMethodVisibility(accessor.Attributes);
			isStatic = accessor.IsStatic;
			isVirtual = accessor.IsVirtual && !accessor.IsAbstract ? true : null;
			isAbstract = accessor.IsAbstract ? true : null;
			isOverride = accessor.Attributes.HasFlag(MethodAttributes.ReuseSlot) && accessor.IsVirtual ? true : null;
			isSealed = accessor.IsFinal ? true : null;
		}

		string? propertyType = property.Signature?.ReturnType?.FullName;
		bool? hasGetter = property.GetMethod != null ? true : null;
		bool? hasSetter = property.SetMethod != null ? true : null;
		bool? hasParameters = property.Semantics?.Count > 0 && property.Semantics.Any(s => s.Method?.Parameters?.Count > 0) ? true : null;

		List<string>? attributes = ExtractAttributeNames(property.CustomAttributes);

		TypeMemberRecord record = new TypeMemberRecord
		{
			Domain = "type_members",
			Pk = pk,
			AssemblyGuid = assemblyGuid,
			TypeFullName = typeFullName,
			MemberName = propertyName,
			MemberKind = "property",
			MemberType = propertyType ?? "unknown",
			Visibility = visibility,
			IsStatic = isStatic,
			Serialized = false,  // Unity doesn't serialize properties
			
			// Method modifiers (from accessor)
			IsVirtual = isVirtual,
			IsOverride = isOverride,
			IsSealed = isSealed,
			IsAbstract = isAbstract,
			
			// Property-specific
			HasGetter = hasGetter,
			HasSetter = hasSetter,
			HasParameters = hasParameters,
			
			// Attributes
			Attributes = attributes
		};

		return record;
	}

	/// <summary>
	/// Creates a member record for a method.
	/// </summary>
	private TypeMemberRecord? CreateMethodRecord(
		MethodDefinition method,
		string assemblyName,
		string assemblyGuid,
		string namespaceName,
		string typeName,
		string typeFullName)
	{
		string? methodName = method.Name?.Value;
		if (string.IsNullOrEmpty(methodName))
		{
			return null;
		}

		string pk = CreateMemberKey(assemblyName, namespaceName, typeName, methodName);
		string visibility = GetMethodVisibility(method.Attributes);
		string? returnType = method.Signature?.ReturnType?.FullName ?? "void";

		// Method modifiers
		bool? isVirtual = method.IsVirtual && !method.IsAbstract ? true : null;
		bool? isAbstract = method.IsAbstract ? true : null;
		bool? isOverride = method.Attributes.HasFlag(MethodAttributes.ReuseSlot) && method.IsVirtual ? true : null;
		bool? isSealed = method.IsFinal ? true : null;

		// Generic information
		bool? isGeneric = method.GenericParameters.Count > 0 ? true : null;
		int? genericParameterCount = method.GenericParameters.Count > 0 ? method.GenericParameters.Count : null;

		// Parameters
		int? parameterCount = method.Parameters.Count > 0 ? method.Parameters.Count : null;
		List<ParameterInfo>? parameters = null;
		
		if (method.Parameters.Count > 0)
		{
			parameters = new List<ParameterInfo>();
			for (int i = 0; i < method.Parameters.Count; i++)
			{
				AsmResolver.DotNet.Collections.Parameter param = method.Parameters[i];
				string paramName = $"param{i}";
				string paramType = "unknown";
				
				// Get parameter type from signature
				if (method.Signature?.ParameterTypes != null && i < method.Signature.ParameterTypes.Count)
				{
					paramType = method.Signature.ParameterTypes[i].FullName ?? "unknown";
				}
				
				// Get parameter name from definition
				if (param.Definition != null)
				{
					paramName = param.Definition.Name ?? paramName;
				}
				
				parameters.Add(new ParameterInfo
				{
					Name = paramName,
					Type = paramType,
					IsOptional = null,  // AsmResolver doesn't easily expose optional info
					DefaultValue = null  // AsmResolver doesn't easily expose default values
				});
			}
		}

		List<string>? attributes = ExtractAttributeNames(method.CustomAttributes);

		string memberKind = method.IsConstructor ? "constructor" : "method";

		TypeMemberRecord record = new TypeMemberRecord
		{
			Domain = "type_members",
			Pk = pk,
			AssemblyGuid = assemblyGuid,
			TypeFullName = typeFullName,
			MemberName = methodName,
			MemberKind = memberKind,
			MemberType = returnType,
			Visibility = visibility,
			IsStatic = method.IsStatic,
			Serialized = false,  // Unity doesn't serialize methods
			
			// Method modifiers
			IsVirtual = isVirtual,
			IsOverride = isOverride,
			IsSealed = isSealed,
			IsAbstract = isAbstract,
			
			// Method-specific
			ParameterCount = parameterCount,
			Parameters = parameters,
			IsGeneric = isGeneric,
			GenericParameterCount = genericParameterCount,
			
			// Attributes
			Attributes = attributes
		};

		return record;
	}

	/// <summary>
	/// Creates a member record for an event.
	/// </summary>
	private TypeMemberRecord? CreateEventRecord(
		EventDefinition evt,
		string assemblyName,
		string assemblyGuid,
		string namespaceName,
		string typeName,
		string typeFullName)
	{
		string? eventName = evt.Name?.Value;
		if (string.IsNullOrEmpty(eventName))
		{
			return null;
		}

		string pk = CreateMemberKey(assemblyName, namespaceName, typeName, eventName);

		// Determine visibility from add/remove accessors
		string visibility = "private";
		bool isStatic = false;

		MethodDefinition? accessor = evt.AddMethod ?? evt.RemoveMethod;
		if (accessor != null)
		{
			visibility = GetMethodVisibility(accessor.Attributes);
			isStatic = accessor.IsStatic;
		}

		string? eventType = evt.EventType?.FullName;
		List<string>? attributes = ExtractAttributeNames(evt.CustomAttributes);

		TypeMemberRecord record = new TypeMemberRecord
		{
			Domain = "type_members",
			Pk = pk,
			AssemblyGuid = assemblyGuid,
			TypeFullName = typeFullName,
			MemberName = eventName,
			MemberKind = "event",
			MemberType = eventType ?? "unknown",
			Visibility = visibility,
			IsStatic = isStatic,
			Serialized = false,  // Unity doesn't serialize events
			
			// Attributes
			Attributes = attributes
		};

		return record;
	}

	/// <summary>
	/// Checks if a field should be serialized by Unity.
	/// </summary>
	private bool CheckUnitySerialization(FieldDefinition field, string visibility)
	{
		// Static, const, readonly are never serialized
		if (field.IsStatic || field.IsLiteral || field.IsInitOnly)
		{
			return false;
		}

		// [NonSerialized] prevents serialization
		if (HasAttribute(field.CustomAttributes, "NonSerialized") == true)
		{
			return false;
		}

		// Public fields are serialized by default
		if (visibility == "public")
		{
			return true;
		}

		// Private/protected fields with [SerializeField] are serialized
		if (HasAttribute(field.CustomAttributes, "SerializeField") == true)
		{
			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks if member has specific attribute.
	/// </summary>
	private bool? HasAttribute(IList<CustomAttribute> attributes, string attributeName)
	{
		if (attributes == null || attributes.Count == 0)
		{
			return null;
		}

		foreach (CustomAttribute attr in attributes)
		{
			string? attrName = attr.Constructor?.DeclaringType?.Name?.Value;
			if (string.IsNullOrEmpty(attrName))
				continue;

			// Match with or without "Attribute" suffix
			if (attrName == attributeName || attrName == attributeName + "Attribute")
			{
				return true;
			}
		}

		return null;
	}

	/// <summary>
	/// Extracts attribute fully qualified names from custom attributes.
	/// </summary>
	private List<string>? ExtractAttributeNames(IList<CustomAttribute> attributes)
	{
		if (attributes == null || attributes.Count == 0)
		{
			return null;
		}

		List<string> attrNames = new List<string>();
		foreach (CustomAttribute attr in attributes)
		{
			string? fullName = attr.Constructor?.DeclaringType?.FullName;
			if (!string.IsNullOrEmpty(fullName))
			{
				attrNames.Add(fullName);
			}
		}

		return attrNames.Count > 0 ? attrNames : null;
	}

	/// <summary>
	/// Checks if member is compiler-generated.
	/// </summary>
	private bool IsCompilerGenerated(IMemberDescriptor member)
	{
		IList<CustomAttribute>? attributes = null;

		if (member is FieldDefinition field)
			attributes = field.CustomAttributes;
		else if (member is PropertyDefinition property)
			attributes = property.CustomAttributes;
		else if (member is MethodDefinition method)
			attributes = method.CustomAttributes;

		if (attributes == null)
			return false;

		foreach (CustomAttribute attr in attributes)
		{
			if (attr.Constructor?.DeclaringType?.FullName == typeof(CompilerGeneratedAttribute).FullName)
			{
				return true;
			}
		}

		// Also check name patterns
		string? name = member.Name;
		if (!string.IsNullOrEmpty(name) && name.StartsWith("<", StringComparison.Ordinal))
		{
			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks if method is a property accessor.
	/// </summary>
	private bool IsPropertyAccessor(MethodDefinition method)
	{
		string? name = method.Name?.Value;
		if (string.IsNullOrEmpty(name))
			return false;

		return name.StartsWith("get_", StringComparison.Ordinal) || 
		       name.StartsWith("set_", StringComparison.Ordinal);
	}

	/// <summary>
	/// Checks if method is an event accessor.
	/// </summary>
	private bool IsEventAccessor(MethodDefinition method)
	{
		string? name = method.Name?.Value;
		if (string.IsNullOrEmpty(name))
			return false;

		return name.StartsWith("add_", StringComparison.Ordinal) || 
		       name.StartsWith("remove_", StringComparison.Ordinal);
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
		if (string.IsNullOrEmpty(typeName))
		{
			return false;
		}

		// Skip compiler-generated types
		if (typeName.StartsWith("<", StringComparison.Ordinal))
		{
			return false;
		}

		// Skip module type
		if (typeName == "<Module>")
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// Creates member key using :: separator (V2 format).
	/// </summary>
	private static string CreateMemberKey(string assemblyName, string namespaceName, string typeName, string memberName)
	{
		// Format: ASSEMBLY::NAMESPACE::TYPENAME::MEMBERNAME (V2 format with :: separator)
		return $"{assemblyName}::{namespaceName}::{typeName}::{memberName}";
	}

	private static string ComputeAssemblyGuid(AssemblyDefinition assembly)
	{
		// Use SHA256 hash of assembly name for stable GUID generation
		string name = GetAssemblyName(assembly);
		using SHA256 hash = SHA256.Create();
		byte[] hashBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(name));
		
		// Convert first 16 bytes to GUID format, then to uppercase hex string (32 chars)
		return new Guid(hashBytes.Take(16).ToArray()).ToString("N").ToUpperInvariant();
	}

	private static string GetAssemblyName(AssemblyDefinition assembly)
	{
		return assembly.Name ?? "Unknown";
	}
}
