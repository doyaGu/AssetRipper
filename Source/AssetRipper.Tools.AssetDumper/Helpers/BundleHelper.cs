using AssetRipper.Assets.Bundles;
using AssetRipper.Tools.AssetDumper.Models.Common;

namespace AssetRipper.Tools.AssetDumper.Helpers;

/// <summary>
/// Shared utilities for bundle operations across exporters.
/// </summary>
/// <remarks>
/// Consolidates duplicate code from:
/// AssetFactsExporter, AssetExporter, CollectionExporter, CollectionFactsExporter,
/// BundleExporter, BundleHierarchyExporter, SceneExporter
/// </remarks>
public static class BundleHelper
{
	/// <summary>
	/// Builds the lineage list from root to leaf bundle.
	/// Uses O(n) approach: Add() + Reverse() instead of Insert(0, x) which is O(n^2).
	/// </summary>
	public static List<Bundle> BuildLineage(Bundle? bundle)
	{
		List<Bundle> lineage = new List<Bundle>();
		Bundle? current = bundle;

		while (current != null)
		{
			lineage.Add(current);
			current = current.Parent;
		}

		// Reverse to get root-to-leaf order
		lineage.Reverse();
		return lineage;
	}

	/// <summary>
	/// Computes a stable hash key for a bundle lineage.
	/// </summary>
	public static string ComputeStableKey(List<Bundle> lineage)
	{
		string composite = string.Join("|", lineage.Select(static b => $"{b.GetType().FullName}:{b.Name}"));
		return ExportHelper.ComputeStableHash(composite);
	}

	/// <summary>
	/// Computes a stable hash key for a single bundle by building its lineage.
	/// </summary>
	public static string ComputeStableKey(Bundle bundle)
	{
		List<Bundle> lineage = BuildLineage(bundle);
		return ComputeStableKey(lineage);
	}

	/// <summary>
	/// Builds a BundleRef for the given bundle.
	/// </summary>
	public static BundleRef BuildBundleRef(Bundle bundle)
	{
		string bundlePk = ComputeStableKey(bundle);
		return new BundleRef
		{
			BundlePk = bundlePk,
			BundleName = bundle.Name
		};
	}

	/// <summary>
	/// Builds a HierarchyPath for the given bundle.
	/// </summary>
	public static HierarchyPath? BuildHierarchyPath(Bundle? bundle)
	{
		if (bundle == null)
		{
			return null;
		}

		List<Bundle> lineage = BuildLineage(bundle);

		if (lineage.Count == 0)
		{
			return null;
		}

		List<string> bundlePath = new List<string>(lineage.Count);
		List<string> bundleNames = new List<string>(lineage.Count);

		foreach (Bundle b in lineage)
		{
			string bundlePk = ExportHelper.ComputeBundlePk(b);
			bundlePath.Add(bundlePk);
			bundleNames.Add(b.Name ?? string.Empty);
		}

		return new HierarchyPath
		{
			BundlePath = bundlePath,
			BundleNames = bundleNames,
			Depth = lineage.Count - 1
		};
	}

	/// <summary>
	/// Gets the bundle name with null safety.
	/// </summary>
	public static string GetBundleName(Bundle? bundle)
	{
		return bundle?.Name ?? string.Empty;
	}

	/// <summary>
	/// Checks if a bundle is a root bundle (has no parent).
	/// </summary>
	public static bool IsRoot(Bundle bundle)
	{
		return bundle.Parent == null;
	}

	/// <summary>
	/// Gets the depth of a bundle in the hierarchy (0 for root).
	/// </summary>
	public static int GetDepth(Bundle bundle)
	{
		int depth = 0;
		Bundle? current = bundle.Parent;

		while (current != null)
		{
			depth++;
			current = current.Parent;
		}

		return depth;
	}
}
