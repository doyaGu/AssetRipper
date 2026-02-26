using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Facts;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Handles the execution of the Facts export pipeline.
/// </summary>
public sealed class FactsExportPipeline
{
	private readonly ExportContext _context;

	public FactsExportPipeline(ExportContext context)
	{
		_context = context;
	}

	/// <summary>
	/// Executes the Facts domain export pipeline.
	/// </summary>
	public void Execute()
	{
		// Export collections
		if (_context.Options.ExportCollections)
		{
			ExportCollectionFacts();
		}

		// Export assets/types share the same extraction pass.
		if (_context.Options.ExportAssetFacts || _context.Options.ExportTypeFacts)
		{
			ExportAssetFacts();
		}
	}

	private void ExportCollectionFacts()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting collection facts...");
		}

		try
		{
			CollectionExporter exporter = new CollectionExporter(
				_context.Options,
				_context.CompressionKind);

			DomainExportResult result = exporter.ExportCollections(_context.GameData);
			_context.AddResult(result, ExportPipelineOwner.FactsCore);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export collection facts", ex);
			throw;
		}
	}

	private void ExportAssetFacts()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting asset facts...");
		}

		try
		{
			AssetExporter assetExporter = new AssetExporter(
				_context.Options,
				_context.CompressionKind,
				_context.EnableIndex);

			DomainExportResult assetResult = assetExporter.ExportAssets(_context.GameData);
			bool includeAssetResult = _context.Options.ExportAssetFacts || _context.Options.ExportTypeFacts;
			if (!_context.Options.ExportAssetFacts && _context.Options.ExportTypeFacts && !_context.Options.Silent)
			{
				Logger.Warning("facts/types requires facts/assets extraction; including facts/assets in the export set.");
			}

			if (includeAssetResult)
			{
				_context.AddResult(assetResult, ExportPipelineOwner.FactsCore);
			}

			// Export type facts based on collected type dictionary
			if (_context.Options.ExportTypeFacts && !_context.Options.Silent)
			{
				Logger.Info("Exporting type facts...");
			}

			if (_context.Options.ExportTypeFacts)
			{
				TypeExporter typeExporter = new TypeExporter(_context.Options);
				DomainExportResult typeResult = typeExporter.ExportTypes(assetExporter.TypeDictionary.Entries);
				_context.AddResult(typeResult, ExportPipelineOwner.FactsCore);
			}
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export asset facts", ex);
			throw;
		}
	}
}
