using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Core;

public class OptionsTests
{
	[Fact]
	public void RelationDefaults_ShouldIncludeScriptTypeMappings()
	{
		Options options = new Options
		{
			ExportDomains = "facts,relations"
		};

		options.ExportRelationDependencies.Should().BeTrue();
		options.ExportRelationHierarchy.Should().BeTrue();
		options.ExportRelationScriptTypeMapping.Should().BeTrue();
	}

	[Fact]
	public void RelationTables_DependencyAndHierarchyOnly_ShouldDisableMappings()
	{
		Options options = new Options
		{
			ExportDomains = "relations",
			RelationTables = "dependencies,hierarchy"
		};

		options.ExportRelationDependencies.Should().BeTrue();
		options.ExportRelationHierarchy.Should().BeTrue();
		options.ExportRelationScriptTypeMapping.Should().BeFalse();
	}

	[Fact]
	public void RelationTables_MappingsOnly_ShouldExportMappingsOnly()
	{
		Options options = new Options
		{
			ExportDomains = "relations",
			RelationTables = "mappings"
		};

		options.ExportRelationDependencies.Should().BeFalse();
		options.ExportRelationHierarchy.Should().BeFalse();
		options.ExportRelationScriptTypeMapping.Should().BeTrue();
	}

	[Fact]
	public void RelationTables_None_ShouldDisableAllRelationTables()
	{
		Options options = new Options
		{
			ExportDomains = "relations",
			RelationTables = "none"
		};

		options.ExportRelationDependencies.Should().BeFalse();
		options.ExportRelationHierarchy.Should().BeFalse();
		options.ExportRelationScriptTypeMapping.Should().BeFalse();
	}

	[Fact]
	public void RelationTables_ScriptTypeMappingAlias_ShouldBeRecognized()
	{
		Options options = new Options
		{
			ExportDomains = "relations",
			RelationTables = "script_type_mapping"
		};

		options.ExportRelationScriptTypeMapping.Should().BeTrue();
	}

	[Fact]
	public void CodeAnalysisMappings_ShouldStillEnableRelationScriptTypeMapping()
	{
		Options options = new Options
		{
			ExportDomains = "code-analysis",
			CodeAnalysisTables = "mappings",
			RelationTables = "none"
		};

		options.ExportRelationScriptTypeMapping.Should().BeTrue();
	}
}
