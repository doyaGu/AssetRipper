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

		// Export assets
		ExportAssetFacts();
	}

	private void ExportCollectionFacts()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting collection facts...");
		}

		try
		{
			CollectionFactsExporter exporter = new CollectionFactsExporter(
				_context.Options,
				_context.CompressionKind);

			DomainExportResult result = exporter.ExportCollections(_context.GameData);
			_context.AddResult(result);
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
			AssetFactsExporter assetExporter = new AssetFactsExporter(
				_context.Options,
				_context.CompressionKind,
				_context.EnableIndex);

			DomainExportResult assetResult = assetExporter.ExportAssets(_context.GameData);
			_context.AddResult(assetResult);

			// Export type facts based on collected type dictionary
			if (!_context.Options.Silent)
			{
				Logger.Info("Exporting type facts...");
			}

			TypeFactsExporter typeExporter = new TypeFactsExporter(_context.Options);
			DomainExportResult typeResult = typeExporter.ExportTypes(assetExporter.TypeDictionary.Entries);
			_context.AddResult(typeResult);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export asset facts", ex);
			throw;
		}
	}
}
