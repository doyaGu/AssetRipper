using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Common;
using AssetRipper.Tools.AssetDumper.Writers;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Exporters.Facts;

/// <summary>
/// Writes authoritative script source records to NDJSON shards.
/// </summary>
internal sealed class ScriptSourceExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly CompressionKind _compressionKind;
	private readonly bool _enableIndex;

	public ScriptSourceExporter(Options options, CompressionKind compressionKind, bool enableIndex)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_compressionKind = compressionKind;
		_enableIndex = enableIndex;
		_jsonSettings = new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
	}

	public DomainExportResult ExportSources(ScriptSourceIndexBuildResult buildResult)
	{
		if (buildResult is null)
		{
			throw new ArgumentNullException(nameof(buildResult));
		}

		Logger.Info(LogCategory.Export, "Exporting script sources...");

		DomainExportResult result = new DomainExportResult(
			domain: "script_sources",
			tableId: "facts/script_sources",
			schemaPath: "Schemas/v2/facts/script_sources.schema.json");

		ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
			_options.OutputPath,
			result.ShardDirectory,
			_jsonSettings,
			maxRecordsPerShard: _options.ShardSize > 0 ? _options.ShardSize : 20000,
			maxBytesPerShard: 50 * 1024 * 1024,
			_compressionKind,
			collectIndexEntries: _enableIndex,
			descriptorDomain: result.TableId);

		try
		{
			foreach (ScriptSourceRecordWithKey item in buildResult.Records)
			{
				string? indexKey = _enableIndex ? item.Pk : null;
				writer.WriteRecord(item.Record, item.Pk, indexKey);
			}
		}
		finally
		{
			writer.Dispose();
		}

		result.Shards.AddRange(writer.ShardDescriptors);
		if (_enableIndex)
		{
			result.IndexEntries.AddRange(writer.IndexEntries);
		}

		Logger.Info(LogCategory.Export, $"Exported {buildResult.Records.Count} script source records across {writer.ShardCount} shards");
		Logger.Info(LogCategory.Export, $"Matched: {buildResult.MatchedScripts}, Unmatched: {buildResult.UnmatchedFiles}");
		Logger.Info(
			LogCategory.Export,
			$"Authoritative AST coverage: {buildResult.AstValidatedCount}/{buildResult.MatchedScripts} validated, {buildResult.MissingAst.Count} missing, {buildResult.InvalidAst.Count} invalid");

		if (buildResult.MissingAst.Count > 0 || buildResult.InvalidAst.Count > 0)
		{
			foreach (string error in buildResult.MissingAst.Take(10))
			{
				Logger.Error(LogCategory.Export, $"Missing AST: {error}");
			}

			foreach (string error in buildResult.InvalidAst.Take(10))
			{
				Logger.Error(LogCategory.Export, $"Invalid AST: {error}");
			}

			int remaining = Math.Max(0, buildResult.MissingAst.Count - 10) + Math.Max(0, buildResult.InvalidAst.Count - 10);
			if (remaining > 0)
			{
				Logger.Error(LogCategory.Export, $"... and {remaining} more AST coverage issue(s)");
			}

			throw new InvalidOperationException(
				$"Authoritative AST coverage incomplete: {buildResult.AstValidatedCount}/{buildResult.MatchedScripts} validated, " +
				$"{buildResult.MissingAst.Count} missing, {buildResult.InvalidAst.Count} invalid");
		}

		return result;
	}
}
