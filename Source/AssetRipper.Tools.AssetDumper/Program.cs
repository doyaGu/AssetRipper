using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Constants;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Processors;
using CommandLine;
using System.Text.RegularExpressions;

namespace AssetRipper.Tools.AssetDumper;

internal static class Program
{
	private static readonly ConsoleLogger ConsoleLogger = new(false);

	public static int Main(string[] args)
	{
		Logger.Add(ConsoleLogger);

		return Parser.Default.ParseArguments<Options>(args)
			.MapResult(
				options =>
				{
					ConfigureLogging(options);
					Logger.LogSystemInformation("AssetDumper");
					return RunWithOptions(options);
				},
				errors => HandleParseErrors(errors)
			);
	}

	private static int RunWithOptions(Options options)
	{
		try
		{
			int validationResult = ValidateOptions(options);
			if (validationResult != 0)
				return validationResult;

			AssetProcessor processor = new AssetProcessor(options);
			return processor.ProcessAssets();
		}
		catch (Exception ex)
		{
			Logger.Error("Unexpected error occurred", ex);
			return 1;
		}
	}

	private static void ConfigureLogging(Options options)
	{
		if (options.TraceDependencies)
		{
			options.Verbose = true;
		}

		if (options.Silent && options.Verbose)
		{
			Logger.Warning("Both --silent and --verbose specified. Verbose mode takes precedence.");
			options.Silent = false;
		}

		Logger.Clear();
		Logger.AllowVerbose = options.Verbose;
		Logger.Add(new OptionFilteredLogger(options, ConsoleLogger));

		if (options.Verbose)
		{
			Logger.Verbose("Verbose logging enabled");
		}
	}

	private static int ValidateOptions(Options options)
	{
		try
		{
			// Validate input path
			if (!Directory.Exists(options.InputPath) && !File.Exists(options.InputPath))
			{
				Logger.Error($"Input path does not exist: {options.InputPath}");
				return 2;
			}

			// Prevent using AssetDumper output as input
			if (Directory.Exists(options.InputPath))
			{
				if (IsAssetDumperOutput(options.InputPath))
				{
					Logger.Error($"Input path appears to be an AssetDumper export directory (contains manifest.json).");
					Logger.Error("AssetDumper requires a Unity game directory as input, not a previous export result.");
					Logger.Error("Please provide the original Unity game directory (e.g., GameName_Data).");
					return 2;
				}
			}

			// Validate sample rate
			if (options.SampleRate <= 0 || options.SampleRate > 1.0)
			{
				Logger.Error($"Sample rate must be between 0 and 1.0, got: {options.SampleRate}");
				return 4;
			}

			// Validate parallel degree
			if (options.ParallelDegree < 0)
			{
				Logger.Error($"Parallel degree must be >= 0, got: {options.ParallelDegree}");
				return 4;
			}

			// Validate regex patterns
			if (!ValidateRegexPattern(options.AssemblyFilter, "assembly filter"))
				return 4;

			if (!ValidateRegexPattern(options.ExcludePattern, "exclude pattern"))
				return 4;

			if (!ValidateRegexPattern(options.SceneFilter, "scene filter"))
				return 4;

			// Validate numeric options
			if (options.MaxFileSizeBytes < 0)
			{
				Logger.Error("Max file size cannot be negative");
				return 4;
			}

			if (options.MinimumLines < 0)
			{
				Logger.Error("Minimum lines cannot be negative");
				return 4;
			}

			// Create and validate output directory
			try
			{
				Directory.CreateDirectory(options.OutputPath);
			}
			catch (Exception ex)
			{
				Logger.Error($"Cannot create output directory: {ex.Message}");
				return 3;
			}

			// Log validation success
			if (options.Verbose)
			{
				Logger.Info("Options validation completed successfully");
				LogConfigurationSummary(options);
			}
			else if (!options.Silent)
			{
				Logger.Info($"Input: {options.InputPath} -> Output: {options.OutputPath}");
			}

			return 0;
		}
		catch (Exception ex)
		{
			Logger.Error($"Validation error: {ex.Message}");
			return 1;
		}
	}

	private static void LogConfigurationSummary(Options options)
	{
		Logger.Info("=== Configuration Summary ===");
		Logger.Info($"Input Path: {options.InputPath}");
		Logger.Info($"Output Path: {options.OutputPath}");
		Logger.Info($"Export Scripts: {options.ExportScripts}");
		Logger.Info($"Generate AST: {options.GenerateAst}");
		Logger.Info($"Export Scenes: {options.ExportScenes}");
		Logger.Info($"Export Assemblies: {options.ExportAssemblies}");
		Logger.Info($"Export Script Metadata Facts: {options.ExportScriptMetadata}");
		Logger.Info($"Export Facts: {options.ExportFacts}");
		Logger.Info($"Export Relations: {options.ExportRelations}");
		Logger.Info($"Export Manifest: {options.ExportManifest}");
		Logger.Info($"Export Indexes: {options.ExportIndexes}");
		Logger.Info($"Export Metrics: {options.ExportMetrics}");
		Logger.Info($"Enable Index: {options.EnableIndex}");
		Logger.Info($"Trace Dependencies: {options.TraceDependencies}");
		Logger.Info($"Sample Rate: {options.SampleRate:P0}");
		Logger.Info($"Unity Project Only: {options.UnityProjectOnly}");
		Logger.Info($"Skip Auto-Generated: {options.SkipAutoGenerated}");
		Logger.Info($"Incremental Processing: {options.IncrementalProcessing}");
		Logger.Info($"Parallel Degree: {(options.ParallelDegree == 0 ? "Auto" : options.ParallelDegree.ToString())}");

		if (!string.IsNullOrEmpty(options.AssemblyFilter))
			Logger.Info($"Assembly Filter: {options.AssemblyFilter}");
		if (!string.IsNullOrEmpty(options.ExcludePattern))
			Logger.Info($"Exclude Pattern: {options.ExcludePattern}");
		if (!string.IsNullOrEmpty(options.SceneFilter))
			Logger.Info($"Scene Filter: {options.SceneFilter}");

		Logger.Info("==============================");
	}

	private static bool ValidateRegexPattern(string? pattern, string patternName)
	{
		if (string.IsNullOrEmpty(pattern))
			return true;

		try
		{
			Regex _ = new Regex(pattern, RegexOptions.IgnoreCase);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error($"Invalid {patternName} regex pattern: {ex.Message}");
			return false;
		}
	}

	private static bool IsAssetDumperOutput(string directoryPath)
	{
		// Check for manifest.json - the primary indicator of AssetDumper output
		string manifestPath = Path.Combine(directoryPath, "manifest.json");
		if (File.Exists(manifestPath))
		{
			return true;
		}

		// Check for typical AssetDumper output directories
		// If multiple characteristic directories exist, it's likely an export
		string[] exportDirs = { "facts", "relations", "schema", "indexes", "metrics" };
		int foundCount = exportDirs.Count(dir => Directory.Exists(Path.Combine(directoryPath, dir)));
		
		// If 3 or more characteristic directories found, likely an export
		if (foundCount >= 3)
		{
			return true;
		}

		return false;
	}

	private static int HandleParseErrors(IEnumerable<CommandLine.Error> errors)
	{
		Logger.Error("Invalid command line arguments. Use --help for usage information.");

		// Log specific errors (limit to prevent spam)
		List<CommandLine.Error> errorList = errors.Take(ValidationConstants.MaxErrorsToShow).ToList();
		foreach (CommandLine.Error error in errorList)
		{
			Logger.Error($"Parse error: {error}");
		}

		if (errors.Count() > ValidationConstants.MaxErrorsToShow)
		{
			Logger.Error($"... and {errors.Count() - ValidationConstants.MaxErrorsToShow} more errors");
		}

		return 1;
	}

	private sealed class OptionFilteredLogger : ILogger
	{
		private readonly Options _options;
		private readonly ILogger _inner;

		public OptionFilteredLogger(Options options, ILogger inner)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));
			_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		}

		public void BlankLine(int numLines)
		{
			if (!_options.Silent)
			{
				_inner.BlankLine(numLines);
			}
		}

		public void Log(LogType type, LogCategory category, string message)
		{
			if (_options.Silent)
			{
				if (type == LogType.Info || type == LogType.Verbose || type == LogType.Debug)
				{
					return;
				}
			}

			if (!_options.Verbose && type == LogType.Verbose)
			{
				return;
			}

			_inner.Log(type, category, message);
		}
	}
}
