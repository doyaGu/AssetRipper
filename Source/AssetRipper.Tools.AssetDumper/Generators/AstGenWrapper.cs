using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AssetRipper.Tools.AssetDumper.Generators;

public class AstGenWrapper
{
	public AstGenWrapper(string fileName, SyntaxTree tree)
	{
		AstRoot = tree.GetCompilationUnitRoot();
		FileName = fileName;
	}

	public CompilationUnitSyntax AstRoot { get; set; }
	public string FileName { get; set; }
}

public class SyntaxMetaDataProvider : IValueProvider
{
	public object GetValue(object target)
	{
		return target.GetType().IsAssignableTo(typeof(SyntaxNode))
			? GetNodeMetadata((SyntaxNode)target)
			: new SyntaxMetaData();
	}

	private static SyntaxMetaData GetNodeMetadata(SyntaxNode node)
	{
		var span = node.SyntaxTree.GetLineSpan(node.Span);
		return new SyntaxMetaData(
			$"ast.{node.Kind()}",
			span.StartLinePosition.Line,
			span.EndLinePosition.Line,
			span.StartLinePosition.Character,
			span.EndLinePosition.Character,
			node.WithoutTrivia().ToFullString().Trim()
		);
	}

	public void SetValue(object target, object? value)
	{
		// Ignore - read-only
	}

	public static JsonProperty CreateMetaDataProperty()
	{
		return new JsonProperty
		{
			PropertyName = "MetaData",
			PropertyType = typeof(SyntaxMetaData),
			DeclaringType = typeof(SyntaxNode),
			ValueProvider = new SyntaxMetaDataProvider(),
			AttributeProvider = null,
			Readable = true,
			Writable = false,
			ShouldSerialize = _ => true
		};
	}
}

public class SyntaxMetaData
{
	public SyntaxMetaData()
	{
	}

	public SyntaxMetaData(string kind, int lineStart, int lineEnd, int columnStart, int columnEnd, string code)
	{
		Kind = kind;
		LineStart = lineStart;
		LineEnd = lineEnd;
		ColumnStart = columnStart;
		ColumnEnd = columnEnd;
		Code = code;
	}

	public string Kind { get; set; } = "ast.None";
	public int LineStart { get; set; } = -1;
	public int LineEnd { get; set; } = -1;
	public int ColumnStart { get; set; } = -1;
	public int ColumnEnd { get; set; } = -1;
	public string Code { get; set; } = "<empty>";

	public override string ToString()
	{
		return JsonConvert.SerializeObject(this);
	}
}

internal class SyntaxNodePropertiesResolver : DefaultContractResolver
{
	private readonly HashSet<string> _propsToAllow = new(new[]
	{
		"Value", "Usings", "Name", "Identifier", "Left", "Right", "Members", "ConstraintClauses",
		"Alias", "NamespaceOrType", "Arguments", "Expression", "Declaration", "ElementType", "Initializer", "Else",
		"Condition", "Statement", "Statements", "Variables", "WhenNotNull", "AllowsAnyExpression", "Expressions",
		"Modifiers", "ReturnType", "IsUnboundGenericName", "Default", "IsConst", "Types",
		"ExplicitInterfaceSpecifier", "MetaData", "Kind", "AstRoot", "FileName", "Code", "Operand", "Block",
		"Catches", "Finally", "Keyword", "Incrementors", "Sections", "Pattern", "Labels", "Elements", "WhenTrue",
		"WhenFalse", "Initializers", "NameEquals", "Contents", "Attributes", "Designation", "Accessors"
	});

	private readonly List<string> _regexToAllow = new(new[]
	{
		".*Token$", ".*Lists?$", ".*Body$", "(Line|Column)(Start|End)", ".*Type$", "Parameters?"
	});

	private readonly List<string> _regexToIgnore = new(new[]
	{
		".*(Semicolon|Brace|Bracket|EndOfFile|Paren|Dot)Token$",
		"(Unsafe|Global|Static|Using)Keyword"
	});

	private bool MatchesAllow(string input) =>
		_regexToAllow.Any(regex => Regex.IsMatch(input, regex));

	private bool MatchesIgnore(string input) =>
		_regexToIgnore.Any(regex => Regex.IsMatch(input, regex));

	protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
	{
		var properties = base.CreateProperties(type, memberSerialization);
		var isSyntaxNode = type.IsAssignableTo(typeof(SyntaxNode));
		if (!isSyntaxNode) return properties;

		properties.Add(SyntaxMetaDataProvider.CreateMetaDataProperty());
		return properties;
	}

	protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
	{
		var property = base.CreateProperty(member, memberSerialization);
		var propertyName = property.PropertyName ?? "";
		var shouldSerialize = propertyName != "" &&
							  (_propsToAllow.Contains(propertyName) || MatchesAllow(propertyName)) &&
							  !MatchesIgnore(propertyName);

		property.ShouldSerialize = _ => shouldSerialize;
		return property;
	}
}
