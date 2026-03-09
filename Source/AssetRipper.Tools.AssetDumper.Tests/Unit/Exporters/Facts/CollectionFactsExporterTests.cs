using System;
using System.IO;
using System.Reflection;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Facts;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Exporters.Facts;

/// <summary>
/// Tests for CollectionExporter class.
/// Priority A2 in NEXT_STEPS_ACTION_PLAN.md
/// </summary>
public class CollectionFactsExporterTests : IDisposable
{
	private readonly string _testOutputPath;

	public CollectionFactsExporterTests()
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
		var exporter = new CollectionExporter(options, CompressionKind.None);

		// Assert
		exporter.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
	{
		// Act & Assert
		Action act = () => new CollectionExporter(null!, CompressionKind.None);
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
		var exporter = new CollectionExporter(options, compressionKind);

		// Assert
		exporter.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_ShouldSerializeAssetCountWhenValueIsZero()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var exporter = new CollectionExporter(options, CompressionKind.None);
		var settingsField = typeof(CollectionExporter).GetField("_jsonSettings", BindingFlags.Instance | BindingFlags.NonPublic);
		settingsField.Should().NotBeNull();
		var settings = (JsonSerializerSettings)settingsField!.GetValue(exporter)!;

		var record = new CollectionRecord
		{
			CollectionId = "collection-1",
			Name = "Collection",
			Platform = "NoTarget",
			UnityVersion = "2021.3.0f1",
			Endian = "LittleEndian",
			Bundle = new(),
			AssetCount = 0
		};

		// Act
		string json = JsonConvert.SerializeObject(record, settings);

		// Assert
		json.Should().Contain("\"assetCount\":0");
	}

	#endregion

	#region ExportCollections Method Tests

	[Fact]
	public void ExportCollections_WithNullGameData_ShouldThrowArgumentNullException()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var exporter = new CollectionExporter(options, CompressionKind.None);

		// Act & Assert
		Action act = () => exporter.ExportCollections(null!);
		act.Should().Throw<ArgumentNullException>()
			.WithParameterName("gameData");
	}

	#endregion
}
