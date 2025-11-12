using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Assembly dependency relation record for dependency graph analysis.
/// Represents .NET assembly references with complete metadata.
/// </summary>
public sealed class AssemblyDependencyRecord
{
	/// <summary>
	/// Domain identifier, always "assembly_dependencies".
	/// </summary>
	[JsonProperty("domain")]
	public string Domain { get; set; } = "assembly_dependencies";

	/// <summary>
	/// Source assembly GUID (32-character hex string).
	/// </summary>
	[JsonProperty("sourceAssembly")]
	public string SourceAssembly { get; set; } = string.Empty;

	/// <summary>
	/// Name of the module declaring this reference (typically the main module).
	/// </summary>
	[JsonProperty("sourceModule", NullValueHandling = NullValueHandling.Ignore)]
	public string? SourceModule { get; set; }

	/// <summary>
	/// Target assembly GUID; null if dependency was not resolved.
	/// </summary>
	[JsonProperty("targetAssembly", NullValueHandling = NullValueHandling.Ignore)]
	public string? TargetAssembly { get; set; }

	/// <summary>
	/// Target assembly name as declared in the AssemblyReference.
	/// </summary>
	[JsonProperty("targetName")]
	public string TargetName { get; set; } = string.Empty;

	/// <summary>
	/// Required assembly version in .NET format (Major.Minor.Build.Revision).
	/// </summary>
	[JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
	public string? Version { get; set; }

	/// <summary>
	/// Public key token for strong-named assemblies (16-character hex string).
	/// Null for non-strong-named assemblies.
	/// </summary>
	[JsonProperty("publicKeyToken", NullValueHandling = NullValueHandling.Ignore)]
	public string? PublicKeyToken { get; set; }

	/// <summary>
	/// Culture information (e.g., 'neutral', 'en-US').
	/// Empty or 'neutral' for culture-neutral assemblies.
	/// </summary>
	[JsonProperty("culture", NullValueHandling = NullValueHandling.Ignore)]
	public string? Culture { get; set; }

	/// <summary>
	/// Whether the dependency was successfully resolved to a known assembly.
	/// </summary>
	[JsonProperty("isResolved")]
	public bool IsResolved { get; set; }

	/// <summary>
	/// Type of dependency: 'direct' (user assembly), 'framework' (System.*/mscorlib),
	/// 'plugin' (Unity plugins), 'unknown' (unclassified).
	/// </summary>
	[JsonProperty("dependencyType", NullValueHandling = NullValueHandling.Ignore)]
	public string? DependencyType { get; set; }

	/// <summary>
	/// Whether target is a .NET framework/reference assembly.
	/// </summary>
	[JsonProperty("isFrameworkAssembly", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsFrameworkAssembly { get; set; }

	/// <summary>
	/// Human-readable reason why dependency resolution failed.
	/// Present only if IsResolved is false.
	/// </summary>
	[JsonProperty("failureReason", NullValueHandling = NullValueHandling.Ignore)]
	public string? FailureReason { get; set; }
}
