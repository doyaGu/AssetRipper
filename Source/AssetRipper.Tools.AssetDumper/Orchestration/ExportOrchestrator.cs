using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Models;
using System.Diagnostics;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Processors;
using AssetRipper.Tools.AssetDumper.Models.Common;
using AssetRipper.Tools.AssetDumper.Generators;
using AssetRipper.Tools.AssetDumper.Exporters.Facts;
using AssetRipper.Tools.AssetDumper.Exporters.Relations;

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
	/// Executes the complete export workflow asynchronously.
	/// </summary>
	public async Task<int> ExecuteAsync(GameData gameData)
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
			await ExecuteFactsExportAsync(context, existingManifest);
			await ExecuteRelationsExportAsync(context, existingManifest);
			ExecuteOptionalExports(context, existingManifest);
			// Scripts must be decompiled before we can export script source links or AST-dependent metadata.
			ProcessScripts(gameData);
			ExecuteScriptCodeExport(context, existingManifest);
			GenerateManifest(context);

			totalStopwatch.Stop();

			// Validation and diagnostics
			_validationService.LogExportDiagnostics(context.DomainResults);
			_validationService.ValidateShardOutputs(context.DomainResults);

			if (!await _validationService.ValidateSchemasAsync(context.DomainResults))
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

	private async Task ExecuteFactsExportAsync(ExportContext context, Manifest? existingManifest)
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
			int resultCountBefore = context.DomainResults.Count;
			FactsExportPipeline pipeline = new FactsExportPipeline(context);
			pipeline.Execute();

			// Validate newly exported domains
			await ValidateExportedDomainsAsync(context, resultCountBefore);
		}
	}

	private async Task ExecuteRelationsExportAsync(ExportContext context, Manifest? existingManifest)
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
			int resultCountBefore = context.DomainResults.Count;
			RelationsExportPipeline pipeline = new RelationsExportPipeline(context);
			pipeline.Execute();

			// Validate newly exported domains
			await ValidateExportedDomainsAsync(context, resultCountBefore);
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

	private void ExecuteScriptCodeExport(ExportContext context, Manifest? existingManifest)
	{
		if (!_options.ExportScriptCodeAssociation)
		{
			return;
		}

		// Phase A: Core tables (always required)
		bool canReuseCoreData = existingManifest != null
			&& _incrementalManager.ManifestContainsTables(existingManifest, 
				"facts/assemblies", 
				"facts/type_definitions",
				"relations/script_type_mapping");

		// Phase B: Enhanced relationship tables
		bool canReuseEnhancedData = existingManifest != null
			&& _incrementalManager.ManifestContainsTables(existingManifest,
				"relations/assembly_dependencies",
				"relations/type_inheritance");

		// Phase C: Optional detailed member data
		bool canReuseMemberData = _options.ExportTypeMembers 
			&& existingManifest != null 
			&& _incrementalManager.ManifestContainsTables(existingManifest, "facts/type_members");

		// Source linking (optional)
		bool canReuseSourceData = _options.LinkSourceFiles
			&& existingManifest != null
			&& _incrementalManager.ManifestContainsTables(existingManifest, "facts/script_sources");

		// Determine what can be reused vs needs re-export
		bool needsFullExport = !canReuseCoreData;

		if (needsFullExport)
		{
			// Full export required - core data missing or invalid
			if (!_options.Silent)
			{
				Logger.Info("Exporting script-code associations (full export)...");
			}

			ScriptCodeExportPipeline pipeline = new ScriptCodeExportPipeline(context);
			pipeline.Execute();
		}
		else
		{
			// Incremental reuse with selective re-export
			if (!_options.Silent)
			{
				Logger.Info("Reusing script-code association data (incremental)...");
			}

			// Reuse Phase A: Core data
			ReuseManifestData(existingManifest!, context, 
				"facts/assemblies",
				"facts/type_definitions",
				"relations/script_type_mapping");

			// Reuse or re-export Phase B: Enhanced relationships
			if (canReuseEnhancedData)
			{
				if (!_options.Silent)
				{
					Logger.Verbose(LogCategory.Export, "Reusing assembly dependencies and type inheritance data.");
				}
				ReuseManifestData(existingManifest!, context,
					"relations/assembly_dependencies",
					"relations/type_inheritance");
			}
			else
			{
				// Re-export Phase B data only
				if (!_options.Silent)
				{
					Logger.Info("Re-exporting assembly dependencies and type inheritance (changed)...");
				}
				ExportPhaseB(context);
			}

			// Reuse or re-export Phase C: Type members
			if (_options.ExportTypeMembers)
			{
				if (canReuseMemberData)
				{
					if (!_options.Silent)
					{
						Logger.Verbose(LogCategory.Export, "Reusing type member data.");
					}
					ReuseManifestData(existingManifest!, context, "facts/type_members");
				}
				else
				{
					if (!_options.Silent)
					{
						Logger.Info("Re-exporting type members (changed)...");
					}
					ExportPhaseC(context);
				}
			}

			// Reuse or re-export source linking
			if (_options.LinkSourceFiles)
			{
				if (canReuseSourceData)
				{
					if (!_options.Silent)
					{
						Logger.Verbose(LogCategory.Export, "Reusing script source data.");
					}
					ReuseManifestData(existingManifest!, context, "facts/script_sources");
				}
				else
				{
					if (!_options.Silent)
					{
						Logger.Info("Re-exporting script sources (changed)...");
					}
					ExportSourceLinking(context);
				}
			}
		}
	}

	/// <summary>
	/// Exports Phase B: Enhanced relationship data (assembly dependencies, type inheritance).
	/// </summary>
	private void ExportPhaseB(ExportContext context)
	{
		AssemblyDependencyExporter depExporter = new AssemblyDependencyExporter(
			_options,
			context.CompressionKind,
			context.EnableIndex);
		DomainExportResult depResult = depExporter.ExportDependencies(context.GameData);
		context.AddResult(depResult);

		TypeInheritanceExporter inhExporter = new TypeInheritanceExporter(
			_options,
			context.CompressionKind,
			context.EnableIndex);
		DomainExportResult inhResult = inhExporter.ExportInheritance(context.GameData);
		context.AddResult(inhResult);
	}

	/// <summary>
	/// Exports Phase C: Type member data (V2 schema with enhanced metadata).
	/// </summary>
	private void ExportPhaseC(ExportContext context)
	{
		TypeMemberExporter exporter = new TypeMemberExporter(
			_options,
			context.CompressionKind,
			context.EnableIndex);
		DomainExportResult result = exporter.ExportMembers(context.GameData);
		context.AddResult(result);
	}

	/// <summary>
	/// Exports source file linking data.
	/// </summary>
	private void ExportSourceLinking(ExportContext context)
	{
		ScriptSourceExporter exporter = new ScriptSourceExporter(
			_options,
			context.CompressionKind,
			context.EnableIndex);
		DomainExportResult result = exporter.ExportSources(context.GameData);
		context.AddResult(result);
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
		// Script processing is required for assembly export, decompilation, source linking, and AST generation.
		if (!_options.ExportAssemblies && !_options.ExportScripts && !_options.GenerateAst && !_options.LinkSourceFiles)
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

	/// <summary>
	/// Validates newly exported domains using domain-level validation.
	/// </summary>
	/// <param name="context">Export context containing domain results</param>
	/// <param name="startIndex">Index to start validation from (validates only new domains)</param>
	private async Task ValidateExportedDomainsAsync(ExportContext context, int startIndex)
	{
		if (!_options.ValidateSchemas)
		{
			return;
		}

		// Validate only newly added domains
		for (int i = startIndex; i < context.DomainResults.Count; i++)
		{
			var domainResult = context.DomainResults[i];

			try
			{
				// Use ValidationService for domain validation (now properly async)
				var summary = await _validationService.ValidateDomainAsync(domainResult);

				if (summary is not null && summary.ErrorCount > 0 && !_options.ContinueOnError)
				{
					throw new Exception($"Domain validation failed for {domainResult.TableId} with {summary.ErrorCount} errors");
				}
			}
			catch (Exception ex)
			{
				Logger.Error(LogCategory.Export, $"Domain validation error for {domainResult.TableId}: {ex.Message}");

				if (!_options.ContinueOnError)
				{
					throw;
				}
			}
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
