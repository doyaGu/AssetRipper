using AssetRipper.Import.Logging;

namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Centralized exception handling and logging for AssetDumper.
/// </summary>
public static class ExceptionHandler
{
	/// <summary>
	/// Handles an exception and returns the appropriate error code.
	/// </summary>
	public static int HandleException(Exception ex, bool verbose = false)
	{
		return ex switch
		{
			AssetDumperException ade => HandleAssetDumperException(ade, verbose),
			DirectoryNotFoundException dnf => HandleDirectoryNotFoundException(dnf),
			UnauthorizedAccessException uae => HandleUnauthorizedAccessException(uae),
			IOException ioe => HandleIOException(ioe, verbose),
			_ => HandleUnexpectedException(ex, verbose)
		};
	}

	private static int HandleAssetDumperException(AssetDumperException ex, bool verbose)
	{
		if (ex is SchemaValidationException sve)
		{
			Logger.Error($"Schema validation failed: {ex.Message}");
			if (sve.SchemaPath != null)
			{
				Logger.Error($"Schema path: {sve.SchemaPath}");
			}
			if (sve.ErrorCount > 0)
			{
				Logger.Error($"Error count: {sve.ErrorCount}");
			}
		}
		else if (ex is ManifestException)
		{
			Logger.Error($"Manifest error: {ex.Message}");
		}
		else if (ex is GameDataLoadException gdle)
		{
			Logger.Error($"Failed to load game data from: {gdle.InputPath}");
			Logger.Error($"Error: {ex.Message}");
		}
		else if (ex is CompressionException)
		{
			Logger.Error($"Compression error: {ex.Message}");
		}
		else
		{
			Logger.Error($"AssetDumper error: {ex.Message}");
		}

		if (verbose && ex.InnerException != null)
		{
			Logger.Error("Inner exception:", ex.InnerException);
		}

		return (int)ex.ErrorCode;
	}

	private static int HandleDirectoryNotFoundException(DirectoryNotFoundException ex)
	{
		Logger.Error($"Directory not found: {ex.Message}");
		return (int)ErrorCode.DirectoryNotFound;
	}

	private static int HandleUnauthorizedAccessException(UnauthorizedAccessException ex)
	{
		Logger.Error($"Access denied: {ex.Message}");
		Logger.Error("Please check file/folder permissions and try again.");
		return (int)ErrorCode.AccessDenied;
	}

	private static int HandleIOException(IOException ex, bool verbose)
	{
		Logger.Error($"I/O error: {ex.Message}");
		if (verbose)
		{
			Logger.Error("Stack trace:", ex);
		}
		return (int)ErrorCode.ProcessingFailed;
	}

	private static int HandleUnexpectedException(Exception ex, bool verbose)
	{
		Logger.Error("Unexpected error occurred", ex);
		if (verbose)
		{
			Logger.Error($"Exception type: {ex.GetType().FullName}");
		}
		return (int)ErrorCode.ProcessingFailed;
	}

	/// <summary>
	/// Executes an action and handles any exceptions that occur.
	/// </summary>
	public static int ExecuteWithErrorHandling(Action action, bool verbose = false)
	{
		try
		{
			action();
			return (int)ErrorCode.Success;
		}
		catch (Exception ex)
		{
			return HandleException(ex, verbose);
		}
	}

	/// <summary>
	/// Executes a function and handles any exceptions that occur.
	/// </summary>
	public static int ExecuteWithErrorHandling(Func<int> func, bool verbose = false)
	{
		try
		{
			return func();
		}
		catch (Exception ex)
		{
			return HandleException(ex, verbose);
		}
	}

	/// <summary>
	/// Executes an async function and handles any exceptions that occur.
	/// </summary>
	public static async Task<int> ExecuteWithErrorHandlingAsync(Func<Task<int>> func, bool verbose = false)
	{
		try
		{
			return await func();
		}
		catch (Exception ex)
		{
			return HandleException(ex, verbose);
		}
	}
}
