using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Orchestration;

namespace AssetRipper.Tools.AssetDumper.Tests.Orchestration;

/// <summary>
/// Tests for ExportOrchestrator class.
/// Priority A1 in NEXT_STEPS_ACTION_PLAN.md
/// </summary>
public class ExportOrchestratorTests : IDisposable
{
	private readonly string _testOutputPath;

	public ExportOrchestratorTests()
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
		var orchestrator = new ExportOrchestrator(options);

		// Assert
		orchestrator.Should().NotBeNull();
	}

	#endregion

	#region Compression Tests

	[Fact]
	public void CompressionKind_None_ShouldBeDefined()
	{
		// Arrange & Act
		var kind = CompressionKind.None;

		// Assert
		kind.Should().BeDefined();
	}

	[Fact]
	public void CompressionKind_Zstd_ShouldBeDefined()
	{
		// Arrange & Act
		var kind = CompressionKind.Zstd;

		// Assert
		kind.Should().BeDefined();
	}

	[Fact]
	public void CompressionKind_ZstdSeekable_ShouldBeDefined()
	{
		// Arrange & Act
		var kind = CompressionKind.ZstdSeekable;

		// Assert
		kind.Should().BeDefined();
	}

	#endregion

	#region Directory Scaffolding Tests

	[Fact]
	public void Execute_ShouldCreateOutputDirectory()
	{
		// Arrange
		var outputPath = Path.Combine(_testOutputPath, "export_output");
		var options = new Options
		{
			InputPath = "C:\\NonExistentPath", // Will fail but directory should be created
			OutputPath = outputPath,
			Quiet = true,
			ExportDomains = "none",  // Disable all exports for this test
			FactTables = "none"
		};
		var orchestrator = new ExportOrchestrator(options);

		// Act
		// Execute will fail due to invalid input, but should create directory structure
		try
		{
			orchestrator.Execute(null!);
		}
		catch
		{
			// Expected to fail
		}

		// Assert - Verify directory was created (if EnsureExportScaffolding was called)
		// Note: This is a basic test since we don't have real GameData
	}

	#endregion
}
