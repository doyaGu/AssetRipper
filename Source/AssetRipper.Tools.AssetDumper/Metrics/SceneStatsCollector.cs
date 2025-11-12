using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Processing;
using AssetRipper.Processing.Prefabs;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.Tools.AssetDumper.Core;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Metrics;

/// <summary>
/// Collects statistics about scenes in the game data from SceneHierarchyObject.
/// Implements the scene_stats metric extracting comprehensive scene metadata.
/// </summary>
public sealed class SceneStatsCollector : BaseMetricsCollector
{
	public override string MetricsId => "scene_stats";
	public override string SchemaUri => "https://schemas.assetripper.dev/assetdump/v2/metrics/scene_stats.schema.json";
	public override bool HasData => _sceneStats.Count > 0;

	private readonly List<SceneStatRecord> _sceneStats = new();

	public SceneStatsCollector(Options options) : base(options)
	{
	}

	public override void Collect(GameData gameData)
	{
		_sceneStats.Clear();

		if (gameData == null)
			return;

		// Iterate through all collections to find SceneHierarchyObject instances
		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			foreach (IUnityObjectBase asset in collection)
			{
				if (asset is SceneHierarchyObject sceneHierarchy)
				{
					SceneStatRecord stat = CollectSceneStats(sceneHierarchy);
					_sceneStats.Add(stat);
				}
			}
		}
	}

	private SceneStatRecord CollectSceneStats(SceneHierarchyObject sceneHierarchy)
	{
		SceneDefinition scene = sceneHierarchy.Scene;

		// Count root GameObjects
		int rootCount = sceneHierarchy.GetRoots().Count();

		return new SceneStatRecord
		{
			SceneGuid = scene.GUID.ToString(),
			SceneName = scene.Name,
			ScenePath = scene.Path,
			HierarchyAssetPk = new AssetPKRecord
			{
				CollectionId = sceneHierarchy.Collection.Name,
				PathId = sceneHierarchy.PathID
			},
			Counts = new SceneCountsRecord
			{
				GameObjects = sceneHierarchy.GameObjects.Count,
				Components = sceneHierarchy.Components.Count,
				PrefabInstances = sceneHierarchy.PrefabInstances.Count,
				Managers = sceneHierarchy.Managers.Count,
				RootGameObjects = rootCount,
				StrippedAssets = sceneHierarchy.StrippedAssets.Count,
				HiddenAssets = sceneHierarchy.HiddenAssets.Count,
				Collections = scene.Collections.Count
			},
			HasSceneRoots = sceneHierarchy.SceneRoots != null
		};
	}

	protected override object? GetMetricsData()
	{
		return _sceneStats.Count > 0 ? _sceneStats : null;
	}

	/// <summary>
	/// Record for a single scene's statistics.
	/// </summary>
	private class SceneStatRecord
	{
		[JsonProperty("domain")]
		public string Domain { get; set; } = "scene_stats";

		[JsonProperty("sceneGuid")]
		public string SceneGuid { get; set; } = string.Empty;

		[JsonProperty("sceneName")]
		public string SceneName { get; set; } = string.Empty;

		[JsonProperty("scenePath", NullValueHandling = NullValueHandling.Ignore)]
		public string? ScenePath { get; set; }

		[JsonProperty("hierarchyAssetPk", NullValueHandling = NullValueHandling.Ignore)]
		public AssetPKRecord? HierarchyAssetPk { get; set; }

		[JsonProperty("counts")]
		public SceneCountsRecord Counts { get; set; } = new();

		[JsonProperty("hasSceneRoots", NullValueHandling = NullValueHandling.Ignore)]
		public bool? HasSceneRoots { get; set; }

		[JsonProperty("notes", NullValueHandling = NullValueHandling.Ignore)]
		public string? Notes { get; set; }
	}

	/// <summary>
	/// Counts for various scene elements.
	/// </summary>
	private class SceneCountsRecord
	{
		[JsonProperty("gameObjects")]
		public int GameObjects { get; set; }

		[JsonProperty("components")]
		public int Components { get; set; }

		[JsonProperty("prefabInstances")]
		public int PrefabInstances { get; set; }

		[JsonProperty("managers")]
		public int Managers { get; set; }

		[JsonProperty("rootGameObjects")]
		public int RootGameObjects { get; set; }

		[JsonProperty("strippedAssets", NullValueHandling = NullValueHandling.Ignore)]
		public int? StrippedAssets { get; set; }

		[JsonProperty("hiddenAssets", NullValueHandling = NullValueHandling.Ignore)]
		public int? HiddenAssets { get; set; }

		[JsonProperty("collections", NullValueHandling = NullValueHandling.Ignore)]
		public int? Collections { get; set; }
	}

	/// <summary>
	/// Asset primary key reference.
	/// </summary>
	private class AssetPKRecord
	{
		[JsonProperty("collectionId")]
		public string CollectionId { get; set; } = string.Empty;

		[JsonProperty("pathId")]
		public long PathId { get; set; }
	}
}
