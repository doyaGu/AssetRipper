using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Writers;
using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Import.Structure.Assembly;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace AssetRipper.Tools.AssetDumper.Exporters.Records;

/// <summary>
/// Exports script source facts to NDJSON shards for the script_sources domain.
/// Links decompiled source files to MonoScript assets.
/// </summary>
internal sealed class ScriptSourceExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public ScriptSourceExporter(Options options, CompressionKind compressionKind, bool enableIndex)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
		_compressionKind = compressionKind;
		_enableIndex = enableIndex;
	}

	/// <summary>
	/// Exports all script source records to NDJSON shards.
	/// Returns shard descriptors for manifest generation.
	/// </summary>
	public DomainExportResult ExportSources(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting script sources...");

		string scriptsDir = Path.Combine(_options.OutputPath, "Scripts");
		if (!Directory.Exists(scriptsDir))
		{
			Logger.Info(LogCategory.Export, "Scripts directory not found. Skipping script source export.");
			return CreateEmptyResult();
		}

		// Build MonoScript → GUID mapping
		Dictionary<string, ScriptInfo> scriptInfoMap = BuildScriptInfoMap(gameData);
		Logger.Info(LogCategory.Export, $"Built script info map with {scriptInfoMap.Count} entries");

		// Scan all source files
		string[] sourceFiles = Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories);
		Logger.Info(LogCategory.Export, $"Found {sourceFiles.Length} source files");

		if (sourceFiles.Length == 0)
		{
			return CreateEmptyResult();
		}

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 20000;
		long maxBytesPerShard = 50 * 1024 * 1024; // 50MB per shard

		DomainExportResult result = new DomainExportResult(
			domain: "script_sources",
			tableId: "facts/script_sources",
			schemaPath: "Schemas/v2/facts/script_sources.schema.json");

		ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard,
			maxBytesPerShard,
			_compressionKind,
			collectIndexEntries: _enableIndex,
			descriptorDomain: result.TableId);

		int totalExported = 0;
		int matchedScripts = 0;
		int unmatchedFiles = 0;

		try
		{
			// Process source files with parallel processing
			List<ScriptSourceRecordWithKey> recordsWithKeys = ParallelProcessor.ProcessInParallelWithNulls(
				sourceFiles,
				sourceFile =>
				{
					try
					{
						string typeFullName = InferTypeFullNameFromPath(sourceFile, scriptsDir);
						
						if (scriptInfoMap.TryGetValue(typeFullName, out ScriptInfo? scriptInfo))
						{
							ScriptSourceRecord record = CreateSourceRecord(sourceFile, scriptsDir, scriptInfo);
							return new ScriptSourceRecordWithKey(record, record.Pk);
						}
						else
						{
							// File doesn't match any known MonoScript
							return null;
						}
					}
					catch (Exception ex)
					{
						Logger.Verbose(LogCategory.Export, $"Failed to process source file {sourceFile}: {ex.Message}");
						return null;
					}
				},
				maxParallelism: 0); // 0 = auto-detect based on CPU cores

			// Sequential write phase
			foreach (ScriptSourceRecordWithKey item in recordsWithKeys)
			{
				matchedScripts++;
				string? indexKey = _enableIndex ? item.Pk : null;
				writer.WriteRecord(item.Record, item.Pk, indexKey);
				totalExported++;
			}

			unmatchedFiles = sourceFiles.Length - matchedScripts;
		}
		finally
		{
			writer.Dispose();
		}

		result.Shards.AddRange(writer.ShardDescriptors);
		if (_enableIndex)
		{
			result.IndexEntries.AddRange(writer.IndexEntries);
		}

		Logger.Info(LogCategory.Export, $"Exported {totalExported} script source records across {writer.ShardCount} shards");
		Logger.Info(LogCategory.Export, $"Matched: {matchedScripts}, Unmatched: {unmatchedFiles}");

		return result;
	}

	private Dictionary<string, ScriptInfo> BuildScriptInfoMap(GameData gameData)
	{
		Dictionary<string, ScriptInfo> map = new();

		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			string collectionId = ExportHelper.ComputeCollectionId(collection);

			foreach (IMonoScript script in collection.OfType<IMonoScript>())
			{
				try
				{
					string fullName = script.GetFullName();
					string scriptGuid = ScriptHashing.CalculateScriptGuid(script).ToString();
					string scriptPk = StableKeyHelper.Create(collectionId, script.PathID);
					string assemblyName = script.GetAssemblyNameFixed();
					string assemblyGuid = ComputeAssemblyGuid(assemblyName);

					// Use full name as key (last wins if duplicates)
					map[fullName] = new ScriptInfo(scriptGuid, scriptPk, assemblyGuid);
				}
				catch (Exception ex)
				{
					Logger.Verbose(LogCategory.Export, $"Failed to index script {script.ClassName}: {ex.Message}");
				}
			}
		}

		return map;
	}

	private ScriptSourceRecord CreateSourceRecord(string sourceFile, string scriptsDir, ScriptInfo scriptInfo)
	{
		string relativePath = Path.GetRelativePath(_options.OutputPath, sourceFile);
		FileInfo fileInfo = new FileInfo(sourceFile);
		int lineCount = CountLines(sourceFile);
		string sha256 = ComputeSha256(sourceFile);

		ScriptSourceRecord record = new ScriptSourceRecord
		{
			Domain = "script_sources",
			Pk = scriptInfo.ScriptGuid,
			ScriptPk = scriptInfo.ScriptPk,
			AssemblyGuid = scriptInfo.AssemblyGuid,
			SourcePath = relativePath,
			SourceSize = fileInfo.Length,
			LineCount = lineCount,
			CharacterCount = (int)Math.Min(fileInfo.Length, int.MaxValue),
			Sha256 = sha256,
			Language = "CSharp",
			Decompiler = "ICSharpCode.Decompiler",
			DecompilerVersion = GetDecompilerVersion()
		};

		// Check for AST file
		string astPath = GetPotentialAstPath(relativePath);
		string fullAstPath = Path.Combine(_options.OutputPath, astPath);
		if (File.Exists(fullAstPath))
		{
			record.HasAst = true;
			record.AstPath = astPath;
		}

		return record;
	}

	private string InferTypeFullNameFromPath(string filePath, string scriptsRoot)
	{
		// Example: Scripts/Assembly-CSharp/Game/Player/PlayerController.cs
		// → Game.Player.PlayerController

		string relativePath = Path.GetRelativePath(scriptsRoot, filePath);
		string[] parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

		if (parts.Length < 2)
		{
			// Just filename, no namespace
			return Path.GetFileNameWithoutExtension(parts[^1]);
		}

		// Skip assembly directory (parts[0])
		// Take all intermediate directories as namespace parts
		// Last part is the class name (without extension)
		string[] namespaceParts = parts.Skip(1).Take(parts.Length - 2).ToArray();
		string className = Path.GetFileNameWithoutExtension(parts[^1]);

		if (namespaceParts.Length > 0)
		{
			return string.Join(".", namespaceParts) + "." + className;
		}
		else
		{
			return className;
		}
	}

	private static int CountLines(string filePath)
	{
		try
		{
			int count = 0;
			using StreamReader reader = new StreamReader(filePath);
			while (reader.ReadLine() != null)
			{
				count++;
			}
			return count;
		}
		catch
		{
			return 0;
		}
	}

	private static string ComputeSha256(string filePath)
	{
		using FileStream stream = File.OpenRead(filePath);
		using SHA256 sha256 = SHA256.Create();
		byte[] hashBytes = sha256.ComputeHash(stream);
		return Convert.ToHexString(hashBytes).ToLowerInvariant();
	}

	private static string ComputeAssemblyGuid(string assemblyName)
	{
		using SHA256 hash = SHA256.Create();
		byte[] hashBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(assemblyName));
		return new Guid(hashBytes.Take(16).ToArray()).ToString("N").ToUpperInvariant();
	}

	private static string GetDecompilerVersion()
	{
		// Try to get ILSpy decompiler version
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
		// Convert Scripts/... to AST/...
		string directory = Path.GetDirectoryName(sourcePath) ?? "";
		string fileName = Path.GetFileNameWithoutExtension(sourcePath) + ".json";
		
		if (directory.StartsWith("Scripts", StringComparison.OrdinalIgnoreCase))
		{
			directory = "AST" + directory.Substring(7);
		}

		return Path.Combine(directory, fileName);
	}

	private DomainExportResult CreateEmptyResult()
	{
		return new DomainExportResult(
			domain: "script_sources",
			tableId: "facts/script_sources",
			schemaPath: "Schemas/v2/facts/script_sources.schema.json");
	}
}

/// <summary>
/// Helper class to hold script identification info.
/// </summary>
internal sealed class ScriptInfo
{
	public string ScriptGuid { get; }
	public string ScriptPk { get; }
	public string AssemblyGuid { get; }

	public ScriptInfo(string scriptGuid, string scriptPk, string assemblyGuid)
	{
		ScriptGuid = scriptGuid;
		ScriptPk = scriptPk;
		AssemblyGuid = assemblyGuid;
	}
}

/// <summary>
/// Helper class to hold source record with its PK for parallel processing.
/// </summary>
internal sealed class ScriptSourceRecordWithKey
{
	public ScriptSourceRecord Record { get; }
	public string Pk { get; }

	public ScriptSourceRecordWithKey(ScriptSourceRecord record, string pk)
	{
		Record = record;
		Pk = pk;
	}
}
