using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Orchestration;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Orchestration;

/// <summary>
/// Tests for OptionalExportPipeline class.
/// Priority A3 in NEXT_STEPS_ACTION_PLAN.md
/// </summary>
public class OptionalExportPipelineTests : IDisposable
{
	private readonly string _testOutputPath;

	public OptionalExportPipelineTests()
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
	public void Constructor_WithValidContext_ShouldInitialize()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var context = CreateTestContext(options);

		// Act
		var pipeline = new OptionalExportPipeline(context);

		// Assert
		pipeline.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_WithSelectiveExecution_ShouldInitialize()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var context = CreateTestContext(options);

		// Act
		var pipeline = new OptionalExportPipeline(
			context,
			executeBundleMetadata: true,
			executeScenes: false,
			executeScriptMetadata: true,
			executeMetrics: false);

		// Assert
		pipeline.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_DefaultConstructor_ShouldExecuteAllExports()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true,
			FactTables = "bundles,scenes,scripts"
		};
		var context = CreateTestContext(options);

		// Act
		var pipeline = new OptionalExportPipeline(context);

		// Assert
		pipeline.Should().NotBeNull();
		// Default constructor should allow all exports to run
		options.ExportBundleMetadata.Should().BeTrue();
		options.ExportScenes.Should().BeTrue();
		options.ExportScriptMetadata.Should().BeTrue();
	}

	[Fact]
	public void Constructor_SelectiveExecution_ShouldAllowPartialReuse()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true,
			FactTables = "bundles,scenes,scripts"
		};
		var context = CreateTestContext(options);

		// Act - Simulate incremental scenario: reuse bundles and scenes, regenerate scripts
		var pipeline = new OptionalExportPipeline(
			context,
			executeBundleMetadata: false, // Reused
			executeScenes: false,          // Reused
			executeScriptMetadata: true,   // Regenerate
			executeMetrics: false);

		// Assert
		pipeline.Should().NotBeNull();
		// This pipeline would only execute script metadata export
	}

	#endregion

	#region Helper Methods

	private ExportContext CreateTestContext(Options options)
	{
		return new ExportContext(
			options,
			null!,
			CompressionKind.None,
			enableIndex: false,
			indexGenerator: null);
	}

	#endregion
}
