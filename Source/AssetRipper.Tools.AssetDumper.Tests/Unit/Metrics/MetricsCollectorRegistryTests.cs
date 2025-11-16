using System;
using System.Linq;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Metrics;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Metrics;

/// <summary>
/// Tests for MetricsCollectorRegistry which manages registration and creation of metrics collectors.
/// </summary>
public class MetricsCollectorRegistryTests
{
	#region Constructor Tests

	[Fact]
	public void Constructor_ShouldInitialize()
	{
		// Act
		var registry = new MetricsCollectorRegistry();

		// Assert
		registry.Should().NotBeNull();
	}

	#endregion

	#region Register Tests

	[Fact]
	public void Register_WithValidParameters_ShouldRegisterCollector()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();
		var mockCollector = new Mock<IMetricsCollector>();
		mockCollector.Setup(c => c.MetricsId).Returns("test_metrics");

		// Act
		registry.Register("test_metrics", options => mockCollector.Object);

		// Assert
		var registeredIds = registry.GetRegisteredIds();
		registeredIds.Should().Contain("test_metrics");
	}

	[Fact]
	public void Register_WithNullMetricsId_ShouldThrowArgumentException()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();

		// Act
		var act = () => registry.Register(null!, options => Mock.Of<IMetricsCollector>());

		// Assert
		act.Should().Throw<ArgumentException>()
		   .WithParameterName("metricsId");
	}

	[Fact]
	public void Register_WithEmptyMetricsId_ShouldThrowArgumentException()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();

		// Act
		var act = () => registry.Register("", options => Mock.Of<IMetricsCollector>());

		// Assert
		act.Should().Throw<ArgumentException>()
		   .WithParameterName("metricsId");
	}

	[Fact]
	public void Register_WithWhitespaceMetricsId_ShouldThrowArgumentException()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();

		// Act
		var act = () => registry.Register("   ", options => Mock.Of<IMetricsCollector>());

		// Assert
		act.Should().Throw<ArgumentException>()
		   .WithParameterName("metricsId");
	}

	[Fact]
	public void Register_WithNullFactory_ShouldThrowArgumentNullException()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();

		// Act
		var act = () => registry.Register("test_metrics", null!);

		// Assert
		act.Should().Throw<ArgumentNullException>()
		   .WithParameterName("factory");
	}

	[Fact]
	public void Register_SameIdTwice_ShouldOverwriteFirst()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();
		var mockCollector1 = new Mock<IMetricsCollector>();
		mockCollector1.Setup(c => c.MetricsId).Returns("test_metrics");
		var mockCollector2 = new Mock<IMetricsCollector>();
		mockCollector2.Setup(c => c.MetricsId).Returns("test_metrics");

		// Act
		registry.Register("test_metrics", options => mockCollector1.Object);
		registry.Register("test_metrics", options => mockCollector2.Object);

		// Assert
		var options = new Options { InputPath = "test", OutputPath = "test" };
		var created = registry.Create("test_metrics", options);
		created.Should().BeSameAs(mockCollector2.Object);
	}

	#endregion

	#region CreateAll Tests

	[Fact]
	public void CreateAll_WithNoRegistrations_ShouldReturnEmpty()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();
		var options = new Options { InputPath = "test", OutputPath = "test" };

		// Act
		var collectors = registry.CreateAll(options);

		// Assert
		collectors.Should().BeEmpty();
	}

	[Fact]
	public void CreateAll_WithMultipleRegistrations_ShouldCreateAll()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();
		var mockCollector1 = new Mock<IMetricsCollector>();
		var mockCollector2 = new Mock<IMetricsCollector>();
		var mockCollector3 = new Mock<IMetricsCollector>();

		registry.Register("metrics1", options => mockCollector1.Object);
		registry.Register("metrics2", options => mockCollector2.Object);
		registry.Register("metrics3", options => mockCollector3.Object);

		var options = new Options { InputPath = "test", OutputPath = "test" };

		// Act
		var collectors = registry.CreateAll(options).ToList();

		// Assert
		collectors.Should().HaveCount(3);
		collectors.Should().Contain(mockCollector1.Object);
		collectors.Should().Contain(mockCollector2.Object);
		collectors.Should().Contain(mockCollector3.Object);
	}

	[Fact]
	public void CreateAll_ShouldPassOptionsToFactory()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();
		var capturedOptions = (Options?)null;
		
		registry.Register("test_metrics", options =>
		{
			capturedOptions = options;
			return Mock.Of<IMetricsCollector>();
		});

		var expectedOptions = new Options { InputPath = "input", OutputPath = "output" };

		// Act
		var collectors = registry.CreateAll(expectedOptions).ToList();

		// Assert
		capturedOptions.Should().BeSameAs(expectedOptions);
	}

	#endregion

	#region Create Tests

	[Fact]
	public void Create_WithRegisteredId_ShouldReturnCollector()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();
		var mockCollector = new Mock<IMetricsCollector>();
		registry.Register("test_metrics", options => mockCollector.Object);
		var options = new Options { InputPath = "test", OutputPath = "test" };

		// Act
		var created = registry.Create("test_metrics", options);

		// Assert
		created.Should().BeSameAs(mockCollector.Object);
	}

	[Fact]
	public void Create_WithUnregisteredId_ShouldReturnNull()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();
		var options = new Options { InputPath = "test", OutputPath = "test" };

		// Act
		var created = registry.Create("nonexistent", options);

		// Assert
		created.Should().BeNull();
	}

	[Fact]
	public void Create_ShouldPassOptionsToFactory()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();
		var capturedOptions = (Options?)null;
		
		registry.Register("test_metrics", options =>
		{
			capturedOptions = options;
			return Mock.Of<IMetricsCollector>();
		});

		var expectedOptions = new Options { InputPath = "input", OutputPath = "output" };

		// Act
		var created = registry.Create("test_metrics", expectedOptions);

		// Assert
		capturedOptions.Should().BeSameAs(expectedOptions);
	}

	#endregion

	#region GetRegisteredIds Tests

	[Fact]
	public void GetRegisteredIds_WithNoRegistrations_ShouldReturnEmpty()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();

		// Act
		var ids = registry.GetRegisteredIds();

		// Assert
		ids.Should().BeEmpty();
	}

	[Fact]
	public void GetRegisteredIds_WithMultipleRegistrations_ShouldReturnAllIds()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();
		registry.Register("metrics1", options => Mock.Of<IMetricsCollector>());
		registry.Register("metrics2", options => Mock.Of<IMetricsCollector>());
		registry.Register("metrics3", options => Mock.Of<IMetricsCollector>());

		// Act
		var ids = registry.GetRegisteredIds().ToList();

		// Assert
		ids.Should().HaveCount(3);
		ids.Should().Contain("metrics1");
		ids.Should().Contain("metrics2");
		ids.Should().Contain("metrics3");
	}

	[Fact]
	public void GetRegisteredIds_AfterOverwrite_ShouldReturnUniqueIds()
	{
		// Arrange
		var registry = new MetricsCollectorRegistry();
		registry.Register("metrics1", options => Mock.Of<IMetricsCollector>());
		registry.Register("metrics1", options => Mock.Of<IMetricsCollector>()); // Overwrite

		// Act
		var ids = registry.GetRegisteredIds().ToList();

		// Assert
		ids.Should().HaveCount(1);
		ids.Should().Contain("metrics1");
	}

	#endregion
}
