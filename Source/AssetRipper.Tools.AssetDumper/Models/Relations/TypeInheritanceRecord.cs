using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Relations;

/// <summary>
/// Type inheritance relation record for hierarchy and polymorphism analysis.
/// Captures both class inheritance and interface implementations with full type hierarchy information.
/// </summary>
public sealed class TypeInheritanceRecord
{
	/// <summary>
	/// Domain identifier for type inheritance relationships.
	/// </summary>
	[JsonProperty("domain")]
	public string Domain { get; set; } = "type_inheritance";

	/// <summary>
	/// Fully qualified derived type name in format 'Namespace.TypeName'.
	/// For nested types, uses '+' separator (e.g., 'Outer+Inner').
	/// Source: TypeDefinition.FullName
	/// </summary>
	[JsonProperty("derivedType")]
	public string DerivedType { get; set; } = string.Empty;

	/// <summary>
	/// Assembly containing the derived type.
	/// Source: AssemblyDefinition.Name
	/// </summary>
	[JsonProperty("derivedAssembly")]
	public string DerivedAssembly { get; set; } = string.Empty;

	/// <summary>
	/// Fully qualified base type name.
	/// For class inheritance: immediate base class.
	/// For interface implementation: implemented interface.
	/// Source: TypeDefinition.BaseType or InterfaceImplementation.Interface
	/// </summary>
	[JsonProperty("baseType")]
	public string BaseType { get; set; } = string.Empty;

	/// <summary>
	/// Assembly containing the base type.
	/// Resolved from TypeReference.Scope (AssemblyReference or ModuleReference).
	/// May be null for unresolved external types.
	/// </summary>
	[JsonProperty("baseAssembly")]
	public string BaseAssembly { get; set; } = string.Empty;

	/// <summary>
	/// Type of inheritance relationship.
	/// 'class_inheritance' for base class relationships (including abstract classes).
	/// 'interface_implementation' for implemented interfaces.
	/// Source: TypeDefinition.BaseType vs TypeDefinition.Interfaces
	/// </summary>
	[JsonProperty("relationshipType")]
	public string RelationshipType { get; set; } = string.Empty;

	/// <summary>
	/// Distance in inheritance chain from derived to base type.
	/// Always 1 for direct relationships (immediate base class or directly implemented interface).
	/// For transitive relationships: 2 = grandparent, 3 = great-grandparent, etc.
	/// </summary>
	[JsonProperty("inheritanceDistance")]
	public int InheritanceDistance { get; set; } = 1;

	/// <summary>
	/// Depth of derived type in complete inheritance hierarchy from root.
	/// For Unity types: 0 = UnityObjectBase/UnityAssetBase, 1 = Object, 2 = Component, etc.
	/// For standard .NET types: distance from System.Object.
	/// Calculated by traversing BaseType chain.
	/// Source: Pass920_InterfaceInheritance.GetInheritanceDepth()
	/// </summary>
	[JsonProperty("inheritanceDepth", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public int InheritanceDepth { get; set; }

	/// <summary>
	/// Type arguments if base type is generic (e.g., 'List&lt;string&gt;' has ['System.String']).
	/// Empty array or null for non-generic base types.
	/// Source: GenericInstanceTypeSignature.TypeArguments
	/// </summary>
	[JsonProperty("baseTypeArguments", NullValueHandling = NullValueHandling.Ignore)]
	public string[]? BaseTypeArguments { get; set; }

	/// <summary>
	/// Total number of types that directly or transitively inherit from the derived type (including the type itself).
	/// Useful for identifying leaf types (count=1) vs base types with many descendants.
	/// Source: Pass011_ApplyInheritance.SetDescendantCount()
	/// </summary>
	[JsonProperty("descendantCount", NullValueHandling = NullValueHandling.Ignore)]
	public int? DescendantCount { get; set; }
}
