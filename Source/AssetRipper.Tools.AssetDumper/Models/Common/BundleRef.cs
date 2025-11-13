using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Common;

/// <summary>
/// Reference to a Bundle node using stable PK.
/// </summary>
public sealed class BundleRef
{
	[JsonProperty("bundlePk")]
	public string BundlePk { get; set; } = string.Empty;

	[JsonProperty("bundleName", NullValueHandling = NullValueHandling.Ignore)]
	public string? BundleName { get; set; }
}
