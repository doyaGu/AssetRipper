using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Orchestration;

namespace AssetRipper.Tools.AssetDumper.Tests.Orchestration;

/// <summary>
/// Tests for FactsExportPipeline class.
/// Priority A3 in NEXT_STEPS_ACTION_PLAN.md
/// </summary>
public class FactsExportPipelineTests : IDisposable
{
	private readonly string _testOutputPath;

	public FactsExportPipelineTests()
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
			Silent = true
		};
		var context = CreateTestContext(options);

		// Act
		var pipeline = new FactsExportPipeline(context);

		// Assert
		pipeline.Should().NotBeNull();
	}

	#endregion

	#region Helper Methods

	private ExportContext CreateTestContext(Options options)
	{
		// Since we can't create GameData without complex dependencies,
		// we'll create a minimal context for testing
		return new ExportContext(
			options,
			null!, // GameData - would require mock
			CompressionKind.None,
			enableIndex: false,
			indexGenerator: null);
	}

	#endregion
}
