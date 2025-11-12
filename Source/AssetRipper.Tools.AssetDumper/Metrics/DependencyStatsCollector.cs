using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.Processing;
using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Metrics;

/// <summary>
/// Collects dependency graph statistics at the asset level (PPtr references).
/// Analyzes FetchDependencies() results to track reference patterns, degree distribution,
/// and dependency health metrics including internal/external/cross-bundle reference classification.
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

		// Per-asset degree tracking
		Dictionary<AssetKey, AssetDegreeInfo> assetDegrees = new();
		
		// Per-type statistics tracking
		Dictionary<int, TypeStats> typeStats = new();
		
		// Global counters
		long totalEdges = 0;
		long internalReferences = 0;
		long externalReferences = 0;
		long crossBundleReferences = 0;
		long nullReferences = 0;
		long unresolvedReferences = 0;

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
				string fromBundleName = collection.Bundle.Name;
				int classId = asset.ClassID;
				
				// Initialize asset degree info
				if (!assetDegrees.ContainsKey(fromKey))
				{
					assetDegrees[fromKey] = new AssetDegreeInfo
					{
						ClassId = classId,
						ClassName = asset.ClassName ?? $"ClassID_{classId}"
					};
				}
				
				// Initialize type stats
				if (!typeStats.ContainsKey(classId))
				{
					typeStats[classId] = new TypeStats
					{
						ClassId = classId,
						ClassName = asset.ClassName ?? $"ClassID_{classId}"
					};
				}
				
				TypeStats typeInfo = typeStats[classId];
				typeInfo.Count++;
				
				HashSet<AssetKey> uniqueTargets = new HashSet<AssetKey>();
				Dictionary<PointerSignature, int>? pointerRepeatCounts = null;

				try
				{
					foreach ((string fieldName, PPtr pptr) in asset.FetchDependencies())
					{
						// Track pointer repeats to avoid infinite loops
						PointerSignature signature = PointerSignature.From(fieldName, pptr);
						pointerRepeatCounts ??= new Dictionary<PointerSignature, int>();
						int repeatCount = pointerRepeatCounts.TryGetValue(signature, out int currentCount) ? currentCount + 1 : 1;
						pointerRepeatCounts[signature] = repeatCount;

						if (repeatCount >= PointerRepeatBreakThreshold)
						{
							string displayField = string.IsNullOrEmpty(signature.Field) ? "<null>" : signature.Field;
							Logger.Warning(LogCategory.Export, $"DependencyStats: asset {fromKey.CollectionName}#{fromKey.PathId} detected repeating pointer fileID={signature.FileId}, pathID={signature.PathId}, field='{displayField}' after {repeatCount} occurrences – terminating dependency enumeration");
							break;
						}

						// Handle null references
						if (pptr.IsNull)
						{
							nullReferences++;
							continue;
						}

						// Try to resolve reference
						IUnityObjectBase? target = collection.TryGetAsset(pptr);
						if (target == null)
						{
							unresolvedReferences++;
							continue;
						}

						AssetKey toKey = new AssetKey(target.Collection.Name, target.PathID);
						
						// Skip duplicates and self-references
						if (toKey.Equals(fromKey) || !uniqueTargets.Add(toKey))
						{
							continue;
						}
						
						// Classify reference type
						string toBundleName = target.Collection.Bundle.Name;
						
						if (fromKey.CollectionName == toKey.CollectionName)
						{
							// Same collection: internal reference
							internalReferences++;
						}
						else if (fromBundleName == toBundleName)
						{
							// Different collection, same bundle: external reference
							externalReferences++;
						}
						else
						{
							// Different bundle: cross-bundle reference
							crossBundleReferences++;
						}
						
						totalEdges++;
						
						// Track incoming degree for target
						if (!assetDegrees.ContainsKey(toKey))
						{
							assetDegrees[toKey] = new AssetDegreeInfo
							{
								ClassId = target.ClassID,
								ClassName = target.ClassName ?? $"ClassID_{target.ClassID}"
							};
						}
						assetDegrees[toKey].InDegree++;
					}
				}
				catch (System.Exception ex)
				{
					Logger.Warning(LogCategory.Export, $"Failed to resolve dependencies for asset {fromKey.CollectionName}#{fromKey.PathId}: {ex.Message}");
				}

				// Update outgoing degree
				int outDegree = uniqueTargets.Count;
				assetDegrees[fromKey].OutDegree = outDegree;

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
			Logger.Verbose($"DependencyStats: captured {assetDegrees.Count} asset nodes and {totalEdges} outgoing edges");
		}

		int totalAssets = assetDegrees.Count;
		if (totalAssets == 0)
		{
			return;
		}

		// Calculate degree statistics
		List<int> outDegrees = new List<int>(totalAssets);
		List<int> inDegrees = new List<int>(totalAssets);
		
		int maxOut = 0;
		int minOut = int.MaxValue;
		double sumOutDegrees = 0;
		
		int maxIn = 0;
		int minIn = int.MaxValue;
		double sumInDegrees = 0;
		
		int noOutgoingRefs = 0;
		int noIncomingRefs = 0;
		int completelyIsolated = 0;

		foreach (var kvp in assetDegrees)
		{
			AssetDegreeInfo degreeInfo = kvp.Value;
			int outDegree = degreeInfo.OutDegree;
			int inDegree = degreeInfo.InDegree;
			
			// Track for median calculation
			outDegrees.Add(outDegree);
			inDegrees.Add(inDegree);
			
			// Update global statistics
			sumOutDegrees += outDegree;
			sumInDegrees += inDegree;
			
			if (outDegree > maxOut) maxOut = outDegree;
			if (outDegree < minOut) minOut = outDegree;
			
			if (inDegree > maxIn) maxIn = inDegree;
			if (inDegree < minIn) minIn = inDegree;
			
			// Update health indicators
			if (outDegree == 0) noOutgoingRefs++;
			if (inDegree == 0) noIncomingRefs++;
			if (outDegree == 0 && inDegree == 0) completelyIsolated++;
			
			// Update type statistics
			if (typeStats.TryGetValue(degreeInfo.ClassId, out TypeStats? typeInfo))
			{
				typeInfo.SumOutDegree += outDegree;
				typeInfo.SumInDegree += inDegree;
				
				if (outDegree > typeInfo.MaxOutDegree) typeInfo.MaxOutDegree = outDegree;
				if (inDegree > typeInfo.MaxInDegree) typeInfo.MaxInDegree = inDegree;
			}
		}
		
		// Calculate averages
		double averageOut = totalAssets > 0 ? sumOutDegrees / totalAssets : 0;
		double averageIn = totalAssets > 0 ? sumInDegrees / totalAssets : 0;
		
		// Calculate medians
		outDegrees.Sort();
		inDegrees.Sort();
		
		double medianOut = totalAssets > 0 ? outDegrees[(totalAssets - 1) / 2] : 0;
		double medianIn = totalAssets > 0 ? inDegrees[(totalAssets - 1) / 2] : 0;
		
		// Handle empty case for min values
		if (minOut == int.MaxValue) minOut = 0;
		if (minIn == int.MaxValue) minIn = 0;

		// Calculate type averages and prepare output
		List<TypeStatsOutput> typeStatsOutput = typeStats.Values
			.Select(ts => new TypeStatsOutput
			{
				ClassId = ts.ClassId,
				ClassName = ts.ClassName,
				Count = ts.Count,
				AverageOutDegree = ts.Count > 0 ? (double)ts.SumOutDegree / ts.Count : 0,
				AverageInDegree = ts.Count > 0 ? (double)ts.SumInDegree / ts.Count : 0,
				MaxOutDegree = ts.MaxOutDegree,
				MaxInDegree = ts.MaxInDegree
			})
			.OrderByDescending(ts => ts.Count)
			.Take(30)  // Top 30 types
			.ToList();

		_stats = new DependencyStats
		{
			TotalEdges = totalEdges,
			InternalReferences = internalReferences,
			ExternalReferences = externalReferences,
			CrossBundleReferences = crossBundleReferences,
			NullReferences = nullReferences,
			UnresolvedReferences = unresolvedReferences,
			
			TotalAssets = totalAssets,
			
			AverageOutDegree = averageOut,
			MinOutDegree = minOut,
			MaxOutDegree = maxOut,
			MedianOutDegree = medianOut,
			
			AverageInDegree = averageIn,
			MinInDegree = minIn,
			MaxInDegree = maxIn,
			MedianInDegree = medianIn,
			
			NoOutgoingRefs = noOutgoingRefs,
			NoIncomingRefs = noIncomingRefs,
			CompletelyIsolated = completelyIsolated,
			
			TypeStats = typeStatsOutput
		};

		if (Options.Verbose)
		{
			Logger.Verbose($"DependencyStats: max out-degree {maxOut}, max in-degree {maxIn}, completely isolated {completelyIsolated}");
			Logger.Verbose($"DependencyStats: internal refs {internalReferences}, external refs {externalReferences}, cross-bundle refs {crossBundleReferences}");
		}
	}

	protected override object? GetMetricsData()
	{
		if (_stats == null)
			return null;

		return new
		{
			domain = "dependency_stats",
			edges = new
			{
				total = _stats.TotalEdges,
				averagePerAsset = _stats.TotalAssets > 0 ? 
					(double)_stats.TotalEdges / _stats.TotalAssets : 0,
				internalReferences = _stats.InternalReferences,
				externalReferences = _stats.ExternalReferences,
				crossBundleReferences = _stats.CrossBundleReferences,
				nullReferences = _stats.NullReferences,
				unresolvedReferences = _stats.UnresolvedReferences
			},
			degree = new
			{
				outgoing = new
				{
					average = _stats.AverageOutDegree,
					min = _stats.MinOutDegree,
					max = _stats.MaxOutDegree,
					median = _stats.MedianOutDegree
				},
				incoming = new
				{
					average = _stats.AverageInDegree,
					min = _stats.MinInDegree,
					max = _stats.MaxInDegree,
					median = _stats.MedianInDegree
				}
			},
			health = new
			{
				totalAssets = _stats.TotalAssets,
				noOutgoingRefs = _stats.NoOutgoingRefs,
				noIncomingRefs = _stats.NoIncomingRefs,
				completelyIsolated = _stats.CompletelyIsolated
			},
			byType = _stats.TypeStats
		};
	}

	/// <summary>
	/// Aggregated dependency statistics for the entire project.
	/// </summary>
	private class DependencyStats
	{
		// Edge statistics
		public long TotalEdges { get; set; }
		public long InternalReferences { get; set; }
		public long ExternalReferences { get; set; }
		public long CrossBundleReferences { get; set; }
		public long NullReferences { get; set; }
		public long UnresolvedReferences { get; set; }
		
		// Asset count
		public int TotalAssets { get; set; }
		
		// Out-degree statistics
		public double AverageOutDegree { get; set; }
		public int MinOutDegree { get; set; }
		public int MaxOutDegree { get; set; }
		public double MedianOutDegree { get; set; }
		
		// In-degree statistics
		public double AverageInDegree { get; set; }
		public int MinInDegree { get; set; }
		public int MaxInDegree { get; set; }
		public double MedianInDegree { get; set; }
		
		// Health indicators
		public int NoOutgoingRefs { get; set; }
		public int NoIncomingRefs { get; set; }
		public int CompletelyIsolated { get; set; }
		
		// Per-type statistics
		public List<TypeStatsOutput> TypeStats { get; set; } = new();
	}

	/// <summary>
	/// Degree information for a single asset.
	/// </summary>
	private class AssetDegreeInfo
	{
		public int ClassId { get; set; }
		public string ClassName { get; set; } = string.Empty;
		public int OutDegree { get; set; }
		public int InDegree { get; set; }
	}

	/// <summary>
	/// Accumulated statistics for a ClassID type.
	/// </summary>
	private class TypeStats
	{
		public int ClassId { get; set; }
		public string ClassName { get; set; } = string.Empty;
		public int Count { get; set; }
		public long SumOutDegree { get; set; }
		public long SumInDegree { get; set; }
		public int MaxOutDegree { get; set; }
		public int MaxInDegree { get; set; }
	}

	/// <summary>
	/// Per-type statistics for JSON output.
	/// </summary>
	private class TypeStatsOutput
	{
		public int ClassId { get; set; }
		public string ClassName { get; set; } = string.Empty;
		public int Count { get; set; }
		public double AverageOutDegree { get; set; }
		public double AverageInDegree { get; set; }
		public int MaxOutDegree { get; set; }
		public int MaxInDegree { get; set; }
	}

	/// <summary>
	/// Signature identifying a PPtr reference by FileID, PathID, and field name.
	/// Used to detect infinite loops in circular dependency chains.
	/// </summary>
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

	/// <summary>
	/// Unique identifier for an asset by collection name and PathID.
	/// </summary>
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
