using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Files;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Orchestration;
using System.Diagnostics;
using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Processors;

/// <summary>
/// Main entry point for processing Unity game assets and exporting data.
/// Coordinates loading, preview, and export operations.
/// </summary>
internal class AssetProcessor
{
	private readonly Options _options;
	private readonly FilterManager _filterManager;

	public AssetProcessor(Options options)
	{
		_options = options;
		_filterManager = new FilterManager(_options);
	}

	/// <summary>
	/// Main entry point for asset processing workflow.
	/// Loads game data, then either shows preview or executes export asynchronously.
	/// </summary>
	public async Task<int> ProcessAssetsAsync()
	{
		return await ExceptionHandler.ExecuteWithErrorHandlingAsync(async () =>
		{
			var totalStopwatch = Stopwatch.StartNew();

			try
			{
				if (_options.Verbose)
				{
					Logger.Info($"Input: {_options.InputPath} -> Output: {_options.OutputPath}");
				}

				// Load game data
				GameData gameData = LoadGameData();

				// Create output directory
				Directory.CreateDirectory(_options.OutputPath);

				// Preview mode
				if (_options.PreviewOnly)
				{
					ShowPreview(gameData);
					return (int)ErrorCode.Success;
				}

				// Execute export asynchronously
				return await ExecuteExportAsync(gameData);
			}
			finally
			{
				totalStopwatch.Stop();
				if (_options.Verbose)
				{
					Logger.Info($"Total processing time: {totalStopwatch.Elapsed}");
				}
			}
		}, _options.Verbose);
	}

	/// <summary>
	/// Loads and processes game data from input path.
	/// </summary>
	private GameData LoadGameData()
	{
		if (!_options.Silent)
		{
			Logger.Info("Loading game data...");
		}

		try
		{
			string[] inputPaths = { _options.InputPath };
			FullConfiguration settings = new FullConfiguration();
			AssetDumperExportHandler exportHandler = new AssetDumperExportHandler(settings);
			return exportHandler.LoadAndProcess(inputPaths, LocalFileSystem.Instance);
		}
		catch (Exception ex)
		{
			throw new GameDataLoadException(_options.InputPath, "Failed to load game data", ex);
		}
	}

	/// <summary>
	/// Shows preview information without performing actual export.
	/// </summary>
	private void ShowPreview(GameData gameData)
	{
		PreviewService previewService = new PreviewService(_options, _filterManager);
		previewService.ShowPreview(gameData);
	}

	/// <summary>
	/// Executes the export workflow using the orchestrator asynchronously.
	/// </summary>
	private async Task<int> ExecuteExportAsync(GameData gameData)
	{
		ExportOrchestrator orchestrator = new ExportOrchestrator(_options);
		return await orchestrator.ExecuteAsync(gameData);
	}
}

