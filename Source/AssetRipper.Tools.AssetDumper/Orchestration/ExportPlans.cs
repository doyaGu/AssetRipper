using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

internal abstract class ExportPlan
{
	protected ExportPlan(Options options, ExportTableSelection tableSelection)
	{
		Options = options ?? throw new ArgumentNullException(nameof(options));
		TableSelection = tableSelection ?? throw new ArgumentNullException(nameof(tableSelection));
	}

	public Options Options { get; }
	public ExportTableSelection TableSelection { get; }
}

internal sealed class ImportBackedExportPlan : ExportPlan
{
	public ImportBackedExportPlan(Options options, ExportTableSelection tableSelection)
		: base(options, tableSelection)
	{
	}
}

internal sealed class DumpBackedExportPlan : ExportPlan
{
	private static readonly string[] SupportedTables =
	[
		"facts/assemblies",
		"facts/script_sources"
	];

	public DumpBackedExportPlan(Options options, ExportTableSelection tableSelection)
		: base(options, tableSelection)
	{
	}

	public static bool CanHandle(Options options, ExportTableSelection tableSelection)
	{
		if (options is null)
		{
			throw new ArgumentNullException(nameof(options));
		}

		if (tableSelection is null)
		{
			throw new ArgumentNullException(nameof(tableSelection));
		}

		if (options.PreviewOnly || tableSelection.SelectedTables.Count == 0)
		{
			return false;
		}

		if (options.DecompileScripts || options.GenerateAst || options.ExportAssemblyFiles)
		{
			return false;
		}

		if (tableSelection.SelectedTables.Any(tableId => !SupportedTables.Contains(tableId, StringComparer.OrdinalIgnoreCase)))
		{
			return false;
		}

		return RequiredInputsExist(options.OutputPath);
	}

	public static bool RequiredInputsExist(string outputPath)
	{
		return Directory.Exists(Path.Combine(outputPath, "facts", "script_metadata")) &&
			Directory.Exists(Path.Combine(outputPath, "facts", "assemblies")) &&
			Directory.Exists(Path.Combine(outputPath, "scripts")) &&
			Directory.Exists(Path.Combine(outputPath, "ast"));
	}
}

internal static class ExportPlanSelector
{
	public static ExportPlan Select(Options options)
	{
		ExportTableSelection tableSelection = options.ResolveExportTables();
		return DumpBackedExportPlan.CanHandle(options, tableSelection)
			? new DumpBackedExportPlan(options, tableSelection)
			: new ImportBackedExportPlan(options, tableSelection);
	}
}
