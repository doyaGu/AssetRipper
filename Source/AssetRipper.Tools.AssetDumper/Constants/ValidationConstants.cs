namespace AssetRipper.Tools.AssetDumper.Constants;

/// <summary>
/// Constants for validation, error handling, and reporting.
/// </summary>
public static class ValidationConstants
{
	/// <summary>
	/// Maximum number of schema validation errors to report in logs.
	/// </summary>
	public const int MaxReportedErrors = 50;

	/// <summary>
	/// Maximum number of errors to show in console output.
	/// </summary>
	public const int MaxErrorsToShow = 5;

	/// <summary>
	/// Threshold for breaking pointer repeat cycles in dependency analysis.
	/// </summary>
	public const int PointerRepeatBreakThreshold = 512;

	/// <summary>
	/// Divisor for converting bytes to kilobytes/megabytes/etc.
	/// </summary>
	public const double ByteUnitDivisor = 1024.0;
}
