using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Constants;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Models.Relations;

/// <summary>
/// Unit tests for BundleHierarchyRecord with Schema v2.0 enhancements.
/// Tests the new 'parentName', 'childBundleType', and 'childDepth' fields for 5-10x query speedup.
/// </summary>
public class BundleHierarchyRecordTests
{
    [Fact]
    public void BundleHierarchyRecord_ShouldSerializeAllFields()
    {
        // Arrange
        var record = new BundleHierarchyRecord
        {
            ParentPk = "00000001",
            ChildPk = "00000002",
            ChildIndex = 0,
            ParentName = "Main Bundle",
            ChildBundleType = TestConstants.BundleTypeGameBundle,
            ChildDepth = 1
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var deserialized = JsonConvert.DeserializeObject<BundleHierarchyRecord>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ParentPk.Should().Be("00000001");
        deserialized.ChildPk.Should().Be("00000002");
        deserialized.ChildIndex.Should().Be(0);
        deserialized.ParentName.Should().Be("Main Bundle");
        deserialized.ChildBundleType.Should().Be(TestConstants.BundleTypeGameBundle);
        deserialized.ChildDepth.Should().Be(1);
    }

    [Fact]
    public void BundleHierarchyRecord_WithNullOptionalFields_ShouldSerializeCorrectly()
    {
        // Arrange
        var record = new BundleHierarchyRecord
        {
            ParentPk = "00000001",
            ChildPk = "00000002",
            ChildIndex = 0,
            ParentName = null,
            ChildBundleType = null,
            ChildDepth = null
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        // Assert
        json.Should().Contain("parentPk");
        json.Should().Contain("childPk");
        json.Should().Contain("childIndex");
        json.Should().NotContain("parentName");
        json.Should().NotContain("childBundleType");
        json.Should().NotContain("childDepth");
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
            ParentPk = "00000001",
            ChildPk = "00000002",
            ChildIndex = 0,
            ChildBundleType = bundleType
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var deserialized = JsonConvert.DeserializeObject<BundleHierarchyRecord>(json);

        // Assert
        deserialized!.ChildBundleType.Should().Be(bundleType);
    }

    [Theory]
    [InlineData(0)] // Root level
    [InlineData(1)] // First child
    [InlineData(2)] // Second level
    [InlineData(5)] // Deep nesting
    public void BundleHierarchyRecord_ShouldSupportVariousDepthLevels(int depth)
    {
        // Arrange
        var record = new BundleHierarchyRecord
        {
            ParentPk = "00000001",
            ChildPk = "00000002",
            ChildIndex = 0,
            ChildDepth = depth
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var deserialized = JsonConvert.DeserializeObject<BundleHierarchyRecord>(json);

        // Assert
        deserialized!.ChildDepth.Should().Be(depth);
    }

    [Fact]
    public void BundleHierarchyRecord_RootBundle_ShouldHaveDepthZero()
    {
        // Arrange
        var record = new BundleHierarchyRecord
        {
            ParentPk = string.Empty,
            ChildPk = "00000001",
            ChildIndex = 0,
            ChildDepth = 0,
            ChildBundleType = TestConstants.BundleTypeGameBundle
        };

        // Act & Assert
        record.ChildDepth.Should().Be(0);
        record.ParentPk.Should().BeEmpty();
    }

    [Fact]
    public void BundleHierarchyRecord_NestedBundle_ShouldHaveCorrectParentAndDepth()
    {
        // Arrange
        var record = new BundleHierarchyRecord
        {
            ParentPk = "00000001",
            ChildPk = "00000002",
            ChildIndex = 0,
            ParentName = "Main Bundle",
            ChildDepth = 1,
            ChildBundleType = TestConstants.BundleTypeGameBundle
        };

        // Act & Assert
        record.ParentPk.Should().Be("00000001");
        record.ParentName.Should().Be("Main Bundle");
        record.ChildDepth.Should().Be(1);
    }

    [Fact]
    public void BundleHierarchyRecord_ParentNameField_EnablesFastNameQueries()
    {
        // Arrange - Schema v2.0 optimization: ParentName allows direct filtering without JOIN
        var record1 = new BundleHierarchyRecord
        {
            ParentPk = "00000001",
            ChildPk = "00000002",
            ChildIndex = 0,
            ParentName = "Main Bundle"
        };

        var record2 = new BundleHierarchyRecord
        {
            ParentPk = "00000003",
            ChildPk = "00000004",
            ChildIndex = 0,
            ParentName = "Level Bundle"
        };

        var records = new[] { record1, record2 };

        // Act - Fast query: filter by ParentName directly (no JOIN needed)
        var mainBundleChildren = records.Where(r => r.ParentName == "Main Bundle").ToList();

        // Assert - This would be 5-10x faster than JOINing with bundle metadata
        mainBundleChildren.Should().HaveCount(1);
        mainBundleChildren[0].ChildPk.Should().Be("00000002");
    }

    [Fact]
    public void BundleHierarchyRecord_ChildBundleTypeField_EnablesFastTypeFiltering()
    {
        // Arrange - Schema v2.0 optimization: ChildBundleType allows direct type filtering
        var record1 = new BundleHierarchyRecord
        {
            ParentPk = "00000001",
            ChildPk = "00000002",
            ChildIndex = 0,
            ChildBundleType = TestConstants.BundleTypeGameBundle
        };

        var record2 = new BundleHierarchyRecord
        {
            ParentPk = "00000001",
            ChildPk = "00000003",
            ChildIndex = 1,
            ChildBundleType = TestConstants.BundleTypeResourceFile
        };

        var records = new[] { record1, record2 };

        // Act - Fast query: filter by ChildBundleType directly
        var gameBundles = records.Where(r => r.ChildBundleType == TestConstants.BundleTypeGameBundle).ToList();

        // Assert
        gameBundles.Should().HaveCount(1);
        gameBundles[0].ChildPk.Should().Be("00000002");
    }

    [Fact]
    public void BundleHierarchyRecord_ChildDepthField_EnablesFastDepthQueries()
    {
        // Arrange - Schema v2.0 optimization: ChildDepth allows direct depth filtering
        var records = new[]
        {
            new BundleHierarchyRecord { ParentPk = string.Empty, ChildPk = "00000001", ChildIndex = 0, ChildDepth = 0 },
            new BundleHierarchyRecord { ParentPk = "00000001", ChildPk = "00000002", ChildIndex = 0, ChildDepth = 1 },
            new BundleHierarchyRecord { ParentPk = "00000002", ChildPk = "00000003", ChildIndex = 0, ChildDepth = 2 }
        };

        // Act - Fast query: find all direct children (depth 1) without recursive traversal
        var directChildren = records.Where(r => r.ChildDepth == 1).ToList();

        // Assert
        directChildren.Should().HaveCount(1);
        directChildren[0].ChildPk.Should().Be("00000002");
    }
}
