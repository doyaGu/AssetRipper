using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models;

namespace AssetRipper.Tools.AssetDumper.Metrics;

/// <summary>
/// Collects asset distribution statistics grouped by class type.
/// Provides comprehensive metrics on asset counts and sizes at both global and bundle levels.
/// Uses SerializedObjectMetadata.ByteSize for accurate size reporting where available.
/// </summary>
public sealed class AssetDistributionCollector : BaseMetricsCollector
{
	public override string MetricsId => "asset_distribution";
	public override string SchemaUri => "https://example.org/assetdump/v2/metrics/asset_distribution.schema.json";
	public override bool HasData => _globalStats.Count > 0;

	// Global statistics by classKey
	private readonly Dictionary<int, ClassStatistics> _globalStats = new();
	
	// Bundle statistics by bundle name
	private readonly Dictionary<string, BundleStatistics> _bundleStats = new();

	public AssetDistributionCollector(Options options) : base(options)
	{
	}

	public override void Collect(GameData gameData)
	{
		_globalStats.Clear();
		_bundleStats.Clear();

		if (gameData == null)
			return;

		// First pass: Build TypeDictionary for consistent classKey assignment
		TypeDictionaryBuilder typeDictionary = new TypeDictionaryBuilder();
		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			foreach (IUnityObjectBase asset in collection)
			{
				SerializedObjectMetadata metadata = SerializedObjectMetadata.FromAsset(asset);
				typeDictionary.GetOrAdd(asset, metadata);
			}
		}

		// Second pass: Collect statistics with classKey
		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			string bundleName = collection.Bundle.Name;
			
			// Initialize bundle stats if not exists
			if (!_bundleStats.ContainsKey(bundleName))
			{
				_bundleStats[bundleName] = new BundleStatistics
				{
					BundleName = bundleName,
					ClassStats = new Dictionary<int, ClassStatistics>()
				};
			}
			
			BundleStatistics bundleStats = _bundleStats[bundleName];
			bundleStats.Collections++;

			foreach (IUnityObjectBase asset in collection)
			{
				SerializedObjectMetadata metadata = SerializedObjectMetadata.FromAsset(asset);
				int classKey = typeDictionary.GetOrAdd(asset, metadata);
				string className = asset.ClassName ?? $"ClassID_{metadata.ClassId}";

				// Update global stats
				if (!_globalStats.ContainsKey(classKey))
				{
					_globalStats[classKey] = new ClassStatistics
					{
						ClassKey = classKey,
						ClassId = metadata.ClassId,
						ClassName = className
					};
				}
				
				ClassStatistics globalStat = _globalStats[classKey];
				globalStat.Count++;
				
				if (metadata.ByteSize >= 0)
				{
					globalStat.CountWithByteSize++;
					globalStat.TotalBytes += metadata.ByteSize;
					globalStat.ByteSizes.Add(metadata.ByteSize);
				}

				// Update bundle stats
				if (!bundleStats.ClassStats.ContainsKey(classKey))
				{
					bundleStats.ClassStats[classKey] = new ClassStatistics
					{
						ClassKey = classKey,
						ClassId = metadata.ClassId,
						ClassName = className
					};
				}
				
				ClassStatistics bundleStat = bundleStats.ClassStats[classKey];
				bundleStat.Count++;
				bundleStats.TotalAssets++;
				
				if (metadata.ByteSize >= 0)
				{
					bundleStat.CountWithByteSize++;
					bundleStat.TotalBytes += metadata.ByteSize;
					bundleStat.ByteSizes.Add(metadata.ByteSize);
					bundleStats.AssetsWithByteSize++;
					bundleStats.TotalBytes += metadata.ByteSize;
				}
			}
		}

		// Calculate statistics (averages, min, max, median)
		foreach (ClassStatistics stat in _globalStats.Values)
		{
			stat.CalculateStatistics();
		}

		foreach (BundleStatistics bundleStat in _bundleStats.Values)
		{
			foreach (ClassStatistics classStat in bundleStat.ClassStats.Values)
			{
				classStat.CalculateStatistics();
			}
			
			// Calculate bundle-level average
			if (bundleStat.AssetsWithByteSize > 0)
			{
				bundleStat.AverageBytes = bundleStat.TotalBytes / bundleStat.AssetsWithByteSize;
			}
		}
	}

	protected override object? GetMetricsData()
	{
		if (_globalStats.Count == 0)
			return null;

		long totalAssets = _globalStats.Values.Sum(s => s.Count);
		long assetsWithByteSize = _globalStats.Values.Sum(s => s.CountWithByteSize);
		long totalBytes = _globalStats.Values.Sum(s => s.TotalBytes);
		int totalCollections = _bundleStats.Values.Sum(b => b.Collections);

		return new
		{
			domain = "asset_distribution",
			summary = new
			{
				totalAssets,
				totalBytes,
				uniqueClasses = _globalStats.Count,
				totalCollections,
				totalBundles = _bundleStats.Count,
				assetsWithByteSize
			},
			byClass = _globalStats.Values
				.OrderByDescending(s => s.Count)
				.Select(s => s.ToGlobalRecord())
				.ToList(),
			byBundle = _bundleStats.Values
				.OrderByDescending(b => b.TotalAssets)
				.Select(b => new
				{
					bundleName = b.BundleName,
					collections = b.Collections,
					totalAssets = b.TotalAssets,
					assetsWithByteSize = b.AssetsWithByteSize,
					totalBytes = b.TotalBytes > 0 ? (long?)b.TotalBytes : null,
					averageBytes = b.AverageBytes > 0 ? (long?)b.AverageBytes : null,
					uniqueClasses = b.ClassStats.Count,
					byClass = b.ClassStats.Values
						.OrderByDescending(s => s.Count)
						.Take(20)  // Top 20 classes per bundle
						.Select(s => s.ToRecord())
						.ToList()
				})
				.ToList()
		};
	}

	/// <summary>
	/// Statistics for a single class across all assets.
	/// </summary>
	private class ClassStatistics
	{
		public int ClassKey { get; set; }
		public int ClassId { get; set; }
		public string ClassName { get; set; } = string.Empty;
		public long Count { get; set; }
		public long CountWithByteSize { get; set; }
		public long TotalBytes { get; set; }
		public long AverageBytes { get; set; }
		public long MinBytes { get; set; }
		public long MaxBytes { get; set; }
		public long MedianBytes { get; set; }
		
		// Temporary storage for calculating statistics
		public List<int> ByteSizes { get; set; } = new List<int>();

		public void CalculateStatistics()
		{
			if (ByteSizes.Count == 0)
				return;

			// Calculate average
			if (CountWithByteSize > 0)
			{
				AverageBytes = TotalBytes / CountWithByteSize;
			}

			// Calculate min, max, median
			ByteSizes.Sort();
			MinBytes = ByteSizes[0];
			MaxBytes = ByteSizes[ByteSizes.Count - 1];
			
			// Median: Use lower-middle for even count
			int medianIndex = (ByteSizes.Count - 1) / 2;
			MedianBytes = ByteSizes[medianIndex];
		}

		/// <summary>
		/// Convert to record for global byClass output (includes optional statistics).
		/// </summary>
		public object ToGlobalRecord()
		{
			var record = new Dictionary<string, object>
			{
				["classKey"] = ClassKey,
				["classId"] = ClassId,
				["className"] = ClassName,
				["count"] = Count,
				["countWithByteSize"] = CountWithByteSize
			};

			// Only include size statistics if we have byte size data
			if (CountWithByteSize > 0)
			{
				record["totalBytes"] = TotalBytes;
				record["averageBytes"] = AverageBytes;
				record["minBytes"] = MinBytes;
				record["maxBytes"] = MaxBytes;
				record["medianBytes"] = MedianBytes;
			}

			return record;
		}

		/// <summary>
		/// Convert to record for bundle-level byClass output (includes optional statistics).
		/// </summary>
		public object ToRecord()
		{
			var record = new Dictionary<string, object>
			{
				["classKey"] = ClassKey,
				["classId"] = ClassId,
				["className"] = ClassName,
				["count"] = Count,
				["countWithByteSize"] = CountWithByteSize
			};

			// Only include size statistics if we have byte size data
			if (CountWithByteSize > 0)
			{
				record["totalBytes"] = TotalBytes;
				record["averageBytes"] = AverageBytes;
				record["minBytes"] = MinBytes;
				record["maxBytes"] = MaxBytes;
				record["medianBytes"] = MedianBytes;
			}

			return record;
		}
	}

	/// <summary>
	/// Statistics for all assets within a bundle.
	/// </summary>
	private class BundleStatistics
	{
		public string BundleName { get; set; } = string.Empty;
		public int Collections { get; set; }
		public long TotalAssets { get; set; }
		public long AssetsWithByteSize { get; set; }
		public long TotalBytes { get; set; }
		public long AverageBytes { get; set; }
		public Dictionary<int, ClassStatistics> ClassStats { get; set; } = new();
	}
}
