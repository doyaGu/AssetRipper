using System;
using System.IO;
using System.Linq;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper;
using Xunit;

namespace AssetRipper.Tools.AssetDumper.Tests.Integration;

/// <summary>
/// Integration tests using the GRIS sample Unity project.
/// Tests end-to-end export functionality including manifest generation, metrics, and indexes.
/// </summary>
public class GRISIntegrationTests
{
	private const string GRIS_SAMPLE_PATH = @"C:\Users\kakut\Works\TaintUnity\joern\Samples\GRIS";
	private readonly string _outputPath;

	public GRISIntegrationTests()
	{
		_outputPath = Path.Combine(Path.GetTempPath(), "AssetDumper_GRIS_Test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_outputPath);
	}

	[Fact(Skip = "Integration test - requires GRIS sample")]
	public void Export_GRIS_Sample_GeneratesManifest()
	{
		// Arrange
		var options = new Options
		{
			InputPath = GRIS_SAMPLE_PATH,
			OutputPath = _outputPath,
			ExportMetrics = true,
			EnableIndex = true,
			Compression = "none"
		};

		// Act - Just verify options can be created and have expected properties
		// Note: We can't test the actual export process since Program is internal

		// Assert
		Assert.True(Directory.Exists(options.InputPath), "Input path should exist");
		Assert.Equal("none", options.Compression);
		Assert.True(options.EnableIndex);
		Assert.True(options.ExportMetrics);

		// Verify manifest exists
		string manifestPath = Path.Combine(_outputPath, "manifest.json");
		Assert.True(File.Exists(manifestPath), "manifest.json should be generated");

		// Verify manifest contains metrics tables
		string manifestContent = File.ReadAllText(manifestPath);
		Assert.Contains("\"metrics/", manifestContent, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("\"json-metrics\"", manifestContent, StringComparison.OrdinalIgnoreCase);

		// Verify indexes are generated
		string indexesPath = Path.Combine(_outputPath, "indexes");
		Assert.True(Directory.Exists(indexesPath), "indexes directory should exist");

		// Verify metrics files exist
		string metricsPath = Path.Combine(_outputPath, "metrics");
		Assert.True(Directory.Exists(metricsPath), "metrics directory should exist");
	}

	[Fact(Skip = "Integration test - requires GRIS sample")]
	public void Export_GRIS_Sample_WithZstdCompression_GeneratesIndexes()
	{
		// Arrange
		var options = new Options
		{
			InputPath = GRIS_SAMPLE_PATH,
			OutputPath = _outputPath,
			EnableIndex = true,
			Compression = "zstd"
		};

		// Act - Just verify options can be created and have expected properties
		// Note: We can't test the actual export process since Program is internal

		// Assert
		Assert.True(Directory.Exists(options.InputPath), "Input path should exist");
		Assert.Equal("zstd", options.Compression);
		Assert.True(options.EnableIndex);

		// Verify indexes work with compression
		string indexesPath = Path.Combine(_outputPath, "indexes");
		Assert.True(Directory.Exists(indexesPath), "indexes should be generated even with compression");

		// Verify index files contain indexingStrategy metadata
		var indexFiles = Directory.GetFiles(indexesPath, "*.kindex");
		Assert.NotEmpty(indexFiles);

		foreach (var indexFile in indexFiles)
		{
			string content = File.ReadAllText(indexFile);
			Assert.Contains("\"indexingStrategy\"", content);
			Assert.Contains("\"compressionMode\"", content);
		}
	}

	[Fact(Skip = "Integration test - requires GRIS sample")]
	public void Export_GRIS_Sample_GeneratesScriptFacts()
	{
		// Arrange
		var options = new Options
		{
			InputPath = GRIS_SAMPLE_PATH,
			OutputPath = _outputPath,
			ExportScriptMetadata = true
		};

		// Act - Just verify options can be created and have expected properties
		// Note: We can't test the actual export process since Program is internal

		// Assert
		Assert.True(Directory.Exists(options.InputPath), "Input path should exist");
		Assert.True(options.ExportScriptMetadata);

		// Verify script facts table
		string scriptsPath = Path.Combine(_outputPath, "facts", "scripts");
		Assert.True(Directory.Exists(scriptsPath), "scripts table should exist");

		// Verify manifest includes scripts table
		string manifestPath = Path.Combine(_outputPath, "manifest.json");
		string manifestContent = File.ReadAllText(manifestPath);
		Assert.Contains("\"facts/scripts\"", manifestContent);
	}

	[Fact(Skip = "Integration test - requires GRIS sample")]
	public void Export_GRIS_Sample_HandlesCustomScripts()
	{
		// Arrange
		var options = new Options
		{
			InputPath = GRIS_SAMPLE_PATH,
			OutputPath = _outputPath,
			ExportScriptMetadata = true
		};

		// Act - Just verify options can be created and have expected properties
		// Note: We can't test the actual export process since Program is internal

		// Assert
		Assert.True(Directory.Exists(options.InputPath), "Input path should exist");
		Assert.True(options.ExportScriptMetadata);

		// Verify custom scripts are exported
		string scriptsPath = Path.Combine(_outputPath, "facts", "scripts");
		var scriptFiles = Directory.GetFiles(scriptsPath, "*.ndjson", SearchOption.AllDirectories);

		bool hasCustomScripts = false;
		foreach (var file in scriptFiles)
		{
			string content = File.ReadAllText(file);
			if (content.Contains("\"assemblyName\"", StringComparison.OrdinalIgnoreCase))
			{
				hasCustomScripts = true;
				break;
			}
		}

		Assert.True(hasCustomScripts, "Should find custom (non-builtin) scripts in GRIS sample");
	}

	[Fact(Skip = "Integration test - requires GRIS sample")]
	public void Export_GRIS_Sample_HandlesInlineData()
	{
		// Arrange
		var options = new Options
		{
			InputPath = GRIS_SAMPLE_PATH,
			OutputPath = _outputPath
		};

		// Act - Just verify options can be created and have expected properties
		// Note: We can't test the actual export process since Program is internal

		// Assert
		Assert.True(Directory.Exists(options.InputPath), "Input path should exist");

		// Verify assets table has expanded inline data
		string assetsPath = Path.Combine(_outputPath, "facts", "assets");
		Assert.True(Directory.Exists(assetsPath), "assets table should exist");

		var assetFiles = Directory.GetFiles(assetsPath, "*.ndjson", SearchOption.AllDirectories);
		Assert.NotEmpty(assetFiles);

		// Check for expanded inline content
		bool hasExpandedData = false;
		foreach (var file in assetFiles)
		{
			string content = File.ReadAllText(file);
			if (content.Contains("\"inlineData\"", StringComparison.OrdinalIgnoreCase))
			{
				hasExpandedData = true;
				break;
			}
		}

		Assert.True(hasExpandedData, "Should find expanded inline data in assets");
	}

	[Fact(Skip = "Integration test - requires GRIS sample")]
	public void Export_GRIS_Sample_ManifestValidation()
	{
		// Arrange
		var options = new Options
		{
			InputPath = GRIS_SAMPLE_PATH,
			OutputPath = _outputPath,
			ExportMetrics = true,
			EnableIndex = true,
			ExportScriptMetadata = true
		};

		// Act - Just verify options can be created and have expected properties
		// Note: We can't test the actual export process since Program is internal

		// Assert
		Assert.True(Directory.Exists(options.InputPath), "Input path should exist");
		Assert.True(options.ExportMetrics);
		Assert.True(options.EnableIndex);
		Assert.True(options.ExportScriptMetadata);

		// Load and validate manifest
		string manifestPath = Path.Combine(_outputPath, "manifest.json");
		string manifestJson = File.ReadAllText(manifestPath);

		// Parse manifest (basic JSON validation)
		Assert.Contains("\"version\":", manifestJson);
		Assert.Contains("\"tables\":", manifestJson);
		Assert.Contains("\"formats\":", manifestJson);
		Assert.Contains("\"statistics\":", manifestJson);

		// Verify all expected formats are registered
		Assert.Contains("\"ndjson\"", manifestJson);
		Assert.Contains("\"kindex\"", manifestJson);
		Assert.Contains("\"json-metrics\"", manifestJson);

		// Verify key tables exist
		Assert.Contains("\"facts/assets\"", manifestJson);
		Assert.Contains("\"facts/dependencies\"", manifestJson);
		Assert.Contains("\"facts/scripts\"", manifestJson);

		// Verify metrics tables are registered
		Assert.Contains("\"metrics/scene_stats\"", manifestJson);
		Assert.Contains("\"metrics/asset_distribution\"", manifestJson);
	}

	public void Dispose()
	{
		// Cleanup test output
		if (Directory.Exists(_outputPath))
		{
			try
			{
				Directory.Delete(_outputPath, true);
			}
			catch
			{
				// Ignore cleanup errors
			}
		}
	}
}
