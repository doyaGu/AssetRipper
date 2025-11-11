using System;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Orchestration;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Tests.Orchestration;

/// <summary>
/// Tests for IncrementalManager class.
/// </summary>
public class IncrementalManagerTests : IDisposable
{
	private readonly string _testOutputPath;

	public IncrementalManagerTests()
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
		var manager = new IncrementalManager(options);

		// Assert
		manager.Should().NotBeNull();
	}

	#endregion

	#region TryLoadExistingManifest Tests

	[Fact]
	public void TryLoadExistingManifest_WhenManifestDoesNotExist_ShouldReturnNull()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var manager = new IncrementalManager(options);

		// Act
		var result = manager.TryLoadExistingManifest();

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public void TryLoadExistingManifest_WhenIncrementalProcessingDisabled_ShouldReturnNull()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			IncrementalMode = false,
			Quiet = true
		};
		var manager = new IncrementalManager(options);
		
		// Create a manifest file
		CreateValidManifest();

		// Act
		var result = manager.TryLoadExistingManifest();

		// Assert
		result.Should().BeNull("incremental processing is disabled");
	}

	[Fact]
	public void TryLoadExistingManifest_WithValidManifest_ShouldReturnManifest()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			IncrementalMode = true,
			Quiet = true
		};
		var manager = new IncrementalManager(options);
		
		// Create a valid manifest
		CreateValidManifest();

		// Act
		var result = manager.TryLoadExistingManifest();

		// Assert
		result.Should().NotBeNull();
		result!.Tables.Should().ContainKey("facts/collections");
		result.Tables.Should().ContainKey("relations/bundle_hierarchy");
		result.Tables.Should().ContainKey("relations/collection_dependencies");
	}

	#endregion

	#region ManifestContainsTables Tests

	[Fact]
	public void ManifestContainsTables_WhenAllTablesExist_ShouldReturnTrue()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var manager = new IncrementalManager(options);
		var manifest = CreateValidManifest();
		
		// Create the actual files
		CreateTestFile("facts/collections.ndjson");
		CreateTestFile("relations/bundle_hierarchy.ndjson");

		// Act
		var result = manager.ManifestContainsTables(manifest, 
			"facts/collections", 
			"relations/bundle_hierarchy");

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public void ManifestContainsTables_WhenTableMissing_ShouldReturnFalse()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var manager = new IncrementalManager(options);
		var manifest = CreateValidManifest();

		// Act
		var result = manager.ManifestContainsTables(manifest, 
			"facts/collections", 
			"facts/nonexistent");

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public void ManifestContainsTables_WhenFileNotOnDisk_ShouldReturnFalse()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var manager = new IncrementalManager(options);
		var manifest = CreateValidManifest();
		
		// Don't create the actual file

		// Act
		var result = manager.ManifestContainsTables(manifest, "facts/collections");

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public void ManifestContainsTables_NewRelationTables_ShouldValidateCorrectly()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var manager = new IncrementalManager(options);
		var manifest = CreateValidManifest();
		
		// Create all new relation table files
		CreateTestFile("relations/bundle_hierarchy.ndjson");
		CreateTestFile("relations/collection_dependencies.ndjson");
		CreateTestFile("relations/asset_dependencies.ndjson");

		// Act
		var result = manager.ManifestContainsTables(manifest,
			"relations/bundle_hierarchy",
			"relations/collection_dependencies",
			"relations/asset_dependencies");

		// Assert
		result.Should().BeTrue("all new relation tables exist");
	}

	#endregion

	#region CreateResultFromManifest Tests

	[Fact]
	public void CreateResultFromManifest_WhenTableExists_ShouldCreateResult()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var manager = new IncrementalManager(options);
		var manifest = CreateValidManifest();

		// Act
		var result = manager.CreateResultFromManifest(manifest, "facts/collections");

		// Assert
		result.Should().NotBeNull();
		result!.TableId.Should().Be("facts/collections");
		result.Domain.Should().Be("collections");
	}

	[Fact]
	public void CreateResultFromManifest_WhenTableDoesNotExist_ShouldReturnNull()
	{
		// Arrange
		var options = new Options
		{
			InputPath = "C:\\TestInput",
			OutputPath = _testOutputPath,
			Quiet = true
		};
		var manager = new IncrementalManager(options);
		var manifest = CreateValidManifest();

		// Act
		var result = manager.CreateResultFromManifest(manifest, "facts/nonexistent");

		// Assert
		result.Should().BeNull();
	}

	#endregion

	#region Helper Methods

	private Manifest CreateValidManifest()
	{
		var manifest = new Manifest
		{
			Version = "2.0",
			CreatedAt = DateTime.UtcNow.ToString("o"),
			Producer = new ManifestProducer
			{
				Name = "AssetDumper",
				Version = "1.0.0",
				UnityVersion = "2021.3.0f1"
			}
		};

		// Add tables
		manifest.Tables["facts/collections"] = new ManifestTable
		{
			Schema = "Schemas/v2/facts/collections.schema.json",
			Format = "ndjson",
			File = "facts/collections.ndjson",
			RecordCount = 100,
			ByteCount = 10240
		};
		
		manifest.Tables["facts/assets"] = new ManifestTable
		{
			Schema = "Schemas/v2/facts/assets.schema.json",
			Format = "ndjson",
			File = "facts/assets.ndjson",
			RecordCount = 1000,
			ByteCount = 102400
		};
		
		manifest.Tables["facts/bundles"] = new ManifestTable
		{
			Schema = "Schemas/v2/facts/bundles.schema.json",
			Format = "ndjson",
			File = "facts/bundles.ndjson",
			RecordCount = 10,
			ByteCount = 1024
		};
		
		manifest.Tables["relations/bundle_hierarchy"] = new ManifestTable
		{
			Schema = "Schemas/v2/relations/bundle_hierarchy.schema.json",
			Format = "ndjson",
			File = "relations/bundle_hierarchy.ndjson",
			RecordCount = 9,
			ByteCount = 512
		};
		
		manifest.Tables["relations/collection_dependencies"] = new ManifestTable
		{
			Schema = "Schemas/v2/relations/collection_dependencies.schema.json",
			Format = "ndjson",
			File = "relations/collection_dependencies.ndjson",
			RecordCount = 50,
			ByteCount = 5120
		};
		
		manifest.Tables["relations/asset_dependencies"] = new ManifestTable
		{
			Schema = "Schemas/v2/relations/asset_dependencies.schema.json",
			Format = "ndjson",
			File = "relations/asset_dependencies.ndjson",
			RecordCount = 500,
			ByteCount = 51200
		};

		// Write manifest to disk
		string manifestPath = Path.Combine(_testOutputPath, "manifest.json");
		string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
		File.WriteAllText(manifestPath, json);

		return manifest;
	}

	private void CreateTestFile(string relativePath)
	{
		string fullPath = Path.Combine(_testOutputPath, relativePath);
		string? directory = Path.GetDirectoryName(fullPath);
		if (directory != null && !Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}
		File.WriteAllText(fullPath, "{}");
	}

	#endregion
}

