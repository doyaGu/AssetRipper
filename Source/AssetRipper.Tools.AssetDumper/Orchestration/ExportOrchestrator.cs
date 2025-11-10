using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Models;
using System.Diagnostics;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Processors;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Orchestrates the entire export process including Facts, Relations, and optional domains.
/// </summary>
public sealed class ExportOrchestrator
{
	private readonly Options _options;
	private readonly IncrementalManager _incrementalManager;
	private readonly ValidationService _validationService;

	public ExportOrchestrator(Options options)
	{
		_options = options;
		_incrementalManager = new IncrementalManager(options);
		_validationService = new ValidationService(options);
	}

	/// <summary>
	/// Executes the complete export workflow.
	/// </summary>
	public int Execute(GameData gameData)
	{
		var totalStopwatch = Stopwatch.StartNew();

		// Resolve compression and indexing settings
		CompressionKind compressionKind = ResolveCompressionKind();
		bool enableIndex = ResolveIndexingSetting(compressionKind);
		KeyIndexGenerator? indexGenerator = enableIndex ? new KeyIndexGenerator(_options) : null;

		// Create export context
		ExportContext context = new ExportContext(_options, gameData, compressionKind, enableIndex, indexGenerator);

		// Load existing manifest for incremental processing
		Manifest? existingManifest = _incrementalManager.TryLoadExistingManifest();
		if (existingManifest != null)
		{
			_incrementalManager.LoadExistingIndexes(existingManifest, context);
		}

		// Ensure output directory structure
		EnsureExportScaffolding();

		try
		{
			// Execute export pipelines
			ExecuteFactsExport(context, existingManifest);
			ExecuteRelationsExport(context, existingManifest);
			ExecuteOptionalExports(context, existingManifest);
			GenerateManifest(context);
			ProcessScripts(gameData);

			totalStopwatch.Stop();

			// Validation and diagnostics
			_validationService.LogExportDiagnostics(context.DomainResults);
			_validationService.ValidateShardOutputs(context.DomainResults);

			if (!_validationService.ValidateSchemas(context.DomainResults))
			{
				return 4; // Schema validation failure
			}

			LogCompletionSummary(totalStopwatch.Elapsed, context);
			_validationService.LogProcessingSummary(totalStopwatch.Elapsed);

			return 0;
		}
		catch (DirectoryNotFoundException ex)
		{
			Logger.Error($"Directory not found: {ex.Message}");
			return 2;
		}
		catch (UnauthorizedAccessException ex)
		{
			Logger.Error($"Access denied: {ex.Message}");
			return 3;
		}
		catch (Exception ex)
		{
			Logger.Error("Processing failed", ex);
			return 1;
		}
		finally
		{
			totalStopwatch.Stop();
		}
	}

	private void ExecuteFactsExport(ExportContext context, Manifest? existingManifest)
	{
		if (!_options.ExportFacts)
		{
			return;
		}

		// Check if we can reuse existing facts
		bool canReuseFacts = existingManifest != null
			&& _incrementalManager.ManifestContainsTables(existingManifest,
				"facts/collections",
				"facts/assets",
				"facts/types");

		if (canReuseFacts)
		{
			if (!_options.Silent)
			{
				Logger.Info("Reusing existing facts export (incremental).");
			}

			ReuseManifestData(existingManifest!, context, "facts/collections", "facts/assets", "facts/types");
		}
		else
		{
			FactsExportPipeline pipeline = new FactsExportPipeline(context);
			pipeline.Execute();
		}
	}

	private void ExecuteRelationsExport(ExportContext context, Manifest? existingManifest)
	{
		if (!_options.ExportRelations)
		{
			return;
		}

		// Check if we can reuse existing relations
		bool canReuseRelations = existingManifest != null
			&& _incrementalManager.ManifestContainsTables(existingManifest, 
				"relations/bundle_hierarchy",
				"relations/collection_dependencies",
				"relations/asset_dependencies");

		if (canReuseRelations)
		{
			if (!_options.Silent)
			{
				Logger.Info("Reusing existing relations export (incremental).");
			}

			ReuseManifestData(existingManifest!, context, 
				"relations/bundle_hierarchy",
				"relations/collection_dependencies",
				"relations/asset_dependencies");
		}
		else
		{
			RelationsExportPipeline pipeline = new RelationsExportPipeline(context);
			pipeline.Execute();
		}
	}

	private void ExecuteOptionalExports(ExportContext context, Manifest? existingManifest)
	{
		// Check for incremental reuse of optional exports
		bool canReuseBundleMetadata = _options.ExportBundleMetadata
			&& existingManifest != null
			&& _incrementalManager.ManifestContainsTables(existingManifest, "facts/bundles");

		bool canReuseScenes = _options.ExportScenes
			&& existingManifest != null
			&& _incrementalManager.ManifestContainsTables(existingManifest, "facts/scenes");

		bool canReuseScriptMetadata = _options.ExportScriptMetadata
			&& existingManifest != null
			&& _incrementalManager.ManifestContainsTables(existingManifest, "facts/scripts");

		// Reuse what we can
		if (canReuseBundleMetadata)
		{
			if (!_options.Silent)
			{
				Logger.Info("Reusing existing bundle metadata (incremental).");
			}
			ReuseManifestData(existingManifest!, context, "facts/bundles");
		}

		if (canReuseScenes)
		{
			if (!_options.Silent)
			{
				Logger.Info("Reusing existing scenes export (incremental).");
			}
			ReuseManifestData(existingManifest!, context, "facts/scenes");
		}

		if (canReuseScriptMetadata)
		{
			if (!_options.Silent)
			{
				Logger.Info("Reusing existing script metadata (incremental).");
			}
			ReuseManifestData(existingManifest!, context, "facts/scripts");
		}

		// Export what needs to be regenerated
		bool needsBundleMetadata = _options.ExportBundleMetadata && !canReuseBundleMetadata;
		bool needsScenes = _options.ExportScenes && !canReuseScenes;
		bool needsScriptMetadata = _options.ExportScriptMetadata && !canReuseScriptMetadata;
		bool needsMetrics = _options.ExportMetrics;

		if (needsBundleMetadata || needsScenes || needsScriptMetadata || needsMetrics)
		{
			OptionalExportPipeline pipeline = new OptionalExportPipeline(
				context,
				executeBundleMetadata: needsBundleMetadata,
				executeScenes: needsScenes,
				executeScriptMetadata: needsScriptMetadata,
				executeMetrics: needsMetrics);

			pipeline.Execute();
		}
	}

	private void GenerateManifest(ExportContext context)
	{
		if (!_options.ExportManifest)
		{
			return;
		}

		if (!_options.Silent)
		{
			Logger.Info("Generating manifest...");
		}

		ManifestGenerator generator = new ManifestGenerator(_options);
		generator.GenerateManifest(
			context.GameData,
			context.DomainResults,
			context.IndexRefs.Count > 0 ? context.IndexRefs : null);
	}

	private void ProcessScripts(GameData gameData)
	{
		if (!_options.ExportAssemblies && !_options.ExportScripts && !_options.GenerateAst)
		{
			return;
		}

		if (gameData.AssemblyManager?.IsSet != true)
		{
			if (!_options.Silent)
			{
				Logger.Warning("No assemblies found for script processing");
			}
			return;
		}

		if (!_options.Silent)
		{
			Logger.Info("Processing scripts and assemblies...");
		}

		FilterManager filterManager = new FilterManager(_options);
		ScriptProcessor processor = new ScriptProcessor(_options, filterManager);
		processor.ProcessScripts(gameData);
	}

	private void ReuseManifestData(Manifest manifest, ExportContext context, params string[] tableIds)
	{
		foreach (string tableId in tableIds)
		{
			DomainExportResult? result = _incrementalManager.CreateResultFromManifest(manifest, tableId);
			if (result != null)
			{
				context.AddResult(result);
			}
		}
	}

	private CompressionKind ResolveCompressionKind()
	{
		string? compression = _options.Compression;
		if (string.IsNullOrWhiteSpace(compression))
		{
			return CompressionKind.None;
		}

		switch (compression.Trim().ToLowerInvariant())
		{
			case "none":
				return CompressionKind.None;
			case "zstd":
				return CompressionKind.Zstd;
			case "zstd-seekable":
			case "zstd_seekable":
			case "zstdseekable":
				return CompressionKind.ZstdSeekable;
			case "gzip":
			case "gz":
				return CompressionKind.Gzip;
			default:
				Logger.Warning($"Unknown compression codec '{compression}'. Falling back to no compression.");
				return CompressionKind.None;
		}
	}

	private bool ResolveIndexingSetting(CompressionKind compressionKind)
	{
		bool wantsIndex = _options.EnableIndex;

		if (wantsIndex && !_options.ExportIndexes)
		{
			if (!_options.Silent)
			{
				Logger.Warning("Key index generation requested but index outputs are disabled (--indexes=false). Skipping index creation.");
			}
			return false;
		}

		// Index generation now supports all compression modes
		// - For uncompressed: uses byte offsets for direct seeking
		// - For compressed: uses line numbers for sequential scanning after decompression
		return wantsIndex;
	}

	private void EnsureExportScaffolding()
	{
		if (_options.ExportFacts)
		{
			OutputPathHelper.EnsureSubdirectory(_options.OutputPath, OutputPathHelper.FactsDirectoryName);
		}

		if (_options.ExportRelations)
		{
			OutputPathHelper.EnsureSubdirectory(_options.OutputPath, OutputPathHelper.RelationsDirectoryName);
		}

		if (_options.ExportIndexes)
		{
			OutputPathHelper.EnsureSubdirectory(_options.OutputPath, OutputPathHelper.IndexesDirectoryName);
		}

		if (_options.ExportMetrics)
		{
			OutputPathHelper.EnsureSubdirectory(_options.OutputPath, OutputPathHelper.MetricsDirectoryName);
		}
	}

	private void LogCompletionSummary(TimeSpan elapsed, ExportContext context)
	{
		if (_options.Silent)
		{
			return;
		}

		Logger.Info($"Processing completed successfully in {elapsed:mm\\:ss\\.fff}");
		Logger.Info($"Generated {context.AllShards.Count} shards with {context.AllShards.Sum(s => s.Records)} total records");
	}
}
