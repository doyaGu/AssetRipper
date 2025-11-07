using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Relations;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Handles the execution of the Relations export pipeline.
/// </summary>
public sealed class RelationsExportPipeline
{
	private readonly ExportContext _context;

	public RelationsExportPipeline(ExportContext context)
	{
		_context = context;
	}

	/// <summary>
	/// Executes the Relations domain export pipeline.
	/// </summary>
	public void Execute()
	{
		ExportAssetDependencies();
	}

	private void ExportAssetDependencies()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting asset dependency relations...");
		}

		try
		{
			AssetDependencyRelationsExporter exporter = new AssetDependencyRelationsExporter(
				_context.Options,
				_context.CompressionKind,
				_context.EnableIndex);

			DomainExportResult result = exporter.Export(_context.GameData);
			_context.AddResult(result);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export asset dependencies", ex);
			throw;
		}
	}
}
