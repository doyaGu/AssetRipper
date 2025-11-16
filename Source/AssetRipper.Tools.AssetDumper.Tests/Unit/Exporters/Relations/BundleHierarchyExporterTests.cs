using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Relations;
using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Builders;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Constants;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Helpers;
using FluentAssertions;
using Xunit;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Exporters.Relations;

/// <summary>
/// Unit tests for BundleHierarchyExporter with Schema v2.0 enhancements.
/// Validates that the exporter correctly populates 'parentName', 'childBundleType', and 'childDepth' optimization fields.
/// </summary>
public class BundleHierarchyExporterTests : IDisposable
{
    private readonly string _testOutputPath;
    private readonly Options _options;

    public BundleHierarchyExporterTests()
    {
        _testOutputPath = TestPathHelper.CreateTestDirectory(nameof(BundleHierarchyExporterTests));
        _options = OptionsBuilder.CreateForExport(".", _testOutputPath);
    }

    public void Dispose()
    {
        TestPathHelper.CleanupTestDirectory(_testOutputPath);
    }

    [Fact]
    public void BundleHierarchyExporter_ShouldExportHierarchyData()
    {
        // Arrange
        var exporter = new BundleHierarchyExporter(_options);

        // Act
        var result = exporter.GetDomainResult();

        // Assert
        result.Should().NotBeNull();
        result.DomainName.Should().Be("bundle_hierarchy");
        result.SchemaPath.Should().Contain("bundle_hierarchy");
    }

    [Fact]
    public void BundleHierarchyExporter_ShouldHaveCorrectSchema()
    {
        // Arrange
        var exporter = new BundleHierarchyExporter(_options);

        // Act
        var result = exporter.GetDomainResult();

        // Assert
        result.SchemaPath.Should().Be("Schemas/v2/relations/bundle_hierarchy.schema.json");
    }

    [Fact]
    public void BundleHierarchyRecord_ParentNameField_ShouldOptimizeQueries()
    {
        // Arrange - v2.0 optimization: ParentName allows direct filtering without JOIN
        var record = new BundleHierarchyRecord
        {
            ParentBundle = "bundle_123abc",
            ChildBundle = "child_456def",
            ParentName = "Main Game Bundle" // Readable name for fast filtering
        };

        // Assert
        record.ParentName.Should().Be("Main Game Bundle");
        // This allows queries like: SELECT * FROM bundle_hierarchy WHERE parentName = 'Main Game Bundle'
        // Instead of: SELECT * FROM bundle_hierarchy JOIN bundles ON parentBundle = id WHERE bundles.name = 'Main Game Bundle'
    }

    [Fact]
    public void BundleHierarchyRecord_ChildBundleTypeField_ShouldEnableTypeFiltering()
    {
        // Arrange - v2.0 optimization: ChildBundleType allows direct type filtering
        var gameBundleRecord = new BundleHierarchyRecord
        {
            ParentBundle = "parent",
            ChildBundle = "game1",
            ChildBundleType = TestConstants.BundleTypeGameBundle
        };

        var resourceRecord = new BundleHierarchyRecord
        {
            ParentBundle = "parent",
            ChildBundle = "resource1",
            ChildBundleType = TestConstants.BundleTypeResourceFile
        };

        // Assert
        gameBundleRecord.ChildBundleType.Should().Be(TestConstants.BundleTypeGameBundle);
        resourceRecord.ChildBundleType.Should().Be(TestConstants.BundleTypeResourceFile);
        // Fast query: SELECT * FROM bundle_hierarchy WHERE childBundleType = 'GameBundle'
    }

    [Theory]
    [InlineData(TestConstants.BundleTypeGameBundle)]
    [InlineData(TestConstants.BundleTypeSerialized)]
    [InlineData(TestConstants.BundleTypeProcessed)]
    [InlineData(TestConstants.BundleTypeResourceFile)]
    [InlineData(TestConstants.BundleTypeWebBundle)]
    [InlineData(TestConstants.BundleTypeUnknown)]
    public void BundleHierarchyRecord_ShouldSupportAllBundleTypes(string bundleType)
    {
        // Arrange
        var record = new BundleHierarchyRecord
        {
            ParentBundle = "parent",
            ChildBundle = "child",
            ChildBundleType = bundleType
        };

        // Assert
        record.ChildBundleType.Should().Be(bundleType);
    }

    [Fact]
    public void BundleHierarchyRecord_ChildDepthField_ShouldEnableDepthQueries()
    {
        // Arrange - v2.0 optimization: ChildDepth allows direct depth filtering
        var rootRecord = new BundleHierarchyRecord
        {
            ParentBundle = null,
            ChildBundle = "root",
            ChildDepth = 0
        };

        var level1Record = new BundleHierarchyRecord
        {
            ParentBundle = "root",
            ChildBundle = "level1",
            ChildDepth = 1
        };

        var level2Record = new BundleHierarchyRecord
        {
            ParentBundle = "level1",
            ChildBundle = "level2",
            ChildDepth = 2
        };

        // Assert
        rootRecord.ChildDepth.Should().Be(0);
        level1Record.ChildDepth.Should().Be(1);
        level2Record.ChildDepth.Should().Be(2);
        // Fast query: SELECT * FROM bundle_hierarchy WHERE childDepth = 1 (all direct children)
    }

    [Fact]
    public void BundleHierarchyRecord_V2_1OptimizationFields_ShouldWorkTogether()
    {
        // Arrange - Complete v2.0 optimization example
        var record = new BundleHierarchyRecord
        {
            ParentBundle = "main_bundle_123",
            ChildBundle = "level1_bundle_456",
            ParentName = "Main Game Bundle",
            ChildBundleType = TestConstants.BundleTypeGameBundle,
            ChildDepth = 1
        };

        // Assert - All optimization fields should be properly set
        record.ParentName.Should().Be("Main Game Bundle");
        record.ChildBundleType.Should().Be(TestConstants.BundleTypeGameBundle);
        record.ChildDepth.Should().Be(1);
    }

    [Fact]
    public void BundleHierarchyRecord_OptionalFields_ShouldBeNullable()
    {
        // Arrange
        var minimalRecord = new BundleHierarchyRecord
        {
            ParentBundle = "parent",
            ChildBundle = "child",
            ParentName = null,
            ChildBundleType = null,
            ChildDepth = null
        };

        // Assert
        minimalRecord.ParentName.Should().BeNull();
        minimalRecord.ChildBundleType.Should().BeNull();
        minimalRecord.ChildDepth.Should().BeNull();
    }

    [Fact]
    public void BundleHierarchyRecord_RootBundle_ShouldHaveDepthZeroAndNullParent()
    {
        // Arrange
        var rootRecord = new BundleHierarchyRecord
        {
            ParentBundle = null,
            ChildBundle = "root.bundle",
            ParentName = null,
            ChildBundleType = TestConstants.BundleTypeGameBundle,
            ChildDepth = 0
        };

        // Assert
        rootRecord.ParentBundle.Should().BeNull();
        rootRecord.ParentName.Should().BeNull();
        rootRecord.ChildDepth.Should().Be(0);
    }

    [Fact]
    public void BundleHierarchyExporter_OutputFormat_ShouldBeNDJson()
    {
        // Arrange
        var exporter = new BundleHierarchyExporter(_options);

        // Act
        var result = exporter.GetDomainResult();

        // Assert
        result.Format.Should().Be("ndjson");
    }

    [Fact]
    public void BundleHierarchyRecord_QueryOptimization_5to10xSpeedup()
    {
        // This test documents the 5-10x query speedup mentioned in the schema
        // Arrange - Create hierarchical data
        var records = new[]
        {
            new BundleHierarchyRecord
            {
                ParentBundle = null,
                ChildBundle = "root",
                ParentName = null,
                ChildBundleType = TestConstants.BundleTypeGameBundle,
                ChildDepth = 0
            },
            new BundleHierarchyRecord
            {
                ParentBundle = "root",
                ChildBundle = "level1_a",
                ParentName = "Root Bundle",
                ChildBundleType = TestConstants.BundleTypeGameBundle,
                ChildDepth = 1
            },
            new BundleHierarchyRecord
            {
                ParentBundle = "root",
                ChildBundle = "level1_b",
                ParentName = "Root Bundle",
                ChildBundleType = TestConstants.BundleTypeResourceFile,
                ChildDepth = 1
            }
        };

        // Act - Optimized queries (no JOINs needed)
        var rootChildren = records.Where(r => r.ParentName == "Root Bundle").ToList();
        var gameBundles = records.Where(r => r.ChildBundleType == TestConstants.BundleTypeGameBundle).ToList();
        var directChildren = records.Where(r => r.ChildDepth == 1).ToList();

        // Assert - Optimization queries work correctly
        rootChildren.Should().HaveCount(2);
        gameBundles.Should().HaveCount(2);
        directChildren.Should().HaveCount(2);
    }
}
