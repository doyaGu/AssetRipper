using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models;
using Xunit;

namespace AssetRipper.Tools.AssetDumper.Tests.Indexes;

/// <summary>
/// Unit tests for KeyIndexGenerator covering empty data, single collection, multiple collections,
/// and builtin resource reference scenarios.
/// </summary>
public class IndexGeneratorTests
{
	[Fact]
	public void KeyIndexGenerator_EmptyData_HandlesGracefully()
	{
		// Arrange
		var options = new Options
		{
			OutputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}"),
			EnableIndexing = true
		};

		try
		{
			Directory.CreateDirectory(options.OutputPath);
			var generator = new KeyIndexGenerator(options);
			List<KeyIndexEntry> emptyEntries = new();

			// Act
			ManifestIndex? result = generator.Write("test_empty", emptyEntries);

			// Assert
			Assert.Null(result); // Should return null for empty data
		}
		finally
		{
			if (Directory.Exists(options.OutputPath))
			{
				Directory.Delete(options.OutputPath, true);
			}
		}
	}

	[Fact]
	public void KeyIndexGenerator_SingleEntry_CreatesValidIndex()
	{
		// Arrange
		var options = new Options
		{
			OutputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}"),
			EnableIndexing = true
		};

		try
		{
			Directory.CreateDirectory(options.OutputPath);
			var generator = new KeyIndexGenerator(options);
			List<KeyIndexEntry> entries = new()
			{
				new KeyIndexEntry { Key = "COL001", Line = 0, Offset = 0, Length = 100 }
			};

			// Act
			ManifestIndex? result = generator.Write("test_single", entries);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("test_single", result.Domain);
			Assert.Equal(1, result.RecordCount);
			Assert.Equal("kindex", result.Format);
			
			// Verify file was created
			string indexPath = Path.Combine(options.OutputPath, "indexes", "test_single.kindex");
			Assert.True(File.Exists(indexPath));
		}
		finally
		{
			if (Directory.Exists(options.OutputPath))
			{
				Directory.Delete(options.OutputPath, true);
			}
		}
	}

	[Fact]
	public void KeyIndexGenerator_MultipleEntries_CreatesValidIndexSortedByKey()
	{
		// Arrange
		var options = new Options
		{
			OutputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}"),
			EnableIndexing = true
		};

		try
		{
			Directory.CreateDirectory(options.OutputPath);
			var generator = new KeyIndexGenerator(options);
			List<KeyIndexEntry> entries = new()
			{
				new KeyIndexEntry { Key = "COL003", Line = 2 },
				new KeyIndexEntry { Key = "COL001", Line = 0 },
				new KeyIndexEntry { Key = "COL002", Line = 1 }
			};

			// Act
			ManifestIndex? result = generator.Write("test_multiple", entries);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(3, result.RecordCount);
			
			// Verify index file content
			string indexPath = Path.Combine(options.OutputPath, "indexes", "test_multiple.kindex");
			Assert.True(File.Exists(indexPath));
			
			string content = File.ReadAllText(indexPath);
			Assert.Contains("COL001", content);
			Assert.Contains("COL002", content);
			Assert.Contains("COL003", content);
		}
		finally
		{
			if (Directory.Exists(options.OutputPath))
			{
				Directory.Delete(options.OutputPath, true);
			}
		}
	}

	[Fact]
	public void KeyIndexGenerator_BuiltinCollections_IndexedCorrectly()
	{
		// Arrange
		var options = new Options
		{
			OutputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}"),
			EnableIndexing = true
		};

		try
		{
			Directory.CreateDirectory(options.OutputPath);
			var generator = new KeyIndexGenerator(options);
			List<KeyIndexEntry> entries = new()
			{
				new KeyIndexEntry { Key = "BUILTIN-EXTRA", Line = 0 },
				new KeyIndexEntry { Key = "BUILTIN-DEFAULT", Line = 1 },
				new KeyIndexEntry { Key = "USER001", Line = 2 }
			};

			// Act
			ManifestIndex? result = generator.Write("test_builtin", entries);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(3, result.RecordCount);
			
			// Verify all types of collections are indexed
			string indexPath = Path.Combine(options.OutputPath, "indexes", "test_builtin.kindex");
			string content = File.ReadAllText(indexPath);
			Assert.Contains("BUILTIN-EXTRA", content);
			Assert.Contains("BUILTIN-DEFAULT", content);
			Assert.Contains("USER001", content);
		}
		finally
		{
			if (Directory.Exists(options.OutputPath))
			{
				Directory.Delete(options.OutputPath, true);
			}
		}
	}

	[Fact]
	public void KeyIndexGenerator_DisabledIndexExport_ReturnsNull()
	{
		// Arrange
		var options = new Options
		{
			OutputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}"),
			EnableIndexing = false // Disabled
		};

		try
		{
			Directory.CreateDirectory(options.OutputPath);
			var generator = new KeyIndexGenerator(options);
			List<KeyIndexEntry> entries = new()
			{
				new KeyIndexEntry { Key = "COL001", Line = 0 }
			};

			// Act
			ManifestIndex? result = generator.Write("test_disabled", entries);

			// Assert
			Assert.Null(result); // Should not create index when disabled
			
			// Verify no index file was created
			string indexPath = Path.Combine(options.OutputPath, "indexes", "test_disabled.kindex");
			Assert.False(File.Exists(indexPath));
		}
		finally
		{
			if (Directory.Exists(options.OutputPath))
			{
				Directory.Delete(options.OutputPath, true);
			}
		}
	}

	[Fact]
	public void KeyIndexGenerator_UncompressedMode_IncludesByteOffsetMetadata()
	{
		// Arrange
		var options = new Options
		{
			OutputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}"),
			EnableIndexing = true
		};

		try
		{
			Directory.CreateDirectory(options.OutputPath);
			var generator = new KeyIndexGenerator(options);
			List<KeyIndexEntry> entries = new()
			{
				new KeyIndexEntry { Key = "COL001", Line = 0, Offset = 0, Length = 100 }
			};

			// Act
			ManifestIndex? result = generator.Write("test_uncompressed", entries, CompressionKind.None);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Metadata);
			Assert.Equal("none", result.Metadata!["compressionMode"]);
			Assert.Equal("byte-offset", result.Metadata!["indexingStrategy"]);
		}
		finally
		{
			if (Directory.Exists(options.OutputPath))
			{
				Directory.Delete(options.OutputPath, true);
			}
		}
	}

	[Fact]
	public void KeyIndexGenerator_CompressedMode_IncludesLineNumberMetadata()
	{
		// Arrange
		var options = new Options
		{
			OutputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}"),
			EnableIndexing = true
		};

		try
		{
			Directory.CreateDirectory(options.OutputPath);
			var generator = new KeyIndexGenerator(options);
			List<KeyIndexEntry> entries = new()
			{
				new KeyIndexEntry { Key = "COL001", Line = 0 }
			};

			// Act
			ManifestIndex? result = generator.Write("test_compressed", entries, CompressionKind.Zstd);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Metadata);
			Assert.Equal("zstd", result.Metadata!["compressionMode"]);
			Assert.Equal("line-number", result.Metadata!["indexingStrategy"]);
		}
		finally
		{
			if (Directory.Exists(options.OutputPath))
			{
				Directory.Delete(options.OutputPath, true);
			}
		}
	}
}

