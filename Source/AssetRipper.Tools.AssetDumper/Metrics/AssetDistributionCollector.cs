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
		// Placeholder estimation - in real implementation would use actual byte sizes
		// from object info table (data.byteSize field)
		return 1024; // Default 1KB estimate
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
