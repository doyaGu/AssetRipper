using AssetRipper.Assets.Bundles;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Constants;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Writers;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Exporters;

/// <summary>
/// Base class for all domain exporters providing common initialization, workflow, and finalization.
/// </summary>
/// <remarks>
/// This base class eliminates code duplication across exporters by providing:
/// <list type="bullet">
/// <item>Unified JSON serializer settings</item>
/// <item>Common ShardedNdjsonWriter initialization</item>
/// <item>Standard export workflow with try-finally pattern</item>
/// <item>Shared logging utilities</item>
/// </list>
/// </remarks>
internal abstract class ExporterBase
{
	protected readonly Options _options;
	protected readonly JsonSerializerSettings _jsonSettings;
	protected readonly CompressionKind _compressionKind;
	protected readonly bool _enableIndex;

	/// <summary>
	/// Initializes the exporter with common configuration.
	/// </summary>
	protected ExporterBase(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = JsonSettingsFactory.CreateDefault();
		_compressionKind = ResolveCompressionKind(options.Compression);
		_enableIndex = options.EnableIndex;
	}

	/// <summary>
	/// Gets the domain name for this exporter (e.g., "assets", "asset_dependencies").
	/// </summary>
	protected abstract string Domain { get; }

	/// <summary>
	/// Gets the table ID for this exporter (e.g., "facts/assets", "relations/asset_dependencies").
	/// </summary>
	protected abstract string TableId { get; }

	/// <summary>
	/// Gets the schema path for this exporter.
	/// </summary>
	protected abstract string SchemaPath { get; }

	/// <summary>
	/// Gets the default maximum records per shard for this exporter.
	/// </summary>
	protected virtual long DefaultMaxRecordsPerShard => ExportConstants.DefaultMaxRecordsPerShard;

	/// <summary>
	/// Gets the default maximum bytes per shard for this exporter.
	/// </summary>
	protected virtual long DefaultMaxBytesPerShard => ExportConstants.DefaultMaxBytesPerShard;

	/// <summary>
	/// Gets the seekable frame size for compression.
	/// </summary>
	protected virtual int SeekableFrameSize => ExportConstants.DefaultSeekableFrameSize;

	/// <summary>
	/// Creates a DomainExportResult for this exporter.
	/// </summary>
	protected DomainExportResult CreateResult()
	{
		return new DomainExportResult(Domain, TableId, SchemaPath);
	}

	/// <summary>
	/// Creates an empty result (for skipped exports).
	/// </summary>
	protected DomainExportResult CreateEmptyResult()
	{
		return CreateResult();
	}

	/// <summary>
	/// Resolves the effective max records per shard, respecting user override.
	/// </summary>
	protected long GetMaxRecordsPerShard()
	{
		return _options.ShardSize > 0 ? _options.ShardSize : DefaultMaxRecordsPerShard;
	}

	/// <summary>
	/// Creates and configures a ShardedNdjsonWriter for this exporter.
	/// </summary>
	protected ShardedNdjsonWriter CreateWriter(DomainExportResult result)
	{
		return new ShardedNdjsonWriter(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			GetMaxRecordsPerShard(),
			DefaultMaxBytesPerShard,
			_compressionKind,
			seekableFrameSize: SeekableFrameSize,
			collectIndexEntries: _enableIndex,
			descriptorDomain: result.TableId);
	}

	/// <summary>
	/// Finalizes the export by collecting shard descriptors and index entries.
	/// </summary>
	protected void FinalizeResult(ShardedNdjsonWriter writer, DomainExportResult result)
	{
		result.Shards.AddRange(writer.ShardDescriptors);
		if (_enableIndex)
		{
			result.IndexEntries.AddRange(writer.IndexEntries);
		}
	}

	/// <summary>
	/// Logs export completion with standard formatting.
	/// </summary>
	protected void LogExportComplete(string recordType, long count, int shardCount)
	{
		if (_options.Verbose)
		{
			Logger.Info(LogCategory.Export,
				$"Exported {count:N0} {recordType} across {shardCount} shards");
		}
		else
		{
			Logger.Info(LogCategory.Export, $"Exported {count:N0} {recordType}");
		}
	}

	/// <summary>
	/// Logs export start with standard formatting.
	/// </summary>
	protected void LogExportStart(string operation)
	{
		Logger.Info(LogCategory.Export, $"Exporting {operation}...");
	}

	/// <summary>
	/// Validates that GameData is not null.
	/// </summary>
	protected static void ValidateGameData(GameData gameData)
	{
		if (gameData is null)
		{
			throw new ArgumentNullException(nameof(gameData));
		}
	}

	/// <summary>
	/// Checks if the assembly manager is available.
	/// </summary>
	protected static bool HasAssemblyManager(GameData gameData)
	{
		return gameData.AssemblyManager?.IsSet == true;
	}

	/// <summary>
	/// Logs verbose message if verbose mode is enabled.
	/// </summary>
	protected void LogVerbose(string message)
	{
		if (_options.Verbose)
		{
			Logger.Info(LogCategory.Export, message);
		}
	}

	/// <summary>
	/// Logs warning message.
	/// </summary>
	protected static void LogWarning(string message)
	{
		Logger.Warning(LogCategory.Export, message);
	}

	private static CompressionKind ResolveCompressionKind(string? compression)
	{
		if (string.IsNullOrWhiteSpace(compression))
		{
			return CompressionKind.None;
		}

		return compression.ToLowerInvariant() switch
		{
			"zstd" => CompressionKind.Zstd,
			"gzip" => CompressionKind.Gzip,
			"none" => CompressionKind.None,
			_ => CompressionKind.None
		};
	}
}
