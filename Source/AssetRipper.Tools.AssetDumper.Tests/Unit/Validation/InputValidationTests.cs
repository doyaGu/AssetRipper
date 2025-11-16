using Xunit;
using System.IO;
using System;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Validation;

/// <summary>
/// Tests for input path validation logic.
/// Ensures AssetDumper only accepts Unity game directories, not previous export results.
/// </summary>
public class InputValidationTests
{
	[Fact]
	public void InputPath_WithManifestJson_ShouldBeRejected()
	{
		// Arrange: Create a temporary directory that looks like AssetDumper output
		string tempDir = Path.Combine(Path.GetTempPath(), $"AssetDumper_Test_{Guid.NewGuid()}");
		try
		{
			Directory.CreateDirectory(tempDir);
			string manifestPath = Path.Combine(tempDir, "manifest.json");
			File.WriteAllText(manifestPath, "{}");

			// Act & Assert: Should detect this as AssetDumper output
			bool isExportDir = IsAssetDumperOutput(tempDir);

			Assert.True(isExportDir, "Directory with manifest.json should be detected as AssetDumper output");
		}
		finally
		{
			// Cleanup
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, true);
			}
		}
	}

	[Fact]
	public void InputPath_WithMultipleExportDirs_ShouldBeRejected()
	{
		// Arrange: Create a directory with typical AssetDumper export structure
		string tempDir = Path.Combine(Path.GetTempPath(), $"AssetDumper_Test_{Guid.NewGuid()}");
		try
		{
			Directory.CreateDirectory(tempDir);
			Directory.CreateDirectory(Path.Combine(tempDir, "facts"));
			Directory.CreateDirectory(Path.Combine(tempDir, "relations"));
			Directory.CreateDirectory(Path.Combine(tempDir, "schema"));

			// Act & Assert: Should detect this as AssetDumper output (3+ characteristic dirs)
			bool isExportDir = IsAssetDumperOutput(tempDir);

			Assert.True(isExportDir, "Directory with 3+ characteristic export directories should be detected as AssetDumper output");
		}
		finally
		{
			// Cleanup
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, true);
			}
		}
	}

	[Fact]
	public void InputPath_UnityGameDirectory_ShouldBeAccepted()
	{
		// Arrange: Create a directory that looks like a Unity game
		string tempDir = Path.Combine(Path.GetTempPath(), $"AssetDumper_Test_{Guid.NewGuid()}");
		try
		{
			Directory.CreateDirectory(tempDir);
			// Typical Unity game structure
			Directory.CreateDirectory(Path.Combine(tempDir, "Managed"));
			Directory.CreateDirectory(Path.Combine(tempDir, "Resources"));
			File.WriteAllText(Path.Combine(tempDir, "globalgamemanagers"), "dummy");
			File.WriteAllText(Path.Combine(tempDir, "level0"), "dummy");

			// Act & Assert: Should NOT detect this as AssetDumper output
			bool isExportDir = IsAssetDumperOutput(tempDir);

			Assert.False(isExportDir, "Unity game directory should NOT be detected as AssetDumper output");
		}
		finally
		{
			// Cleanup
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, true);
			}
		}
	}

	[Fact]
	public void InputPath_EmptyDirectory_ShouldBeAccepted()
	{
		// Arrange: Create an empty directory
		string tempDir = Path.Combine(Path.GetTempPath(), $"AssetDumper_Test_{Guid.NewGuid()}");
		try
		{
			Directory.CreateDirectory(tempDir);

			// Act & Assert: Empty directory should NOT be detected as AssetDumper output
			bool isExportDir = IsAssetDumperOutput(tempDir);

			Assert.False(isExportDir, "Empty directory should NOT be detected as AssetDumper output");
		}
		finally
		{
			// Cleanup
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, true);
			}
		}
	}

	[Fact]
	public void InputPath_WithOnlyFactsDir_ShouldBeAccepted()
	{
		// Arrange: Directory with only 1 characteristic dir (not enough to trigger rejection)
		string tempDir = Path.Combine(Path.GetTempPath(), $"AssetDumper_Test_{Guid.NewGuid()}");
		try
		{
			Directory.CreateDirectory(tempDir);
			Directory.CreateDirectory(Path.Combine(tempDir, "facts"));

			// Act & Assert: Single characteristic dir is not enough to trigger rejection
			bool isExportDir = IsAssetDumperOutput(tempDir);

			Assert.False(isExportDir, "Directory with only 1 characteristic dir should NOT be detected as AssetDumper output");
		}
		finally
		{
			// Cleanup
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, true);
			}
		}
	}

	[Fact]
	public void InputPath_WithTwoExportDirs_ShouldBeAccepted()
	{
		// Arrange: Directory with 2 characteristic dirs (not enough to trigger rejection)
		string tempDir = Path.Combine(Path.GetTempPath(), $"AssetDumper_Test_{Guid.NewGuid()}");
		try
		{
			Directory.CreateDirectory(tempDir);
			Directory.CreateDirectory(Path.Combine(tempDir, "facts"));
			Directory.CreateDirectory(Path.Combine(tempDir, "relations"));

			// Act & Assert: 2 characteristic dirs is not enough to trigger rejection (threshold is 3)
			bool isExportDir = IsAssetDumperOutput(tempDir);

			Assert.False(isExportDir, "Directory with only 2 characteristic dirs should NOT be detected as AssetDumper output");
		}
		finally
		{
			// Cleanup
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, true);
			}
		}
	}

	/// <summary>
	/// Replicates the validation logic from Program.cs
	/// </summary>
	private static bool IsAssetDumperOutput(string directoryPath)
	{
		// Check for manifest.json - the primary indicator of AssetDumper output
		string manifestPath = Path.Combine(directoryPath, "manifest.json");
		if (File.Exists(manifestPath))
		{
			return true;
		}

		// Check for typical AssetDumper output directories
		// If multiple characteristic directories exist, it's likely an export
		string[] exportDirs = { "facts", "relations", "schema", "indexes", "metrics" };
		int foundCount = exportDirs.Count(dir => Directory.Exists(Path.Combine(directoryPath, dir)));

		// If 3 or more characteristic directories found, likely an export
		if (foundCount >= 3)
		{
			return true;
		}

		return false;
	}
}
