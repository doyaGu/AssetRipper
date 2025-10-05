using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.Processing;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper;

internal class BundleInfoExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public BundleInfoExporter(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = JsonHelper.CreateSettings(_options);
	}

	public void ExportBundleInfo(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting bundle information...");

		string bundleOutputPath = Path.Combine(_options.OutputPath, "Bundles");
		Directory.CreateDirectory(bundleOutputPath);

		ExportGameBundle(gameData.GameBundle, bundleOutputPath);
		ExportChildBundlesRecursively(gameData.GameBundle, bundleOutputPath);
	}

	private void ExportGameBundle(GameBundle gameBundle, string outputPath)
	{
		var overview = new Dictionary<string, object>
		{
			["name"] = gameBundle.Name,
			["totalCollections"] = gameBundle.FetchAssetCollections().Count(),
			["totalAssets"] = gameBundle.FetchAssets().Count(),
			["totalScenes"] = gameBundle.Scenes.Count(),
			["directChildBundles"] = gameBundle.Bundles.Count,
			["totalChildBundles"] = CountAllChildBundles(gameBundle),
			["directCollections"] = gameBundle.Collections.Count,
			["directResources"] = gameBundle.Resources.Count,
			["bundleStructure"] = CreateBundleStructure(gameBundle)
		};

		string overviewFile = Path.Combine(outputPath, "GameBundle.json");
		WriteJsonFile(overview, overviewFile);
	}

	private void ExportChildBundlesRecursively(Bundle parentBundle, string outputPath)
	{
		foreach (var childBundle in parentBundle.Bundles)
		{
			try
			{
				ExportSingleBundle(childBundle, outputPath);
				ExportChildBundlesRecursively(childBundle, outputPath);
			}
			catch (Exception ex)
			{
				Logger.Error(LogCategory.Export, $"Error exporting bundle {childBundle.Name}: {ex.Message}");
			}
		}
	}

	private void ExportSingleBundle(Bundle bundle, string outputPath)
	{
        var bundleInfo = new Dictionary<string, object>
        {
            ["name"] = bundle.Name,
            ["parentBundle"] = bundle.Parent?.Name ?? string.Empty,
            ["bundleType"] = bundle.GetType().Name,

            // Collections info
            ["collections"] = CreateCollectionsSummary(bundle.Collections),

            // Resources info  
            ["resources"] = CreateResourcesSummary(bundle.Resources),

            // Child bundles info
            ["childBundles"] = CreateChildBundlesSummary(bundle.Bundles),

            // Failed files info
            ["failedFiles"] = CreateFailedFilesSummary(bundle.FailedFiles)
        };

		string bundleName = ExportHelper.SanitizeFileName(bundle.Name);
		string bundleFile = Path.Combine(outputPath, $"{bundleName}.json");
		WriteJsonFile(bundleInfo, bundleFile);
	}

	private object CreateBundleStructure(Bundle bundle)
	{
		return new Dictionary<string, object>
		{
			["name"] = bundle.Name,
			["type"] = bundle.GetType().Name,
			["collectionsCount"] = bundle.Collections.Count,
			["resourcesCount"] = bundle.Resources.Count,
			["childBundles"] = bundle.Bundles.Select(CreateBundleStructure).ToList()
		};
	}

	private List<Dictionary<string, object>> CreateCollectionsSummary(IReadOnlyList<AssetCollection> collections)
	{
		return collections.Select(collection => new Dictionary<string, object>
		{
			["name"] = collection.Name,
			["filePath"] = collection.FilePath,
			["version"] = collection.Version.ToString(),
			["platform"] = collection.Platform.ToString(),
			["assetCount"] = collection.Assets.Count,
			["dependencyCount"] = collection.Dependencies.Count,
			["isScene"] = collection.IsScene,
			["sceneName"] = collection.Scene?.Name ?? string.Empty
		}).ToList();
	}

	private List<Dictionary<string, object>> CreateResourcesSummary(IReadOnlyList<ResourceFile> resources)
	{
		return resources.Select(resource => new Dictionary<string, object>
		{
			["name"] = resource.Name,
			["filePath"] = resource.FilePath,
			["size"] = resource.Stream.Length
		}).ToList();
	}

	private List<Dictionary<string, object>> CreateChildBundlesSummary(IReadOnlyList<Bundle> childBundles)
	{
		return childBundles.Select(child => new Dictionary<string, object>
		{
			["name"] = child.Name,
			["type"] = child.GetType().Name,
			["collectionsCount"] = child.Collections.Count,
			["resourcesCount"] = child.Resources.Count,
			["childBundlesCount"] = child.Bundles.Count
		}).ToList();
	}

	private List<Dictionary<string, object>> CreateFailedFilesSummary(IReadOnlyList<FailedFile> failedFiles)
	{
		return failedFiles.Select(failed => new Dictionary<string, object>
		{
			["name"] = failed.Name,
			["filePath"] = failed.FilePath,
			["error"] = failed.StackTrace?.Split('\n')[0] ?? "Unknown error"
		}).ToList();
	}

	private static int CountAllChildBundles(Bundle bundle)
	{
		int count = bundle.Bundles.Count;
		foreach (var childBundle in bundle.Bundles)
		{
			count += CountAllChildBundles(childBundle);
		}
		return count;
	}

	private void WriteJsonFile(object data, string filePath)
	{
		ExportHelper.WriteJsonFile(data, filePath, _jsonSettings);
	}
}
