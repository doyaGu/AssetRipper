using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.Export.PrimaryContent;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.Processing.Prefabs;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_1660057539;
using AssetRipper.SourceGenerated.Extensions;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace AssetRipper.Tools.AssetDumper;

internal class SceneProcessor
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public SceneProcessor(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = JsonHelper.CreateSceneSettings(_options);
	}

	public void ExportScenes(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting scene data...");
		GenerateSceneJsonWithHierarchy(gameData);
	}

	private void GenerateSceneJsonWithHierarchy(GameData gameData)
	{
		string scenesOutputPath = Path.Combine(_options.OutputPath, _options.ScenesOutputFolder);
		Directory.CreateDirectory(scenesOutputPath);

		var scenesToProcess = gameData.GameBundle.Scenes.AsEnumerable();

		if (!string.IsNullOrEmpty(_options.SceneFilter))
		{
			var sceneRegex = new Regex(_options.SceneFilter, RegexOptions.IgnoreCase | RegexOptions.Compiled);
			scenesToProcess = scenesToProcess.Where(scene => sceneRegex.IsMatch(scene.Name));
		}

		var sceneList = scenesToProcess.ToList();
		Logger.Info(LogCategory.Export, $"Processing {sceneList.Count} scenes");

		foreach (SceneDefinition scene in sceneList)
		{
			try
			{
				Logger.Debug(LogCategory.Export, $"Processing scene: {scene.Name}");
				ProcessSingleScene(scene, scenesOutputPath);
			}
			catch (Exception ex)
			{
				Logger.Error(LogCategory.Export, $"Error processing scene {scene.Name}: {ex.Message}");
			}
		}

		Logger.Info(LogCategory.Export, $"Scene processing completed: {sceneList.Count} scenes processed");
	}

	private void ProcessSingleScene(SceneDefinition scene, string scenesOutputPath)
	{
		SceneHierarchyObject? hierarchy = scene.Assets
			.OfType<SceneHierarchyObject>()
			.FirstOrDefault();

		if (hierarchy != null)
		{
			GenerateHierarchicalSceneJson(hierarchy, scenesOutputPath, scene.Name);
		}
		else
		{
			Logger.Warning(LogCategory.Export, $"No hierarchy found for scene: {scene.Name}");
		}
	}

	private void GenerateHierarchicalSceneJson(SceneHierarchyObject hierarchy, string outputPath, string sceneName)
	{
		string safeName = SanitizeFileName(sceneName);
		string sceneOutputPath = Path.Combine(outputPath, safeName);
		Directory.CreateDirectory(sceneOutputPath);

		try
		{
			ExportSceneMetadata(hierarchy, sceneOutputPath, sceneName);
			ExportAssetsByType(hierarchy, sceneOutputPath);
			ExportHierarchyStructure(hierarchy, sceneOutputPath);
			ExportManagers(hierarchy, sceneOutputPath);

			if (hierarchy.SceneRoots != null)
			{
				ExportSceneRoots(hierarchy.SceneRoots, sceneOutputPath);
			}
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Error exporting scene {sceneName}: {ex.Message}");
		}
	}

	private void ExportSceneMetadata(SceneHierarchyObject hierarchy, string outputPath, string sceneName)
	{
		var metadata = new Dictionary<string, object>
		{
			["name"] = sceneName,
			["collection"] = hierarchy.Collection.Name,
			["path"] = $"Assets/Scenes/{sceneName}.unity",
			["assetCount"] = hierarchy.Assets.Count(),
			["gameObjectCount"] = hierarchy.GameObjects.Count,
			["componentCount"] = hierarchy.Components.Count,
			["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
		};

		string metadataFile = Path.Combine(outputPath, "metadata.json");
		WriteJsonFile(metadata, metadataFile);
	}

	private void ExportAssetsByType(SceneHierarchyObject hierarchy, string outputPath)
	{
		var assetsByType = hierarchy.Assets
			.GroupBy(asset => asset.GetType().Name)
			.ToDictionary(g => g.Key, g => g.ToList());

		string assetsDir = Path.Combine(outputPath, "assets");
		Directory.CreateDirectory(assetsDir);

		foreach (var (typeName, assets) in assetsByType)
		{
			try
			{
				List<object> serializedAssets = SerializeAssetsWithMetadata(assets);
				string safeTypeName = SanitizeFileName(typeName);
				string typeFile = Path.Combine(assetsDir, $"{safeTypeName}.json");
				WriteJsonFile(serializedAssets, typeFile);
			}
			catch (Exception ex)
			{
				Logger.Warning(LogCategory.Export, $"Error exporting assets of type {typeName}: {ex.Message}");
			}
		}
	}

	private void ExportHierarchyStructure(SceneHierarchyObject hierarchy, string outputPath)
	{
		try
		{
			List<object> hierarchyData = BuildGameObjectHierarchyWithSchema(hierarchy.GetRoots().ToList());
			string hierarchyFile = Path.Combine(outputPath, "hierarchy.json");
			WriteJsonFile(hierarchyData, hierarchyFile);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Error exporting hierarchy structure: {ex.Message}");
		}
	}

	private void ExportManagers(SceneHierarchyObject hierarchy, string outputPath)
	{
		if (hierarchy.Managers.Count > 0)
		{
			try
			{
				List<object> managersData = SerializeAssetsWithMetadata(hierarchy.Managers.Cast<IUnityObjectBase>().ToList());
				string managersFile = Path.Combine(outputPath, "managers.json");
				WriteJsonFile(managersData, managersFile);
			}
			catch (Exception ex)
			{
				Logger.Warning(LogCategory.Export, $"Error exporting managers: {ex.Message}");
			}
		}
	}

	private void ExportSceneRoots(ISceneRoots sceneRoots, string outputPath)
	{
		try
		{
			Dictionary<string, object> rootsData = SerializeAssetWithMetadata(sceneRoots);
			string rootsFile = Path.Combine(outputPath, "sceneRoots.json");
			WriteJsonFile(rootsData, rootsFile);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Error exporting scene roots: {ex.Message}");
		}
	}

	private void WriteJsonFile(object data, string filePath)
	{
		try
		{
			string json = JsonConvert.SerializeObject(data, _jsonSettings);
			File.WriteAllText(filePath, json);
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to write JSON file {filePath}: {ex.Message}");
		}
	}

	private List<object> SerializeAssetsWithMetadata<T>(IReadOnlyList<T> assets) where T : IUnityObjectBase
	{
		var resolvedAssets = new List<object>();
		foreach (T asset in assets)
		{
			try
			{
				resolvedAssets.Add(SerializeAssetWithMetadata(asset));
			}
			catch (Exception ex)
			{
				Logger.Warning(LogCategory.Export, $"Error serializing asset {asset.PathID}: {ex.Message}");
			}
		}
		return resolvedAssets;
	}

	private Dictionary<string, object> SerializeAssetWithMetadata(IUnityObjectBase asset)
	{
		var jsonWalker = new JsonWalker(asset.Collection);
		string assetJson = jsonWalker.SerializeStandard(asset);
		Dictionary<string, object> assetData = JsonConvert.DeserializeObject<Dictionary<string, object>>(assetJson)!;

		var result = new Dictionary<string, object>
		{
			["pathID"] = asset.PathID,
			["classID"] = asset.ClassID,
			["className"] = asset.GetType().Name,
			["data"] = assetData
		};

		if (_options.IncludeAssetMetadata)
		{
			result["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
			result["collection"] = asset.Collection.Name;
		}

		return result;
	}

	private static List<object> BuildGameObjectHierarchyWithSchema(IEnumerable<IGameObject> gameObjects)
	{
		var hierarchyData = new List<object>();

		foreach (IGameObject gameObject in gameObjects)
		{
			try
			{
				var gameObjectData = new Dictionary<string, object>
				{
					["pathID"] = gameObject.PathID,
					["name"] = gameObject.Name.String,
					["classID"] = gameObject.ClassID
				};

				var componentRefs = new List<object>();
				foreach (var componentPtr in gameObject.FetchComponents())
				{
					if (componentPtr.TryGetAsset(gameObject.Collection, out var component) && component != null)
					{
						componentRefs.Add(new Dictionary<string, object>
						{
							["pathID"] = component.PathID,
							["classID"] = component.ClassID,
							["className"] = component.GetType().Name
						});
					}
				}
				gameObjectData["componentRefs"] = componentRefs;

				var children = gameObject.GetChildren().ToList();
				if (children.Count > 0)
				{
					gameObjectData["children"] = BuildGameObjectHierarchyWithSchema(children);
				}

				hierarchyData.Add(gameObjectData);
			}
			catch (Exception ex)
			{
				Logger.Warning(LogCategory.Export, $"Error processing game object {gameObject.PathID}: {ex.Message}");
			}
		}

		return hierarchyData;
	}

	private static string SanitizeFileName(string fileName)
	{
		var invalidChars = Path.GetInvalidFileNameChars();
		return string.Concat(fileName.Where(c => !invalidChars.Contains(c)));
	}

	private sealed class JsonWalker : DefaultJsonWalker
	{
		private readonly AssetCollection collection;

		public JsonWalker(AssetCollection collection)
		{
			this.collection = collection;
		}

		public override void VisitPPtr<TAsset>(PPtr<TAsset> pptr)
		{
			AssetCollection? targetCollection = pptr.FileID >= 0 && pptr.FileID < collection.Dependencies.Count
				? collection.Dependencies[pptr.FileID]
				: collection;

			if (targetCollection != null)
			{
				Writer.Write("{ \"m_Collection\": \"");
				Writer.Write(System.Web.HttpUtility.JavaScriptStringEncode(targetCollection.Name));
				Writer.Write("\", \"m_PathID\": ");
				Writer.Write(pptr.PathID);
				Writer.Write(" }");
			}
			else
			{
				base.VisitPPtr(pptr);
			}
		}
	}
}
