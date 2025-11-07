using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Metrics;

namespace AssetRipper.Tools.AssetDumper.Tests.Metrics;

/// <summary>
/// Tests for AssetDistributionCollector which gathers asset distribution statistics.
/// </summary>
public class AssetDistributionCollectorTests
{
	private readonly string _testOutputPath;

	public AssetDistributionCollectorTests()
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
		var collector = new AssetDistributionCollector(options);

		// Assert
		collector.Should().NotBeNull();
		collector.MetricsId.Should().Be("asset_distribution");
		collector.SchemaUri.Should().NotBeNullOrEmpty();
		collector.SchemaUri.Should().Contain("asset_distribution");
	}

	[Fact]
	public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
	{
		// Act
		var act = () => new AssetDistributionCollector(null!);

		// Assert
		act.Should().Throw<ArgumentNullException>()
		   .WithParameterName("options");
	}

	#endregion

	#region MetricsId Tests

	[Fact]
	public void MetricsId_ShouldReturnAssetDistribution()
	{
		// Arrange
		var options = new Options { InputPath = "test", OutputPath = "test" };
		var collector = new AssetDistributionCollector(options);

		// Act
		var metricsId = collector.MetricsId;

		// Assert
		metricsId.Should().Be("asset_distribution");
	}

	#endregion

	#region SchemaUri Tests

	[Fact]
	public void SchemaUri_ShouldReturnValidUri()
	{
		// Arrange
		var options = new Options { InputPath = "test", OutputPath = "test" };
		var collector = new AssetDistributionCollector(options);

		// Act
		var schemaUri = collector.SchemaUri;

		// Assert
		schemaUri.Should().NotBeNullOrEmpty();
		schemaUri.Should().StartWith("https://");
		schemaUri.Should().Contain("asset_distribution");
		schemaUri.Should().Contain("schema.json");
	}

	#endregion

	#region HasData Tests

	[Fact]
	public void HasData_BeforeCollect_ShouldReturnFalse()
	{
		// Arrange
		var options = new Options { InputPath = "test", OutputPath = "test" };
		var collector = new AssetDistributionCollector(options);

		// Act
		var hasData = collector.HasData;

		// Assert
		hasData.Should().BeFalse();
	}

	[Fact]
	public void HasData_AfterCollectWithNullData_ShouldReturnFalse()
	{
		// Arrange
		var options = new Options { InputPath = "test", OutputPath = "test" };
		var collector = new AssetDistributionCollector(options);

		// Act
		collector.Collect(null!);
		var hasData = collector.HasData;

		// Assert
		hasData.Should().BeFalse();
	}

	#endregion

	#region Collect Tests

	[Fact]
	public void Collect_WithNullGameData_ShouldNotThrow()
	{
		// Arrange
		var options = new Options { InputPath = "test", OutputPath = "test" };
		var collector = new AssetDistributionCollector(options);

		// Act
		var act = () => collector.Collect(null!);

		// Assert
		act.Should().NotThrow();
		collector.HasData.Should().BeFalse();
	}

	[Fact]
	public void Collect_CalledTwice_ShouldClearPreviousData()
	{
		// Arrange
		var options = new Options { InputPath = "test", OutputPath = "test" };
		var collector = new AssetDistributionCollector(options);

		// Act
		collector.Collect(null!);
		var hasDataFirst = collector.HasData;
		collector.Collect(null!);
		var hasDataSecond = collector.HasData;

		// Assert
		hasDataFirst.Should().BeFalse();
		hasDataSecond.Should().BeFalse();
	}

	#endregion

	#region WriteMetrics Tests

	[Fact]
	public void WriteMetrics_WithNoData_ShouldReturnNull()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "test",
			OutputPath = _testOutputPath,
			Silent = true
		};
		var collector = new AssetDistributionCollector(options);

		// Act
		var outputPath = collector.WriteMetrics(_testOutputPath);

		// Assert
		outputPath.Should().BeNull();
	}

	[Fact]
	public void WriteMetrics_WithNoData_ShouldNotCreateFile()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "test",
			OutputPath = _testOutputPath,
			Silent = true
		};
		var collector = new AssetDistributionCollector(options);

		// Act
		collector.WriteMetrics(_testOutputPath);

		// Assert
		var expectedPath = Path.Combine(_testOutputPath, "metrics", "asset_distribution.json");
		File.Exists(expectedPath).Should().BeFalse();
	}

	#endregion

	#region Integration with BaseMetricsCollector

	[Fact]
	public void Collector_ShouldImplementIMetricsCollector()
	{
		// Arrange
		var options = new Options { InputPath = "test", OutputPath = "test" };

		// Act
		var collector = new AssetDistributionCollector(options);

		// Assert
		collector.Should().BeAssignableTo<IMetricsCollector>();
	}

	[Fact]
	public void Collector_ShouldInheritFromBaseMetricsCollector()
	{
		// Arrange
		var options = new Options { InputPath = "test", OutputPath = "test" };

		// Act
		var collector = new AssetDistributionCollector(options);

		// Assert
		collector.Should().BeAssignableTo<BaseMetricsCollector>();
	}

	#endregion
}
