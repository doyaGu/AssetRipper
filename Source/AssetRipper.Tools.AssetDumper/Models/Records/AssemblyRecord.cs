using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Assembly fact record capturing metadata for managed assemblies.
/// </summary>
public sealed class AssemblyRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "assemblies";

	[JsonProperty("pk")]
	public string Pk { get; set; } = string.Empty;

	[JsonProperty("name")]
	public string Name { get; set; } = string.Empty;

	[JsonProperty("fullName")]
	public string FullName { get; set; } = string.Empty;

	[JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
	public string? Version { get; set; }

	[JsonProperty("targetFramework", NullValueHandling = NullValueHandling.Ignore)]
	public string? TargetFramework { get; set; }

	[JsonProperty("scriptingBackend")]
	public string ScriptingBackend { get; set; } = "Unknown";

	[JsonProperty("runtime", NullValueHandling = NullValueHandling.Ignore)]
	public string? Runtime { get; set; }

	[JsonProperty("assemblyType")]
	public string AssemblyType { get; set; } = "User";

	[JsonProperty("dllPath", NullValueHandling = NullValueHandling.Ignore)]
	public string? DllPath { get; set; }

	[JsonProperty("dllSize", NullValueHandling = NullValueHandling.Ignore)]
	public long? DllSize { get; set; }

	[JsonProperty("dllSha256", NullValueHandling = NullValueHandling.Ignore)]
	public string? DllSha256 { get; set; }

	[JsonProperty("typeCount")]
	public int TypeCount { get; set; }

	[JsonProperty("scriptCount")]
	public int ScriptCount { get; set; }

	[JsonProperty("isDynamic")]
	public bool IsDynamic { get; set; }

	[JsonProperty("isEditor")]
	public bool IsEditor { get; set; }

	[JsonProperty("platform", NullValueHandling = NullValueHandling.Ignore)]
	public string? Platform { get; set; }

	[JsonProperty("mscorlibVersion", NullValueHandling = NullValueHandling.Ignore)]
	public int? MscorlibVersion { get; set; }

	[JsonProperty("references", NullValueHandling = NullValueHandling.Ignore)]
	public List<string>? References { get; set; }

	[JsonProperty("exportType", NullValueHandling = NullValueHandling.Ignore)]
	public string? ExportType { get; set; }

	[JsonProperty("isModified", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsModified { get; set; }
}
