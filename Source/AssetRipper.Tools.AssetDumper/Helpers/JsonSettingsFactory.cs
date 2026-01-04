using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Helpers;

/// <summary>
/// Factory for creating consistent JSON serializer settings across all exporters.
/// </summary>
public static class JsonSettingsFactory
{
	/// <summary>
	/// Creates the default JSON serializer settings used by all exporters.
	/// </summary>
	/// <remarks>
	/// Settings:
	/// <list type="bullet">
	/// <item>No formatting (compact output)</item>
	/// <item>Null values ignored</item>
	/// <item>Default values ignored</item>
	/// </list>
	/// </remarks>
	public static JsonSerializerSettings CreateDefault()
	{
		return new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
	}

	/// <summary>
	/// Creates JSON serializer settings with pretty printing for debugging.
	/// </summary>
	public static JsonSerializerSettings CreatePretty()
	{
		return new JsonSerializerSettings
		{
			Formatting = Formatting.Indented,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
	}
}
