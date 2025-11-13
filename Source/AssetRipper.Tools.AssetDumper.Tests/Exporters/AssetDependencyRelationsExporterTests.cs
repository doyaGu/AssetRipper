using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Relations;

namespace AssetRipper.Tools.AssetDumper.Tests.Exporters;

/// <summary>
/// Tests for AssetDependencyExporter class.
/// Priority A2 in NEXT_STEPS_ACTION_PLAN.md
/// </summary>
public class AssetDependencyRelationsExporterTests : IDisposable
{
	private readonly string _testOutputPath;

	public AssetDependencyRelationsExporterTests()
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
	public void Constructor_WithValidParameters_ShouldInitialize()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};

		// Act
		var exporter = new AssetDependencyExporter(options, CompressionKind.None, enableIndex: false);

		// Assert
		exporter.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
	{
		// Act & Assert
		Action act = () => new AssetDependencyExporter(null!, CompressionKind.None, enableIndex: false);
		act.Should().Throw<ArgumentNullException>()
			.WithParameterName("options");
	}

	[Theory]
	[InlineData(CompressionKind.None)]
	[InlineData(CompressionKind.Zstd)]
	[InlineData(CompressionKind.ZstdSeekable)]
	internal void Constructor_WithDifferentCompressionKinds_ShouldInitialize(CompressionKind compressionKind)
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};

		// Act
		var exporter = new AssetDependencyExporter(options, compressionKind, enableIndex: false);

		// Assert
		exporter.Should().NotBeNull();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Constructor_WithDifferentIndexSettings_ShouldInitialize(bool enableIndex)
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};

		// Act
		var exporter = new AssetDependencyExporter(options, CompressionKind.None, enableIndex);

		// Assert
		exporter.Should().NotBeNull();
	}

	#endregion

	#region Export Method Tests

	[Fact]
	public void Export_WithNullGameData_ShouldThrowArgumentNullException()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var exporter = new AssetDependencyExporter(options, CompressionKind.None, enableIndex: false);

		// Act & Assert
		Action act = () => exporter.Export(null!);
		act.Should().Throw<ArgumentNullException>()
			.WithParameterName("gameData");
	}

	#endregion
}
