using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// 类型成员记录（Type Members Domain）
/// 导出类型的字段、属性、方法等成员的详细元数据
/// </summary>
public class TypeMemberRecord
{
	/// <summary>
	/// Domain 固定为 "type_members"
	/// </summary>
	[JsonProperty("domain")]
	public string Domain { get; set; } = "type_members";

	/// <summary>
	/// 主键：复合键 ASSEMBLY::NAMESPACE::TYPENAME::MEMBERNAME（使用 :: 分隔避免冲突）
	/// 示例: "Assembly-CSharp::Game.Controllers::PlayerController::currentHealth"
	/// </summary>
	[JsonProperty("pk")]
	public string Pk { get; set; } = string.Empty;

	/// <summary>
	/// 程序集 GUID (16 字符十六进制，链接到 assemblies.pk)
	/// 来源：FNV1a.ComputeHash(AssemblyDefinition.FullName)
	/// </summary>
	[JsonProperty("assemblyGuid")]
	public string AssemblyGuid { get; set; } = string.Empty;

	/// <summary>
	/// 所属类型完全限定名
	/// 来源：TypeDefinition.FullName
	/// </summary>
	[JsonProperty("typeFullName")]
	public string TypeFullName { get; set; } = string.Empty;

	/// <summary>
	/// 成员名称
	/// 来源：FieldDefinition.Name / PropertyDefinition.Name / MethodDefinition.Name
	/// </summary>
	[JsonProperty("memberName")]
	public string MemberName { get; set; } = string.Empty;

	/// <summary>
	/// 成员类型：field/property/method/event/constructor/nestedType
	/// 来源：MemberReference 的具体类型
	/// </summary>
	[JsonProperty("memberKind")]
	public string MemberKind { get; set; } = string.Empty;

	/// <summary>
	/// 成员类型（字段类型、属性类型或方法返回类型）
	/// 来源：FieldDefinition.FieldType.FullName / PropertyDefinition.PropertyType.FullName / MethodDefinition.ReturnType.FullName
	/// </summary>
	[JsonProperty("memberType")]
	public string MemberType { get; set; } = string.Empty;

	/// <summary>
	/// 可见性：public/internal/private/protected/protected internal/private protected
	/// 来源：MemberDefinition.Attributes (通过 GetVisibility 方法解析)
	/// </summary>
	[JsonProperty("visibility")]
	public string Visibility { get; set; } = string.Empty;

	/// <summary>
	/// 是否为静态成员
	/// 来源：FieldDefinition.IsStatic / MethodDefinition.IsStatic / PropertyDefinition.GetMethod.IsStatic
	/// </summary>
	[JsonProperty("isStatic")]
	public bool IsStatic { get; set; }

	/// <summary>
	/// Unity 是否序列化此成员
	/// 检查 Unity 序列化规则（public 字段、[SerializeField]、非 [NonSerialized]、支持的类型）
	/// </summary>
	[JsonProperty("serialized")]
	public bool Serialized { get; set; }

	/// <summary>
	/// 是否为虚成员（方法/属性）
	/// 来源：MethodDefinition.IsVirtual
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("isVirtual", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsVirtual { get; set; }

	/// <summary>
	/// 是否重写基类成员
	/// 来源：MethodDefinition.IsReuseSlot && MethodDefinition.IsVirtual
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("isOverride", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsOverride { get; set; }

	/// <summary>
	/// 是否为密封成员（防止进一步重写）
	/// 来源：MethodDefinition.IsFinal
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("isSealed", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsSealed { get; set; }

	/// <summary>
	/// 应用的 C# 特性（完全限定名）
	/// 来源：MemberDefinition.CustomAttributes
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("attributes", NullValueHandling = NullValueHandling.Ignore)]
	public List<string>? Attributes { get; set; }

	/// <summary>
	/// XML 文档摘要
	/// 来源：DocumentationHandler / AssemblyParser.DocumentationString
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("documentationString", NullValueHandling = NullValueHandling.Ignore)]
	public string? DocumentationString { get; set; }

	/// <summary>
	/// Obsolete 特性消息（如果成员已过时）
	/// 来源：AssemblyParser.ObsoleteMessage / [Obsolete] 特性
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("obsoleteMessage", NullValueHandling = NullValueHandling.Ignore)]
	public string? ObsoleteMessage { get; set; }

	/// <summary>
	/// Unity 原生名称
	/// 来源：AssemblyParser.NativeName / [NativeName] 特性
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("nativeName", NullValueHandling = NullValueHandling.Ignore)]
	public string? NativeName { get; set; }

	/// <summary>
	/// 是否由编译器生成（应从文档中排除）
	/// 来源：MemberDefinition.IsCompilerGenerated() / [CompilerGenerated] 特性
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("isCompilerGenerated", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsCompilerGenerated { get; set; }

	/// <summary>
	/// 属性是否有 getter（仅属性）
	/// 来源：PropertyDefinition.GetMethod != null
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("hasGetter", NullValueHandling = NullValueHandling.Ignore)]
	public bool? HasGetter { get; set; }

	/// <summary>
	/// 属性是否有 setter（仅属性）
	/// 来源：PropertyDefinition.SetMethod != null
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("hasSetter", NullValueHandling = NullValueHandling.Ignore)]
	public bool? HasSetter { get; set; }

	/// <summary>
	/// 属性是否有参数（索引器，仅属性）
	/// 来源：PropertyDefinition.HasParameters()
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("hasParameters", NullValueHandling = NullValueHandling.Ignore)]
	public bool? HasParameters { get; set; }

	/// <summary>
	/// 字段是否为常量（仅字段）
	/// 来源：FieldDefinition.IsLiteral
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("isConst", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsConst { get; set; }

	/// <summary>
	/// 字段是否为只读（仅字段）
	/// 来源：FieldDefinition.IsInitOnly
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("isReadOnly", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsReadOnly { get; set; }

	/// <summary>
	/// 常量值（对于 const 字段）
	/// 来源：FieldDefinition.Constant (对于 IsLiteral 字段)
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("constantValue", NullValueHandling = NullValueHandling.Ignore)]
	public object? ConstantValue { get; set; }

	/// <summary>
	/// 方法参数数量（仅方法/构造函数）
	/// 来源：MethodDefinition.Parameters.Count
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("parameterCount", NullValueHandling = NullValueHandling.Ignore)]
	public int? ParameterCount { get; set; }

	/// <summary>
	/// 方法参数详情（仅方法/构造函数）
	/// 来源：MethodDefinition.Parameters
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("parameters", NullValueHandling = NullValueHandling.Ignore)]
	public List<ParameterInfo>? Parameters { get; set; }

	/// <summary>
	/// 是否有 [SerializeField] 特性（强制 Unity 序列化私有字段）
	/// 来源：FieldDefinition.CustomAttributes
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("serializeField", NullValueHandling = NullValueHandling.Ignore)]
	public bool? SerializeField { get; set; }

	/// <summary>
	/// 是否有 [HideInInspector] 特性（从 Unity Inspector 隐藏）
	/// 来源：FieldDefinition.CustomAttributes
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("hideInInspector", NullValueHandling = NullValueHandling.Ignore)]
	public bool? HideInInspector { get; set; }

	/// <summary>
	/// 成员是否为抽象（仅方法/属性）
	/// 来源：MethodDefinition.IsAbstract
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("isAbstract", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsAbstract { get; set; }

	/// <summary>
	/// 方法/类型是否为泛型
	/// 来源：MethodDefinition.HasGenericParameters
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("isGeneric", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsGeneric { get; set; }

	/// <summary>
	/// 泛型参数数量（仅泛型方法）
	/// 来源：MethodDefinition.GenericParameters.Count
	/// 2025-01-20 新增
	/// </summary>
	[JsonProperty("genericParameterCount", NullValueHandling = NullValueHandling.Ignore)]
	public int? GenericParameterCount { get; set; }
}

/// <summary>
/// 方法参数信息
/// </summary>
public class ParameterInfo
{
	/// <summary>
	/// 参数名称
	/// 来源：ParameterDefinition.Name
	/// </summary>
	[JsonProperty("name")]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// 参数类型（完全限定名）
	/// 来源：ParameterDefinition.ParameterType.FullName
	/// </summary>
	[JsonProperty("type")]
	public string Type { get; set; } = string.Empty;

	/// <summary>
	/// 参数是否可选
	/// 来源：ParameterDefinition.IsOptional
	/// </summary>
	[JsonProperty("isOptional", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsOptional { get; set; }

	/// <summary>
	/// 可选参数的默认值
	/// 来源：ParameterDefinition.Constant
	/// </summary>
	[JsonProperty("defaultValue", NullValueHandling = NullValueHandling.Ignore)]
	public object? DefaultValue { get; set; }
}
