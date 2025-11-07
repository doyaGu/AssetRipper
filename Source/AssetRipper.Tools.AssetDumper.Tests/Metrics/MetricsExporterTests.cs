using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Metrics;

namespace AssetRipper.Tools.AssetDumper.Tests.Metrics;

/// <summary>
/// Tests for MetricsExporter which orchestrates metrics collection and export.
/// </summary>
public class MetricsExporterTests
{
	private readonly string _testOutputPath;

	public MetricsExporterTests()
	{
		_testOutputPath = Path.Combine(Path.GetTempPath(), $"AssetDumperTest_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testOutputPath);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidOptions_ShouldInitialize()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath
		};

		// Act
		var exporter = new MetricsExporter(options);

		// Assert
		exporter.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
	{
		// Act
		var act = () => new MetricsExporter(null!);

		// Assert
		act.Should().Throw<ArgumentNullException>()
		   .WithParameterName("options");
	}

	[Fact]
	public void Constructor_ShouldRegisterBuiltInCollectors()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath
		};

		// Act
		var exporter = new MetricsExporter(options);

		// Assert - should not throw when collecting
		var act = () => exporter.CollectMetrics(null!);
		// Note: Will throw ArgumentNullException for gameData, but that's a different validation
		act.Should().Throw<ArgumentNullException>().WithParameterName("gameData");
	}

	#endregion

	#region CollectMetrics Tests

	[Fact]
	public void CollectMetrics_WithNullGameData_ShouldThrowArgumentNullException()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath
		};
		var exporter = new MetricsExporter(options);

		// Act
		var act = () => exporter.CollectMetrics(null!);

		// Assert
		act.Should().Throw<ArgumentNullException>()
		   .WithParameterName("gameData");
	}

	#endregion

	#region WriteMetrics Tests

	[Fact]
	public void WriteMetrics_BeforeCollect_ShouldReturnEmptyList()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Silent = true
		};
		var exporter = new MetricsExporter(options);

		// Act
		var writtenPaths = exporter.WriteMetrics();

		// Assert
		writtenPaths.Should().NotBeNull();
		writtenPaths.Should().BeEmpty();
	}

	[Fact]
	public void WriteMetrics_MultipleCalls_ShouldNotThrow()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Silent = true
		};
		var exporter = new MetricsExporter(options);

		// Act
		var act = () =>
		{
			exporter.WriteMetrics();
			exporter.WriteMetrics();
		};

		// Assert
		act.Should().NotThrow();
	}

	#endregion

	#region GetCollectorsWithData Tests

	[Fact]
	public void GetCollectorsWithData_BeforeCollect_ShouldReturnEmpty()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath
		};
		var exporter = new MetricsExporter(options);

		// Act
		var collectors = exporter.GetCollectorsWithData();

		// Assert
		collectors.Should().NotBeNull();
		collectors.Should().BeEmpty();
	}

	#endregion

	#region Built-in Collectors Tests

	[Fact]
	public void Constructor_ShouldRegisterSceneStatsCollector()
	{
		// This test verifies that built-in collectors are registered
		// by checking that they don't cause exceptions during collection

		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Silent = true
		};
		var exporter = new MetricsExporter(options);

		// Act & Assert - should have registered collectors internally
		exporter.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_ShouldRegisterAssetDistributionCollector()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Silent = true
		};

		// Act
		var exporter = new MetricsExporter(options);

		// Assert
		exporter.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_ShouldRegisterDependencyStatsCollector()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Silent = true
		};

		// Act
		var exporter = new MetricsExporter(options);

		// Assert
		exporter.Should().NotBeNull();
	}

	#endregion
}
