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
		if (_context.Options.ExportRelationHierarchy)
		{
			ExportBundleHierarchy();
		}

		if (_context.Options.ExportRelationDependencies)
		{
			ExportCollectionDependencies();
			ExportAssetDependencies();
		}

		if (_context.Options.ExportRelationScriptTypeMapping)
		{
			ExportScriptTypeMappings();
		}
	}

	private void ExportAssetDependencies()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting asset dependency relations...");
		}

		try
		{
			AssetDependencyExporter exporter = new AssetDependencyExporter(
				_context.Options,
				_context.CompressionKind,
				_context.EnableIndex);

			DomainExportResult result = exporter.Export(_context.GameData);
			_context.AddResult(result, ExportPipelineOwner.Relations);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export asset dependencies", ex);
			throw;
		}
	}

	private void ExportBundleHierarchy()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting bundle hierarchy relations...");
		}

		try
		{
			BundleHierarchyExporter exporter = new BundleHierarchyExporter(
				_context.Options,
				_context.CompressionKind);

			DomainExportResult result = exporter.Export(_context.GameData);
			_context.AddResult(result, ExportPipelineOwner.Relations);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export bundle hierarchy", ex);
			throw;
		}
	}

	private void ExportCollectionDependencies()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting collection dependency relations...");
		}

		try
		{
			CollectionDependencyExporter exporter = new CollectionDependencyExporter(
				_context.Options,
				_context.CompressionKind);

			DomainExportResult result = exporter.Export(_context.GameData);
			_context.AddResult(result, ExportPipelineOwner.Relations);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export collection dependencies", ex);
			throw;
		}
	}

	private void ExportScriptTypeMappings()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting script-type mapping relations...");
		}

		try
		{
			ScriptTypeMappingExporter exporter = new ScriptTypeMappingExporter(
				_context.Options,
				_context.CompressionKind,
				_context.EnableIndex);

			DomainExportResult result = exporter.ExportMappings(_context.GameData);
			_context.AddResult(result, ExportPipelineOwner.Relations);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export script-type mappings", ex);
			throw;
		}
	}
}
