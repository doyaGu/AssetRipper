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
}
