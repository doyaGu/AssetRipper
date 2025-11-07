using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Facts;

namespace AssetRipper.Tools.AssetDumper.Tests.Exporters;

/// <summary>
/// Tests for AssetFactsExporter class.
/// Priority A2 in NEXT_STEPS_ACTION_PLAN.md
/// </summary>
public class AssetFactsExporterTests : IDisposable
{
	private readonly string _testOutputPath;

	public AssetFactsExporterTests()
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
			Silent = true
		};

		// Act
		var exporter = new AssetFactsExporter(options, CompressionKind.None, enableIndex: false);

		// Assert
		exporter.Should().NotBeNull();
		exporter.TypeDictionary.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
	{
		// Act & Assert
		Action act = () => new AssetFactsExporter(null!, CompressionKind.None, enableIndex: false);
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
			Silent = true
		};

		// Act
		var exporter = new AssetFactsExporter(options, compressionKind, enableIndex: false);

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
			Silent = true
		};

		// Act
		var exporter = new AssetFactsExporter(options, CompressionKind.None, enableIndex);

		// Assert
		exporter.Should().NotBeNull();
	}

	#endregion

	#region TypeDictionary Property Tests

	[Fact]
	public void TypeDictionary_ShouldBeAccessible()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Silent = true
		};
		var exporter = new AssetFactsExporter(options, CompressionKind.None, enableIndex: false);

		// Act
		var typeDictionary = exporter.TypeDictionary;

		// Assert
		typeDictionary.Should().NotBeNull();
	}

	#endregion

	#region ExportAssets Method Tests

	[Fact]
	public void ExportAssets_WithNullGameData_ShouldThrowArgumentNullException()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Silent = true
		};
		var exporter = new AssetFactsExporter(options, CompressionKind.None, enableIndex: false);

		// Act & Assert
		Action act = () => exporter.ExportAssets(null!);
		act.Should().Throw<ArgumentNullException>()
			.WithParameterName("gameData");
	}

	#endregion
}
