using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Models;
using Newtonsoft.Json;
using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Writes key index sidecars (<domain>.kindex) to accelerate shard lookups.
/// 
/// Index behavior by compression mode:
/// - Uncompressed: Stores byte offset and length for direct random access
/// - Compressed: Stores line numbers only; consumers must decompress and scan sequentially
/// 
/// All index entries include line numbers which remain valid after compression.
/// </summary>
public sealed class KeyIndexGenerator
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public KeyIndexGenerator(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = new JsonSerializerSettings
		{
			Formatting = _options.CompactJson ? Formatting.None : Formatting.Indented,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
	}

	/// <summary>
	/// Persist an index for the specified domain. Returns the manifest reference or null when no entries.
	/// </summary>
	public ManifestIndex? Write(string domain, IReadOnlyList<KeyIndexEntry> entries, CompressionKind compressionKind = CompressionKind.None)
	{
		if (entries == null || entries.Count == 0 || !_options.ExportIndexes)
		{
			return null;
		}

		try
		{
			string indexRoot = OutputPathHelper.EnsureSubdirectory(_options.OutputPath, OutputPathHelper.IndexesDirectoryName);
			string fileName = $"{domain}.kindex";
			string absolutePath = Path.Combine(indexRoot, fileName);

			var document = new KeyIndexDocument
			{
				Domain = domain,
				CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				Entries = entries
					.OrderBy(static entry => entry.Key, StringComparer.Ordinal)
					.ToList(),
				Metadata = new Dictionary<string, object>
				{
					{"recordCount", entries.Count},
					{"generatedBy", "AssetRipper.Tools.AssetDumper"},
					{"compressionMode", compressionKind.ToString().ToLowerInvariant()},
					{"indexingStrategy", compressionKind == CompressionKind.None ? "byte-offset" : "line-number"}
				}
			};

			string json = JsonConvert.SerializeObject(document, _jsonSettings);
			File.WriteAllText(absolutePath, json);

			return new ManifestIndex
			{
				Type = "kindex",
				Domain = domain,
				Format = "kindex",
				Path = ExportHelper.ToManifestRelativePath(_options.OutputPath, absolutePath),
				RecordCount = entries.Count,
				CreatedAt = document.CreatedAt,
				Metadata = document.Metadata
			};
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Failed to write key index for domain '{domain}': {ex.Message}");
			return null;
		}
	}
}
