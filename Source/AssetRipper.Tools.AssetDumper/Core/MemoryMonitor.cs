using System.Diagnostics;
using AssetRipper.Import.Logging;

namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Monitors memory usage during export operations and provides warnings when memory pressure is high.
/// </summary>
public sealed class MemoryMonitor
{
    private readonly long _warningThresholdBytes;
    private readonly long _criticalThresholdBytes;
    private readonly TimeSpan _checkInterval;
    private readonly bool _enableGcMonitoring;
    
    private DateTime _lastCheckTime;
    private long _lastWorkingSet;
    private long _peakWorkingSet;
    private int _warningCount;
    private int _lastGen0Collections;
    private int _lastGen1Collections;
    private int _lastGen2Collections;

    /// <summary>
    /// Creates a new memory monitor with specified thresholds.
    /// </summary>
    /// <param name="warningThresholdMb">Warning threshold in megabytes (default: 2048 MB)</param>
    /// <param name="criticalThresholdMb">Critical threshold in megabytes (default: 4096 MB)</param>
    /// <param name="checkIntervalSeconds">Interval between checks in seconds (default: 10)</param>
    /// <param name="enableGcMonitoring">Enable GC collection monitoring (default: true)</param>
    public MemoryMonitor(
        long warningThresholdMb = 2048,
        long criticalThresholdMb = 4096,
        int checkIntervalSeconds = 10,
        bool enableGcMonitoring = true)
    {
        _warningThresholdBytes = warningThresholdMb * 1024 * 1024;
        _criticalThresholdBytes = criticalThresholdMb * 1024 * 1024;
        _checkInterval = TimeSpan.FromSeconds(checkIntervalSeconds);
        _enableGcMonitoring = enableGcMonitoring;
        
        _lastCheckTime = DateTime.UtcNow;
        _lastWorkingSet = 0;
        _peakWorkingSet = 0;
        _warningCount = 0;
        
        if (_enableGcMonitoring)
        {
            _lastGen0Collections = GC.CollectionCount(0);
            _lastGen1Collections = GC.CollectionCount(1);
            _lastGen2Collections = GC.CollectionCount(2);
        }
    }

    /// <summary>
    /// Checks current memory usage and logs warnings if thresholds are exceeded.
    /// Should be called periodically during long-running operations.
    /// </summary>
    /// <param name="context">Context information for logging (e.g., "Processing asset 1000/5000")</param>
    /// <returns>True if memory usage is within acceptable limits, false if critical threshold exceeded</returns>
    public bool CheckMemoryUsage(string? context = null)
    {
        DateTime now = DateTime.UtcNow;
        
        // Only check at specified intervals
        if (now - _lastCheckTime < _checkInterval)
        {
            return true;
        }
        
        _lastCheckTime = now;
        
        using Process currentProcess = Process.GetCurrentProcess();
        long currentWorkingSet = currentProcess.WorkingSet64;
        
        // Update peak
        if (currentWorkingSet > _peakWorkingSet)
        {
            _peakWorkingSet = currentWorkingSet;
        }
        
        // Calculate change since last check
        long deltaBytes = currentWorkingSet - _lastWorkingSet;
        _lastWorkingSet = currentWorkingSet;
        
        // Check GC statistics if enabled
        if (_enableGcMonitoring)
        {
            CheckGcActivity();
        }
        
        // Check thresholds
        if (currentWorkingSet >= _criticalThresholdBytes)
        {
            LogCriticalMemory(currentWorkingSet, deltaBytes, context);
            return false;
        }
        
        if (currentWorkingSet >= _warningThresholdBytes)
        {
            LogWarningMemory(currentWorkingSet, deltaBytes, context);
            _warningCount++;
            
            // Suggest GC if we've had multiple warnings
            if (_warningCount % 3 == 0)
            {
                Logger.Warning("Memory pressure remains high. Consider reducing batch size or enabling incremental processing.");
                SuggestGarbageCollection();
            }
        }
        else
        {
            // Reset warning count if memory is back to normal
            _warningCount = 0;
        }
        
        return true;
    }

    /// <summary>
    /// Forces a garbage collection if memory usage is high.
    /// Use sparingly as it can impact performance.
    /// </summary>
    public void SuggestGarbageCollection()
    {
        long currentMemory = GC.GetTotalMemory(false);
        
        if (currentMemory > _warningThresholdBytes / 2)
        {
            Logger.Info("Triggering garbage collection to free memory...");
            
            // Collect all generations
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            
            long afterMemory = GC.GetTotalMemory(false);
            long freed = currentMemory - afterMemory;
            
            Logger.Info($"Garbage collection completed. Freed {FormatBytes(freed)}");
        }
    }

    /// <summary>
    /// Gets a summary of memory usage statistics.
    /// </summary>
    public MemoryStatistics GetStatistics()
    {
        using Process currentProcess = Process.GetCurrentProcess();
        
        return new MemoryStatistics
        {
            CurrentWorkingSetBytes = currentProcess.WorkingSet64,
            PeakWorkingSetBytes = _peakWorkingSet,
            ManagedMemoryBytes = GC.GetTotalMemory(false),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }

    /// <summary>
    /// Logs a summary of memory usage.
    /// </summary>
    public void LogSummary()
    {
        MemoryStatistics stats = GetStatistics();
        
        Logger.Info("=== Memory Usage Summary ===");
        Logger.Info($"Current Working Set: {FormatBytes(stats.CurrentWorkingSetBytes)}");
        Logger.Info($"Peak Working Set: {FormatBytes(stats.PeakWorkingSetBytes)}");
        Logger.Info($"Managed Memory: {FormatBytes(stats.ManagedMemoryBytes)}");
        
        if (_enableGcMonitoring)
        {
            Logger.Info($"GC Collections - Gen0: {stats.Gen0Collections}, Gen1: {stats.Gen1Collections}, Gen2: {stats.Gen2Collections}");
        }
    }

    private void CheckGcActivity()
    {
        int gen0Collections = GC.CollectionCount(0);
        int gen1Collections = GC.CollectionCount(1);
        int gen2Collections = GC.CollectionCount(2);
        
        int gen0Delta = gen0Collections - _lastGen0Collections;
        int gen1Delta = gen1Collections - _lastGen1Collections;
        int gen2Delta = gen2Collections - _lastGen2Collections;
        
        _lastGen0Collections = gen0Collections;
        _lastGen1Collections = gen1Collections;
        _lastGen2Collections = gen2Collections;
        
        // Warn if excessive Gen2 collections (indicates memory pressure)
        if (gen2Delta > 5)
        {
            Logger.Warning($"High GC activity detected: {gen2Delta} Gen2 collections since last check. This may indicate memory pressure.");
        }
    }

    private void LogWarningMemory(long currentBytes, long deltaBytes, string? context)
    {
        string contextStr = string.IsNullOrEmpty(context) ? "" : $" ({context})";
        string deltaStr = deltaBytes > 0 ? $" (+{FormatBytes(deltaBytes)})" : "";
        
        Logger.Warning($"Memory usage high{contextStr}: {FormatBytes(currentBytes)}{deltaStr} (Warning threshold: {FormatBytes(_warningThresholdBytes)})");
    }

    private void LogCriticalMemory(long currentBytes, long deltaBytes, string? context)
    {
        string contextStr = string.IsNullOrEmpty(context) ? "" : $" ({context})";
        string deltaStr = deltaBytes > 0 ? $" (+{FormatBytes(deltaBytes)})" : "";
        
        Logger.Error($"CRITICAL: Memory usage exceeded threshold{contextStr}: {FormatBytes(currentBytes)}{deltaStr} (Critical threshold: {FormatBytes(_criticalThresholdBytes)})");
        Logger.Error("Consider stopping the export and using smaller input files or enabling incremental processing.");
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return $"{size:F2} {suffixes[suffixIndex]}";
    }
}

/// <summary>
/// Memory usage statistics snapshot.
/// </summary>
public sealed class MemoryStatistics
{
    /// <summary>
    /// Current working set size (physical memory used by process).
    /// </summary>
    public long CurrentWorkingSetBytes { get; init; }
    
    /// <summary>
    /// Peak working set size during the session.
    /// </summary>
    public long PeakWorkingSetBytes { get; init; }
    
    /// <summary>
    /// Total managed memory allocated by the CLR.
    /// </summary>
    public long ManagedMemoryBytes { get; init; }
    
    /// <summary>
    /// Total number of Generation 0 garbage collections.
    /// </summary>
    public int Gen0Collections { get; init; }
    
    /// <summary>
    /// Total number of Generation 1 garbage collections.
    /// </summary>
    public int Gen1Collections { get; init; }
    
    /// <summary>
    /// Total number of Generation 2 garbage collections.
    /// </summary>
    public int Gen2Collections { get; init; }
}
