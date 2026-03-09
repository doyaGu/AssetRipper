using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models.Common;
using AssetRipper.Tools.AssetDumper.Writers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ZstdSharp;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Loads reusable table metadata from an existing manifest or directly from disk.
/// </summary>
internal sealed class TableResultLoader
{
	private readonly Options _options;
	private readonly IncrementalManager _incrementalManager;

	public TableResultLoader(Options options, IncrementalManager incrementalManager)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_incrementalManager = incrementalManager ?? throw new ArgumentNullException(nameof(incrementalManager));
	}

	public DomainExportResult LoadRequiredTable(string domain, string tableId, string schemaPath, Manifest? existingManifest)
	{
		if (existingManifest != null && _incrementalManager.ManifestContainsTables(existingManifest, tableId))
		{
			DomainExportResult? result = _incrementalManager.CreateResultFromManifest(existingManifest, tableId);
			if (result != null)
			{
				return result;
			}
		}

		return LoadFromDisk(domain, tableId, schemaPath);
	}

	private DomainExportResult LoadFromDisk(string domain, string tableId, string schemaPath)
	{
		string tableDirectory = Path.Combine(_options.OutputPath, tableId.Replace('/', Path.DirectorySeparatorChar));
		if (!Directory.Exists(tableDirectory))
		{
			throw new DirectoryNotFoundException($"Required table directory not found: {tableDirectory}");
		}

		DomainExportResult result = new(domain, tableId, schemaPath);
		string[] shardPaths = Directory
			.EnumerateFiles(tableDirectory, "part-*.ndjson*", SearchOption.TopDirectoryOnly)
			.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (shardPaths.Length == 0)
		{
			throw new InvalidOperationException($"No shards found for {tableId} in {tableDirectory}");
		}

		foreach (string shardPath in shardPaths)
		{
			FileInfo fileInfo = new(shardPath);
			string compression = ResolveCompression(shardPath);
			result.Shards.Add(new ShardDescriptor
			{
				Domain = tableId,
				Shard = OutputPathHelper.NormalizeRelativePath(Path.GetRelativePath(_options.OutputPath, shardPath)),
				Records = CountRecords(shardPath, compression),
				Bytes = fileInfo.Length,
				Compression = compression,
				UncompressedBytes = compression.Equals("none", StringComparison.OrdinalIgnoreCase) ? fileInfo.Length : null,
				Sha256 = ComputeSha256(shardPath)
			});
		}

		return result;
	}

	private static string ResolveCompression(string shardPath)
	{
		if (shardPath.EndsWith(".ndjson.gz", StringComparison.OrdinalIgnoreCase))
		{
			return "gzip";
		}

		if (shardPath.EndsWith(".ndjson.zst", StringComparison.OrdinalIgnoreCase))
		{
			return "zstd";
		}

		return "none";
	}

	private static long CountRecords(string shardPath, string compression)
	{
		using Stream stream = OpenShardStream(shardPath, compression);
		using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
		long count = 0;
		while (reader.ReadLine() != null)
		{
			count++;
		}

		return count;
	}

	private static Stream OpenShardStream(string shardPath, string compression)
	{
		FileStream fileStream = File.OpenRead(shardPath);
		return compression.ToLowerInvariant() switch
		{
			"gzip" => new GZipStream(fileStream, CompressionMode.Decompress),
			"zstd" => new DecompressionStream(fileStream),
			_ => fileStream
		};
	}

	private static string ComputeSha256(string filePath)
	{
		using FileStream stream = File.OpenRead(filePath);
		using SHA256 sha256 = SHA256.Create();
		return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
	}
}
