using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Orchestration;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Orchestration;

/// <summary>
/// Tests for ValidationService class.
/// </summary>
public class ValidationServiceTests : IDisposable
{
	private readonly string _testOutputPath;

	public ValidationServiceTests()
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
			Quiet = true
		};

		// Act
		var service = new ValidationService(options);

		// Assert
		service.Should().NotBeNull();
	}

	#endregion
}
