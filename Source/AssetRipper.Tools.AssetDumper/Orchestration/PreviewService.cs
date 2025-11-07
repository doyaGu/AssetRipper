using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Processing;
using MonoScriptAsset = AssetRipper.SourceGenerated.Classes.ClassID_115.IMonoScript;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Processors;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Provides preview information about what would be exported without performing actual export.
/// </summary>
internal sealed class PreviewService
{
	private readonly Options _options;
	private readonly FilterManager _filterManager;

	public PreviewService(Options options, FilterManager filterManager)
	{
		_options = options;
		_filterManager = filterManager;
	}

	/// <summary>
	/// Displays preview information for the export operation.
	/// </summary>
	public void ShowPreview(GameData gameData)
	{
		if (!_options.Silent)
		{
			Logger.Info("=== PREVIEW MODE ===");
		}

		if (_options.ExportScenes)
		{
			PreviewScenes(gameData);
		}

		if (_options.ExportBundleMetadata)
		{
			PreviewBundleMetadata(gameData);
		}

		if (gameData.AssemblyManager?.IsSet == true)
		{
			if (_options.ExportScriptMetadata)
			{
				PreviewScriptMetadata(gameData);
			}

			if (_options.ExportAssemblies || _options.ExportScripts || _options.GenerateAst)
			{
				ScriptProcessor scriptProcessor = new ScriptProcessor(_options, _filterManager);
				scriptProcessor.PreviewScriptProcessing(gameData);
			}
		}

		if (!_options.Silent)
		{
			Logger.Info("=== END PREVIEW ===");
		}
	}

	private void PreviewScenes(GameData gameData)
	{
		int sceneCount = gameData.GameBundle.Scenes.Count();
		if (!_options.Silent)
		{
			Logger.Info($"Scenes: {sceneCount} would be exported");
		}
	}

	private void PreviewBundleMetadata(GameData gameData)
	{
		try
		{
			Bundle? root = gameData.GameBundle;
			if (root is null)
			{
				if (!_options.Silent)
				{
					Logger.Info("Bundle metadata: no bundle hierarchy detected");
				}
				return;
			}

			int bundleCount = 0;
			int collectionCount = 0;
			int resourceCount = 0;
			Queue<Bundle> queue = new();
			queue.Enqueue(root);

			while (queue.Count > 0)
			{
				Bundle current = queue.Dequeue();
				bundleCount++;
				collectionCount += current.Collections.Count;
				resourceCount += current.Resources.Count;

				foreach (Bundle child in current.Bundles)
				{
					queue.Enqueue(child);
				}
			}

			if (!_options.Silent)
			{
				Logger.Info($"Bundle metadata: {bundleCount} bundle nodes covering {collectionCount} collections and {resourceCount} resources would be exported");
			}
		}
		catch (Exception ex)
		{
			Logger.Warning($"Error during bundle metadata preview: {ex.Message}");
		}
	}

	private void PreviewScriptMetadata(GameData gameData)
	{
		try
		{
			int scriptCount = 0;
			int collectionCount = 0;
			HashSet<string> assemblyNames = new HashSet<string>();
			HashSet<string> namespaces = new HashSet<string>();

			foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
			{
				List<MonoScriptAsset> scripts = collection.OfType<MonoScriptAsset>().ToList();
				if (scripts.Count > 0)
				{
					collectionCount++;
					scriptCount += scripts.Count;

					// Limit to 100 scripts to avoid excessive processing in preview
					foreach (MonoScriptAsset script in scripts.Take(100))
					{
						assemblyNames.Add(script.GetAssemblyNameFixed());
						string ns = script.Namespace.String;
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
}
