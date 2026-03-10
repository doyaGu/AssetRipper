using System.IO.Compression;
using System.Text;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Facts;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Helpers;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Exporters.Facts;

public sealed class ScriptSourceIndexBuilderTests : IDisposable
{
	private readonly DisposableDirectory _testDirectory = TestPathHelper.CreateDisposableDirectory(nameof(ScriptSourceIndexBuilderTests));

	public void Dispose()
	{
		_testDirectory.Dispose();
	}

	[Fact]
	public void Build_WhenScriptMetadataShardIsGzipCompressed_ShouldLoadMatchingScript()
	{
		string scriptMetadataPath = Path.Combine(_testDirectory.Path, "facts", "script_metadata", "part-00000.ndjson.gz");
		string sourcePath = Path.Combine(_testDirectory.Path, "scripts", "Assembly-CSharp", "MyNamespace", "MyClass.cs");
		string astPath = Path.Combine(_testDirectory.Path, "ast", "Assembly-CSharp", "MyNamespace", "MyClass.json");

		Directory.CreateDirectory(Path.GetDirectoryName(scriptMetadataPath)!);
		Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
		Directory.CreateDirectory(Path.GetDirectoryName(astPath)!);

		WriteGzipText(
			scriptMetadataPath,
			"""
			{"fullName":"MyNamespace.MyClass","scriptGuid":"SCRIPTGUID","pk":"COLLECTION:1","assemblyGuid":"ASSEMBLYGUID"}
			""");
		File.WriteAllText(sourcePath, "namespace MyNamespace { public class MyClass {} }");
		File.WriteAllText(astPath, """{"FileName":"scripts/Assembly-CSharp/MyNamespace/MyClass.cs"}""");

		Options options = new()
		{
			InputPath = _testDirectory.Path,
			OutputPath = _testDirectory.Path,
			Quiet = true
		};

		ScriptSourceIndexBuildResult result = new ScriptSourceIndexBuilder(options).Build();

		result.Records.Should().HaveCount(1);
		result.MatchedScripts.Should().Be(1);
		result.MissingAst.Should().BeEmpty();
		result.InvalidAst.Should().BeEmpty();
		result.Records[0].Record.SourcePath.Should().Be("scripts/Assembly-CSharp/MyNamespace/MyClass.cs");
		result.Records[0].Record.AstPath.Should().Be("ast/Assembly-CSharp/MyNamespace/MyClass.json");
	}

	[Fact]
	public void Build_WhenScriptMetadataContainsDuplicateLogicalIdentity_ShouldChooseDeterministicScriptPk()
	{
		string scriptMetadataPath = Path.Combine(_testDirectory.Path, "facts", "script_metadata", "part-00000.ndjson");
		string sourcePath = Path.Combine(_testDirectory.Path, "scripts", "Assembly-CSharp", "MyNamespace", "MyClass.cs");
		string astPath = Path.Combine(_testDirectory.Path, "ast", "Assembly-CSharp", "MyNamespace", "MyClass.json");

		Directory.CreateDirectory(Path.GetDirectoryName(scriptMetadataPath)!);
		Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
		Directory.CreateDirectory(Path.GetDirectoryName(astPath)!);

		File.WriteAllLines(
			scriptMetadataPath,
			[
				"""{"fullName":"MyNamespace.MyClass","scriptGuid":"SCRIPTGUID","pk":"COLLECTION:2","assemblyGuid":"ASSEMBLYGUID"}""",
				"""{"fullName":"MyNamespace.MyClass","scriptGuid":"SCRIPTGUID","pk":"COLLECTION:1","assemblyGuid":"ASSEMBLYGUID"}"""
			]);
		File.WriteAllText(sourcePath, "namespace MyNamespace { public class MyClass {} }");
		File.WriteAllText(astPath, """{"FileName":"scripts/Assembly-CSharp/MyNamespace/MyClass.cs"}""");

		Options options = new()
		{
			InputPath = _testDirectory.Path,
			OutputPath = _testDirectory.Path,
			Quiet = true
		};

		ScriptSourceIndexBuildResult result = new ScriptSourceIndexBuilder(options).Build();

		result.Records.Should().HaveCount(1);
		result.Records[0].Record.Pk.Should().Be("SCRIPTGUID");
		result.Records[0].Record.ScriptPk.Should().Be("COLLECTION:1");
	}

	[Fact]
	public void Build_WhenScriptMetadataContainsConflictingLogicalIdentity_ShouldThrow()
	{
		string scriptMetadataPath = Path.Combine(_testDirectory.Path, "facts", "script_metadata", "part-00000.ndjson");
		string sourcePath = Path.Combine(_testDirectory.Path, "scripts", "Assembly-CSharp", "MyNamespace", "MyClass.cs");
		string astPath = Path.Combine(_testDirectory.Path, "ast", "Assembly-CSharp", "MyNamespace", "MyClass.json");

		Directory.CreateDirectory(Path.GetDirectoryName(scriptMetadataPath)!);
		Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
		Directory.CreateDirectory(Path.GetDirectoryName(astPath)!);

		File.WriteAllLines(
			scriptMetadataPath,
			[
				"""{"fullName":"MyNamespace.MyClass","scriptGuid":"SCRIPTGUID-A","pk":"COLLECTION:1","assemblyGuid":"ASSEMBLYGUID"}""",
				"""{"fullName":"MyNamespace.MyClass","scriptGuid":"SCRIPTGUID-B","pk":"COLLECTION:2","assemblyGuid":"ASSEMBLYGUID"}"""
			]);
		File.WriteAllText(sourcePath, "namespace MyNamespace { public class MyClass {} }");
		File.WriteAllText(astPath, """{"FileName":"scripts/Assembly-CSharp/MyNamespace/MyClass.cs"}""");

		Options options = new()
		{
			InputPath = _testDirectory.Path,
			OutputPath = _testDirectory.Path,
			Quiet = true
		};

		Action act = () => new ScriptSourceIndexBuilder(options).Build();

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("Ambiguous script_metadata fullName entries detected*");
	}

	private static void WriteGzipText(string path, string contents)
	{
		using FileStream fileStream = File.Create(path);
		using GZipStream gzipStream = new(fileStream, CompressionLevel.Optimal);
		using StreamWriter writer = new(gzipStream, Encoding.UTF8);
		writer.WriteLine(contents);
	}
}
