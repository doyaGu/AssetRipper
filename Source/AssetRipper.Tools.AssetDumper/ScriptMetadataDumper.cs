using AssetRipper.Assets.Collections;
using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using AssetRipper.SourceGenerated.Extensions;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper;

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

		ExportScriptsByCollection(gameData, scriptsOutputPath);
		ExportScriptsOverview(gameData, scriptsOutputPath);
	}

	private void ExportScriptsByCollection(GameData gameData, string outputPath)
	{
		var scriptsByCollection = new Dictionary<AssetCollection, List<IMonoScript>>();

		// Collect all MonoScript assets from all collections  
		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			var scripts = collection.OfType<IMonoScript>().ToList();
			if (scripts.Count > 0)
			{
				scriptsByCollection[collection] = scripts;
			}
		}

		Logger.Info(LogCategory.Export, $"Found scripts in {scriptsByCollection.Count} collections");

		string collectionsDir = Path.Combine(outputPath, "Collections");
		ExportHelper.EnsureDirectoryExists(collectionsDir);

		foreach (var (collection, scripts) in scriptsByCollection)
		{
			try
			{
				var collectionScriptData = new Dictionary<string, object>
				{
					["collectionName"] = collection.Name,
					["collectionFilePath"] = collection.FilePath,
					["collectionVersion"] = collection.Version.ToString(),
					["collectionPlatform"] = collection.Platform.ToString(),
					["scriptCount"] = scripts.Count,
					["scripts"] = scripts.Select(DumpScriptMetadata).ToList()
				};

				string safeCollectionName = ExportHelper.SanitizeFileName(collection.Name);
				string collectionFile = Path.Combine(collectionsDir, $"{safeCollectionName}.json");
				WriteJsonFile(collectionScriptData, collectionFile);

				Logger.Debug(LogCategory.Export, $"Exported {scripts.Count} scripts from collection: {collection.Name}");
			}
			catch (Exception ex)
			{
				Logger.Warning(LogCategory.Export, $"Error exporting scripts from collection {collection.Name}: {ex.Message}");
			}
		}
	}

	private void ExportScriptsOverview(GameData gameData, string outputPath)
	{
		try
		{
			var allScripts = new List<IMonoScript>();
			var assemblies = new HashSet<string>();
			var namespaces = new HashSet<string>();
			var classNames = new HashSet<string>();

			foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
			{
				var scripts = collection.OfType<IMonoScript>().ToList();
				allScripts.AddRange(scripts);

				foreach (var script in scripts)
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
			["scriptGuid"] = ScriptHashing.CalculateScriptGuid(script).ToString(),
			["assemblyGuid"] = ScriptHashing.CalculateAssemblyGuid(script).ToString(),
			["scriptFileID"] = ScriptHashing.CalculateScriptFileID(script),

			// Collection information  
			["collection"] = script.Collection.Name,
			["collectionFlags"] = script.Collection.Flags.ToString()
		};

		// Add properties hash if available  
		if (script.Has_PropertiesHash_Hash128_5())
		{
			var hash = script.GetPropertiesHash();
			metadata["propertiesHash"] = hash.ToString();
		}

		return metadata;
	}

	private void WriteJsonFile(object data, string filePath)
	{
		ExportHelper.WriteJsonFile(data, filePath, _jsonSettings);
	}
}
