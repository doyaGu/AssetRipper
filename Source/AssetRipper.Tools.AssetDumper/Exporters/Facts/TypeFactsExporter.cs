using System.Globalization;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models;
using Newtonsoft.Json;
using AssetRipper.Tools.AssetDumper.Writers;

namespace AssetRipper.Tools.AssetDumper.Exporters.Facts;

/// <summary>
/// Emits facts/types.ndjson dictionary entries derived from the asset class key map.
/// </summary>
public sealed class TypeFactsExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public TypeFactsExporter(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
	}

	public DomainExportResult ExportTypes(IEnumerable<TypeDictionaryEntry> entries)
	{
		if (entries is null)
		{
			throw new ArgumentNullException(nameof(entries));
		}

		List<TypeFactRecord> records = entries
			.Select(ToRecord)
			.OrderBy(static record => record.ClassKey)
			.ToList();

		DomainExportResult result = new DomainExportResult(
			"assets",
			"facts/types",
			"Schemas/v2/facts/types.schema.json");

		if (records.Count == 0)
		{
			return result;
		}

		ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard: long.MaxValue,
			maxBytesPerShard: 16 * 1024 * 1024,
			compressionKind: CompressionKind.None,
			seekableFrameSize: 2 * 1024 * 1024,
			collectIndexEntries: false,
			descriptorDomain: result.TableId);

		try
		{
			foreach (TypeFactRecord record in records)
			{
				string stableKey = record.ClassKey.ToString(CultureInfo.InvariantCulture);
				writer.WriteRecord(record, stableKey);
			}
		}
		finally
		{
			writer.Dispose();
		}

		result.Shards.AddRange(writer.ShardDescriptors);
		return result;
	}

	private static TypeFactRecord ToRecord(TypeDictionaryEntry entry)
	{
		return new TypeFactRecord
		{
			ClassKey = entry.ClassKey,
			ClassId = entry.ClassId,
			ClassName = entry.ClassName,
			ScriptTypeIndex = entry.ScriptTypeIndex,
			IsStripped = entry.IsStripped
		};
	}
}
