using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Metrics;

/// <summary>
/// Collects statistics about scenes in the game data.
/// Implements the scene_stats metric as defined in whitepaper section 6.4.
/// </summary>
public sealed class SceneStatsCollector : BaseMetricsCollector
{
	public override string MetricsId => "scene_stats";
	public override string SchemaUri => "https://example.org/assetdump/v2/metrics/scene_stats.schema.json";
	public override bool HasData => _sceneStats.Count > 0;

	private readonly List<SceneStat> _sceneStats = new();

	public SceneStatsCollector(Options options) : base(options)
	{
	}

	public override void Collect(GameData gameData)
	{
		_sceneStats.Clear();

		if (gameData == null)
			return;

		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			// Look for scene collections (typically have Scene_XXX asset or .unity extension)
			bool isScene = collection.Name.Contains("scene", StringComparison.OrdinalIgnoreCase) ||
						   collection.Name.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

			if (!isScene)
				continue;

			SceneStat stat = CollectSceneStats(collection);
			if (stat.Counts.GameObjects > 0) // Only include scenes with content
			{
				_sceneStats.Add(stat);
			}
		}
	}

	private SceneStat CollectSceneStats(AssetCollection collection)
	{
		int gameObjectCount = 0;
		int componentCount = 0;
		int maxDepth = 0;

		// Count GameObjects and Components
		foreach (IUnityObjectBase asset in collection)
		{
			if (asset is IGameObject gameObject)
			{
				gameObjectCount++;
				
				// Count components precisely using GameObjectExtensions.GetComponentCount()
				componentCount += gameObject.GetComponentCount();
			}
		}

		// Calculate hierarchy depth by traversing transforms
		Dictionary<long, int> depthMap = new();
		foreach (IUnityObjectBase asset in collection)
		{
			if (asset is ITransform transform)
			{
				int depth = CalculateDepth(transform, depthMap, collection);
				if (depth > maxDepth)
				{
					maxDepth = depth;
				}
			}
		}

		return new SceneStat
		{
			Scene = new AssetPK
			{
				CollectionId = collection.Name, // Simplified - in real implementation would use stable ID
				PathId = 0 // Scene root
			},
			Counts = new SceneCounts
			{
				GameObjects = gameObjectCount,
				Components = componentCount,
				MaxHierarchyDepth = maxDepth
			}
		};
	}

	private int CalculateDepth(ITransform transform, Dictionary<long, int> depthMap, AssetCollection collection)
	{
		long pathId = transform.PathID;
		
		if (depthMap.ContainsKey(pathId))
			return depthMap[pathId];

		// If no parent, depth is 0
		ITransform? father = transform.Father_C4P;
		if (father == null)
		{
			depthMap[pathId] = 0;
			return 0;
		}

		// Calculate depth recursively
		int depth = 1 + CalculateDepth(father, depthMap, collection);
		depthMap[pathId] = depth;
		return depth;
	}

	protected override object? GetMetricsData()
	{
		return _sceneStats.Count > 0 ? _sceneStats : null;
	}

	private class SceneStat
	{
		public AssetPK Scene { get; set; } = new();
		public SceneCounts Counts { get; set; } = new();
	}

	private class SceneCounts
	{
		public int GameObjects { get; set; }
		public int Components { get; set; }
		public int MaxHierarchyDepth { get; set; }
	}

	private class AssetPK
	{
		public string CollectionId { get; set; } = string.Empty;
		public long PathId { get; set; }
	}
}
