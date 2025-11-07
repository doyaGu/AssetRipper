using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.Processing;
using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Metrics;

/// <summary>
/// Collects dependency graph statistics.
/// Analyzes reference patterns, degree distribution, and dependency health metrics.
/// </summary>
public sealed class DependencyStatsCollector : BaseMetricsCollector
{
	public override string MetricsId => "dependency_stats";
	public override string SchemaUri => "https://example.org/assetdump/v2/metrics/dependency_stats.schema.json";
	public override bool HasData => _stats != null;

	private const int PointerRepeatBreakThreshold = 512;
	private DependencyStats? _stats;

	public DependencyStatsCollector(Options options) : base(options)
	{
	}

	public override void Collect(GameData gameData)
	{
		_stats = null;

		if (gameData == null)
			return;

		Dictionary<AssetKey, int> outgoingCounts = new();
		Dictionary<AssetKey, int> incomingCounts = new();
		long totalEdges = 0;

		List<AssetCollection> collections = gameData.GameBundle.FetchAssetCollections().ToList();

		if (Options.Verbose)
		{
			Logger.Verbose($"DependencyStats: scanning {collections.Count} collections for dependency edges");
		}

		int processedAssets = 0;
		int collectionIndex = 0;
		foreach (AssetCollection collection in collections)
		{
			collectionIndex++;
			if (Options.Verbose)
			{
				Logger.Verbose($"DependencyStats: processing collection {collectionIndex}/{collections.Count} '{collection.Name}' with {collection.Count} assets");
			}

			foreach (IUnityObjectBase asset in collection)
			{
				processedAssets++;
				AssetKey fromKey = new AssetKey(collection.Name, asset.PathID);
				HashSet<AssetKey> uniqueTargets = new HashSet<AssetKey>();
				Dictionary<PointerSignature, int>? pointerRepeatCounts = null;

				try
				{
					foreach ((string fieldName, PPtr pptr) in asset.FetchDependencies())
					{
						PointerSignature signature = PointerSignature.From(fieldName, pptr);
						pointerRepeatCounts ??= new Dictionary<PointerSignature, int>();
						int repeatCount = pointerRepeatCounts.TryGetValue(signature, out int currentCount) ? currentCount + 1 : 1;
						pointerRepeatCounts[signature] = repeatCount;

						if (repeatCount >= PointerRepeatBreakThreshold)
						{
							string displayField = string.IsNullOrEmpty(signature.Field) ? "<null>" : signature.Field;
							Logger.Warning(LogCategory.Export, $"DependencyStats: asset {fromKey.CollectionName}#{fromKey.PathId} detected repeating pointer fileID={signature.FileId}, pathID={signature.PathId}, field='{displayField}' after {repeatCount} occurrences �?terminating dependency enumeration");
							break;
						}

						if (pptr.IsNull)
						{
							continue;
						}

						IUnityObjectBase? target = collection.TryGetAsset(pptr);
						if (target == null)
						{
							continue;
						}

						AssetKey toKey = new AssetKey(target.Collection.Name, target.PathID);
						uniqueTargets.Add(toKey);
					}
				}
				catch (System.Exception ex)
				{
					Logger.Warning(LogCategory.Export, $"Failed to resolve dependencies for asset {fromKey.CollectionName}#{fromKey.PathId}: {ex.Message}");
				}

				int outDegree = uniqueTargets.Count;
				outgoingCounts[fromKey] = outDegree;
				totalEdges += outDegree;

				foreach (AssetKey toKey in uniqueTargets)
				{
					if (incomingCounts.TryGetValue(toKey, out int current))
					{
						incomingCounts[toKey] = current + 1;
					}
					else
					{
						incomingCounts[toKey] = 1;
					}
				}

				if (Options.Verbose && processedAssets % 100000 == 0)
				{
					Logger.Verbose($"DependencyStats: processed {processedAssets} assets");
				}
			}

			if (Options.Verbose)
			{
				Logger.Verbose($"DependencyStats: completed collection '{collection.Name}'");
			}
		}

		if (Options.Verbose)
		{
			Logger.Verbose($"DependencyStats: captured {outgoingCounts.Count} asset nodes and {totalEdges} outgoing edges");
		}

		int totalAssets = outgoingCounts.Count;
		if (totalAssets == 0)
		{
			return;
		}

		int maxOut = 0;
		double sumOutDegrees = 0;
		foreach (int outDegree in outgoingCounts.Values)
		{
			sumOutDegrees += outDegree;
			if (outDegree > maxOut)
			{
				maxOut = outDegree;
			}
		}
		double averageOut = totalAssets > 0 ? sumOutDegrees / totalAssets : 0;

		int maxIn = 0;
		double sumInDegrees = 0;
		foreach (AssetKey key in outgoingCounts.Keys)
		{
			int inDegree = incomingCounts.TryGetValue(key, out int count) ? count : 0;
			sumInDegrees += inDegree;
			if (inDegree > maxIn)
			{
				maxIn = inDegree;
			}
		}
		double averageIn = totalAssets > 0 ? sumInDegrees / totalAssets : 0;

		int isolatedAssets = 0;
		foreach (KeyValuePair<AssetKey, int> kvp in outgoingCounts)
		{
			int inDegree = incomingCounts.TryGetValue(kvp.Key, out int count) ? count : 0;
			if (kvp.Value == 0 && inDegree == 0)
			{
				isolatedAssets++;
			}
		}

		_stats = new DependencyStats
		{
			TotalEdges = totalEdges,
			TotalAssets = totalAssets,
			AverageOutDegree = averageOut,
			AverageInDegree = averageIn,
			MaxOutDegree = maxOut,
			MaxInDegree = maxIn,
			IsolatedAssets = isolatedAssets
		};

		if (Options.Verbose)
		{
			Logger.Verbose($"DependencyStats: max out-degree {maxOut}, max in-degree {maxIn}, isolated assets {isolatedAssets}");
		}
	}

	protected override object? GetMetricsData()
	{
		if (_stats == null)
			return null;

		return new
		{
			edges = new
			{
				total = _stats.TotalEdges,
				averagePerAsset = _stats.TotalAssets > 0 ? 
					(double)_stats.TotalEdges / _stats.TotalAssets : 0
			},
			degree = new
			{
				outgoing = new
				{
					average = _stats.AverageOutDegree,
					max = _stats.MaxOutDegree
				},
				incoming = new
				{
					average = _stats.AverageInDegree,
					max = _stats.MaxInDegree
				}
			},
			health = new
			{
				isolatedAssets = _stats.IsolatedAssets,
				totalAssets = _stats.TotalAssets
			}
		};
	}

	private class DependencyStats
	{
		public long TotalEdges { get; set; }
		public int TotalAssets { get; set; }
		public double AverageOutDegree { get; set; }
		public double AverageInDegree { get; set; }
		public int MaxOutDegree { get; set; }
		public int MaxInDegree { get; set; }
		public int IsolatedAssets { get; set; }
	}

	private readonly struct PointerSignature : IEquatable<PointerSignature>
	{
		private PointerSignature(int fileId, long pathId, string field)
		{
			FileId = fileId;
			PathId = pathId;
			Field = field;
		}

		public int FileId { get; }
		public long PathId { get; }
		public string Field { get; }

		public static PointerSignature From(string? fieldName, PPtr pointer)
		{
			return new PointerSignature(pointer.FileID, pointer.PathID, fieldName ?? string.Empty);
		}

		public bool Equals(PointerSignature other)
		{
			return FileId == other.FileId
				&& PathId == other.PathId
				&& string.Equals(Field, other.Field, System.StringComparison.Ordinal);
		}

		public override bool Equals(object? obj)
		{
			return obj is PointerSignature other && Equals(other);
		}

		public override int GetHashCode()
		{
			System.HashCode hash = new System.HashCode();
			hash.Add(FileId);
			hash.Add(PathId);
			hash.Add(Field, System.StringComparer.Ordinal);
			return hash.ToHashCode();
		}
	}

	private struct AssetKey : IEquatable<AssetKey>
	{
		public string CollectionName { get; }
		public long PathId { get; }

		public AssetKey(string collectionName, long pathId)
		{
			CollectionName = collectionName;
			PathId = pathId;
		}

		public bool Equals(AssetKey other)
		{
			return CollectionName == other.CollectionName && PathId == other.PathId;
		}

		public override bool Equals(object? obj)
		{
			return obj is AssetKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(CollectionName, PathId);
		}
	}
}
