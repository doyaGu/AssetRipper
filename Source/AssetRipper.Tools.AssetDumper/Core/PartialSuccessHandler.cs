using AssetRipper.Import.Logging;

namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Tracks the result of an operation that may partially succeed.
/// Used for scenarios where some items succeed while others fail.
/// </summary>
public sealed class PartialSuccessResult
{
    /// <summary>
    /// Gets or sets the total number of items processed.
    /// </summary>
    public int TotalItems { get; set; }
    
    /// <summary>
    /// Gets or sets the number of items that succeeded.
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Gets or sets the number of items that failed.
    /// </summary>
    public int FailureCount { get; set; }
    
    /// <summary>
    /// Gets or sets the number of items that were skipped.
    /// </summary>
    public int SkippedCount { get; set; }
    
    /// <summary>
    /// Gets the list of errors that occurred during processing.
    /// </summary>
    public List<OperationError> Errors { get; } = new();
    
    /// <summary>
    /// Gets whether the operation was completely successful.
    /// </summary>
    public bool IsCompleteSuccess => FailureCount == 0 && SkippedCount == 0;
    
    /// <summary>
    /// Gets whether the operation had any successes.
    /// </summary>
    public bool HasAnySuccess => SuccessCount > 0;
    
    /// <summary>
    /// Gets whether the operation was a complete failure.
    /// </summary>
    public bool IsCompleteFailure => SuccessCount == 0 && TotalItems > 0;
    
    /// <summary>
    /// Gets the success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate => TotalItems > 0 ? (SuccessCount * 100.0 / TotalItems) : 0;
    
    /// <summary>
    /// Adds an error to the result.
    /// </summary>
    public void AddError(string item, Exception exception, bool isCritical = false)
    {
        Errors.Add(new OperationError
        {
            Item = item,
            Exception = exception,
            IsCritical = isCritical,
            Timestamp = DateTime.UtcNow
        });
        FailureCount++;
    }
    
    /// <summary>
    /// Adds a skipped item.
    /// </summary>
    public void AddSkipped(string item, string reason)
    {
        Errors.Add(new OperationError
        {
            Item = item,
            Message = reason,
            IsSkipped = true,
            Timestamp = DateTime.UtcNow
        });
        SkippedCount++;
    }
    
    /// <summary>
    /// Records a successful item.
    /// </summary>
    public void AddSuccess()
    {
        SuccessCount++;
    }
    
    /// <summary>
    /// Logs a summary of the result.
    /// </summary>
    public void LogSummary(string operationName)
    {
        if (IsCompleteSuccess)
        {
            Logger.Info($"{operationName} completed successfully: {SuccessCount}/{TotalItems} items processed");
        }
        else if (IsCompleteFailure)
        {
            Logger.Error($"{operationName} failed completely: 0/{TotalItems} items succeeded");
        }
        else
        {
            Logger.Warning($"{operationName} completed with partial success:");
            Logger.Warning($"  Success: {SuccessCount}/{TotalItems} ({SuccessRate:F1}%)");
            if (FailureCount > 0)
            {
                Logger.Warning($"  Failed: {FailureCount}");
            }
            if (SkippedCount > 0)
            {
                Logger.Warning($"  Skipped: {SkippedCount}");
            }
        }
        
        // Log first few errors for context
        if (Errors.Count > 0)
        {
            int errorsToShow = Math.Min(5, Errors.Count);
            Logger.Warning($"First {errorsToShow} error(s):");
            
            for (int i = 0; i < errorsToShow; i++)
            {
                OperationError error = Errors[i];
                if (error.IsSkipped)
                {
                    Logger.Warning($"  [{i + 1}] Skipped '{error.Item}': {error.Message}");
                }
                else
                {
                    string critical = error.IsCritical ? " [CRITICAL]" : "";
                    Logger.Warning($"  [{i + 1}]{critical} '{error.Item}': {error.Exception?.Message ?? error.Message}");
                }
            }
            
            if (Errors.Count > errorsToShow)
            {
                Logger.Warning($"  ... and {Errors.Count - errorsToShow} more error(s)");
            }
        }
    }
    
    /// <summary>
    /// Determines the appropriate error code based on the result.
    /// </summary>
    public ErrorCode GetErrorCode()
    {
        if (IsCompleteSuccess)
        {
            return ErrorCode.Success;
        }
        
        if (IsCompleteFailure)
        {
            return ErrorCode.ProcessingFailed;
        }
        
        // Partial success - check if there were critical errors
        bool hasCriticalErrors = Errors.Any(e => e.IsCritical);
        if (hasCriticalErrors)
        {
            return ErrorCode.ProcessingFailed;
        }
        
        // Partial success without critical errors
        return ErrorCode.PartialSuccess;
    }
}

/// <summary>
/// Represents an error that occurred during an operation.
/// </summary>
public sealed class OperationError
{
    /// <summary>
    /// Gets or sets the item that failed.
    /// </summary>
    public string Item { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the exception that occurred.
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// Gets or sets a custom error message.
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// Gets or sets whether this error is critical and should cause the operation to fail.
    /// </summary>
    public bool IsCritical { get; set; }
    
    /// <summary>
    /// Gets or sets whether this item was skipped rather than failed.
    /// </summary>
    public bool IsSkipped { get; set; }
    
    /// <summary>
    /// Gets or sets when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Enhanced exception handler with support for partial success scenarios.
/// </summary>
public static class ExceptionHandlerExtensions
{
    /// <summary>
    /// Executes an action for each item in a collection, tracking successes and failures.
    /// Continues processing even if individual items fail.
    /// </summary>
    /// <typeparam name="T">Type of items to process</typeparam>
    /// <param name="items">Items to process</param>
    /// <param name="action">Action to perform on each item</param>
    /// <param name="itemSelector">Function to get a description of each item for logging</param>
    /// <param name="continueOnError">Whether to continue processing after errors</param>
    /// <param name="maxConsecutiveErrors">Maximum consecutive errors before aborting (0 = no limit)</param>
    /// <returns>Result containing success/failure statistics</returns>
    public static PartialSuccessResult ProcessWithRecovery<T>(
        IEnumerable<T> items,
        Action<T> action,
        Func<T, string> itemSelector,
        bool continueOnError = true,
        int maxConsecutiveErrors = 10)
    {
        var result = new PartialSuccessResult();
        int consecutiveErrors = 0;
        
        foreach (T item in items)
        {
            result.TotalItems++;
            string itemName = itemSelector(item);
            
            try
            {
                action(item);
                result.AddSuccess();
                consecutiveErrors = 0; // Reset on success
            }
            catch (Exception ex)
            {
                result.AddError(itemName, ex);
                consecutiveErrors++;
                
                // Check if we should abort due to too many consecutive errors
                if (maxConsecutiveErrors > 0 && consecutiveErrors >= maxConsecutiveErrors)
                {
                    Logger.Error($"Aborting after {consecutiveErrors} consecutive errors");
                    break;
                }
                
                if (!continueOnError)
                {
                    break;
                }
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Executes an async action for each item, tracking successes and failures.
    /// </summary>
    public static async Task<PartialSuccessResult> ProcessWithRecoveryAsync<T>(
        IEnumerable<T> items,
        Func<T, Task> action,
        Func<T, string> itemSelector,
        bool continueOnError = true,
        int maxConsecutiveErrors = 10)
    {
        var result = new PartialSuccessResult();
        int consecutiveErrors = 0;
        
        foreach (T item in items)
        {
            result.TotalItems++;
            string itemName = itemSelector(item);
            
            try
            {
                await action(item);
                result.AddSuccess();
                consecutiveErrors = 0;
            }
            catch (Exception ex)
            {
                result.AddError(itemName, ex);
                consecutiveErrors++;
                
                if (maxConsecutiveErrors > 0 && consecutiveErrors >= maxConsecutiveErrors)
                {
                    Logger.Error($"Aborting after {consecutiveErrors} consecutive errors");
                    break;
                }
                
                if (!continueOnError)
                {
                    break;
                }
            }
        }
        
        return result;
    }
}
