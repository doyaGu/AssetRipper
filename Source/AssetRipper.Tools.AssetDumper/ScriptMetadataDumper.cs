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

		string scriptsOutputPath = Path.Combine(_options.OutputPath, "Scripts");
		Directory.CreateDirectory(scriptsOutputPath);

		ExportScriptsByCollection(gameData, scriptsOutputPath);
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

		foreach (var (collection, scripts) in scriptsByCollection)
		{
			try
			{
				var collectionScriptData = new Dictionary<string, object>
				{
					["collectionName"] = collection.Name,
					["collectionPath"] = collection.FilePath,
					["collectionVersion"] = collection.Version.ToString(),
					["scriptCount"] = scripts.Count,
					["scripts"] = scripts.Select(DumpScriptMetadata).ToList()
				};

				string safeCollectionName = SanitizeFileName(collection.Name);
				string collectionFile = Path.Combine(outputPath, $"{safeCollectionName}.json");
				WriteJsonFile(collectionScriptData, collectionFile);

				Logger.Info(LogCategory.Export, $"Exported {scripts.Count} scripts from collection: {collection.Name}");
			}
			catch (Exception ex)
			{
				Logger.Warning(LogCategory.Export, $"Error exporting scripts from collection {collection.Name}: {ex.Message}");
			}
		}
	}

	private Dictionary<string, object> DumpScriptMetadata(IMonoScript script)
	{
		var metadata = new Dictionary<string, object>
		{
			// Basic asset metadata  
			["pathID"] = script.PathID,
			["classID"] = script.ClassID,
			["className"] = script.GetType().Name,

			// Script-specific metadata  
			["assemblyName"] = script.AssemblyName.String,
			["assemblyNameFixed"] = script.GetAssemblyNameFixed(),
			["namespace"] = script.Namespace.String,
			["scriptClassName"] = script.ClassName_R.String,
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
			metadata["propertiesHash"] = new Dictionary<string, object>
			{
				["bytes0"] = hash.Bytes__0,
				["bytes1"] = hash.Bytes__1,
				["bytes2"] = hash.Bytes__2,
				["bytes3"] = hash.Bytes__3
			};
		}

		return metadata;
	}

	private void WriteJsonFile(object data, string filePath)
	{
		try
		{
			string json = JsonConvert.SerializeObject(data, _jsonSettings);
			File.WriteAllText(filePath, json);
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to write JSON file {filePath}: {ex.Message}");
		}
	}

	private static string SanitizeFileName(string fileName)
	{
		var invalidChars = Path.GetInvalidFileNameChars();
		return string.Concat(fileName.Where(c => !invalidChars.Contains(c)));
	}
}
