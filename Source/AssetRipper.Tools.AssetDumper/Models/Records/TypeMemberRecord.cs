using System.Text.Json.Serialization;

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
	[JsonPropertyName("domain")]
	public required string Domain { get; init; } = "type_members";

	/// <summary>
	/// 主键：复合键 ASSEMBLY::NAMESPACE::TYPENAME::MEMBERNAME（使用 :: 分隔避免冲突）
	/// 示例: "Assembly-CSharp::Game.Controllers::PlayerController::currentHealth"
	/// </summary>
	[JsonPropertyName("pk")]
	public required string Pk { get; init; }

	/// <summary>
	/// 程序集 GUID (16 字符十六进制，链接到 assemblies.pk)
	/// 来源：FNV1a.ComputeHash(AssemblyDefinition.FullName)
	/// </summary>
	[JsonPropertyName("assemblyGuid")]
	public required string AssemblyGuid { get; init; }

	/// <summary>
	/// 所属类型完全限定名
	/// 来源：TypeDefinition.FullName
	/// </summary>
	[JsonPropertyName("typeFullName")]
	public required string TypeFullName { get; init; }

	/// <summary>
	/// 成员名称
	/// 来源：FieldDefinition.Name / PropertyDefinition.Name / MethodDefinition.Name
	/// </summary>
	[JsonPropertyName("memberName")]
	public required string MemberName { get; init; }

	/// <summary>
	/// 成员类型：field/property/method/event/constructor/nestedType
	/// 来源：MemberReference 的具体类型
	/// </summary>
	[JsonPropertyName("memberKind")]
	public required string MemberKind { get; init; }

	/// <summary>
	/// 成员类型（字段类型、属性类型或方法返回类型）
	/// 来源：FieldDefinition.FieldType.FullName / PropertyDefinition.PropertyType.FullName / MethodDefinition.ReturnType.FullName
	/// </summary>
	[JsonPropertyName("memberType")]
	public required string MemberType { get; init; }

	/// <summary>
	/// 可见性：public/internal/private/protected/protected internal/private protected
	/// 来源：MemberDefinition.Attributes (通过 GetVisibility 方法解析)
	/// </summary>
	[JsonPropertyName("visibility")]
	public required string Visibility { get; init; }

	/// <summary>
	/// 是否为静态成员
	/// 来源：FieldDefinition.IsStatic / MethodDefinition.IsStatic / PropertyDefinition.GetMethod.IsStatic
	/// </summary>
	[JsonPropertyName("isStatic")]
	public required bool IsStatic { get; init; }

	/// <summary>
	/// Unity 是否序列化此成员
	/// 检查 Unity 序列化规则（public 字段、[SerializeField]、非 [NonSerialized]、支持的类型）
	/// </summary>
	[JsonPropertyName("serialized")]
	public required bool Serialized { get; init; }

	/// <summary>
	/// 是否为虚成员（方法/属性）
	/// 来源：MethodDefinition.IsVirtual
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("isVirtual")]
	public bool? IsVirtual { get; init; }

	/// <summary>
	/// 是否重写基类成员
	/// 来源：MethodDefinition.IsReuseSlot && MethodDefinition.IsVirtual
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("isOverride")]
	public bool? IsOverride { get; init; }

	/// <summary>
	/// 是否为密封成员（防止进一步重写）
	/// 来源：MethodDefinition.IsFinal
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("isSealed")]
	public bool? IsSealed { get; init; }

	/// <summary>
	/// 应用的 C# 特性（完全限定名）
	/// 来源：MemberDefinition.CustomAttributes
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("attributes")]
	public List<string>? Attributes { get; init; }

	/// <summary>
	/// XML 文档摘要
	/// 来源：DocumentationHandler / AssemblyParser.DocumentationString
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("documentationString")]
	public string? DocumentationString { get; init; }

	/// <summary>
	/// Obsolete 特性消息（如果成员已过时）
	/// 来源：AssemblyParser.ObsoleteMessage / [Obsolete] 特性
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("obsoleteMessage")]
	public string? ObsoleteMessage { get; init; }

	/// <summary>
	/// Unity 原生名称
	/// 来源：AssemblyParser.NativeName / [NativeName] 特性
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("nativeName")]
	public string? NativeName { get; init; }

	/// <summary>
	/// 是否由编译器生成（应从文档中排除）
	/// 来源：MemberDefinition.IsCompilerGenerated() / [CompilerGenerated] 特性
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("isCompilerGenerated")]
	public bool? IsCompilerGenerated { get; init; }

	/// <summary>
	/// 属性是否有 getter（仅属性）
	/// 来源：PropertyDefinition.GetMethod != null
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("hasGetter")]
	public bool? HasGetter { get; init; }

	/// <summary>
	/// 属性是否有 setter（仅属性）
	/// 来源：PropertyDefinition.SetMethod != null
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("hasSetter")]
	public bool? HasSetter { get; init; }

	/// <summary>
	/// 属性是否有参数（索引器，仅属性）
	/// 来源：PropertyDefinition.HasParameters()
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("hasParameters")]
	public bool? HasParameters { get; init; }

	/// <summary>
	/// 字段是否为常量（仅字段）
	/// 来源：FieldDefinition.IsLiteral
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("isConst")]
	public bool? IsConst { get; init; }

	/// <summary>
	/// 字段是否为只读（仅字段）
	/// 来源：FieldDefinition.IsInitOnly
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("isReadOnly")]
	public bool? IsReadOnly { get; init; }

	/// <summary>
	/// 常量值（对于 const 字段）
	/// 来源：FieldDefinition.Constant (对于 IsLiteral 字段)
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("constantValue")]
	public object? ConstantValue { get; init; }

	/// <summary>
	/// 方法参数数量（仅方法/构造函数）
	/// 来源：MethodDefinition.Parameters.Count
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("parameterCount")]
	public int? ParameterCount { get; init; }

	/// <summary>
	/// 方法参数详情（仅方法/构造函数）
	/// 来源：MethodDefinition.Parameters
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("parameters")]
	public List<ParameterInfo>? Parameters { get; init; }

	/// <summary>
	/// 是否有 [SerializeField] 特性（强制 Unity 序列化私有字段）
	/// 来源：FieldDefinition.CustomAttributes
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("serializeField")]
	public bool? SerializeField { get; init; }

	/// <summary>
	/// 是否有 [HideInInspector] 特性（从 Unity Inspector 隐藏）
	/// 来源：FieldDefinition.CustomAttributes
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("hideInInspector")]
	public bool? HideInInspector { get; init; }

	/// <summary>
	/// 成员是否为抽象（仅方法/属性）
	/// 来源：MethodDefinition.IsAbstract
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("isAbstract")]
	public bool? IsAbstract { get; init; }

	/// <summary>
	/// 方法/类型是否为泛型
	/// 来源：MethodDefinition.HasGenericParameters
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("isGeneric")]
	public bool? IsGeneric { get; init; }

	/// <summary>
	/// 泛型参数数量（仅泛型方法）
	/// 来源：MethodDefinition.GenericParameters.Count
	/// 2025-01-20 新增
	/// </summary>
	[JsonPropertyName("genericParameterCount")]
	public int? GenericParameterCount { get; init; }
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
	[JsonPropertyName("name")]
	public required string Name { get; init; }

	/// <summary>
	/// 参数类型（完全限定名）
	/// 来源：ParameterDefinition.ParameterType.FullName
	/// </summary>
	[JsonPropertyName("type")]
	public required string Type { get; init; }

	/// <summary>
	/// 参数是否可选
	/// 来源：ParameterDefinition.IsOptional
	/// </summary>
	[JsonPropertyName("isOptional")]
	public bool? IsOptional { get; init; }

	/// <summary>
	/// 可选参数的默认值
	/// 来源：ParameterDefinition.Constant
	/// </summary>
	[JsonPropertyName("defaultValue")]
	public object? DefaultValue { get; init; }
}
