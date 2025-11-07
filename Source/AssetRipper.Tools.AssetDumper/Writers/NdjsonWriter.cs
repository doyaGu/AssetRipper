using System.Text;
using Newtonsoft.Json;
using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Writers;

/// <summary>
/// Writes NDJSON (newline-delimited JSON) shards with optional compression.
/// Tracks shard metrics for manifest generation.
/// </summary>
internal class NdjsonWriter : IDisposable
{
	private readonly string _shardPath;
	private readonly StreamWriter _writer;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly Encoding _encoding;
	private long _recordCount;
	private long _bytesWritten;
	private string? _firstKey;
	private string? _lastKey;
	private long _lastRecordOffset;
	private long _lastRecordLength;

	public NdjsonWriter(string shardPath, JsonSerializerSettings? jsonSettings = null)
	{
		_shardPath = shardPath ?? throw new ArgumentNullException(nameof(shardPath));
		_jsonSettings = jsonSettings ?? new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};

		// Ensure directory exists
		string? directory = Path.GetDirectoryName(_shardPath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		// Create writer with UTF-8 encoding
		_encoding = new UTF8Encoding(false);
		_writer = new StreamWriter(_shardPath, false, _encoding)
		{
			AutoFlush = false
		};
		_recordCount = 0;
		_bytesWritten = 0;
	}

	public string ShardPath => _shardPath;
	public long RecordCount => _recordCount;
	public long BytesWritten => _bytesWritten;
	public string? FirstKey => _firstKey;
	public string? LastKey => _lastKey;
	public long LastRecordOffset => _lastRecordOffset;
	public long LastRecordLength => _lastRecordLength;
	public long LastRecordLine => _recordCount > 0 ? _recordCount - 1 : 0;

	/// <summary>
	/// Writes a single record as a JSON line.
	/// </summary>
	public void WriteRecord(object record, string? stableKey = null)
	{
		if (record == null) throw new ArgumentNullException(nameof(record));

		long offset = _bytesWritten;
		string json = JsonConvert.SerializeObject(record, _jsonSettings);
		_writer.WriteLine(json);
		_recordCount++;
		long bytes = _encoding.GetByteCount(json) + Environment.NewLine.Length;
		_bytesWritten += bytes;
		_lastRecordOffset = offset;
		_lastRecordLength = bytes;

		// Track key range for sorted shards
		if (!string.IsNullOrEmpty(stableKey))
		{
			if (_firstKey == null)
				_firstKey = stableKey;
			_lastKey = stableKey;
		}
	}

	/// <summary>
	/// Flushes pending writes to disk.
	/// </summary>
	public void Flush()
	{
		_writer.Flush();
	}

	public void Dispose()
	{
		_writer?.Dispose();
	}

	/// <summary>
	/// Creates a shard descriptor for the manifest.
	/// </summary>
	public ShardDescriptor CreateDescriptor(string domain, string relativePath, string? compression = null)
	{
		Flush();
		FileInfo fileInfo = new FileInfo(_shardPath);

		return new ShardDescriptor
		{
			Domain = domain,
			Shard = OutputPathHelper.NormalizeRelativePath(relativePath),
			Records = _recordCount,
			Bytes = fileInfo.Length,
			Compression = compression ?? "none",
			UncompressedBytes = fileInfo.Length,
			FirstKey = _firstKey,
			LastKey = _lastKey
		};
	}
}

/// <summary>
/// Shard descriptor for manifest generation.
/// </summary>
public class ShardDescriptor
{
	[JsonProperty("domain")]
	public string Domain { get; set; } = string.Empty;

	[JsonProperty("shard")]
	public string Shard { get; set; } = string.Empty;

	[JsonProperty("records")]
	public long Records { get; set; }

	[JsonProperty("bytes")]
	public long Bytes { get; set; }

	[JsonProperty("compression")]
	public string? Compression { get; set; }

	[JsonProperty("firstKey", NullValueHandling = NullValueHandling.Ignore)]
	public string? FirstKey { get; set; }

	[JsonProperty("lastKey", NullValueHandling = NullValueHandling.Ignore)]
	public string? LastKey { get; set; }

	[JsonProperty("uncompressedBytes", NullValueHandling = NullValueHandling.Ignore)]
	public long? UncompressedBytes { get; set; }

	[JsonProperty("frame_size", NullValueHandling = NullValueHandling.Ignore)]
	public int? FrameSize { get; set; }

	[JsonProperty("sha256", NullValueHandling = NullValueHandling.Ignore)]
	public string? Sha256 { get; set; }
}
