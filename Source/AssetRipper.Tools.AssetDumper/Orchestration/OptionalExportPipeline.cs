using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Metrics;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Metadata;
using AssetRipper.Tools.AssetDumper.Exporters.Records;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Handles the execution of optional domain exports (scenes, bundles, scripts, metrics).
/// </summary>
public sealed class OptionalExportPipeline
{
	private readonly ExportContext _context;

	public OptionalExportPipeline(ExportContext context)
	{
		_context = context;
	}

	/// <summary>
	/// Executes optional export pipelines based on configuration.
	/// </summary>
	public void Execute()
	{
		if (_context.Options.ExportScenes)
		{
			ExportScenes();
		}

		if (_context.Options.ExportBundleMetadata)
		{
			ExportBundleMetadata();
		}

		if (_context.Options.ExportScriptMetadata)
		{
			ExportScriptMetadata();
		}

		if (_context.Options.ExportMetrics)
		{
			List<DomainExportResult> metricsResults = ExportMetrics();
			foreach (DomainExportResult result in metricsResults)
			{
				_context.AddResult(result);
			}
		}
	}

	private void ExportScenes()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting scene records...");
		}

		try
		{
			SceneRecordExporter exporter = new SceneRecordExporter(
				_context.Options,
				_context.CompressionKind,
				_context.EnableIndex);

			DomainExportResult result = exporter.ExportScenes(_context.GameData);
			_context.AddResult(result);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export scenes", ex);
			throw;
		}
	}

	private void ExportBundleMetadata()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting bundle metadata facts...");
		}

		try
		{
			BundleMetadataExporter exporter = new BundleMetadataExporter(
				_context.Options,
				_context.CompressionKind,
				_context.EnableIndex);

			DomainExportResult result = exporter.Export(_context.GameData);
			_context.AddResult(result);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export bundle metadata", ex);
			throw;
		}
	}

	private void ExportScriptMetadata()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting script metadata facts...");
		}

		try
		{
			ScriptRecordExporter exporter = new ScriptRecordExporter(
				_context.Options,
				_context.CompressionKind,
				_context.EnableIndex);

			DomainExportResult result = exporter.ExportScripts(_context.GameData);
			_context.AddResult(result);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export script metadata", ex);
			throw;
		}
	}

	/// <summary>
	/// Export optional metrics tables if enabled.
	/// Returns DomainExportResults for manifest registration.
	/// </summary>
	public List<DomainExportResult> ExportMetrics()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Collecting and exporting metrics...");
		}

		try
		{
			MetricsExporter exporter = new MetricsExporter(_context.Options);
			exporter.CollectMetrics(_context.GameData);
			return exporter.WriteMetricsWithResults();
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to export metrics", ex);
			throw;
		}
	}
}
