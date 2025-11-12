using CommandLine;
using CommandLine.Text;

namespace AssetRipper.Tools.AssetDumper.Core;

public class Options
{
	// ========================================
	// Core I/O Options
	// ========================================
	
	[Option('i', "input", Required = true,
		HelpText = "Input Unity game directory or data folder")]
	public string InputPath { get; set; } = "";

	[Option('o', "output", Required = true,
		HelpText = "Output directory for exported data")]
	public string OutputPath { get; set; } = "";

	[Option("preset", Required = false, Default = null,
		HelpText = "Configuration preset: fast, full, analysis, minimal, debug")]
	public string? Preset { get; set; }

	// ========================================
	// Export Domains (what to export)
	// ========================================

	[Option("export", Required = false, Default = "facts,relations",
		HelpText = "Comma-separated export domains: facts, relations, scripts, assemblies, code-analysis")]
	public string ExportDomains { get; set; } = "facts,relations";

	[Option("facts", Required = false, Default = "assets,collections,scenes,scripts,bundles,types",
		HelpText = "Fact tables to export (comma-separated): assets, collections, scenes, scripts, bundles, types, all, none")]
	public string FactTables { get; set; } = "assets,collections,scenes,scripts,bundles,types";

	[Option("relations", Required = false, Default = "dependencies,hierarchy",
		HelpText = "Relation tables to export (comma-separated): dependencies, hierarchy, all, none")]
	public string RelationTables { get; set; } = "dependencies,hierarchy";

	[Option("code-analysis", Required = false, Default = "types,members,inheritance,mappings",
		HelpText = "Code analysis tables: types, members, inheritance, mappings, dependencies, sources, all, none")]
	public string CodeAnalysisTables { get; set; } = "types,members,inheritance,mappings";

	// ========================================
	// Script & Code Export
	// ========================================

	[Option("decompile", Required = false, Default = false,
		HelpText = "Decompile C# assemblies to source code")]
	public bool DecompileScripts { get; set; } = false;

	[Option("generate-ast", Required = false, Default = false,
		HelpText = "Generate abstract syntax trees from decompiled code")]
	public bool GenerateAst { get; set; } = false;

	[Option("export-assemblies", Required = false, Default = false,
		HelpText = "Export raw assembly DLL files")]
	public bool ExportAssemblyFiles { get; set; } = false;

	// ========================================
	// Filtering Options
	// ========================================

	[Option("include", Required = false, Default = null,
		HelpText = "Regex pattern to include assets/files (applied first)")]
	public string? IncludePattern { get; set; }

	[Option("exclude", Required = false, Default = null,
		HelpText = "Regex pattern to exclude assets/files (applied after include)")]
	public string? ExcludePattern { get; set; }

	[Option("scenes", Required = false, Default = null,
		HelpText = "Regex pattern to filter scenes (null = all scenes)")]
	public string? SceneFilter { get; set; }

	[Option("assemblies", Required = false, Default = null,
		HelpText = "Regex pattern to filter assemblies (null = all assemblies)")]
	public string? AssemblyFilter { get; set; }

	[Option("unity-only", Required = false, Default = false,
		HelpText = "Process only Unity game code (exclude framework/plugins)")]
	public bool UnityProjectOnly { get; set; } = false;

	[Option("skip-builtin", Required = false, Default = true,
		HelpText = "Skip built-in Unity resources and dependencies")]
	public bool SkipBuiltinResources { get; set; } = true;

	[Option("skip-generated", Required = false, Default = true,
		HelpText = "Skip auto-generated files (AssemblyInfo.cs, etc.)")]
	public bool SkipGeneratedFiles { get; set; } = true;

	// ========================================
	// Output Format & Quality
	// ========================================

	[Option("compression", Required = false, Default = "none",
		HelpText = "Compression format: none, gzip, zstd")]
	public string Compression { get; set; } = "none";

	[Option("shard-size", Required = false, Default = 100000,
		HelpText = "Maximum records per shard (0 = no sharding)")]
	public int ShardSize { get; set; } = 100000;

	[Option("enable-index", Required = false, Default = false,
		HelpText = "Generate searchable key indexes (.idx files)")]
	public bool EnableIndexing { get; set; } = false;

	[Option("validate-schema", Required = false, Default = false,
		HelpText = "Validate output against JSON schemas")]
	public bool ValidateSchemas { get; set; } = false;

	[Option("include-metadata", Required = false, Default = false,
		HelpText = "Include extended metadata in exports")]
	public bool IncludeExtendedMetadata { get; set; } = false;

	// ========================================
	// Performance & Optimization
	// ========================================

	[Option("incremental", Required = false, Default = true,
		HelpText = "Enable incremental processing (skip unchanged outputs)")]
	public bool IncrementalMode { get; set; } = true;

	[Option("parallel", Required = false, Default = 0,
		HelpText = "Parallelism degree (0 = auto, 1 = sequential, N = N threads)")]
	public int ParallelThreads { get; set; } = 0;

	[Option("sample-rate", Required = false, Default = 1.0,
		HelpText = "Asset sampling rate for testing (0.0-1.0, 1.0 = process all)")]
	public double SampleRate { get; set; } = 1.0;

	[Option("timeout", Required = false, Default = 30,
		HelpText = "Timeout in seconds for processing individual assets")]
	public int TimeoutSeconds { get; set; } = 30;

	[Option("max-size", Required = false, Default = 0,
		HelpText = "Maximum asset size to process in bytes (0 = unlimited)")]
	public long MaxAssetSizeBytes { get; set; } = 0;

	// ========================================
	// Logging & Debugging
	// ========================================

	[Option('v', "verbose", Required = false, Default = false,
		HelpText = "Enable verbose logging")]
	public bool Verbose { get; set; } = false;

	[Option('q', "quiet", Required = false, Default = false,
		HelpText = "Suppress all non-error output")]
	public bool Quiet { get; set; } = false;

	[Option("trace-dependencies", Required = false, Default = false,
		HelpText = "Trace dependency resolution (implies --verbose)")]
	public bool TraceDependencies { get; set; } = false;

	[Option("dry-run", Required = false, Default = false,
		HelpText = "Analyze without writing outputs")]
	public bool DryRun { get; set; } = false;

	// ========================================
	// Advanced Options
	// ========================================

	[Option("min-lines", Required = false, Default = 3,
		HelpText = "Minimum code lines to process for AST generation")]
	public int MinCodeLines { get; set; } = 3;

	[Option("output-folders", Required = false, Default = null,
		HelpText = "Custom output folder structure (JSON config)")]
	public string? OutputFolderConfig { get; set; }

	// ========================================
	// Computed Properties (for backward compatibility)
	// ========================================

	public bool ExportFacts => ExportDomains.Contains("facts", StringComparison.OrdinalIgnoreCase);
	public bool ExportRelations => ExportDomains.Contains("relations", StringComparison.OrdinalIgnoreCase);
	public bool ExportScripts => DecompileScripts;
	public bool ExportAssemblies => ExportAssemblyFiles;
	public bool Silent => Quiet;
	public bool EnableIndex => EnableIndexing;
	public int ParallelDegree => ParallelThreads;
	public int FileTimeoutSeconds => TimeoutSeconds;
	public bool ValidateSchema => ValidateSchemas;
	public bool SkipAutoGenerated => SkipGeneratedFiles;
	public bool SkipBuiltinDeps => SkipBuiltinResources;
	public bool IncrementalProcessing => IncrementalMode;
	public bool PreviewOnly => DryRun;
	public long MaxFileSizeBytes => MaxAssetSizeBytes;
	public int MinimumLines => MinCodeLines;
	public bool IncludeAssetMetadata => IncludeExtendedMetadata;

	// Granular fact/relation flags (computed from tables)
	public bool ExportCollections => FactTables.Contains("collections", StringComparison.OrdinalIgnoreCase) || FactTables.Contains("all", StringComparison.OrdinalIgnoreCase);
	public bool ExportScenes => FactTables.Contains("scenes", StringComparison.OrdinalIgnoreCase) || FactTables.Contains("all", StringComparison.OrdinalIgnoreCase);
	public bool ExportScriptMetadata => FactTables.Contains("scripts", StringComparison.OrdinalIgnoreCase) || FactTables.Contains("all", StringComparison.OrdinalIgnoreCase);
	public bool ExportBundleMetadata => FactTables.Contains("bundles", StringComparison.OrdinalIgnoreCase) || FactTables.Contains("all", StringComparison.OrdinalIgnoreCase);
	public bool ExportManifest => !FactTables.Contains("none", StringComparison.OrdinalIgnoreCase);
	public bool ExportIndexes => EnableIndexing;
	public bool ExportMetrics => false; // Deprecated

	// Code analysis flags
	public bool ExportScriptCodeAssociation => CodeAnalysisTables != "none";
	public bool ExportTypeDefinitions => CodeAnalysisTables.Contains("types", StringComparison.OrdinalIgnoreCase) || CodeAnalysisTables.Contains("all", StringComparison.OrdinalIgnoreCase);
	public bool ExportTypeMembers => CodeAnalysisTables.Contains("members", StringComparison.OrdinalIgnoreCase) || CodeAnalysisTables.Contains("all", StringComparison.OrdinalIgnoreCase);
	public bool LinkSourceFiles => CodeAnalysisTables.Contains("sources", StringComparison.OrdinalIgnoreCase) || CodeAnalysisTables.Contains("all", StringComparison.OrdinalIgnoreCase);

	// Dependency filtering
	public bool MinimalDeps => false; // Deprecated
	public bool SkipSelfRefs => true;
	public bool MinimalOutput => false;

	// Removed/deprecated
	public bool CompactJson => false;
	public bool IgnoreNullValues => true;
	public string AstOutputFolder => "ast";
	public string ScenesOutputFolder => "scenes";

	// ========================================
	// Configuration Presets
	// ========================================

	/// <summary>
	/// Apply a configuration preset to simplify common use cases.
	/// </summary>
	public void ApplyPreset(ConfigPreset preset)
	{
		switch (preset)
		{
			case ConfigPreset.Fast:
				// Fast iteration for development/testing
				ExportDomains = "facts";
				FactTables = "assets,scripts";
				CodeAnalysisTables = "none";
				Verbose = true;
				IncrementalMode = true;
				ParallelThreads = 0; // Auto
				Compression = "none";
				ValidateSchemas = false;
				EnableIndexing = false;
				break;

			case ConfigPreset.Full:
				// Complete export with all features
				ExportDomains = "facts,relations,code-analysis";
				FactTables = "all";
				RelationTables = "all";
				CodeAnalysisTables = "all";
				DecompileScripts = true;
				GenerateAst = true;
				Verbose = false;
				Quiet = false;
				IncrementalMode = false; // Full export
				ParallelThreads = 0; // Auto
				Compression = "zstd";
				ValidateSchemas = true;
				EnableIndexing = true;
				IncludeExtendedMetadata = true;
				break;

			case ConfigPreset.Analysis:
				// Optimized for code analysis
				ExportDomains = "facts,code-analysis";
				FactTables = "scripts,types";
				CodeAnalysisTables = "all";
				DecompileScripts = true;
				GenerateAst = true;
				ExportAssemblyFiles = true;
				UnityProjectOnly = true; // Focus on game code
				SkipGeneratedFiles = true;
				Verbose = false;
				EnableIndexing = true;
				Compression = "gzip";
				break;

			case ConfigPreset.Minimal:
				// Minimal output for structure analysis
				ExportDomains = "facts";
				FactTables = "assets,collections";
				CodeAnalysisTables = "none";
				Quiet = true;
				IncrementalMode = true;
				Compression = "zstd";
				SkipBuiltinResources = true;
				EnableIndexing = false;
				break;

			case ConfigPreset.Debug:
				// Detailed debugging
				ExportDomains = "facts,relations,code-analysis";
				FactTables = "all";
				RelationTables = "all";
				CodeAnalysisTables = "all";
				Verbose = true;
				TraceDependencies = true;
				IncrementalMode = false;
				ParallelThreads = 1; // Sequential
				Compression = "none";
				ValidateSchemas = true;
				EnableIndexing = true;
				IncludeExtendedMetadata = true;
				TimeoutSeconds = 120;
				break;
		}
	}

	/// <summary>
	/// Create options with a preset configuration.
	/// </summary>
	public static Options CreateWithPreset(ConfigPreset preset, string inputPath, string outputPath)
	{
		var options = new Options
		{
			InputPath = inputPath,
			OutputPath = outputPath
		};
		options.ApplyPreset(preset);
		return options;
	}

	/// <summary>
	/// Parse preset from string name.
	/// </summary>
	public static ConfigPreset? ParsePreset(string? presetName)
	{
		if (string.IsNullOrWhiteSpace(presetName))
			return null;

		return presetName.ToLowerInvariant() switch
		{
			"fast" => ConfigPreset.Fast,
			"full" => ConfigPreset.Full,
			"analysis" => ConfigPreset.Analysis,
			"minimal" => ConfigPreset.Minimal,
			"debug" => ConfigPreset.Debug,
			_ => null
		};
	}

	[Usage(ApplicationAlias = "AssetDumper")]
	public static IEnumerable<Example> Examples
	{
		get
		{
			return new List<Example>
			{
				new Example("Full export with preset", new Options {
					InputPath = @"C:\Games\MyUnityGame",
					OutputPath = @"C:\Output",
					Preset = "full"
				}),
				new Example("Fast testing export", new Options {
					InputPath = @"C:\Games\MyUnityGame",
					OutputPath = @"C:\Output",
					Preset = "fast"
				}),
				new Example("Code analysis only", new Options {
					InputPath = @"C:\Games\MyUnityGame",
					OutputPath = @"C:\Output",
					Preset = "analysis"
				}),
				new Example("Custom export domains", new Options {
					InputPath = @"C:\Games\MyUnityGame",
					OutputPath = @"C:\Output",
					ExportDomains = "facts,code-analysis",
					FactTables = "assets,scripts",
					CodeAnalysisTables = "types,members"
				}),
				new Example("Filtered export", new Options {
					InputPath = @"C:\Games\MyUnityGame",
					OutputPath = @"C:\Output",
					AssemblyFilter = "Assembly-CSharp",
					SceneFilter = "MainMenu|Level.*",
					UnityProjectOnly = true
				}),
				new Example("Compressed output with indexing", new Options {
					InputPath = @"C:\Games\MyUnityGame",
					OutputPath = @"C:\Output",
					Compression = "zstd",
					EnableIndexing = true,
					ShardSize = 50000
				}),
				new Example("Debug mode", new Options {
					InputPath = @"C:\Games\MyUnityGame",
					OutputPath = @"C:\Output",
					Verbose = true,
					TraceDependencies = true,
					ParallelThreads = 1
				}),
				new Example("Dry run analysis", new Options {
					InputPath = @"C:\Games\MyUnityGame",
					OutputPath = @"C:\Output",
					DryRun = true,
					Verbose = true
				})
			};
		}
	}
}
