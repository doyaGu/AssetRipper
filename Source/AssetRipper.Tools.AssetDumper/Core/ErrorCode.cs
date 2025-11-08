namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Error codes for different failure scenarios in AssetDumper.
/// </summary>
public enum ErrorCode
{
	/// <summary>
	/// Operation completed successfully.
	/// </summary>
	Success = 0,

	/// <summary>
	/// Generic processing failure.
	/// </summary>
	ProcessingFailed = 1,

	/// <summary>
	/// Directory or file not found.
	/// </summary>
	DirectoryNotFound = 2,

	/// <summary>
	/// Insufficient permissions to access file system.
	/// </summary>
	AccessDenied = 3,

	/// <summary>
	/// Schema validation failed.
	/// </summary>
	SchemaValidationFailed = 4,

	/// <summary>
	/// Invalid command-line arguments.
	/// </summary>
	InvalidArguments = 5,

	/// <summary>
	/// Manifest generation or loading failed.
	/// </summary>
	ManifestError = 6,

	/// <summary>
	/// Export operation was cancelled by user.
	/// </summary>
	OperationCancelled = 7,

	/// <summary>
	/// Insufficient disk space for export.
	/// </summary>
	InsufficientDiskSpace = 8,

	/// <summary>
	/// Compression or decompression failed.
	/// </summary>
	CompressionError = 9,

	/// <summary>
	/// Game data loading failed.
	/// </summary>
	GameDataLoadError = 10,

	/// <summary>
	/// Operation completed with partial success (some items succeeded, some failed).
	/// </summary>
	PartialSuccess = 11,
}
