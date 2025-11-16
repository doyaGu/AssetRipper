using System;
using System.IO;

namespace AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Helpers;

/// <summary>
/// Helper class for managing test file paths and directories.
/// Provides centralized test directory management to avoid Windows temp directory permission issues.
/// </summary>
public static class TestPathHelper
{
    /// <summary>
    /// Creates a unique temporary directory for a test in the current directory.
    /// Avoids using Path.GetTempPath() which can have permission issues on Windows.
    /// </summary>
    /// <param name="testName">Optional test name for easier identification in logs.</param>
    /// <returns>Absolute path to the created test directory.</returns>
    public static string CreateTestDirectory(string? testName = null)
    {
        string dirName = testName != null
            ? $"test-{testName}-{Guid.NewGuid():N}"
            : $"test-{Guid.NewGuid():N}";

        string testDir = Path.Combine(Directory.GetCurrentDirectory(), dirName);
        Directory.CreateDirectory(testDir);

        return testDir;
    }

    /// <summary>
    /// Safely deletes a test directory, ignoring errors.
    /// </summary>
    /// <param name="directory">Directory to delete.</param>
    public static void CleanupTestDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors - test cleanup should not fail tests
        }
    }

    /// <summary>
    /// Creates a disposable test directory that auto-cleans on disposal.
    /// </summary>
    /// <param name="testName">Optional test name for easier identification.</param>
    /// <returns>Disposable directory wrapper.</returns>
    public static DisposableDirectory CreateDisposableDirectory(string? testName = null)
    {
        return new DisposableDirectory(CreateTestDirectory(testName));
    }
}

/// <summary>
/// Disposable wrapper for test directories that auto-cleans on disposal.
/// Usage: using var testDir = TestPathHelper.CreateDisposableDirectory();
/// </summary>
public sealed class DisposableDirectory : IDisposable
{
    public string Path { get; }

    public DisposableDirectory(string path)
    {
        Path = path;
    }

    public void Dispose()
    {
        TestPathHelper.CleanupTestDirectory(Path);
    }

    public static implicit operator string(DisposableDirectory dir) => dir.Path;
}
