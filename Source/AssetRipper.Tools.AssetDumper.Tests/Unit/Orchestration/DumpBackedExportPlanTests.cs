using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Orchestration;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Helpers;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Orchestration;

public sealed class DumpBackedExportPlanTests : IDisposable
{
	private readonly DisposableDirectory _testDirectory = TestPathHelper.CreateDisposableDirectory(nameof(DumpBackedExportPlanTests));

	public void Dispose()
	{
		_testDirectory.Dispose();
	}

	[Fact]
	public void CanHandle_WhenOnlyAssembliesAreSelected_ShouldRequireOnlyAssembliesDirectory()
	{
		Directory.CreateDirectory(Path.Combine(_testDirectory.Path, "facts", "assemblies"));

		Options options = new()
		{
			InputPath = _testDirectory.Path,
			OutputPath = _testDirectory.Path,
			ExportDomains = "code-analysis",
			CodeAnalysisTables = "facts/assemblies",
			Quiet = true
		};

		ExportTableSelection selection = options.ResolveExportTables();

		DumpBackedExportPlan.CanHandle(options, selection).Should().BeTrue();
	}

	[Fact]
	public void CanHandle_WhenOnlyScriptSourcesAreSelected_ShouldRequireScriptMetadataScriptsAndAst()
	{
		Options options = new()
		{
			InputPath = _testDirectory.Path,
			OutputPath = _testDirectory.Path,
			ExportDomains = "code-analysis",
			CodeAnalysisTables = "facts/script_sources",
			Quiet = true
		};

		ExportTableSelection selection = options.ResolveExportTables();
		DumpBackedExportPlan.CanHandle(options, selection).Should().BeFalse();

		Directory.CreateDirectory(Path.Combine(_testDirectory.Path, "facts", "script_metadata"));
		Directory.CreateDirectory(Path.Combine(_testDirectory.Path, "scripts"));
		Directory.CreateDirectory(Path.Combine(_testDirectory.Path, "ast"));

		DumpBackedExportPlan.CanHandle(options, selection).Should().BeTrue();
	}
}
