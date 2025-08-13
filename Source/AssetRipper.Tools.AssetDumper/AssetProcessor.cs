using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Processing;
using System.Diagnostics;

namespace AssetRipper.Tools.AssetDumper;

internal class AssetProcessor
{
	private readonly Options _options;
	private readonly FilterManager _filterManager;

	public AssetProcessor(Options options)
	{
		_options = options;
		_filterManager = new FilterManager(_options);
	}

	public int ProcessAssets()
	{
		var totalStopwatch = Stopwatch.StartNew();

		try
		{
			if (_options.Verbose)
			{
				Logger.Info($"Input: {_options.InputPath} -> Output: {_options.OutputPath}");
			}

			// Load game data
			if (!_options.Silent)
			{
				Logger.Info("Loading game data...");
			}

			string[] inputPaths = { _options.InputPath };
			var settings = new LibraryConfiguration();
			var exportHandler = new ExportHandler(settings);
			var gameData = exportHandler.LoadAndProcess(inputPaths);

			// Create output directory
			Directory.CreateDirectory(_options.OutputPath);

			// Preview mode
			if (_options.PreviewOnly)
			{
				PreviewProcessing(gameData);
				return 0;
			}

			if (_options.ExportBundles)
			{
				if (!_options.Silent)
				{
					Logger.Info("Exporting asset bundles...");
				}
				var bundleStopwatch = Stopwatch.StartNew();
				var bundleExporter = new BundleInfoExporter(_options);
				bundleExporter.ExportBundleInfo(gameData);
				bundleStopwatch.Stop();
				if (_options.Verbose)
				{
					Logger.Info($"Bundle export completed in {bundleStopwatch.Elapsed:mm\\:ss\\.fff}");
				}
			}

			if (_options.ExportCollections)
			{
				if (!_options.Silent)
				{
					Logger.Info("Exporting asset collections...");
				}
				var collectionStopwatch = Stopwatch.StartNew();
				var collectionExporter = new CollectionInfoExporter(_options);
				collectionExporter.ExportCollectionInfo(gameData);
				collectionStopwatch.Stop();
				if (_options.Verbose)
				{
					Logger.Info($"Collection export completed in {collectionStopwatch.Elapsed:mm\\:ss\\.fff}");
				}
			}

			// Process asset types
			if (_options.ExportScenes)
			{
				if (!_options.Silent)
				{
					Logger.Info("Processing scenes...");
				}
				var sceneStopwatch = Stopwatch.StartNew();
				var sceneProcessor = new SceneProcessor(_options);
				sceneProcessor.ExportScenes(gameData);
				sceneStopwatch.Stop();

				if (_options.Verbose)
				{
					Logger.Info($"Scene processing completed in {sceneStopwatch.Elapsed:mm\\:ss\\.fff}");
				}
			}

			// Process scripts, assemblies, and script metadata
			if (_options.ExportAssemblies || _options.ExportScripts || _options.GenerateAst || _options.ExportScriptMetadata)
			{
				if (gameData.AssemblyManager?.IsSet == true)
				{
					// Script metadata export
					if (_options.ExportScriptMetadata)
					{
						if (!_options.Silent)
						{
							Logger.Info("Processing script metadata...");
						}
						var metadataStopwatch = Stopwatch.StartNew();
						var scriptMetadataDumper = new ScriptMetadataDumper(_options);
						scriptMetadataDumper.ExportScriptMetadata(gameData);
						metadataStopwatch.Stop();

						if (_options.Verbose)
						{
							Logger.Info($"Script metadata processing completed in {metadataStopwatch.Elapsed:mm\\:ss\\.fff}");
						}
					}

					// Scripts and assemblies processing
					if (_options.ExportAssemblies || _options.ExportScripts || _options.GenerateAst)
					{
						if (!_options.Silent)
						{
							Logger.Info("Processing scripts and assemblies...");
						}
						var scriptStopwatch = Stopwatch.StartNew();
						var scriptProcessor = new ScriptProcessor(_options, _filterManager);
						scriptProcessor.ProcessScripts(gameData);
						scriptStopwatch.Stop();

						if (_options.Verbose)
						{
							Logger.Info($"Script processing completed in {scriptStopwatch.Elapsed:mm\\:ss\\.fff}");
						}
					}
				}
				else
				{
					if (!_options.Silent)
					{
						Logger.Warning("No assemblies found for script processing");
					}
				}
			}

			totalStopwatch.Stop();

			if (!_options.Silent)
			{
				Logger.Info($"Processing completed successfully in {totalStopwatch.Elapsed:mm\\:ss\\.fff}");
			}

			if (_options.Verbose)
			{
				LogProcessingSummary(totalStopwatch.Elapsed);
			}

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

	private void PreviewProcessing(GameData gameData)
	{
		if (!_options.Silent)
		{
			Logger.Info("=== PREVIEW MODE ===");
		}

		if (_options.ExportScenes)
		{
			var sceneCount = gameData.GameBundle.Scenes.Count();
			if (!_options.Silent)
			{
				Logger.Info($"Scenes: {sceneCount} would be exported");
			}
		}

		if (gameData.AssemblyManager?.IsSet == true)
		{
			// Preview script metadata export
			if (_options.ExportScriptMetadata)
			{
				PreviewScriptMetadata(gameData);
			}

			// Preview script processing
			if (_options.ExportAssemblies || _options.ExportScripts || _options.GenerateAst)
			{
				var scriptProcessor = new ScriptProcessor(_options, _filterManager);
				scriptProcessor.PreviewScriptProcessing(gameData);
			}
		}

		if (!_options.Silent)
		{
			Logger.Info("=== END PREVIEW ===");
		}
	}

	private void PreviewScriptMetadata(GameData gameData)
	{
		try
		{
			// Count MonoScript assets across all collections
			var scriptCount = 0;
			var collectionCount = 0;
			var assemblyNames = new HashSet<string>();
			var namespaces = new HashSet<string>();

			foreach (var collection in gameData.GameBundle.FetchAssetCollections())
			{
				var scripts = collection.OfType<AssetRipper.SourceGenerated.Classes.ClassID_115.IMonoScript>().ToList();
				if (scripts.Count > 0)
				{
					collectionCount++;
					scriptCount += scripts.Count;

					foreach (var script in scripts.Take(100)) // Limit to avoid excessive processing in preview
					{
						assemblyNames.Add(script.GetAssemblyNameFixed());
						var ns = script.Namespace.String;
						if (!string.IsNullOrEmpty(ns))
						{
							namespaces.Add(ns);
						}
					}
				}
			}

			if (!_options.Silent)
			{
				Logger.Info($"Script metadata: {scriptCount} MonoScript assets across {collectionCount} collections would be exported");
			}

			if (_options.Verbose)
			{
				Logger.Info($"  - {assemblyNames.Count} unique assemblies");
				Logger.Info($"  - {namespaces.Count} unique namespaces");

				if (assemblyNames.Count > 0)
				{
					Logger.Info($"  - Sample assemblies: {string.Join(", ", assemblyNames.Take(5))}");
					if (assemblyNames.Count > 5)
					{
						Logger.Info($"    ... and {assemblyNames.Count - 5} more");
					}
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Warning($"Error during script metadata preview: {ex.Message}");
		}
	}

	private void LogProcessingSummary(TimeSpan totalTime)
	{
		Logger.Info("=== Processing Summary ===");
		Logger.Info($"Total Time: {totalTime:mm\\:ss\\.fff}");
		Logger.Info($"Output Directory: {_options.OutputPath}");

		// Log output directory contents
		if (Directory.Exists(_options.OutputPath))
		{
			var directories = Directory.GetDirectories(_options.OutputPath);
			var files = Directory.GetFiles(_options.OutputPath);

			Logger.Info($"Output contains: {directories.Length} directories, {files.Length} files");

			foreach (var dir in directories)
			{
				var dirName = Path.GetFileName(dir);
				var dirSize = GetDirectorySize(dir);
				var fileCount = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
				Logger.Info($"  {dirName}/: {fileCount} files, {FormatBytes(dirSize)}");
			}
		}

		Logger.Info("===========================");
	}

	private static long GetDirectorySize(string directory)
	{
		try
		{
			return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
				.Sum(file => new FileInfo(file).Length);
		}
		catch
		{
			return 0;
		}
	}

	private static string FormatBytes(long bytes)
	{
		string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
		int counter = 0;
		double number = bytes;

		while (number >= 1024 && counter < suffixes.Length - 1)
		{
			number /= 1024;
			counter++;
		}

		return $"{number:N1} {suffixes[counter]}";
	}
}
