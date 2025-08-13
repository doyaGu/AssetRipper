using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper;

internal class CollectionInfoExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public CollectionInfoExporter(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = JsonHelper.CreateSettings(_options);
	}

	public void ExportCollectionInfo(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting collection information...");

		string collectionOutputPath = Path.Combine(_options.OutputPath, "Collections");
		Directory.CreateDirectory(collectionOutputPath);

		var allCollections = gameData.GameBundle.FetchAssetCollections().ToList();
		Logger.Info(LogCategory.Export, $"Processing {allCollections.Count} collections");

		ExportCollectionsOverview(allCollections, collectionOutputPath);
		ExportDetailedCollections(allCollections, collectionOutputPath);
	}

	private void ExportCollectionsOverview(List<AssetCollection> collections, string outputPath)
	{
		var overview = new Dictionary<string, object>
		{
			["totalCollections"] = collections.Count,
			["collectionsByType"] = collections.GroupBy(c => c.GetType().Name)
				.ToDictionary(g => g.Key, g => g.Count()),
			["collectionsByPlatform"] = collections.GroupBy(c => c.Platform.ToString())
				.ToDictionary(g => g.Key, g => g.Count()),
			["collectionsByVersion"] = collections.GroupBy(c => c.Version.ToString())
				.ToDictionary(g => g.Key, g => g.Count()),
			["sceneCollections"] = collections.Count(c => c.IsScene),
			["totalAssets"] = collections.Sum(c => c.Assets.Count),
			["largestCollections"] = collections
				.OrderByDescending(c => c.Assets.Count)
				.Take(10)
				.Select(c => new Dictionary<string, object>
				{
					["name"] = c.Name,
					["assetCount"] = c.Assets.Count,
					["type"] = c.GetType().Name,
					["isScene"] = c.IsScene
				}).ToList(),
			["collections"] = collections.Select(CreateCollectionSummary).ToList()
		};

		string overviewFile = Path.Combine(outputPath, "CollectionsOverview.json");
		WriteJsonFile(overview, overviewFile);
	}

	private void ExportDetailedCollections(List<AssetCollection> collections, string outputPath)
	{
		foreach (AssetCollection collection in collections)
		{
			try
			{
				ExportSingleCollectionDetails(collection, outputPath);
			}
			catch (Exception ex)
			{
				Logger.Error(LogCategory.Export, $"Error exporting collection {collection.Name}: {ex.Message}");
			}
		}
	}

	private void ExportSingleCollectionDetails(AssetCollection collection, string outputPath)
	{
		var detailedInfo = new Dictionary<string, object>
		{
			// Basic info
			["name"] = collection.Name,
			["filePath"] = collection.FilePath,
			["type"] = collection.GetType().Name,
			["version"] = collection.Version.ToString(),
			["platform"] = collection.Platform.ToString(),
			["flags"] = collection.Flags.ToString(),
			["endianType"] = collection.EndianType.ToString(),

			// Bundle info
			["bundle"] = collection.Bundle.Name,

			// Asset info
			["assetCount"] = collection.Assets.Count,
			["assetTypes"] = CreateAssetTypes(collection),

			// Dependencies info
			["dependencyCount"] = collection.Dependencies.Count,
			["dependencies"] = CreateDependenciesSummary(collection),

			// Scene info
			["isScene"] = collection.IsScene
		};

		if (collection.IsScene)
		{
			detailedInfo["sceneInfo"] = new Dictionary<string, object>
			{
				["sceneName"] = collection.Scene!.Name,
				["scenePath"] = collection.Scene.Path,
				["sceneGuid"] = collection.Scene.GUID.ToString()
			};
		}

		// Add SerializedAssetCollection specific info
		if (collection is SerializedAssetCollection)
		{
			detailedInfo["collectionType"] = "SerializedAssetCollection";
		}
		else if (collection is ProcessedAssetCollection)
		{
			detailedInfo["collectionType"] = "ProcessedAssetCollection";
		}
		else
		{
			detailedInfo["collectionType"] = "VirtualAssetCollection";
		}

		string collectionName = ExportHelper.SanitizeFileName(collection.Name);
		string collectionFile = Path.Combine(outputPath, $"{collectionName}.json");
		WriteJsonFile(detailedInfo, collectionFile);
	}

    private Dictionary<string, object> CreateCollectionSummary(AssetCollection collection)
    {
        return new Dictionary<string, object>
        {
            ["name"] = collection.Name,
            ["filePath"] = collection.FilePath,
            ["type"] = collection.GetType().Name,
            ["platform"] = collection.Platform.ToString(),
            ["version"] = collection.Version.ToString(),
            ["assetCount"] = collection.Assets.Count,
            ["dependencyCount"] = collection.Dependencies.Count,
            ["isScene"] = collection.IsScene,
            ["sceneName"] = collection.Scene != null ? collection.Scene.Name : string.Empty,
            ["bundleName"] = collection.Bundle.Name
        };
    }

    private Dictionary<string, object> CreateAssetTypes(AssetCollection collection)
    {
        return collection.Assets.Values
            .GroupBy(asset => asset.ClassName)
            .ToDictionary(
                g => g.Key,
                g => (object)new Dictionary<string, object>
                {
                    ["count"] = g.Count(),
                    ["classId"] = g.First().ClassID,
                    ["assets"] = g.Select(a => new Dictionary<string, object>
                    {
                        ["pathID"] = a.PathID,
                        ["name"] = a.GetBestName(),
						["originalPath"] = a.OriginalPath ?? string.Empty,
                    }).ToList()
                }
            );
    }

	private List<Dictionary<string, object>> CreateDependenciesSummary(AssetCollection collection)
	{
		var dependencies = new List<Dictionary<string, object>>();

		for (int i = 0; i < collection.Dependencies.Count; i++)
		{
			var dep = collection.Dependencies[i];
			if (dep != null)
			{
				dependencies.Add(new Dictionary<string, object>
				{
					["index"] = i,
					["name"] = dep.Name,
					["filePath"] = dep.FilePath,
					["type"] = dep.GetType().Name,
					["assetCount"] = dep.Assets.Count,
					["isSelfReference"] = i == 0
				});
			}
			else
			{
				dependencies.Add(new Dictionary<string, object>
				{
					["index"] = i,
					["name"] = String.Empty,
					["status"] = i == 0 ? "SelfReference" : "MissingDependency",
					["isSelfReference"] = i == 0
				});
			}
		}

		return dependencies;
	}

	private void WriteJsonFile(object data, string filePath)
	{
		ExportHelper.WriteJsonFile(data, filePath, _jsonSettings);
	}
}
