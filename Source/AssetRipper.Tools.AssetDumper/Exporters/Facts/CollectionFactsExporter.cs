using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using AssetRipper.Assets.Collections;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Models;
using Newtonsoft.Json;
using AssetRipper.Tools.AssetDumper.Writers;
using AssetRipper.Tools.AssetDumper.Helpers;

using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Exporters.Facts;

/// <summary>
/// Emits facts/collections.ndjson according to the AssetDump v2 schema.
/// </summary>
public sealed class CollectionFactsExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;

	public CollectionFactsExporter(Options options, CompressionKind compressionKind)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_compressionKind = compressionKind;
		_jsonSettings = new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
	}

	public DomainExportResult ExportCollections(GameData gameData)
	{
		if (gameData is null)
		{
			throw new ArgumentNullException(nameof(gameData));
		}

		IEnumerable<SerializedAssetCollection> serializedCollections = gameData.GameBundle
			.FetchAssetCollections()
			.OfType<SerializedAssetCollection>();

		List<CollectionFactRecord> records = serializedCollections
			.Select(CreateRecord)
			.Where(static record => record is not null)
			.Select(static record => record!)
			.ToList();

		records.Sort(static (left, right) => string.CompareOrdinal(left.CollectionId, right.CollectionId));

		DomainExportResult result = new DomainExportResult(
			domain: "collections",
			tableId: "facts/collections",
			schemaPath: "Schemas/v2/facts/collections.schema.json");

		long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 100_000;
		long maxBytesPerShard = 100 * 1024 * 1024;

		ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard,
			maxBytesPerShard,
			_compressionKind,
			seekableFrameSize: 2 * 1024 * 1024,
			collectIndexEntries: false,
			descriptorDomain: result.TableId);

		try
		{
			foreach (CollectionFactRecord record in records)
			{
				writer.WriteRecord(record, record.CollectionId);
			}
		}
		finally
		{
			writer.Dispose();
		}

		result.Shards.AddRange(writer.ShardDescriptors);
		return result;
	}

	private CollectionFactRecord? CreateRecord(SerializedAssetCollection collection)
	{
		if (collection is null)
		{
			return null;
		}

		string collectionId = ExportHelper.ComputeCollectionId(collection);
		List<string>? flags = BuildFlags(collection.Flags);
		string? normalizedPath = NormalizePath(collection.FilePath);
		CollectionSourceRecord? source = BuildSource(normalizedPath);
		CollectionUnityRecord? unity = BuildUnityRecord(collection);
		string? friendlyName = BuildFriendlyName(collection);
		string? flagsRaw = BuildFlagsRaw(collection.Flags);
		bool? isSceneCollection = collection.IsScene ? true : null;
		int? formatVersion = ResolveFormatVersion(collection);

		return new CollectionFactRecord
		{
			CollectionId = collectionId,
			Name = collection.Name,
			FriendlyName = friendlyName,
			FilePath = normalizedPath,
			BundleName = collection.Bundle?.Name,
			Platform = collection.Platform.ToString(),
			UnityVersion = collection.Version.ToString(),
			FormatVersion = formatVersion,
			Endian = collection.EndianType.ToString(),
			FlagsRaw = flagsRaw,
			Flags = flags,
			IsSceneCollection = isSceneCollection,
			Source = source,
			Unity = unity
		};
	}

	private static int? ResolveFormatVersion(SerializedAssetCollection collection)
	{
		PropertyInfo? property = collection.GetType().GetProperty("FormatVersion", BindingFlags.Public | BindingFlags.Instance);
		if (property is null)
		{
			return null;
		}

		object? rawValue;
		try
		{
			rawValue = property.GetValue(collection);
		}
		catch
		{
			return null;
		}

		return TryConvertToInt(rawValue);
	}

	private static int? TryConvertToInt(object? value)
	{
		if (value is null)
		{
			return null;
		}

		if (value is int alreadyInt)
		{
			return alreadyInt;
		}

		if (value is IConvertible convertible)
		{
			try
			{
				return convertible.ToInt32(CultureInfo.InvariantCulture);
			}
			catch
			{
				return null;
			}
		}

		return null;
	}

	private static string? NormalizePath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}

		return path.Replace('\\', '/');
	}

	private static string? BuildFriendlyName(SerializedAssetCollection collection)
	{
		if (collection.Scene is not { } scene)
		{
			return null;
		}

		string? friendlyFromPath = BeautifyScenePath(scene.Path);
		if (!string.IsNullOrWhiteSpace(friendlyFromPath))
		{
			return friendlyFromPath;
		}

		if (!string.IsNullOrWhiteSpace(scene.Name))
		{
			string beautifiedName = BeautifySegment(scene.Name);
			return string.IsNullOrWhiteSpace(beautifiedName) ? null : beautifiedName;
		}

		return null;
	}

	private static string? BeautifyScenePath(string? rawPath)
	{
		if (string.IsNullOrWhiteSpace(rawPath))
		{
			return null;
		}

		string normalized = rawPath.Replace('\\', '/').Trim();
		if (normalized.Length == 0)
		{
			return null;
		}

		normalized = RemovePrefix(normalized, "Assets/");
		normalized = CollapseSeparators(normalized);

		string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length == 0)
		{
			return null;
		}

		// Strip .unity extension from the final segment when present.
		int lastIndex = segments.Length - 1;
		string lastSegment = segments[lastIndex];
		if (lastSegment.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
		{
			segments[lastIndex] = lastSegment[..^6];
		}

		List<string> friendlySegments = new(segments.Length);
		for (int i = 0; i < segments.Length; i++)
		{
			string segment = segments[i];
			if (i == 0 && string.Equals(segment, "Scenes", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string beautified = BeautifySegment(segment);
			if (!string.IsNullOrWhiteSpace(beautified))
			{
				friendlySegments.Add(beautified);
			}
		}

		if (friendlySegments.Count == 0)
		{
			return null;
		}

		return string.Join('/', friendlySegments);
	}

	private static string BeautifySegment(string segment)
	{
		if (string.IsNullOrWhiteSpace(segment))
		{
			return string.Empty;
		}

		StringBuilder builder = new(segment.Length);
		bool previousWasSeparator = false;
		foreach (char character in segment)
		{
			if (character == '_' || character == '-' || char.IsWhiteSpace(character))
			{
				if (!previousWasSeparator)
				{
					builder.Append(' ');
					previousWasSeparator = true;
				}

				continue;
			}

			builder.Append(character);
			previousWasSeparator = false;
		}

		return builder.ToString().Trim();
	}

	private static string RemovePrefix(string value, string prefix)
	{
		if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return value[prefix.Length..];
		}

		return value;
	}

	private static string CollapseSeparators(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		StringBuilder builder = new(value.Length);
		char previous = '\0';
		foreach (char character in value)
		{
			if (character == '/' && previous == '/')
			{
				continue;
			}

			builder.Append(character);
			previous = character;
		}

		return builder.ToString();
	}

	private static string? BuildFlagsRaw(TransferInstructionFlags flags)
	{
		if (flags == TransferInstructionFlags.NoTransferInstructionFlags)
		{
			return null;
		}

		string value = flags.ToString("F");
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	private static List<string>? BuildFlags(TransferInstructionFlags flags)
	{
		if (flags == TransferInstructionFlags.NoTransferInstructionFlags)
		{
			return null;
		}

		HashSet<string> unique = new(StringComparer.Ordinal);
		foreach (TransferInstructionFlags value in Enum.GetValues<TransferInstructionFlags>())
		{
			if (value == TransferInstructionFlags.NoTransferInstructionFlags)
			{
				continue;
			}

			if (flags.HasFlag(value))
			{
				string name = value.ToString();
				if (!string.IsNullOrWhiteSpace(name))
				{
					unique.Add(name);
				}
			}
		}

		if (unique.Count == 0)
		{
			return null;
		}

		List<string> ordered = unique.ToList();
		ordered.Sort(StringComparer.Ordinal);
		return ordered;
	}

	private static CollectionSourceRecord? BuildSource(string? filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			return null;
		}

		return new CollectionSourceRecord
		{
			Uri = filePath
		};
	}

	private static CollectionUnityRecord? BuildUnityRecord(SerializedAssetCollection collection)
	{
		string? classification = ResolveBuiltInClassification(collection.Name);
		return classification is null
			? null
			: new CollectionUnityRecord { BuiltInClassification = classification };
	}

	private static string? ResolveBuiltInClassification(string? name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}

		string normalized = SpecialFileNames.FixFileIdentifier(name);
		if (SpecialFileNames.IsBuiltinExtra(normalized))
		{
			return "BUILTIN-EXTRA";
		}

		if (SpecialFileNames.IsDefaultResource(normalized))
		{
			return "BUILTIN-DEFAULT";
		}

		if (SpecialFileNames.IsEditorResource(normalized))
		{
			return "BUILTIN-EDITOR";
		}

		return null;
	}
}
