using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Models;

/// <summary>
/// Script source fact record linking MonoScripts to decompiled source files.
/// </summary>
public sealed class ScriptSourceRecord
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = "script_sources";

	[JsonProperty("pk")]
	public string Pk { get; set; } = string.Empty;

	[JsonProperty("scriptPk")]
	public string ScriptPk { get; set; } = string.Empty;

	[JsonProperty("assemblyGuid")]
	public string AssemblyGuid { get; set; } = string.Empty;

	[JsonProperty("sourcePath")]
	public string SourcePath { get; set; } = string.Empty;

	[JsonProperty("sourceSize")]
	public long SourceSize { get; set; }

	[JsonProperty("lineCount")]
	public int LineCount { get; set; }

	[JsonProperty("characterCount", NullValueHandling = NullValueHandling.Ignore)]
	public int? CharacterCount { get; set; }

	[JsonProperty("sha256")]
	public string Sha256 { get; set; } = string.Empty;

	[JsonProperty("language")]
	public string Language { get; set; } = "CSharp";

	[JsonProperty("decompiler")]
	public string Decompiler { get; set; } = string.Empty;

	[JsonProperty("decompilerVersion", NullValueHandling = NullValueHandling.Ignore)]
	public string? DecompilerVersion { get; set; }

	[JsonProperty("decompilationStatus")]
	public string DecompilationStatus { get; set; } = "success";

	[JsonProperty("isEmpty", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsEmpty { get; set; }

	[JsonProperty("errorMessage", NullValueHandling = NullValueHandling.Ignore)]
	public string? ErrorMessage { get; set; }

	[JsonProperty("isPresent", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsPresent { get; set; }

	[JsonProperty("isGeneric", NullValueHandling = NullValueHandling.Ignore)]
	public bool? IsGeneric { get; set; }

	[JsonProperty("hasAst", NullValueHandling = NullValueHandling.Ignore)]
	public bool? HasAst { get; set; }

	[JsonProperty("astPath", NullValueHandling = NullValueHandling.Ignore)]
	public string? AstPath { get; set; }
}
