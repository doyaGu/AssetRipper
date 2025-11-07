using AssetRipper.Assets.Collections;
using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.Hash128;
using Newtonsoft.Json;

using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Helpers;

internal class ScriptMetadataDumper
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public ScriptMetadataDumper(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = JsonHelper.CreateScriptSettings(_options);
	}

	public void ExportScriptMetadata(GameData gameData)
	{
		Logger.Info(LogCategory.Export, "Exporting script metadata...");

		string scriptsOutputPath = Path.Combine(_options.OutputPath, "ScriptMetadata");
		ExportHelper.EnsureDirectoryExists(scriptsOutputPath);

		var allCollections = gameData.GameBundle.FetchAssetCollections().ToList();
		ExportScriptsByCollection(allCollections, scriptsOutputPath);
		ExportScriptsOverview(allCollections, scriptsOutputPath);
	}

	private void ExportScriptsByCollection(IReadOnlyList<AssetCollection> collections, string outputPath)
	{
		var scriptsByCollection = new Dictionary<AssetCollection, List<IMonoScript>>();
		foreach (AssetCollection collection in collections)
		{
			var scripts = collection.OfType<IMonoScript>().ToList();
			if (scripts.Count > 0)
			{
				scriptsByCollection[collection] = scripts;
			}
		}

		int totalDiscovered = scriptsByCollection.Sum(kvp => kvp.Value.Count);
		string collectionsDir = Path.Combine(outputPath, "Collections");
		ExportHelper.EnsureDirectoryExists(collectionsDir);

		var indexEntries = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.Ordinal);
		int totalExported = 0;

		foreach (KeyValuePair<AssetCollection, List<IMonoScript>> entry in scriptsByCollection)
		{
			AssetCollection collection = entry.Key;
			List<IMonoScript> scripts = entry.Value;
			List<Dictionary<string, object>> scriptEntries = new List<Dictionary<string, object>>();

			foreach (IMonoScript script in scripts)
			{
				try
				{
					scriptEntries.Add(DumpScriptMetadata(script));
				}
				catch (Exception ex)
				{
					Logger.Warning(LogCategory.Export, $"Failed to export metadata for script {script.GetFullName()}: {ex.Message}");
					scriptEntries.Add(BuildFallbackMetadata(script));
				}
			}

			totalExported += scriptEntries.Count;
			try
			{
				string collectionId = ExportHelper.ComputeCollectionId(collection);
				string fileName = $"{collectionId}.json";
				string collectionFile = Path.Combine(collectionsDir, fileName);

				Dictionary<string, object> collectionDocument = BuildCollectionScriptDocument(collection, collectionId, scriptEntries, scripts.Count);
				WriteJsonFile(collectionDocument, collectionFile);

				if (!indexEntries.TryGetValue(collection.Name, out List<Dictionary<string, object>>? existingEntries) || existingEntries is null)
				{
					existingEntries = new List<Dictionary<string, object>>();
					indexEntries[collection.Name] = existingEntries;
				}
				List<Dictionary<string, object>> entriesForName = existingEntries;

				entriesForName.Add(new Dictionary<string, object>
				{
					["collectionId"] = collectionId,
					["bundleName"] = collection.Bundle?.Name ?? string.Empty,
					["file"] = fileName,
					["scriptCount"] = scriptEntries.Count,
					["discoveredScriptCount"] = scripts.Count,
					["flags"] = collection.Flags.ToString(),
					["platform"] = collection.Platform.ToString(),
					["version"] = collection.Version.ToString()
				});

				Logger.Info(LogCategory.Export, $"Exported {scriptEntries.Count}/{scripts.Count} MonoScripts for collection '{collection.Name}' (collectionId={collectionId})");
			}
			catch (Exception ex)
			{
				Logger.Warning(LogCategory.Export, $"Error exporting scripts from collection {collection.Name}: {ex.Message}");
			}
		}

		Logger.Info(
			LogCategory.Export,
			$"Found {totalDiscovered} MonoScript assets across {indexEntries.Count} collections; exported {totalExported} metadata entries");

		var indexDocument = new Dictionary<string, object>
		{
			["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
			["collectionCount"] = indexEntries.Count,
			["discoveredScriptCount"] = totalDiscovered,
			["exportedScriptCount"] = totalExported,
			["collections"] = indexEntries
		};

		string indexFile = Path.Combine(collectionsDir, "index.json");
		WriteJsonFile(indexDocument, indexFile);
	}

	private void ExportScriptsOverview(IReadOnlyList<AssetCollection> collections, string outputPath)
	{
		try
		{
			var allScripts = new List<IMonoScript>();
			var assemblies = new HashSet<string>();
			var namespaces = new HashSet<string>();
			var classNames = new HashSet<string>();

			foreach (AssetCollection collection in collections)
			{
				var scripts = collection.OfType<IMonoScript>().ToList();
				allScripts.AddRange(scripts);

				foreach (IMonoScript script in scripts)
				{
					assemblies.Add(script.GetAssemblyNameFixed());
					if (!string.IsNullOrEmpty(script.Namespace.String))
					{
						namespaces.Add(script.Namespace.String);
					}
					classNames.Add(script.ClassName_R.String);
				}
			}

			var overview = new Dictionary<string, object>
			{
				["totalScripts"] = allScripts.Count,
				["totalAssemblies"] = assemblies.Count,
				["totalNamespaces"] = namespaces.Count,
				["totalUniqueClassNames"] = classNames.Count,
				["assemblies"] = assemblies.OrderBy(a => a).ToList(),
				["topNamespaces"] = namespaces
					.Where(ns => !string.IsNullOrEmpty(ns))
					.GroupBy(ns => ns.Split('.')[0])
					.OrderByDescending(g => g.Count())
					.Take(10)
					.Select(g => new { Namespace = g.Key, Count = g.Count() })
					.ToList(),
				["scriptsByAssembly"] = allScripts
					.GroupBy(s => s.GetAssemblyNameFixed())
					.ToDictionary(g => g.Key, g => g.Count()),
				["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
			};

			string overviewFile = Path.Combine(outputPath, "ScriptsOverview.json");
			WriteJsonFile(overview, overviewFile);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Error creating scripts overview: {ex.Message}");
		}
	}

	private Dictionary<string, object> DumpScriptMetadata(IMonoScript script)
	{
		var metadata = new Dictionary<string, object>
		{
			// Basic asset metadata  
			["pathID"] = script.PathID,
			["classID"] = script.ClassID,
			["className"] = script.ClassName,

			// Script-specific metadata  
			["assemblyName"] = script.GetAssemblyNameFixed(),
			["namespace"] = script.Namespace.String,
			["fullName"] = script.GetFullName(),
			["executionOrder"] = script.ExecutionOrder,

			// Calculated identifiers
			["scriptGuid"] = SafeCompute(() => ScriptHashing.CalculateScriptGuid(script).ToString(), string.Empty, script, "script guid"),
			["assemblyGuid"] = SafeCompute(() => ScriptHashing.CalculateAssemblyGuid(script).ToString(), string.Empty, script, "assembly guid"),
			["scriptFileId"] = SafeCompute(() => ScriptHashing.CalculateScriptFileID(script), 0, script, "script file id"),

			// Collection information  
			["collection"] = script.Collection?.Name ?? string.Empty,
			["collectionFlags"] = script.Collection?.Flags.ToString() ?? string.Empty,
			["collectionId"] = script.Collection is not null ? ExportHelper.ComputeCollectionId(script.Collection) : string.Empty,
			["collectionFilePath"] = script.Collection?.FilePath ?? string.Empty,
			["collectionVersion"] = script.Collection?.Version.ToString() ?? string.Empty,
			["collectionPlatform"] = script.Collection?.Platform.ToString() ?? string.Empty,
			["bundleName"] = script.Collection?.Bundle?.Name ?? string.Empty
		};

		TryAddPropertiesHash(script, metadata);

		return metadata;
	}

	private static void TryAddPropertiesHash(IMonoScript script, Dictionary<string, object> metadata)
	{
		if (!script.Has_PropertiesHash_Hash128_5())
		{
			return;
		}

		try
		{
			Hash128_5 hash = script.GetPropertiesHash();
			metadata["propertiesHash"] = Hash128Utilities.ToLowerHex(hash);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export,
				$"Failed to read properties hash for script {script.GetFullName()}: {ex.Message}");
		}
	}

	private Dictionary<string, object> BuildFallbackMetadata(IMonoScript script)
	{
		return new Dictionary<string, object>
		{
			["pathID"] = script.PathID,
			["classID"] = script.ClassID,
			["className"] = script.ClassName,
			["assemblyName"] = script.GetAssemblyNameFixed(),
			["namespace"] = script.Namespace.String,
			["fullName"] = script.GetFullName(),
			["executionOrder"] = script.ExecutionOrder,
			["scriptGuid"] = string.Empty,
			["assemblyGuid"] = string.Empty,
			["scriptFileId"] = 0,
			["collection"] = script.Collection?.Name ?? string.Empty,
			["collectionFlags"] = script.Collection?.Flags.ToString() ?? string.Empty,
			["collectionId"] = script.Collection is not null ? ExportHelper.ComputeCollectionId(script.Collection) : string.Empty,
			["collectionFilePath"] = script.Collection?.FilePath ?? string.Empty,
			["collectionVersion"] = script.Collection?.Version.ToString() ?? string.Empty,
			["collectionPlatform"] = script.Collection?.Platform.ToString() ?? string.Empty,
			["bundleName"] = script.Collection?.Bundle?.Name ?? string.Empty
		};
	}

	private void WriteJsonFile(object data, string filePath)
	{
		ExportHelper.WriteJsonFile(data, filePath, _jsonSettings);
	}

	private static Dictionary<string, object> BuildCollectionScriptDocument(
		AssetCollection collection,
		string collectionId,
		List<Dictionary<string, object>> scriptEntries,
		int discoveredCount)
	{
		var document = new Dictionary<string, object>
		{
			["collectionId"] = collectionId,
			["collection"] = new Dictionary<string, object>
			{
				["name"] = collection.Name,
				["filePath"] = collection.FilePath ?? string.Empty,
				["bundleName"] = collection.Bundle?.Name ?? string.Empty,
				["version"] = collection.Version.ToString(),
				["platform"] = collection.Platform.ToString(),
				["flags"] = collection.Flags.ToString()
			},
			["scriptCount"] = scriptEntries.Count,
			["discoveredScriptCount"] = discoveredCount,
			["scripts"] = scriptEntries
		};

		if (collection.IsScene && collection.Scene is not null)
		{
			document["scene"] = new Dictionary<string, object>
			{
				["name"] = collection.Scene.Name,
				["path"] = collection.Scene.Path,
				["guid"] = collection.Scene.GUID.ToString()
			};
		}

		return document;
	}

	private static T SafeCompute<T>(Func<T> computation, T fallback, IMonoScript script, string context)
	{
		try
		{
			return computation();
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Failed to compute {context} for script {script.GetFullName()}: {ex.Message}");
			return fallback;
		}
	}
}
