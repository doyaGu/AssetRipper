using System;
using System.Collections.Generic;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Facts;

namespace AssetRipper.Tools.AssetDumper.Tests.Exporters;

/// <summary>
/// Tests for TypeExporter class.
/// Priority A2 in NEXT_STEPS_ACTION_PLAN.md
/// </summary>
public class TypeFactsExporterTests : IDisposable
{
	private readonly string _testOutputPath;

	public TypeFactsExporterTests()
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
		var exporter = new TypeExporter(options);

		// Assert
		exporter.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
	{
		// Act & Assert
		Action act = () => new TypeExporter(null!);
		act.Should().Throw<ArgumentNullException>()
			.WithParameterName("options");
	}

	#endregion

	#region ExportTypes Method Tests

	[Fact]
	public void ExportTypes_WithNullEntries_ShouldThrowArgumentNullException()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var exporter = new TypeExporter(options);

		// Act & Assert
		Action act = () => exporter.ExportTypes(null!);
		act.Should().Throw<ArgumentNullException>()
			.WithParameterName("entries");
	}

	[Fact]
	public void ExportTypes_WithEmptyEntries_ShouldReturnResult()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var exporter = new TypeExporter(options);
		var entries = new List<TypeDictionaryEntry>();

		// Act
		var result = exporter.ExportTypes(entries);

		// Assert
		result.Should().NotBeNull();
		result.Domain.Should().Be("assets");
		result.TableId.Should().Be("facts/types");
	}

	#endregion
}
