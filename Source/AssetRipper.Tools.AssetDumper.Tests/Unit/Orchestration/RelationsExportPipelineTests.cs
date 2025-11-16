using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Orchestration;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Orchestration;

/// <summary>
/// Tests for RelationsExportPipeline class.
/// Priority A3 in NEXT_STEPS_ACTION_PLAN.md
/// </summary>
public class RelationsExportPipelineTests : IDisposable
{
	private readonly string _testOutputPath;

	public RelationsExportPipelineTests()
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
		var pipeline = new RelationsExportPipeline(context);

		// Assert
		pipeline.Should().NotBeNull();
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
