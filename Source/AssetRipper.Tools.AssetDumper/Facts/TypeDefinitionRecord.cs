using System.Text.Json.Serialization;

namespace AssetRipper.Tools.AssetDumper.Facts;

/// <summary>
/// Model for type_definitions domain.
/// Represents .NET type definitions from assemblies with complete metadata.
/// </summary>
public sealed class TypeDefinitionRecord
{
	/// <summary>Fixed domain identifier for type_definitions table.</summary>
	[JsonPropertyName("domain")]
	public string Domain { get; set; } = "type_definitions";

	/// <summary>Composite key: ASSEMBLY::NAMESPACE::TYPENAME.</summary>
	[JsonPropertyName("pk")]
	public required string Pk { get; set; }

	/// <summary>Assembly GUID reference (16 characters).</summary>
	[JsonPropertyName("assemblyGuid")]
	public required string AssemblyGuid { get; set; }

	/// <summary>Assembly name for readability.</summary>
	[JsonPropertyName("assemblyName")]
	public required string AssemblyName { get; set; }

	/// <summary>Type namespace (empty string for global namespace).</summary>
	[JsonPropertyName("namespace")]
	public string? Namespace { get; set; }

	/// <summary>Simple type name.</summary>
	[JsonPropertyName("typeName")]
	public required string TypeName { get; set; }

	/// <summary>Fully qualified type name.</summary>
	[JsonPropertyName("fullName")]
	public required string FullName { get; set; }

	/// <summary>Whether type is a class.</summary>
	[JsonPropertyName("isClass")]
	public required bool IsClass { get; set; }

	/// <summary>Whether type is a struct.</summary>
	[JsonPropertyName("isStruct")]
	public required bool IsStruct { get; set; }

	/// <summary>Whether type is an interface.</summary>
	[JsonPropertyName("isInterface")]
	public required bool IsInterface { get; set; }

	/// <summary>Whether type is an enum.</summary>
	[JsonPropertyName("isEnum")]
	public required bool IsEnum { get; set; }

	/// <summary>Whether type is abstract.</summary>
	[JsonPropertyName("isAbstract")]
	public required bool IsAbstract { get; set; }

	/// <summary>Whether type is sealed.</summary>
	[JsonPropertyName("isSealed")]
	public required bool IsSealed { get; set; }

	/// <summary>Whether type is generic.</summary>
	[JsonPropertyName("isGeneric")]
	public required bool IsGeneric { get; set; }

	/// <summary>Number of generic parameters.</summary>
	[JsonPropertyName("genericParameterCount")]
	public int? GenericParameterCount { get; set; }

	/// <summary>Type visibility.</summary>
	[JsonPropertyName("visibility")]
	public required string Visibility { get; set; }

	/// <summary>Fully qualified base type name.</summary>
	[JsonPropertyName("baseType")]
	public string? BaseType { get; set; }

	/// <summary>Whether this is a nested type.</summary>
	[JsonPropertyName("isNested")]
	public bool? IsNested { get; set; }

	/// <summary>Fully qualified name of declaring type for nested types.</summary>
	[JsonPropertyName("declaringType")]
	public string? DeclaringType { get; set; }

	/// <summary>Fully qualified names of implemented interfaces.</summary>
	[JsonPropertyName("interfaces")]
	public List<string>? Interfaces { get; set; }

	/// <summary>Number of fields in the type.</summary>
	[JsonPropertyName("fieldCount")]
	public int? FieldCount { get; set; }

	/// <summary>Number of methods in the type.</summary>
	[JsonPropertyName("methodCount")]
	public int? MethodCount { get; set; }

	/// <summary>Number of properties in the type.</summary>
	[JsonPropertyName("propertyCount")]
	public int? PropertyCount { get; set; }

	/// <summary>Whether type derives from MonoBehaviour.</summary>
	[JsonPropertyName("isMonoBehaviour")]
	public bool? IsMonoBehaviour { get; set; }

	/// <summary>Whether type derives from ScriptableObject.</summary>
	[JsonPropertyName("isScriptableObject")]
	public bool? IsScriptableObject { get; set; }

	/// <summary>Whether type is serializable by Unity.</summary>
	[JsonPropertyName("isSerializable")]
	public bool? IsSerializable { get; set; }

	/// <summary>Reference to associated MonoScript asset (if exists).</summary>
	[JsonPropertyName("scriptRef")]
	public ScriptReference? ScriptRef { get; set; }
}

/// <summary>
/// Reference to a MonoScript asset.
/// </summary>
public sealed class ScriptReference
{
	/// <summary>Collection ID where MonoScript asset resides.</summary>
	[JsonPropertyName("collectionId")]
	public required string CollectionId { get; set; }

	/// <summary>PathID of the MonoScript asset.</summary>
	[JsonPropertyName("pathId")]
	public required long PathId { get; set; }

	/// <summary>GUID of the MonoScript asset.</summary>
	[JsonPropertyName("scriptGuid")]
	public string? ScriptGuid { get; set; }
}
