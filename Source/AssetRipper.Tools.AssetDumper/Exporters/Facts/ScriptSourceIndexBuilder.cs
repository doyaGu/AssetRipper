using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetRipper.Tools.AssetDumper.Exporters.Facts;

/// <summary>
/// Builds authoritative script-source records from dump-backed inputs.
/// </summary>
internal sealed class ScriptSourceIndexBuilder
{
	private readonly Options _options;

	public ScriptSourceIndexBuilder(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	public ScriptSourceIndexBuildResult Build()
	{
		string scriptMetadataDir = Path.Combine(_options.OutputPath, "facts", "script_metadata");
		string assembliesDir = Path.Combine(_options.OutputPath, "facts", "assemblies");
		string scriptsDir = Path.Combine(_options.OutputPath, "scripts");
		string astDir = Path.Combine(_options.OutputPath, "ast");

		EnsureRequiredDirectory(scriptMetadataDir, "facts/script_metadata");
		EnsureRequiredDirectory(assembliesDir, "facts/assemblies");
		EnsureRequiredDirectory(scriptsDir, "scripts");
		EnsureRequiredDirectory(astDir, "ast");

		Dictionary<string, ScriptInfo> scriptInfoMap = BuildScriptInfoMap(scriptMetadataDir);
		Logger.Info(LogCategory.Export, $"Loaded script info map with {scriptInfoMap.Count} entries from script_metadata");

		string[] sourceFiles = Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories);
		Logger.Info(LogCategory.Export, $"Found {sourceFiles.Length} source files");

		List<ScriptSourceRecordWithKey> records = new(sourceFiles.Length);
		List<string> missingAst = new();
		List<string> invalidAst = new();
		int matchedScripts = 0;
		int astValidated = 0;

		foreach (string sourceFile in sourceFiles.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
		{
			string typeFullName;
			try
			{
				typeFullName = InferTypeFullNameFromPath(sourceFile, scriptsDir);
			}
			catch (Exception ex)
			{
				Logger.Verbose(LogCategory.Export, $"Failed to infer type name from {sourceFile}: {ex.Message}");
				continue;
			}

			if (!scriptInfoMap.TryGetValue(typeFullName, out ScriptInfo? scriptInfo))
			{
				continue;
			}

			ScriptSourceRecord record = CreateSourceRecord(sourceFile, scriptInfo);
			records.Add(new ScriptSourceRecordWithKey(record, record.Pk));
			matchedScripts++;

			string? astValidationError = ValidateAuthoritativeAst(record);
			if (astValidationError is null)
			{
				astValidated++;
			}
			else if (record.HasAst == true)
			{
				invalidAst.Add($"{record.SourcePath}: {astValidationError}");
			}
			else
			{
				missingAst.Add($"{record.SourcePath}: {astValidationError}");
			}
		}

		return new ScriptSourceIndexBuildResult(
			records,
			sourceFiles.Length,
			matchedScripts,
			astValidated,
			missingAst,
			invalidAst);
	}

	private static void EnsureRequiredDirectory(string path, string logicalName)
	{
		if (!Directory.Exists(path))
		{
			throw new DirectoryNotFoundException($"Required dump input '{logicalName}' not found at: {path}");
		}
	}

	private static Dictionary<string, ScriptInfo> BuildScriptInfoMap(string scriptMetadataDir)
	{
		Dictionary<string, ScriptInfo> map = new(StringComparer.Ordinal);
		List<string> duplicates = new();
		foreach (string shardPath in Directory.EnumerateFiles(scriptMetadataDir, "*.ndjson", SearchOption.AllDirectories))
		{
			using StreamReader reader = new StreamReader(shardPath, Encoding.UTF8);
			string? line;
			while ((line = reader.ReadLine()) != null)
			{
				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}

				JObject json = JObject.Parse(line);
				string? fullName = json["fullName"]?.Value<string>();
				string? scriptGuid = json["scriptGuid"]?.Value<string>();
				string? scriptPk = json["pk"]?.Value<string>();
				string? assemblyGuid = json["assemblyGuid"]?.Value<string>();
				if (string.IsNullOrWhiteSpace(fullName) ||
					string.IsNullOrWhiteSpace(scriptGuid) ||
					string.IsNullOrWhiteSpace(scriptPk) ||
					string.IsNullOrWhiteSpace(assemblyGuid))
				{
					continue;
				}

				ScriptInfo incoming = new ScriptInfo(scriptGuid, scriptPk, assemblyGuid);
				if (map.TryGetValue(fullName, out ScriptInfo? existing))
				{
					if (!existing.IsSameIdentity(incoming))
					{
						duplicates.Add(
							$"{fullName}: existing(scriptGuid={existing.ScriptGuid}, scriptPk={existing.ScriptPk}, assemblyGuid={existing.AssemblyGuid}) " +
							$"vs incoming(scriptGuid={incoming.ScriptGuid}, scriptPk={incoming.ScriptPk}, assemblyGuid={incoming.AssemblyGuid})");
					}

					continue;
				}

				map[fullName] = incoming;
			}
		}

		if (duplicates.Count > 0)
		{
			string detail = string.Join(Environment.NewLine, duplicates.Take(10));
			if (duplicates.Count > 10)
			{
				detail += Environment.NewLine + $"... and {duplicates.Count - 10} more duplicate fullName conflict(s)";
			}

			throw new InvalidOperationException(
				"Ambiguous script_metadata fullName entries detected. Dump-backed script_sources requires unique authoritative fullName identities." +
				Environment.NewLine +
				detail);
		}

		return map;
	}

	private ScriptSourceRecord CreateSourceRecord(string sourceFile, ScriptInfo scriptInfo)
	{
		string relativePath = OutputPathHelper.NormalizeRelativePath(Path.GetRelativePath(_options.OutputPath, sourceFile));
		FileInfo fileInfo = new FileInfo(sourceFile);

		ScriptSourceRecord record = new ScriptSourceRecord
		{
			Domain = "script_sources",
			Pk = scriptInfo.ScriptGuid,
			ScriptPk = scriptInfo.ScriptPk,
			AssemblyGuid = scriptInfo.AssemblyGuid,
			SourcePath = relativePath,
			SourceSize = fileInfo.Length,
			LineCount = CountLines(sourceFile),
			CharacterCount = (int)Math.Min(fileInfo.Length, int.MaxValue),
			Sha256 = ComputeSha256(sourceFile),
			Language = "CSharp",
			Decompiler = "ICSharpCode.Decompiler",
			DecompilerVersion = GetDecompilerVersion(),
			DecompilationStatus = "success",
			IsPresent = true,
			IsEmpty = false
		};

		string astPath = GetPotentialAstPath(relativePath);
		string fullAstPath = Path.Combine(_options.OutputPath, astPath);
		if (File.Exists(fullAstPath))
		{
			record.HasAst = true;
			record.AstPath = astPath;
		}
		else
		{
			record.HasAst = false;
			record.AstPath = astPath;
		}

		return record;
	}

	private static string InferTypeFullNameFromPath(string filePath, string scriptsRoot)
	{
		string relativePath = Path.GetRelativePath(scriptsRoot, filePath);
		string[] parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		if (parts.Length < 2)
		{
			return Path.GetFileNameWithoutExtension(parts[^1]);
		}

		string[] namespaceParts = parts.Skip(1).Take(parts.Length - 2).ToArray();
		string className = Path.GetFileNameWithoutExtension(parts[^1]);
		return namespaceParts.Length > 0
			? string.Join(".", namespaceParts) + "." + className
			: className;
	}

	private static int CountLines(string filePath)
	{
		int count = 0;
		using StreamReader reader = new StreamReader(filePath);
		while (reader.ReadLine() != null)
		{
			count++;
		}
		return count;
	}

	private static string ComputeSha256(string filePath)
	{
		using FileStream stream = File.OpenRead(filePath);
		using SHA256 sha256 = SHA256.Create();
		byte[] hashBytes = sha256.ComputeHash(stream);
		return Convert.ToHexString(hashBytes).ToLowerInvariant();
	}

	private static string GetDecompilerVersion()
	{
		try
		{
			System.Reflection.AssemblyName? ilspyAssembly = AppDomain.CurrentDomain.GetAssemblies()
				.Select(a => a.GetName())
				.FirstOrDefault(a => a.Name?.Contains("Decompiler", StringComparison.OrdinalIgnoreCase) == true);
			return ilspyAssembly?.Version?.ToString() ?? "Unknown";
		}
		catch
		{
			return "Unknown";
		}
	}

	private static string GetPotentialAstPath(string sourcePath)
	{
		string directory = Path.GetDirectoryName(sourcePath) ?? "";
		string fileName = Path.GetFileNameWithoutExtension(sourcePath) + ".json";
		if (directory.StartsWith("scripts", StringComparison.OrdinalIgnoreCase))
		{
			directory = "ast" + directory.Substring(7);
		}

		return OutputPathHelper.NormalizeRelativePath(Path.Combine(directory, fileName));
	}

	private string? ValidateAuthoritativeAst(ScriptSourceRecord record)
	{
		if (record.HasAst != true || string.IsNullOrWhiteSpace(record.AstPath))
		{
			return "missing AST output";
		}

		string fullAstPath = OutputPathHelper.ResolveAbsolutePath(_options.OutputPath, record.AstPath);
		if (!File.Exists(fullAstPath))
		{
			return $"AST file not found at {record.AstPath}";
		}

		string? astFileName = ExtractAstFileName(fullAstPath);
		if (string.IsNullOrWhiteSpace(astFileName))
		{
			return "AST JSON missing FileName";
		}

		string canonicalAstSourcePath = NormalizeAstFileName(astFileName);
		if (!string.Equals(canonicalAstSourcePath, record.SourcePath, StringComparison.Ordinal))
		{
			return $"AST FileName '{canonicalAstSourcePath}' does not match sourcePath '{record.SourcePath}'";
		}

		string expectedAstPath = GetPotentialAstPath(record.SourcePath);
		if (!string.Equals(expectedAstPath, record.AstPath, StringComparison.Ordinal))
		{
			return $"AST path '{record.AstPath}' does not match canonical path '{expectedAstPath}'";
		}

		return null;
	}

	private static string? ExtractAstFileName(string astPath)
	{
		const int tailWindowBytes = 32768;
		using FileStream stream = File.OpenRead(astPath);
		long windowStart = Math.Max(0, stream.Length - tailWindowBytes);
		stream.Seek(windowStart, SeekOrigin.Begin);

		using StreamReader reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
		string tail = reader.ReadToEnd();
		Match match = Regex.Match(tail, "\"FileName\"\\s*:\\s*\"(?<path>(?:\\\\.|[^\"])*)\"", RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			return null;
		}

		string encoded = "\"" + match.Groups["path"].Value + "\"";
		return JsonConvert.DeserializeObject<string>(encoded);
	}

	private string NormalizeAstFileName(string fileName)
	{
		string normalized = OutputPathHelper.NormalizeRelativePath(fileName);
		if (Path.IsPathRooted(fileName))
		{
			string fullPath = Path.GetFullPath(fileName);
			string outputRoot = Path.GetFullPath(_options.OutputPath);
			if (fullPath.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
			{
				normalized = OutputPathHelper.NormalizeRelativePath(Path.GetRelativePath(outputRoot, fullPath));
			}
		}

		return normalized;
	}
}

internal sealed class ScriptSourceIndexBuildResult
{
	public ScriptSourceIndexBuildResult(
		IReadOnlyList<ScriptSourceRecordWithKey> records,
		int sourceFileCount,
		int matchedScripts,
		int astValidatedCount,
		IReadOnlyList<string> missingAst,
		IReadOnlyList<string> invalidAst)
	{
		Records = records;
		SourceFileCount = sourceFileCount;
		MatchedScripts = matchedScripts;
		AstValidatedCount = astValidatedCount;
		MissingAst = missingAst;
		InvalidAst = invalidAst;
	}

	public IReadOnlyList<ScriptSourceRecordWithKey> Records { get; }
	public int SourceFileCount { get; }
	public int MatchedScripts { get; }
	public int UnmatchedFiles => SourceFileCount - MatchedScripts;
	public int AstValidatedCount { get; }
	public IReadOnlyList<string> MissingAst { get; }
	public IReadOnlyList<string> InvalidAst { get; }
}

internal sealed class ScriptInfo
{
	public ScriptInfo(string scriptGuid, string scriptPk, string assemblyGuid)
	{
		ScriptGuid = scriptGuid;
		ScriptPk = scriptPk;
		AssemblyGuid = assemblyGuid;
	}

	public string ScriptGuid { get; }
	public string ScriptPk { get; }
	public string AssemblyGuid { get; }

	public bool IsSameIdentity(ScriptInfo other)
	{
		if (other is null)
		{
			return false;
		}

		return string.Equals(ScriptGuid, other.ScriptGuid, StringComparison.Ordinal) &&
			string.Equals(ScriptPk, other.ScriptPk, StringComparison.Ordinal) &&
			string.Equals(AssemblyGuid, other.AssemblyGuid, StringComparison.Ordinal);
	}
}

internal sealed class ScriptSourceRecordWithKey
{
	public ScriptSourceRecordWithKey(ScriptSourceRecord record, string pk)
	{
		Record = record;
		Pk = pk;
	}

	public ScriptSourceRecord Record { get; }
	public string Pk { get; }
}
