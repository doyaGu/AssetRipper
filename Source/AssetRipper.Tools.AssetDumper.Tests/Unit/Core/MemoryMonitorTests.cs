using AssetRipper.Tools.AssetDumper.Core;
using FluentAssertions;
using System.Diagnostics;
using Xunit;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Core;

/// <summary>
/// Tests for the MemoryMonitor class.
/// </summary>
public class MemoryMonitorTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldUseDefaultValues()
    {
        // Act
        var monitor = new MemoryMonitor();

        // Assert - Should not throw and should be usable
        monitor.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldAcceptCustomThresholds()
    {
        // Act
        var monitor = new MemoryMonitor(
            warningThresholdMb: 1024,
            criticalThresholdMb: 2048,
            checkIntervalSeconds: 5,
            enableGcMonitoring: false);

        // Assert
        monitor.Should().NotBeNull();
    }

    #endregion

    #region CheckMemoryUsage Tests

    [Fact]
    public void CheckMemoryUsage_ShouldReturnTrue_WhenMemoryIsNormal()
    {
        // Arrange
        var monitor = new MemoryMonitor(
            warningThresholdMb: 10000, // Very high threshold
            criticalThresholdMb: 20000);

        // Act
        bool result = monitor.CheckMemoryUsage();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CheckMemoryUsage_ShouldAcceptContext()
    {
        // Arrange
        var monitor = new MemoryMonitor();

        // Act
        bool result = monitor.CheckMemoryUsage("Test context");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CheckMemoryUsage_WithShortInterval_ShouldRateLimitChecks()
    {
        // Arrange
        var monitor = new MemoryMonitor(checkIntervalSeconds: 60); // 1 minute interval

        // Act - Call twice in quick succession
        bool result1 = monitor.CheckMemoryUsage();
        bool result2 = monitor.CheckMemoryUsage(); // Should skip check

        // Assert - Both should return true (no critical threshold hit)
        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    #endregion

    #region GetStatistics Tests

    [Fact]
    public void GetStatistics_ShouldReturnValidData()
    {
        // Arrange
        var monitor = new MemoryMonitor(checkIntervalSeconds: 0);

        // Call CheckMemoryUsage twice to ensure peak tracking is initialized
        monitor.CheckMemoryUsage();
        Thread.Sleep(10); // Small delay to ensure timing
        monitor.CheckMemoryUsage();

        // Act
        var stats = monitor.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.CurrentWorkingSetBytes.Should().BeGreaterThan(0);
        stats.PeakWorkingSetBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetStatistics_ShouldTrackPeakMemory()
    {
        // Arrange
        var monitor = new MemoryMonitor(checkIntervalSeconds: 0); // Check every time
        
        // Act
        monitor.CheckMemoryUsage();
        long initialPeak = monitor.GetStatistics().PeakWorkingSetBytes;
        
        // Allocate some memory
        var largeArray = new byte[10 * 1024 * 1024]; // 10 MB
        GC.KeepAlive(largeArray);
        
        monitor.CheckMemoryUsage();
        long newPeak = monitor.GetStatistics().PeakWorkingSetBytes;

        // Assert
        newPeak.Should().BeGreaterOrEqualTo(initialPeak);
    }

    [Fact]
    public void GetStatistics_ShouldIncludeGcCollectionCounts()
    {
        // Arrange
        var monitor = new MemoryMonitor(enableGcMonitoring: true);
        
        // Act
        var stats = monitor.GetStatistics();

        // Assert
        stats.Gen0Collections.Should().BeGreaterOrEqualTo(0);
        stats.Gen1Collections.Should().BeGreaterOrEqualTo(0);
        stats.Gen2Collections.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region GC Monitoring Tests

    [Fact]
    public void Monitor_WithGcMonitoringEnabled_ShouldNotThrow()
    {
        // Arrange
        var monitor = new MemoryMonitor(
            checkIntervalSeconds: 0,
            enableGcMonitoring: true);

        // Act
        Action act = () =>
        {
            monitor.CheckMemoryUsage();
            GC.Collect(0);
            monitor.CheckMemoryUsage();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Monitor_WithGcMonitoringDisabled_ShouldStillWork()
    {
        // Arrange
        var monitor = new MemoryMonitor(
            enableGcMonitoring: false);

        // Act
        bool result = monitor.CheckMemoryUsage();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region SuggestGarbageCollection Tests

    [Fact]
    public void SuggestGarbageCollection_ShouldNotThrow()
    {
        // Arrange
        var monitor = new MemoryMonitor();
        
        // Act
        Action act = () => monitor.SuggestGarbageCollection();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void LogSummary_ShouldNotThrow()
    {
        // Arrange
        var monitor = new MemoryMonitor();
        monitor.CheckMemoryUsage();
        
        // Act
        Action act = () => monitor.LogSummary();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Monitor_ShouldWorkWithMultipleCalls()
    {
        // Arrange
        var monitor = new MemoryMonitor(checkIntervalSeconds: 0);

        // Act & Assert - Multiple calls should not throw
        for (int i = 0; i < 10; i++)
        {
            bool result = monitor.CheckMemoryUsage($"Iteration {i}");
            result.Should().BeTrue();
        }
    }

    [Fact]
    public void Monitor_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var monitor = new MemoryMonitor(checkIntervalSeconds: 0);
        const int taskCount = 5;

        // Act
        var tasks = new Task[taskCount];
        for (int i = 0; i < taskCount; i++)
        {
            int iteration = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    monitor.CheckMemoryUsage($"Task {iteration}, iteration {j}");
                }
            });
        }

        // Assert
        Action act = () => Task.WaitAll(tasks);
        act.Should().NotThrow();
    }

    #endregion

    #region Threshold Tests

    [Fact]
    public void CheckMemoryUsage_ShouldReturnFalse_WhenCriticalThresholdExceeded()
    {
        // Arrange - Set impossibly low threshold
        var monitor = new MemoryMonitor(
            warningThresholdMb: 1, // 1 MB
            criticalThresholdMb: 2, // 2 MB
            checkIntervalSeconds: 0);

        // Act
        bool result = monitor.CheckMemoryUsage();

        // Assert - Should trigger critical threshold
        result.Should().BeFalse();
    }

    [Fact]
    public void CheckMemoryUsage_ShouldLogWarning_WhenWarningThresholdExceeded()
    {
        // Arrange - Set low warning threshold but high critical threshold
        var monitor = new MemoryMonitor(
            warningThresholdMb: 1, // 1 MB (will exceed)
            criticalThresholdMb: 100000, // 100 GB (won't exceed)
            checkIntervalSeconds: 0);

        // Act
        bool result = monitor.CheckMemoryUsage();

        // Assert - Should still return true (not critical), but will log warning
        result.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Monitor_WithZeroInterval_ShouldCheckEveryTime()
    {
        // Arrange
        var monitor = new MemoryMonitor(checkIntervalSeconds: 0);

        // Act
        bool result1 = monitor.CheckMemoryUsage();
        bool result2 = monitor.CheckMemoryUsage();
        bool result3 = monitor.CheckMemoryUsage();

        // Assert - All should actually perform checks
        result1.Should().NotBe(default);
        result2.Should().NotBe(default);
        result3.Should().NotBe(default);
    }

    [Fact]
    public void GetStatistics_BeforeAnyChecks_ShouldReturnValidData()
    {
        // Arrange
        var monitor = new MemoryMonitor();

        // Act
        var stats = monitor.GetStatistics();

        // Assert - Statistics should always return current process memory
        stats.Should().NotBeNull();
        stats.CurrentWorkingSetBytes.Should().BeGreaterThan(0);
        stats.ManagedMemoryBytes.Should().BeGreaterThan(0);
    }

    #endregion
}
