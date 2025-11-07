using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Structured reference to a Unity asset (collectionId + pathID).
/// </summary>
public class AssetRef
{
	[JsonProperty("c")]
	public string CollectionId { get; set; } = string.Empty;

	[JsonProperty("p")]
	public long PathId { get; set; }

	public AssetRef() { }

	public AssetRef(string collectionId, long pathId)
	{
		CollectionId = collectionId;
		PathId = pathId;
	}
}

/// <summary>
/// Helper to create stable keys for lookups.
/// </summary>
public static class StableKeyHelper
{
	public static string Create(string collectionId, long pathId)
	{
		return $"{collectionId}:{pathId}";
	}

	public static string Create(AssetRef assetRef)
	{
		return $"{assetRef.CollectionId}:{assetRef.PathId}";
	}
}
