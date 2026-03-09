using System.Reflection;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Helpers;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Core;

public sealed class ProgramValidationTests : IDisposable
{
	private readonly DisposableDirectory _testDirectory = TestPathHelper.CreateDisposableDirectory(nameof(ProgramValidationTests));

	public void Dispose()
	{
		_testDirectory.Dispose();
	}

	[Fact]
	public void ValidateOptions_WhenInputAndOutputPointToSupportedDumpBackedExport_ShouldSucceed()
	{
		File.WriteAllText(Path.Combine(_testDirectory.Path, "manifest.json"), "{}");
		Directory.CreateDirectory(Path.Combine(_testDirectory.Path, "facts", "assemblies"));

		Options options = new()
		{
			InputPath = _testDirectory.Path,
			OutputPath = _testDirectory.Path,
			ExportDomains = "code-analysis",
			CodeAnalysisTables = "facts/assemblies",
			Quiet = true
		};

		int result = InvokeValidateOptions(options);

		result.Should().Be(0);
	}

	private static int InvokeValidateOptions(Options options)
	{
		MethodInfo? method = typeof(AssetRipper.Tools.AssetDumper.Program)
			.GetMethod("ValidateOptions", BindingFlags.NonPublic | BindingFlags.Static);

		method.Should().NotBeNull();
		object? result = method!.Invoke(null, [options]);
		return result.Should().BeOfType<int>().Subject;
	}
}
