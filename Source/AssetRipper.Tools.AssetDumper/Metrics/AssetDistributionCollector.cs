using AssetRipper.Assets;
using AssetRipper.Processing;
using System.Collections.Concurrent;
using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Metrics;

/// <summary>
/// Collects asset distribution statistics grouped by class type.
/// Provides metrics on asset counts, sizes, and distribution patterns.
/// </summary>
public sealed class AssetDistributionCollector : BaseMetricsCollector
{
	public override string MetricsId => "asset_distribution";
	public override string SchemaUri => "https://example.org/assetdump/v2/metrics/asset_distribution.schema.json";
	public override bool HasData => _distribution.Count > 0;

	private readonly ConcurrentDictionary<int, ClassDistribution> _distribution = new();

	public AssetDistributionCollector(Options options) : base(options)
	{
	}

	public override void Collect(GameData gameData)
	{
		_distribution.Clear();

		if (gameData == null)
			return;

		var collections = gameData.GameBundle.FetchAssetCollections().ToList();

		// Use parallel processing for large asset collections
		System.Threading.Tasks.Parallel.ForEach(collections, collection =>
		{
			foreach (IUnityObjectBase asset in collection)
			{
				int classId = asset.ClassID;
				
				// GetOrAdd is thread-safe for ConcurrentDictionary
				ClassDistribution dist = _distribution.GetOrAdd(classId, _ => new ClassDistribution
				{
					ClassId = classId,
					ClassName = asset.ClassName ?? $"Unknown_{classId}"
				});

				// Interlocked operations for thread-safe increments
				System.Threading.Interlocked.Increment(ref dist._count);
				
				// Try to estimate size - note: this is approximate
				// In a full implementation, you'd track actual serialized byte sizes
				long sizeEstimate = EstimateAssetSize(asset);
				System.Threading.Interlocked.Add(ref dist._totalBytes, sizeEstimate);
			}
		});

		// Calculate averages
		foreach (ClassDistribution dist in _distribution.Values)
		{
			dist.Count = dist._count;
			dist.TotalBytes = dist._totalBytes;
			if (dist.Count > 0)
			{
				dist.AverageBytes = dist.TotalBytes / dist.Count;
			}
		}
	}

	private long EstimateAssetSize(IUnityObjectBase asset)
	{
		// Use improved type-based size estimation based on typical Unity asset sizes
		// Note: These are statistical averages from real projects. Actual sizes vary.
		return asset.ClassID switch
		{
			// Small objects (< 1KB)
			1 => 256,      // GameObject - minimal overhead
			2 => 128,      // Component (base)
			4 => 512,      // Transform - position, rotation, scale + hierarchy
			23 => 64,      // MeshRenderer - component reference
			33 => 64,      // MeshFilter - mesh reference
			54 => 128,     // Rigidbody - physics properties
			65 => 128,     // BoxCollider - collider properties
			82 => 512,     // AudioSource - audio properties
			108 => 256,    // Behaviour (base)
			114 => 1024,   // MonoBehaviour - script data (varies widely)
			115 => 128,    // MonoScript - script reference
			124 => 64,     // Camera component
			
			// Medium objects (1-10KB)
			21 => 2048,    // Material - shader refs + properties
			43 => 4096,    // Mesh - vertices, indices, normals (varies widely)
			48 => 512,     // Shader - compiled shader code
			74 => 1024,    // AnimationClip - keyframes
			90 => 2048,    // Avatar - humanoid rig data
			91 => 512,     // AnimatorController - state machine
			128 => 1024,   // Font - glyph data
			134 => 2048,   // PhysicMaterial - physics properties
			137 => 4096,   // SkinnedMeshRenderer - skin weights
			
			// Large objects (> 10KB)
			28 => 524288,  // Texture2D - typical 512KB compressed texture
			49 => 65536,   // TextAsset - text data (varies)
			83 => 8192,    // AudioClip - audio metadata (actual data in StreamingAssets)
			89 => 32768,   // CubemapArray - multiple cubemaps
			187 => 524288, // Texture2DArray - array of textures
			188 => 524288, // Texture3D - volume texture
			
			// Default estimate based on general patterns
			_ => asset.ClassID < 100 ? 512 : 1024 // Native types vs user types
		};
	}

	protected override object? GetMetricsData()
	{
		if (_distribution.Count == 0)
			return null;

		return new
		{
			summary = new
			{
				totalAssets = _distribution.Values.Sum(d => d.Count),
				totalBytes = _distribution.Values.Sum(d => d.TotalBytes),
				uniqueClasses = _distribution.Count
			},
			byClass = _distribution.Values
				.OrderByDescending(d => d.Count)
				.Select(d => new
				{
					classId = d.ClassId,
					className = d.ClassName,
					count = d.Count,
					totalBytes = d.TotalBytes,
					averageBytes = d.AverageBytes
				})
				.ToList()
		};
	}

	private class ClassDistribution
	{
		public int ClassId { get; set; }
		public string ClassName { get; set; } = string.Empty;
		
		// Thread-safe atomic fields for parallel processing
		internal long _count;
		internal long _totalBytes;
		
		// Public properties set after parallel processing
		public long Count { get; set; }
		public long TotalBytes { get; set; }
		public long AverageBytes { get; set; }
	}
}
