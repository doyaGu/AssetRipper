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
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Processors;

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
		ExportHelper.EnsureDirectoryExists(scenesOutputPath);

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
		SceneHierarchyObject? hierarchy = null;

		foreach (AssetCollection collection in scene.Collections)
		{
			hierarchy = collection.OfType<SceneHierarchyObject>().FirstOrDefault();
			if (hierarchy != null)
				break;
		}

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
		string safeName = ExportHelper.SanitizeFileName(sceneName);
		string sceneOutputPath = Path.Combine(outputPath, safeName);
		ExportHelper.EnsureDirectoryExists(sceneOutputPath);

		try
		{
			ExportSceneMetadata(hierarchy, sceneOutputPath, sceneName);
			ExportHierarchyStructure(hierarchy, sceneOutputPath);
			ExportGameObjects(hierarchy, sceneOutputPath);
			ExportComponents(hierarchy, sceneOutputPath);
			ExportManagers(hierarchy, sceneOutputPath);
			ExportPrefabInstances(hierarchy, sceneOutputPath);

			if (hierarchy.SceneRoots != null)
			{
				ExportSceneRoots(hierarchy.SceneRoots, sceneOutputPath);
			}

			ExportStrippedAssets(hierarchy.StrippedAssets, sceneOutputPath);
			ExportHiddenAssets(hierarchy.HiddenAssets, sceneOutputPath);
			ExportDependencies(hierarchy.FetchDependencies(), sceneOutputPath);
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Error exporting scene {sceneName}: {ex.Message}");
		}
	}

	private void ExportSceneMetadata(SceneHierarchyObject hierarchy, string outputPath, string sceneName)
	{
		var metadata = ExportHelper.CreateBasicMetadata(sceneName, "Scene");

		// Add scene-specific metadata
		metadata["pathID"] = hierarchy.PathID;
		metadata["classID"] = hierarchy.ClassID;
		metadata["className"] = hierarchy.ClassName;

		metadata["sceneGuid"] = hierarchy.Scene.GUID.ToString();
		metadata["scenePath"] = hierarchy.Scene.Path;
		metadata["sceneCollectionCount"] = hierarchy.Scene.Collections.Count;

		metadata["collection"] = hierarchy.Collection.Name;
		metadata["version"] = hierarchy.Collection.Version.ToString();
		metadata["platform"] = hierarchy.Collection.Platform.ToString();
		metadata["endianType"] = hierarchy.Collection.EndianType.ToString();

		metadata["assetCount"] = hierarchy.Assets.Count();
		metadata["gameObjectCount"] = hierarchy.GameObjects.Count;
		metadata["componentCount"] = hierarchy.Components.Count;
		metadata["managerCount"] = hierarchy.Managers.Count;
		metadata["prefabInstanceCount"] = hierarchy.PrefabInstances.Count;

		metadata["hasSceneRoots"] = hierarchy.SceneRoots != null;
		metadata["rootGameObjectCount"] = hierarchy.GetRoots().Count();
		metadata["strippedAssetCount"] = hierarchy.StrippedAssets.Count;
		metadata["hiddenAssetCount"] = hierarchy.HiddenAssets.Count;

		string metadataFile = Path.Combine(outputPath, "metadata.json");
		WriteJsonFile(metadata, metadataFile);
	}

	private void ExportHierarchyStructure(SceneHierarchyObject hierarchy, string outputPath)
	{
		try
		{
			List<object> hierarchyData = BuildGameObjectHierarchy(hierarchy.GetRoots().ToList());
			string hierarchyFile = Path.Combine(outputPath, "hierarchy.json");
			WriteJsonFile(hierarchyData, hierarchyFile);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Error exporting hierarchy structure: {ex.Message}");
		}
	}

	private void ExportGameObjects(SceneHierarchyObject hierarchy, string outputPath)
	{
		if (hierarchy.GameObjects.Count == 0) return;

		try
		{
			List<Dictionary<string, object>> gameObjectsData = hierarchy.GameObjects
				.Select(SerializeAssetWithMetadata)
				.ToList();

			string gameObjectsFile = Path.Combine(outputPath, "gameObjects.json");
			WriteJsonFile(gameObjectsData, gameObjectsFile);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Error exporting game objects: {ex.Message}");
		}
	}

	private void ExportComponents(SceneHierarchyObject hierarchy, string outputPath)
	{
		if (hierarchy.Components.Count == 0) return;

		try
		{
			List<Dictionary<string, object>> componentData = hierarchy.Components
				.Select(SerializeAssetWithMetadata)
				.ToList();

			string componentFile = Path.Combine(outputPath, "components.json");
			WriteJsonFile(componentData, componentFile);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Error exporting components: {ex.Message}");
		}
	}

	private void ExportManagers(SceneHierarchyObject hierarchy, string outputPath)
	{
		if (hierarchy.Managers.Count == 0) return;

		try
		{
			List<Dictionary<string, object>> managersData = hierarchy.Managers
				.Select(SerializeAssetWithMetadata)
				.ToList();

			string managersFile = Path.Combine(outputPath, "managers.json");
			WriteJsonFile(managersData, managersFile);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Error exporting managers: {ex.Message}");
		}
	}

	private void ExportPrefabInstances(SceneHierarchyObject hierarchy, string outputPath)
	{
		if (hierarchy.PrefabInstances.Count == 0) return;

		try
		{
			List<Dictionary<string, object>> prefabData = hierarchy.PrefabInstances
				.Select(SerializeAssetWithMetadata)
				.ToList();

			string prefabFile = Path.Combine(outputPath, "prefabInstances.json");
			WriteJsonFile(prefabData, prefabFile);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Error exporting prefab instances: {ex.Message}");
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

	private void ExportStrippedAssets(HashSet<IUnityObjectBase> strippedAssets, string outputPath)
	{
		if (strippedAssets.Count == 0) return;

		try
		{
			var strippedData = strippedAssets
				.Select(SerializeAssetWithMetadata)
				.ToList();

			string strippedFile = Path.Combine(outputPath, "stripped_assets.json");
			WriteJsonFile(strippedData, strippedFile);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Error exporting stripped assets: {ex.Message}");
		}
	}

	private void ExportHiddenAssets(HashSet<IUnityObjectBase> hiddenAssets, string outputPath)
	{
		if (hiddenAssets.Count == 0) return;

		try
		{
			var hiddenData = hiddenAssets
				.Select(SerializeAssetWithMetadata)
				.ToList();

			string hiddenFile = Path.Combine(outputPath, "hidden_assets.json");
			WriteJsonFile(hiddenData, hiddenFile);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Error exporting hidden assets: {ex.Message}");
		}
	}

	private void ExportDependencies(IEnumerable<(string, PPtr)> dependencies, string outputPath)
	{
		var dependencyData = new List<object>();

		foreach ((string fieldName, PPtr pptr) in dependencies)
		{
			try
			{
				var dependencyInfo = new Dictionary<string, object>
				{
					["fieldName"] = fieldName,
					["fileID"] = pptr.FileID,
					["pathID"] = pptr.PathID,
					["isNull"] = pptr.IsNull
				};

				dependencyData.Add(dependencyInfo);
			}
			catch (Exception ex)
			{
				Logger.Warning(LogCategory.Export, $"Error processing dependency {fieldName}: {ex.Message}");
			}
		}

		if (dependencyData.Count > 0)
		{
			string dependenciesFile = Path.Combine(outputPath, "dependencies.json");
			WriteJsonFile(dependencyData, dependenciesFile);
		}
	}

	private void WriteJsonFile(object data, string filePath)
	{
		ExportHelper.WriteJsonFile(data, filePath, _jsonSettings);
	}

	private Dictionary<string, object> SerializeAssetWithMetadata(IUnityObjectBase asset)
	{
		var jsonWalker = new JsonWalker(asset.Collection);
		string assetJson = jsonWalker.SerializeStandard(asset);
		Dictionary<string, object> assetData = JsonConvert.DeserializeObject<Dictionary<string, object>>(assetJson)!;

		var result = new Dictionary<string, object>
		{
			["collection"] = asset.Collection.Name,
			["pathID"] = asset.PathID,
			["classID"] = asset.ClassID,
			["className"] = asset.ClassName,
			["bestName"] = asset.GetBestName(),
			["data"] = assetData
		};

		return result;
	}

	private static List<object> BuildGameObjectHierarchy(IEnumerable<IGameObject> gameObjects)
	{
		var hierarchyData = new List<object>();

		foreach (IGameObject gameObject in gameObjects)
		{
			try
			{
				var gameObjectData = new Dictionary<string, object>
				{
					["name"] = gameObject.Name.String,
					["collection"] = gameObject.Collection.Name,
					["pathID"] = gameObject.PathID,
				};

				var children = gameObject.GetChildren();
				if (children.Any())
				{
					gameObjectData["children"] = BuildGameObjectHierarchy(children);
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
