using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Writers;
using AsmResolver.DotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace AssetRipper.Tools.AssetDumper.Exporters.Records;

/// <summary>
/// Exports assembly dependency relationships for dependency graph analysis.
/// Phase B exporter that tracks references between assemblies.
/// </summary>
internal sealed class AssemblyDependencyExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public AssemblyDependencyExporter(Options options, CompressionKind compressionKind, bool enableIndex)
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
	/// Exports assembly dependency relationships to NDJSON format.
	/// </summary>
	public DomainExportResult ExportDependencies(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting assembly dependencies...");

		// Validate assembly manager
		if (gameData.AssemblyManager?.IsSet != true)
		{
			Logger.Warning(LogCategory.Export, "Assembly manager not available. Skipping dependency export.");
			return CreateEmptyResult();
		}

		// Build assembly GUID index for fast lookups
		Dictionary<string, string> assemblyNameToGuid = BuildAssemblyIndex(gameData.AssemblyManager);

		// Collect all dependencies
		List<AssemblyDependencyRecord> dependencies = new List<AssemblyDependencyRecord>();
		foreach (AssemblyDefinition assembly in gameData.AssemblyManager.GetAssemblies())
		{
			List<AssemblyDependencyRecord> assemblyDeps = ExtractDependencies(assembly, assemblyNameToGuid);
			dependencies.AddRange(assemblyDeps);
		}

		Logger.Info(LogCategory.Export, $"Collected {dependencies.Count} assembly dependencies");

		if (dependencies.Count == 0)
		{
			return CreateEmptyResult();
		}

		// Setup writer
		DomainExportResult result = new DomainExportResult(
			domain: "assembly_dependencies",
			tableId: "relations/assembly_dependencies",
			schemaPath: "Schemas/v2/relations/assembly_dependencies.schema.json");

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 10000;
		long maxBytesPerShard = 50 * 1024 * 1024;

		ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard,
			maxBytesPerShard,
			_compressionKind,
			collectIndexEntries: _enableIndex,
			descriptorDomain: result.TableId);

		try
		{
			foreach (AssemblyDependencyRecord record in dependencies)
			{
				string pk = $"{record.SourceAssembly}->{record.TargetName}";
				string? indexKey = _enableIndex ? pk : null;
				writer.WriteRecord(record, pk, indexKey);
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

		Logger.Info(LogCategory.Export, $"Exported {dependencies.Count} assembly dependencies across {writer.ShardCount} shards");
		return result;
	}

	private DomainExportResult CreateEmptyResult()
	{
		return new DomainExportResult(
			domain: "assembly_dependencies",
			tableId: "relations/assembly_dependencies",
			schemaPath: "Schemas/v2/relations/assembly_dependencies.schema.json");
	}

	/// <summary>
	/// Builds a map from assembly names to their GUIDs for efficient lookups.
	/// </summary>
	private Dictionary<string, string> BuildAssemblyIndex(IAssemblyManager assemblyManager)
	{
		Dictionary<string, string> index = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (AssemblyDefinition assembly in assemblyManager.GetAssemblies())
		{
			if (assembly.Modules.Count > 0)
			{
				ModuleDefinition module = assembly.Modules[0];
				string assemblyGuid = ComputeAssemblyGuid(module);
				string assemblyName = assembly.Name?.Value ?? "Unknown";

				index[assemblyName] = assemblyGuid;
			}
		}

		return index;
	}

	/// <summary>
	/// Extracts all dependencies for a given assembly.
	/// </summary>
	private List<AssemblyDependencyRecord> ExtractDependencies(
		AssemblyDefinition assembly,
		Dictionary<string, string> assemblyIndex)
	{
		List<AssemblyDependencyRecord> deps = new List<AssemblyDependencyRecord>();

		if (assembly.Modules.Count == 0)
		{
			return deps;
		}

		ModuleDefinition module = assembly.Modules[0];
		string sourceGuid = ComputeAssemblyGuid(module);
		string? sourceModule = module.Name?.Value;

		// Process assembly references
		foreach (AssemblyReference assemblyRef in module.AssemblyReferences)
		{
			string? refName = assemblyRef.Name?.Value;
			if (string.IsNullOrEmpty(refName))
			{
				continue;
			}

			// Try to resolve the target assembly GUID
			string? targetGuid = null;
			string? failureReason = null;
			if (assemblyIndex.TryGetValue(refName, out string? resolvedGuid))
			{
				targetGuid = resolvedGuid;
			}
			else
			{
				failureReason = $"Assembly '{refName}' not found in loaded assemblies";
			}

			// Classify dependency type
			string dependencyType = DetermineDependencyType(refName);
			bool? isFrameworkAssembly = ClassifyAsFrameworkAssembly(refName);

			// Extract version info
			string? version = assemblyRef.Version?.ToString();

			// Extract public key token (8 bytes as 16-character hex string)
			string? publicKeyToken = null;
			if (assemblyRef.HasPublicKey)
			{
				byte[]? token = assemblyRef.GetPublicKeyToken();
				if (token != null && token.Length == 8)
				{
					publicKeyToken = BitConverter.ToString(token).Replace("-", "").ToLowerInvariant();
				}
			}

			// Extract culture info
			string? culture = assemblyRef.Culture?.Value;
			if (string.IsNullOrEmpty(culture) || culture == "neutral")
			{
				culture = "neutral";
			}

			AssemblyDependencyRecord record = new AssemblyDependencyRecord
			{
				SourceAssembly = sourceGuid,
				SourceModule = sourceModule,
				TargetAssembly = targetGuid,
				TargetName = refName,
				Version = version,
				PublicKeyToken = publicKeyToken,
				Culture = culture,
				IsResolved = targetGuid != null,
				DependencyType = dependencyType,
				IsFrameworkAssembly = isFrameworkAssembly,
				FailureReason = failureReason
			};

			deps.Add(record);
		}

		return deps;
	}

	/// <summary>
	/// Determines the type of dependency based on assembly name patterns.
	/// </summary>
	/// <param name="assemblyName">The assembly name to classify.</param>
	/// <returns>Dependency type: "direct", "framework", "plugin", or "unknown".</returns>
	private static string DetermineDependencyType(string assemblyName)
	{
		// Framework assemblies (.NET BCL)
		if (assemblyName.StartsWith("System", StringComparison.Ordinal) ||
		    assemblyName.StartsWith("mscorlib", StringComparison.Ordinal) ||
		    assemblyName.StartsWith("netstandard", StringComparison.Ordinal) ||
		    assemblyName.StartsWith("Microsoft.", StringComparison.Ordinal))
		{
			return "framework";
		}

		// Unity framework assemblies
		if (assemblyName.StartsWith("UnityEngine", StringComparison.Ordinal) ||
		    assemblyName.StartsWith("UnityEditor", StringComparison.Ordinal))
		{
			return "framework";
		}

		// Plugin assemblies
		if (assemblyName.Contains("Plugin") || 
		    assemblyName.Contains("ThirdParty") ||
		    assemblyName.StartsWith("Newtonsoft.", StringComparison.Ordinal))
		{
			return "plugin";
		}

		// User assemblies (direct dependencies)
		if (assemblyName.StartsWith("Assembly-", StringComparison.Ordinal))
		{
			return "direct";
		}

		return "unknown";
	}

	/// <summary>
	/// Classifies whether an assembly is a framework assembly.
	/// </summary>
	private bool? ClassifyAsFrameworkAssembly(string assemblyName)
	{
		if (assemblyName.StartsWith("UnityEngine", StringComparison.Ordinal) ||
			assemblyName.StartsWith("UnityEditor", StringComparison.Ordinal))
		{
			return true;
		}
		else if (assemblyName.StartsWith("System", StringComparison.Ordinal) ||
				 assemblyName.StartsWith("mscorlib", StringComparison.Ordinal) ||
				 assemblyName.StartsWith("netstandard", StringComparison.Ordinal))
		{
			return true;
		}
		else if (assemblyName.Contains("Plugin") || assemblyName.Contains("ThirdParty"))
		{
			return false;
		}
		return null; // Unknown
	}

	/// <summary>
	/// Computes a deterministic GUID for an assembly based on its full name.
	/// Must match the GUID computation in AssemblyFactsExporter.
	/// </summary>
	private string ComputeAssemblyGuid(ModuleDefinition module)
	{
		string? fullName = module.Assembly?.FullName;
		if (string.IsNullOrEmpty(fullName))
		{
			fullName = module.Name ?? "Unknown";
		}

		using (SHA256 sha256 = SHA256.Create())
		{
			byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fullName));
			// Take first 16 bytes to create GUID
			StringBuilder sb = new StringBuilder(32);
			for (int i = 0; i < 16; i++)
			{
				sb.Append(hashBytes[i].ToString("x2"));
			}
			return sb.ToString();
		}
	}
}
