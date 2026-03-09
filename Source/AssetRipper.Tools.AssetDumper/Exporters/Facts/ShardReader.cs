using System.IO.Compression;
using ZstdSharp;

namespace AssetRipper.Tools.AssetDumper.Exporters.Facts;

internal static class ShardReader
{
	public static Stream OpenShardStream(string shardPath)
	{
		if (string.IsNullOrWhiteSpace(shardPath))
		{
			throw new ArgumentException("Shard path cannot be null or empty", nameof(shardPath));
		}

		FileStream fileStream = File.OpenRead(shardPath);
		try
		{
			return ResolveCompression(shardPath) switch
			{
				"gzip" => new GZipStream(fileStream, CompressionMode.Decompress),
				"zstd" => new DecompressionStream(fileStream),
				_ => fileStream
			};
		}
		catch
		{
			fileStream.Dispose();
			throw;
		}
	}

	public static string ResolveCompression(string shardPath)
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
}
