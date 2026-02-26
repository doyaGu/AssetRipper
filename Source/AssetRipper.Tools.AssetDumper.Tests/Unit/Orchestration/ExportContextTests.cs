using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Orchestration;
using System;
using System.IO;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Orchestration;

public class ExportContextTests
{
	[Fact]
	public void AddResult_WithWrongOwner_ShouldThrow()
	{
		ExportContext context = CreateContext();
		DomainExportResult result = new DomainExportResult(
			domain: "assets",
			tableId: "facts/assets",
			schemaPath: "Schemas/v2/facts/assets.schema.json");

		Action act = () => context.AddResult(result, ExportPipelineOwner.Relations);

		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void AddResult_WithDuplicateTable_ShouldThrow()
	{
		ExportContext context = CreateContext();
		DomainExportResult first = new DomainExportResult(
			domain: "assets",
			tableId: "facts/assets",
			schemaPath: "Schemas/v2/facts/assets.schema.json");
		DomainExportResult second = new DomainExportResult(
			domain: "assets",
			tableId: "facts/assets",
			schemaPath: "Schemas/v2/facts/assets.schema.json");

		context.AddResult(first, ExportPipelineOwner.FactsCore);
		Action act = () => context.AddResult(second, ExportPipelineOwner.FactsCore);

		act.Should().Throw<InvalidOperationException>();
	}

	private static ExportContext CreateContext()
	{
		Options options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = Path.Combine(Path.GetTempPath(), $"AssetDumperTests_{Guid.NewGuid():N}"),
			Quiet = true
		};

		return new ExportContext(
			options,
			options.ResolveExportTables(),
			null!,
			CompressionKind.None,
			enableIndex: false,
			indexGenerator: null);
	}
}
