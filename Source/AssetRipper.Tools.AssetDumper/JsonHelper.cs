using AssetRipper.Tools.AssetDumper.Models;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper;

internal static class JsonHelper
{
	public static JsonSerializerSettings CreateSettings(Options options)
	{
		return new JsonSerializerSettings
		{
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			ContractResolver = new SyntaxNodePropertiesResolver(),
			Formatting = options.CompactJson ? Formatting.None : Formatting.Indented,
			NullValueHandling = options.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include
		};
	}

	public static JsonSerializerSettings CreateSceneSettings(Options options)
	{
		return new JsonSerializerSettings
		{
			Formatting = options.CompactJson ? Formatting.None : Formatting.Indented,
			NullValueHandling = options.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore
		};
	}

	public static JsonSerializerSettings CreateScriptSettings(Options options)
	{
		return new JsonSerializerSettings
		{
			Formatting = options.CompactJson ? Formatting.None : Formatting.Indented,
			NullValueHandling = options.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			DateFormatHandling = DateFormatHandling.IsoDateFormat
		};
	}
}
