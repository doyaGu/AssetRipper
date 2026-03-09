using Json.Schema;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AssetRipper.Tools.AssetDumper.Validation;

internal static class SchemaLoadHelper
{
	private static readonly ConcurrentDictionary<string, JsonSchema> Cache = new(StringComparer.OrdinalIgnoreCase);

	public static JsonSchema LoadFromFile(string schemaPath)
	{
		if (string.IsNullOrWhiteSpace(schemaPath))
		{
			throw new ArgumentException("Schema path cannot be null or empty", nameof(schemaPath));
		}

		string fullPath = Path.GetFullPath(schemaPath);
		if (Cache.TryGetValue(fullPath, out JsonSchema? cachedByPath))
		{
			return cachedByPath;
		}

		string schemaJson = File.ReadAllText(fullPath);
		string? schemaId = TryExtractSchemaId(schemaJson);
		if (!string.IsNullOrWhiteSpace(schemaId) && Cache.TryGetValue(schemaId, out JsonSchema? cachedById))
		{
			Cache.TryAdd(fullPath, cachedById);
			return cachedById;
		}

		JsonSchema schema = JsonSchema.FromText(schemaJson);
		Cache[fullPath] = schema;

		if (!string.IsNullOrWhiteSpace(schemaId))
		{
			Cache[schemaId] = schema;
		}

		return schema;
	}

	private static string? TryExtractSchemaId(string schemaJson)
	{
		using JsonDocument document = JsonDocument.Parse(schemaJson);
		return document.RootElement.TryGetProperty("$id", out JsonElement idElement)
			? idElement.GetString()
			: null;
	}
}
