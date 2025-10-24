using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper;

internal static class ExportHelper
{
	public static void WriteJsonFile(object data, string filePath, JsonSerializerSettings jsonSettings)
	{
		try
		{
			string json = JsonConvert.SerializeObject(data, jsonSettings);
			File.WriteAllText(filePath, json);
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to write JSON file {filePath}: {ex.Message}");
		}
	}

	public static string SanitizeFileName(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
			return "unnamed";

		var invalidChars = Path.GetInvalidFileNameChars();
		return string.Concat(fileName.Where(c => !invalidChars.Contains(c)));
	}

	public static void EnsureDirectoryExists(string path)
	{
		try
		{
			Directory.CreateDirectory(path);
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to create directory {path}: {ex.Message}");
			throw;
		}
	}

	public static Dictionary<string, object> CreateBasicMetadata(string name, string type)
	{
		return new Dictionary<string, object>
		{
			["name"] = name,
			["type"] = type,
			["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
		};
	}

	public static string ComputeCollectionId(AssetCollection collection)
	{
		if (collection is null)
		{
			throw new ArgumentNullException(nameof(collection));
		}

		string bundleName = collection.Bundle?.Name ?? string.Empty;
		string filePath = collection.FilePath ?? string.Empty;
		string version = collection.Version.ToString();
		string platform = collection.Platform.ToString();
		string flags = collection.Flags.ToString();
		string compositeKey = string.Join("|", new[]
		{
			collection.Name ?? string.Empty,
			filePath,
			bundleName,
			version,
			platform,
			flags
		});

		return ComputeStableHash(compositeKey);
	}

	public static string ComputeStableHash(string value)
	{
		unchecked
		{
			uint hash = 2166136261;
			foreach (char c in value)
			{
				hash ^= c;
				hash *= 16777619;
			}
			return hash.ToString("X8");
		}
	}
}
