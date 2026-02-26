using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Core;

public class ExportTableMatrixTests
{
	[Fact]
	public void CodeAnalysisMappings_ShouldSelectRelationOwnedScriptTypeMapping()
	{
		Options options = new Options
		{
			ExportDomains = "code-analysis",
			CodeAnalysisTables = "mappings",
			FactTables = "none",
			RelationTables = "none"
		};

		ExportTableSelection selection = options.ResolveExportTables();

		selection.IsTableSelected("relations/script_type_mapping").Should().BeTrue();
		selection.IsTableSelected("facts/assemblies").Should().BeTrue();
		selection.HasTablesForOwner(ExportPipelineOwner.Relations).Should().BeTrue();
		selection.HasTablesForOwner(ExportPipelineOwner.ScriptCode).Should().BeTrue();
		options.ExportScriptCodeAssociation.Should().BeTrue();
		options.ExportRelationScriptTypeMapping.Should().BeTrue();
	}

	[Fact]
	public void ExportDomainsAll_ShouldEnableAllDomainFlags()
	{
		Options options = new Options
		{
			ExportDomains = "all"
		};

		options.ExportFacts.Should().BeTrue();
		options.ExportRelations.Should().BeTrue();
		options.ExportScriptCodeAssociation.Should().BeTrue();
	}

	[Fact]
	public void ScriptTypeMapping_ShouldHaveSingleOwnerInMatrix()
	{
		bool hasOwner = ExportTableMatrix.TryGetOwner("relations/script_type_mapping", out ExportPipelineOwner owner);

		hasOwner.Should().BeTrue();
		owner.Should().Be(ExportPipelineOwner.Relations);
	}
}
