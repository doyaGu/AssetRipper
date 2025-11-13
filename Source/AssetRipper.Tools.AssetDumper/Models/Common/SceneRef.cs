using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models.Common;

/// <summary>
/// Reference to a Scene using GUID.
/// </summary>
public sealed class SceneRef
{
	[JsonProperty("sceneGuid")]
	public string SceneGuid { get; set; } = string.Empty;

	[JsonProperty("sceneName", NullValueHandling = NullValueHandling.Ignore)]
	public string? SceneName { get; set; }

	[JsonProperty("scenePath", NullValueHandling = NullValueHandling.Ignore)]
	public string? ScenePath { get; set; }
}
