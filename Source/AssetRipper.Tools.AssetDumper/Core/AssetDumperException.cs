namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Base exception for AssetDumper-specific errors.
/// </summary>
public class AssetDumperException : Exception
{
	/// <summary>
	/// Gets the error code associated with this exception.
	/// </summary>
	public ErrorCode ErrorCode { get; }

	public AssetDumperException(ErrorCode errorCode, string message)
		: base(message)
	{
		ErrorCode = errorCode;
	}

	public AssetDumperException(ErrorCode errorCode, string message, Exception innerException)
		: base(message, innerException)
	{
		ErrorCode = errorCode;
	}
}

/// <summary>
/// Exception thrown when schema validation fails.
/// </summary>
public class SchemaValidationException : AssetDumperException
{
	public string? SchemaPath { get; }
	public int ErrorCount { get; }

	public SchemaValidationException(string message, string? schemaPath = null, int errorCount = 0)
		: base(ErrorCode.SchemaValidationFailed, message)
	{
		SchemaPath = schemaPath;
		ErrorCount = errorCount;
	}
}

/// <summary>
/// Exception thrown when manifest operations fail.
/// </summary>
public class ManifestException : AssetDumperException
{
	public ManifestException(string message)
		: base(ErrorCode.ManifestError, message)
	{
	}

	public ManifestException(string message, Exception innerException)
		: base(ErrorCode.ManifestError, message, innerException)
	{
	}
}

/// <summary>
/// Exception thrown when game data cannot be loaded.
/// </summary>
public class GameDataLoadException : AssetDumperException
{
	public string InputPath { get; }

	public GameDataLoadException(string inputPath, string message)
		: base(ErrorCode.GameDataLoadError, message)
	{
		InputPath = inputPath;
	}

	public GameDataLoadException(string inputPath, string message, Exception innerException)
		: base(ErrorCode.GameDataLoadError, message, innerException)
	{
		InputPath = inputPath;
	}
}

/// <summary>
/// Exception thrown when compression operations fail.
/// </summary>
public class CompressionException : AssetDumperException
{
	public CompressionException(string message)
		: base(ErrorCode.CompressionError, message)
	{
	}

	public CompressionException(string message, Exception innerException)
		: base(ErrorCode.CompressionError, message, innerException)
	{
	}
}
