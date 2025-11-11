using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Assembly dependency relation record for dependency graph analysis.
/// </summary>
public sealed class AssemblyDependencyRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "assembly_dependencies";

	[JsonProperty("sourceAssembly")]
	public string SourceAssembly { get; set; } = string.Empty;

	[JsonProperty("targetAssembly", NullValueHandling = NullValueHandling.Ignore)]
	public string? TargetAssembly { get; set; }

	[JsonProperty("targetName")]
	public string TargetName { get; set; } = string.Empty;

	[JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
	public string? Version { get; set; }

	[JsonProperty("isResolved")]
	public bool IsResolved { get; set; }

	[JsonProperty("isFrameworkAssembly", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsFrameworkAssembly { get; set; }
}
