using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Models.Common;
using AssetRipper.Tools.AssetDumper.Writers;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace AssetRipper.Tools.AssetDumper.Exporters.Facts;

/// <summary>
/// Exports assembly facts to NDJSON shards for the assemblies domain.
/// </summary>
internal sealed class AssemblyExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public AssemblyExporter(Options options, CompressionKind compressionKind, bool enableIndex)
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
	/// Exports all assembly facts to NDJSON shards.
	/// Returns shard descriptors for manifest generation.
	/// </summary>
	public DomainExportResult ExportAssemblies(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting assembly facts...");

		// Validate assembly manager availability
		if (gameData.AssemblyManager?.IsSet != true)
		{
			Logger.Warning(LogCategory.Export, "Assembly manager not available. Skipping assembly export.");
			return CreateEmptyResult();
		}

		List<AssemblyDefinition> assemblies = gameData.AssemblyManager.GetAssemblies().ToList();
		int totalAssemblies = assemblies.Count;

		Logger.Info(LogCategory.Export, $"Found {totalAssemblies} assemblies");

		if (totalAssemblies == 0)
		{
			return CreateEmptyResult();
		}

		// Assemblies are typically small in number, so single shard is usually fine
		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 5000;
		long maxBytesPerShard = 50 * 1024 * 1024; // 50MB per shard

		DomainExportResult result = new DomainExportResult(
			domain: "assemblies",
			tableId: "facts/assemblies",
			schemaPath: "Schemas/v2/facts/assemblies.schema.json");

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

		try
		{
			// Build MonoScript count index
			Dictionary<string, int> scriptCountsByAssembly = BuildScriptCountIndex(gameData);

			// Process assemblies (can be parallelized but typically small count)
			List<AssemblyFactRecordWithKey> recordsWithKeys = ParallelProcessor.ProcessInParallelWithNulls(
				assemblies,
				assembly =>
				{
					try
					{
						string assemblyGuid = ComputeAssemblyGuid(assembly);
						AssemblyRecord record = CreateAssemblyRecord(
							assembly, 
							assemblyGuid, 
							gameData, 
							scriptCountsByAssembly);
						return new AssemblyFactRecordWithKey(record, assemblyGuid);
					}
					catch (Exception ex)
					{
						Logger.Warning(LogCategory.Export, $"Failed to export assembly {assembly.Name}: {ex.Message}");
						return null;
					}
				},
				maxParallelism: 0); // 0 = auto-detect based on CPU cores

			// Sequential write phase
			foreach (AssemblyFactRecordWithKey item in recordsWithKeys)
			{
				string? indexKey = _enableIndex ? item.AssemblyGuid : null;
				writer.WriteRecord(item.Record, item.AssemblyGuid, indexKey);
				totalExported++;
			}
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

		Logger.Info(LogCategory.Export, $"Exported {totalExported} assembly records across {writer.ShardCount} shards");

		return result;
	}

	private Dictionary<string, int> BuildScriptCountIndex(GameData gameData)
	{
		Dictionary<string, int> counts = new();

		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			foreach (IMonoScript script in collection.OfType<IMonoScript>())
			{
				string assemblyName = script.GetAssemblyNameFixed();
				counts.TryGetValue(assemblyName, out int count);
				counts[assemblyName] = count + 1;
			}
		}

		return counts;
	}

	private AssemblyRecord CreateAssemblyRecord(
		AssemblyDefinition assembly,
		string assemblyGuid,
		GameData gameData,
		Dictionary<string, int> scriptCountsByAssembly)
	{
		string assemblyName = GetAssemblyName(assembly);

		AssemblyRecord record = new AssemblyRecord
		{
			Domain = "assemblies",
			Pk = assemblyGuid,
			Name = assemblyName,
			FullName = assembly.FullName,
			Version = assembly.Version?.ToString(),
			ScriptingBackend = gameData.AssemblyManager.ScriptingBackend.ToString(),
			AssemblyType = DetermineAssemblyType(assembly, assemblyName),
			TypeCount = CountTypes(assembly),
			ScriptCount = scriptCountsByAssembly.TryGetValue(assemblyName, out int count) ? count : 0,
			IsDynamic = false, // AsmResolver doesn't expose IsDynamic directly
			IsEditor = IsEditorAssembly(assembly),
			Platform = gameData.PlatformStructure?.Name
		};

		// Extract target framework
		TrySetTargetFramework(assembly, record);

		// Try to link to exported DLL if available
		TrySetDllInfo(assembly, record);

		// Extract assembly references
		TrySetReferences(assembly, record);

		// Extract mscorlib version for .NET version tracking
		TrySetMscorlibVersion(assembly, record);

		// Note: exportType and isModified are optional fields that would require
		// access to AssetRipper's export configuration and modification tracking.
		// These fields remain null for now and can be populated by post-processing
		// or when export context is available.

		return record;
	}

	private static string DetermineAssemblyType(AssemblyDefinition assembly, string assemblyName)
	{
		// Predefined Unity assemblies
		if (assemblyName.StartsWith("Assembly-CSharp", StringComparison.OrdinalIgnoreCase) ||
		    assemblyName.Equals("Assembly-UnityScript", StringComparison.OrdinalIgnoreCase) ||
		    assemblyName.Equals("Assembly-Boo", StringComparison.OrdinalIgnoreCase))
		{
			return "Predefined";
		}

		// Unity engine assemblies
		if (assemblyName.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase))
		{
			return "UnityEngine";
		}

		// Unity extensions and packages
		if (assemblyName.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase) ||
		    assemblyName.StartsWith("UnityEditor.", StringComparison.OrdinalIgnoreCase))
		{
			return "UnityExtension";
		}

		// System assemblies
		if (assemblyName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
		    assemblyName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
		    assemblyName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
		{
			return "System";
		}

		// Default to User
		return "User";
	}

	private static void TrySetReferences(AssemblyDefinition assembly, AssemblyRecord record)
	{
		try
		{
			List<string> references = new();
			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (AssemblyReference reference in module.AssemblyReferences)
				{
					if (!string.IsNullOrEmpty(reference.Name) && !references.Contains(reference.Name))
					{
						references.Add(reference.Name);
					}
				}
			}

			if (references.Count > 0)
			{
				record.References = references;
			}
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Failed to extract references for assembly {assembly.Name}: {ex.Message}");
		}
	}

	private static void TrySetMscorlibVersion(AssemblyDefinition assembly, AssemblyRecord record)
	{
		try
		{
			// Look for mscorlib reference in all modules
			AssemblyReference? mscorlibRef = assembly.Modules
				.SelectMany(m => m.AssemblyReferences)
				.FirstOrDefault(r => r.Name == "mscorlib");

			if (mscorlibRef?.Version != null)
			{
				// Store major version only (e.g., 2 for .NET 2.0/3.5, 4 for .NET 4.x)
				record.MscorlibVersion = mscorlibRef.Version.Major;
			}
		}
		catch (Exception ex)
		{
			Logger.Verbose(LogCategory.Export, $"Could not determine mscorlib version for {assembly.Name}: {ex.Message}");
		}
	}

	private static string GetAssemblyName(AssemblyDefinition assembly)
	{
		return assembly.Name ?? "Unknown";
	}

	private static int CountTypes(AssemblyDefinition assembly)
	{
		int count = 0;
		foreach (ModuleDefinition module in assembly.Modules)
		{
			count += module.TopLevelTypes.Count;
		}
		return count;
	}

	private static bool IsEditorAssembly(AssemblyDefinition assembly)
	{
		string name = assembly.Name ?? string.Empty;
		return name.Contains("Editor", StringComparison.OrdinalIgnoreCase) ||
		       name.Equals("UnityEditor", StringComparison.OrdinalIgnoreCase);
	}

	private static void TrySetTargetFramework(AssemblyDefinition assembly, AssemblyRecord record)
	{
		try
		{
			// Try to extract target framework from custom attributes
			CustomAttribute? targetFrameworkAttr = assembly.CustomAttributes
				.FirstOrDefault(a => a.Constructor?.DeclaringType?.Name == "TargetFrameworkAttribute");

			if (targetFrameworkAttr?.Signature?.FixedArguments.Count > 0)
			{
				object? frameworkValue = targetFrameworkAttr.Signature.FixedArguments[0].Element;
				if (frameworkValue is string frameworkName)
				{
					record.TargetFramework = frameworkName;
					return;
				}
			}

			// Fallback: infer from mscorlib/System.Runtime references
			AssemblyReference? mscorlibRef = assembly.Modules.FirstOrDefault()?.AssemblyReferences
				.FirstOrDefault(r => r.Name == "mscorlib" || r.Name == "System.Runtime");

			if (mscorlibRef != null)
			{
				Version? version = mscorlibRef.Version;
				if (version?.Major == 4)
					record.TargetFramework = "net4x";
				else if (version?.Major == 2)
					record.TargetFramework = "net2.0";
				else
					record.TargetFramework = "netstandard2.1";
			}
		}
		catch (Exception ex)
		{
			Logger.Verbose(LogCategory.Export, $"Could not determine target framework for {assembly.Name}: {ex.Message}");
		}
	}

	private void TrySetDllInfo(AssemblyDefinition assembly, AssemblyRecord record)
	{
		try
		{
			string dllFileName = $"{GetAssemblyName(assembly)}.dll";
			string dllPath = Path.Combine("Assemblies", dllFileName);
			string fullDllPath = Path.Combine(_options.OutputPath, dllPath);

			if (File.Exists(fullDllPath))
			{
				FileInfo fileInfo = new FileInfo(fullDllPath);
				record.DllPath = dllPath;
				record.DllSize = fileInfo.Length;
				record.DllSha256 = ComputeSha256(fullDllPath);
			}
		}
		catch (Exception ex)
		{
			Logger.Verbose(LogCategory.Export, $"Could not read DLL info for {assembly.Name}: {ex.Message}");
		}
	}

	private static string ComputeAssemblyGuid(AssemblyDefinition assembly)
	{
		// Use SHA256 hash of assembly name for stable GUID generation
		// This matches the logic used in ScriptHashing for consistency
		string name = GetAssemblyName(assembly);
		using var hash = SHA256.Create();
		byte[] hashBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(name));
		
		// Convert first 16 bytes to GUID format, then to uppercase hex string (32 chars)
		return new Guid(hashBytes.Take(16).ToArray()).ToString("N").ToUpperInvariant();
	}

	private static string ComputeSha256(string filePath)
	{
		using FileStream stream = File.OpenRead(filePath);
		using SHA256 sha256 = SHA256.Create();
		byte[] hashBytes = sha256.ComputeHash(stream);
		return Convert.ToHexString(hashBytes).ToLowerInvariant();
	}

	private DomainExportResult CreateEmptyResult()
	{
		return new DomainExportResult(
			domain: "assemblies",
			tableId: "facts/assemblies",
			schemaPath: "Schemas/v2/facts/assemblies.schema.json");
	}
}

/// <summary>
/// Helper class to hold assembly record with its GUID for parallel processing.
/// </summary>
internal sealed class AssemblyFactRecordWithKey
{
	public AssemblyRecord Record { get; }
	public string AssemblyGuid { get; }

	public AssemblyFactRecordWithKey(AssemblyRecord record, string assemblyGuid)
	{
		Record = record;
		AssemblyGuid = assemblyGuid;
	}
}
