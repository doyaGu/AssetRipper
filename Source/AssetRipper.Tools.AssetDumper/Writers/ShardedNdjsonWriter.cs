using System.Security.Cryptography;
using System.IO.Compression;
using AssetRipper.Tools.AssetDumper.Constants;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Models.Common;
using Newtonsoft.Json;
using ZstdSharp;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Writers;

/// <summary>
/// Writes NDJSON shards following the v2 layout (&lt;table-id&gt;/part-xxxxx.ndjson[.zst]).
/// Tracks shard descriptors for manifest generation and optional key-index entries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This class provides limited thread-safety via an internal write lock.
/// The <see cref="WriteRecord"/> method is thread-safe and can be called from multiple threads.
/// However, properties like <see cref="TotalRecords"/>, <see cref="TotalBytes"/>, and
/// <see cref="ShardCount"/> read mutable state without synchronization and should only be
/// accessed after all writes are complete and the writer is disposed.
/// </para>
/// <para>
/// For most export scenarios, single-threaded usage is recommended. If using from multiple
/// threads, ensure all reads of aggregate properties occur only after <see cref="Dispose"/> is called.
/// </para>
/// </remarks>
internal sealed class ShardedNdjsonWriter : IDisposable
{
	private readonly string _outputRoot;
	private readonly string _shardDirectory;
	private readonly string _descriptorDomain;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly long _maxRecordsPerShard;
	private readonly long _maxBytesPerShard;
	private readonly List<ShardDescriptor> _shardDescriptors;
	private readonly List<KeyIndexEntry> _indexEntries;
	private readonly CompressionKind _compressionKind;
	private readonly int _seekableFrameSize;
	private readonly bool _collectIndexEntries;

	private NdjsonWriter? _currentWriter;
	private string? _currentRelativeShardPath;
	private int _currentShardIndex;
	private long _totalRecords;
	private long _totalBytes;
	private readonly object _writeLock = new object(); // Thread safety for parallel writes

	public ShardedNdjsonWriter(
		string outputRoot,
		string shardDirectory,
		JsonSerializerSettings? jsonSettings = null,
		long maxRecordsPerShard = ExportConstants.DefaultMaxRecordsPerShard,
		long maxBytesPerShard = ExportConstants.DefaultMaxBytesPerShard,
		CompressionKind compressionKind = CompressionKind.None,
		int seekableFrameSize = ExportConstants.DefaultSeekableFrameSize,
		bool collectIndexEntries = false,
		string? descriptorDomain = null)
	{
		_outputRoot = outputRoot ?? throw new ArgumentNullException(nameof(outputRoot));
		_shardDirectory = shardDirectory ?? throw new ArgumentNullException(nameof(shardDirectory));
		_descriptorDomain = string.IsNullOrWhiteSpace(descriptorDomain) ? shardDirectory : descriptorDomain;
		_jsonSettings = jsonSettings ?? new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
		_maxRecordsPerShard = maxRecordsPerShard > 0 ? maxRecordsPerShard : long.MaxValue;
		_maxBytesPerShard = maxBytesPerShard > 0 ? maxBytesPerShard : long.MaxValue;
		_shardDescriptors = new List<ShardDescriptor>();
		_indexEntries = new List<KeyIndexEntry>();
		_compressionKind = compressionKind;
		_seekableFrameSize = seekableFrameSize > 0 ? seekableFrameSize : ExportConstants.DefaultSeekableFrameSize;
		_collectIndexEntries = collectIndexEntries; // Now supports all compression modes
		_currentShardIndex = 0;
		_totalRecords = 0;
		_totalBytes = 0;

		Directory.CreateDirectory(_outputRoot);
	}

	public List<ShardDescriptor> ShardDescriptors => _shardDescriptors;
	public List<KeyIndexEntry> IndexEntries => _indexEntries;
	public long TotalRecords => _totalRecords;
	public long TotalBytes => _totalBytes;
	public int ShardCount => _shardDescriptors.Count;

	/// <summary>
	/// Writes a record, automatically creating a new shard if limits are exceeded.
	/// Thread-safe for parallel writes.
	/// </summary>
	public void WriteRecord(object record, string? stableKey = null, string? indexKey = null)
	{
		if (record == null) throw new ArgumentNullException(nameof(record));

		lock (_writeLock)
		{
			EnsureWriter();

			if (_currentWriter != null)
			{
				bool needsRotation = false;

				if (_maxRecordsPerShard < long.MaxValue &&
					_currentWriter.RecordCount >= _maxRecordsPerShard)
				{
					needsRotation = true;
				}

				if (_maxBytesPerShard < long.MaxValue &&
					_currentWriter.BytesWritten >= _maxBytesPerShard * ExportConstants.ShardRotationThreshold)
				{
					needsRotation = true;
				}

				if (needsRotation)
				{
					CloseCurrentShard();
					CreateNewShard();
				}
			}

			EnsureWriter();

			if (_currentWriter != null)
			{
				_currentWriter.WriteRecord(record, stableKey);
				_totalRecords++;

				if (_collectIndexEntries && !string.IsNullOrEmpty(indexKey) && _currentRelativeShardPath != null)
				{
					// For compressed shards, byte offset/length are not useful after compression
					// Store them for uncompressed mode, but consumers should primarily use line numbers
					KeyIndexEntry entry = new KeyIndexEntry
					{
						Key = indexKey,
						Shard = _currentRelativeShardPath,
						Offset = _compressionKind == CompressionKind.None ? _currentWriter.LastRecordOffset : 0,
						Length = _compressionKind == CompressionKind.None ? _currentWriter.LastRecordLength : 0,
						Line = _currentWriter.LastRecordLine
					};
					_indexEntries.Add(entry);
				}
			}
		}
	}

	/// <summary>
	/// Flushes current shard to disk.
	/// </summary>
	public void Flush()
	{
		_currentWriter?.Flush();
	}

	public void Dispose()
	{
		CloseCurrentShard();
	}

	private void CreateNewShard()
	{
		string shardFileName = FormatShardFileName(_currentShardIndex);
		string relativePath = OutputPathHelper.GetShardRelativePath(_shardDirectory, shardFileName);
		string absolutePath = OutputPathHelper.ResolveAbsolutePath(_outputRoot, relativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

		_currentWriter = new NdjsonWriter(absolutePath, _jsonSettings);
		_currentRelativeShardPath = relativePath;
	}

	private void EnsureWriter()
	{
		if (_currentWriter != null)
		{
			return;
		}

		CreateNewShard();
	}

	private void CloseCurrentShard()
	{
		if (_currentWriter == null || _currentRelativeShardPath == null)
		{
			return;
		}

		_currentWriter.Flush();

		string relativePath = _currentRelativeShardPath;
		string absolutePath = OutputPathHelper.ResolveAbsolutePath(_outputRoot, relativePath);

		ShardDescriptor descriptor = _currentWriter.CreateDescriptor(_descriptorDomain, relativePath, _compressionKind == CompressionKind.None ? "none" : null);
		_currentWriter.Dispose();
		_currentWriter = null;

		FinalizeShard(absolutePath, relativePath, ref descriptor);
		_shardDescriptors.Add(descriptor);
		_totalBytes += descriptor.Bytes;
		_currentRelativeShardPath = null;
		_currentShardIndex++;
	}

	private string FormatShardFileName(int index)
	{
		return $"part-{index:D5}.ndjson";
	}

	private void FinalizeShard(string absolutePath, string relativePath, ref ShardDescriptor descriptor)
	{
		relativePath = OutputPathHelper.NormalizeRelativePath(relativePath);

		if (_compressionKind == CompressionKind.None)
		{
			descriptor.Shard = relativePath;
			descriptor.Compression = "none";
			descriptor.UncompressedBytes = descriptor.Bytes;
			descriptor.Sha256 = ComputeSha256(absolutePath);
			return;
		}

		// Determine compression file extension and method
		string compressionExtension = _compressionKind == CompressionKind.Gzip ? ".gz" : ".zst";
		string compressedRelativePath = OutputPathHelper.NormalizeRelativePath(relativePath + compressionExtension);
		string compressedAbsolutePath = OutputPathHelper.ResolveAbsolutePath(_outputRoot, compressedRelativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(compressedAbsolutePath)!);

		using (FileStream inputStream = File.OpenRead(absolutePath))
		using (FileStream outputStream = File.Create(compressedAbsolutePath))
		{
			if (_compressionKind == CompressionKind.Gzip)
			{
				CompressToGzip(inputStream, outputStream);
			}
			else
			{
				CompressToZstd(inputStream, outputStream);
			}
		}

		descriptor.UncompressedBytes = descriptor.Bytes;
		descriptor.Bytes = new FileInfo(compressedAbsolutePath).Length;
		descriptor.Shard = compressedRelativePath;
		
		// Set compression metadata
		if (_compressionKind == CompressionKind.Gzip)
		{
			descriptor.Compression = "gzip";
			descriptor.FrameSize = null;
		}
		else
		{
			descriptor.Compression = _compressionKind == CompressionKind.ZstdSeekable ? "zstd-seekable" : "zstd";
			descriptor.FrameSize = _compressionKind == CompressionKind.ZstdSeekable ? _seekableFrameSize : null;
		}
		
		descriptor.Sha256 = ComputeSha256(compressedAbsolutePath);

		File.Delete(absolutePath);

	}

	private void CompressToGzip(Stream input, Stream output)
	{
		byte[] buffer = new byte[ExportConstants.FileBufferSize];
		
		using (GZipStream gzipStream = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
		{
			CopyStream(input, gzipStream, buffer);
			gzipStream.Flush();
		}
	}

	private void CompressToZstd(Stream input, Stream output)
	{
		byte[] buffer = new byte[ExportConstants.FileBufferSize];

		if (_compressionKind == CompressionKind.Zstd)
		{
			using CompressionStream compressionStream = new CompressionStream(output, leaveOpen: true);
			CopyStream(input, compressionStream, buffer);
			compressionStream.Flush();
			return;
		}

		// Approximate seekable behaviour by emitting discrete frames of roughly _seekableFrameSize bytes
		long remaining = input.Length - input.Position;
		while (remaining > 0)
		{
			long chunkSize = Math.Min((long)_seekableFrameSize, remaining);
			using CompressionStream frameStream = new CompressionStream(output, leaveOpen: true);
			CopyStream(input, frameStream, buffer, chunkSize);
			frameStream.Flush();
			remaining -= chunkSize;
		}
	}

	private static void CopyStream(Stream source, Stream destination, byte[] buffer)
	{
		int bytesRead;
		while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
		{
			destination.Write(buffer, 0, bytesRead);
		}
	}

	private static void CopyStream(Stream source, Stream destination, byte[] buffer, long limit)
	{
		long remaining = limit;
		while (remaining > 0)
		{
			int readSize = (int)Math.Min(buffer.Length, remaining);
			int bytesRead = source.Read(buffer, 0, readSize);
			if (bytesRead <= 0)
			{
				break;
			}
			destination.Write(buffer, 0, bytesRead);
			remaining -= bytesRead;
		}
	}

	private static string ComputeSha256(string path)
	{
		using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using SHA256 sha256 = SHA256.Create();
		byte[] hash = sha256.ComputeHash(stream);
		return Convert.ToHexString(hash);
	}
}
