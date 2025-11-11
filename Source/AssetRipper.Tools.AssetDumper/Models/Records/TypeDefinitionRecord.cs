using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Type definition fact record from assembly metadata.
/// </summary>
public sealed class TypeDefinitionRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "type_definitions";

	[JsonProperty("pk")]
	public string Pk { get; set; } = string.Empty;

	[JsonProperty("assemblyGuid")]
	public string AssemblyGuid { get; set; } = string.Empty;

	[JsonProperty("assemblyName")]
	public string AssemblyName { get; set; } = string.Empty;

	[JsonProperty("namespace")]
	public string Namespace { get; set; } = string.Empty;

	[JsonProperty("typeName")]
	public string TypeName { get; set; } = string.Empty;

	[JsonProperty("fullName")]
	public string FullName { get; set; } = string.Empty;

	[JsonProperty("isClass")]
	public bool IsClass { get; set; }

	[JsonProperty("isStruct")]
	public bool IsStruct { get; set; }

	[JsonProperty("isInterface")]
	public bool IsInterface { get; set; }

	[JsonProperty("isEnum")]
	public bool IsEnum { get; set; }

	[JsonProperty("isAbstract")]
	public bool IsAbstract { get; set; }

	[JsonProperty("isSealed")]
	public bool IsSealed { get; set; }

	[JsonProperty("isGeneric")]
	public bool IsGeneric { get; set; }

	[JsonProperty("genericParameterCount", NullValueHandling = NullValueHandling.Ignore)]
	public int? GenericParameterCount { get; set; }

	[JsonProperty("visibility")]
	public string Visibility { get; set; } = "public";

	[JsonProperty("baseType", NullValueHandling = NullValueHandling.Ignore)]
	public string? BaseType { get; set; }

	[JsonProperty("scriptRef", NullValueHandling = NullValueHandling.Ignore)]
	public TypeScriptReference? ScriptRef { get; set; }
}

/// <summary>
/// Reference to a MonoScript asset from a type definition.
/// </summary>
public sealed class TypeScriptReference
{
	[JsonProperty("collectionId")]
	public string CollectionId { get; set; } = string.Empty;

	[JsonProperty("pathId")]
	public long PathId { get; set; }

	[JsonProperty("scriptGuid", NullValueHandling = NullValueHandling.Ignore)]
	public string? ScriptGuid { get; set; }
}
