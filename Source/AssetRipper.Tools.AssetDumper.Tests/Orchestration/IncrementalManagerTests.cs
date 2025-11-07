using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Orchestration;

namespace AssetRipper.Tools.AssetDumper.Tests.Orchestration;

/// <summary>
/// Tests for IncrementalManager class.
/// </summary>
public class IncrementalManagerTests : IDisposable
{
	private readonly string _testOutputPath;

	public IncrementalManagerTests()
	{
		_testOutputPath = Path.Combine(Path.GetTempPath(), $"AssetDumperTests_{Guid.NewGuid():N}");
		Directory.CreateDirectory(_testOutputPath);
	}

	public void Dispose()
	{
		if (Directory.Exists(_testOutputPath))
		{
			try
			{
				Directory.Delete(_testOutputPath, recursive: true);
			}
			catch
			{
				// Ignore cleanup errors
			}
		}
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidOptions_ShouldInitialize()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Silent = true
		};

		// Act
		var manager = new IncrementalManager(options);

		// Assert
		manager.Should().NotBeNull();
	}

	#endregion

	#region TryLoadExistingManifest Tests

	[Fact]
	public void TryLoadExistingManifest_WhenManifestDoesNotExist_ShouldReturnNull()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Silent = true
		};
		var manager = new IncrementalManager(options);

		// Act
		var result = manager.TryLoadExistingManifest();

		// Assert
		result.Should().BeNull();
	}

	#endregion
}
