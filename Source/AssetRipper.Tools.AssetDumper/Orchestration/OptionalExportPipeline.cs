using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Metrics;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models.Common;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Exporters.Facts;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Handles the execution of optional domain exports (scenes, bundles, scripts, metrics).
/// </summary>
public sealed class OptionalExportPipeline
{
	private readonly ExportContext _context;
	private readonly bool _executeBundleMetadata;
	private readonly bool _executeScenes;
	private readonly bool _executeScriptMetadata;
	private readonly bool _executeMetrics;

	public OptionalExportPipeline(ExportContext context)
		: this(context, executeBundleMetadata: true, executeScenes: true, executeScriptMetadata: true, executeMetrics: true)
	{
	}

	public OptionalExportPipeline(
		ExportContext context,
		bool executeBundleMetadata,
		bool executeScenes,
		bool executeScriptMetadata,
		bool executeMetrics)
	{
		_context = context;
		_executeBundleMetadata = executeBundleMetadata;
		_executeScenes = executeScenes;
		_executeScriptMetadata = executeScriptMetadata;
		_executeMetrics = executeMetrics;
	}

	/// <summary>
	/// Executes optional export pipelines based on configuration and incremental flags.
	/// </summary>
	public void Execute()
	{
		if (_context.Options.ExportScenes && _executeScenes)
		{
			ExportScenes();
		}

		if (_context.Options.ExportBundleMetadata && _executeBundleMetadata)
		{
			ExportBundleMetadata();
		}

		if (_context.Options.ExportScriptMetadata && _executeScriptMetadata)
		{
			ExportScriptMetadata();
		}

		if (_context.Options.ExportMetrics && _executeMetrics)
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
			SceneExporter exporter = new SceneExporter(
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
			BundleExporter exporter = new BundleExporter(
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
			ScriptMetadataExporter exporter = new ScriptMetadataExporter(
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
