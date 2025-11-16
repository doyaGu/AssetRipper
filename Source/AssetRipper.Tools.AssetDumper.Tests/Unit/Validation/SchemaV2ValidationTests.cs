using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Constants;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Validation;

/// <summary>
/// Comprehensive validation tests for Schema v2 enhancements.
/// Validates all new fields, enumerations, and optimization features across all Relations models.
/// </summary>
public class SchemaV2ValidationTests
{
    #region Collection Dependencies v2

    [Fact]
    public void SchemaV2_1_CollectionDependency_AllFieldsPresent()
    {
        // Arrange
        var record = new CollectionDependencyRecord
        {
            SourceCollection = TestConstants.DefaultCollectionId,
            DependencyIndex = 0,
            TargetCollection = TestConstants.SecondaryCollectionId,
            Resolved = true,
            Source = TestConstants.DependencySourceSerialized,
            FileIdentifier = new FileIdentifierRecord
            {
                Guid = TestConstants.ValidGuid,
                Type = 3,
                PathName = "Assets/Test.unity"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(record);
        var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        // Assert - All v2.0 fields must be serializable
        root.TryGetProperty("resolved", out _).Should().BeTrue();
        root.TryGetProperty("source", out _).Should().BeTrue();
        root.TryGetProperty("fileIdentifier", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData(TestConstants.DependencySourceSerialized)]
    [InlineData(TestConstants.DependencySourceDynamic)]
    [InlineData(TestConstants.DependencySourceBuiltin)]
    public void SchemaV2_1_CollectionDependency_SourceEnumeration_Valid(string source)
    {
        // Arrange
        var record = new CollectionDependencyRecord
        {
            SourceCollection = "test",
            TargetCollection = "target",
            Source = source
        };

        // Act
        var json = JsonSerializer.Serialize(record);

        // Assert - Enum value should serialize correctly
        json.Should().Contain($"\"{source}\"");
    }

    #endregion

    #region Bundle Hierarchy v2.0

    [Fact]
    public void SchemaV2_1_BundleHierarchy_AllFieldsPresent()
    {
        // Arrange
        var record = new BundleHierarchyRecord
        {
            ParentBundle = "main.bundle",
            ChildBundle = "child.bundle",
            ParentName = "Main Bundle",
            ChildBundleType = TestConstants.BundleTypeGameBundle,
            ChildDepth = 1
        };

        // Act
        var json = JsonSerializer.Serialize(record);
        var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        // Assert - All v2.0 optimization fields must be serializable
        root.TryGetProperty("parentName", out _).Should().BeTrue();
        root.TryGetProperty("childBundleType", out _).Should().BeTrue();
        root.TryGetProperty("childDepth", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData(TestConstants.BundleTypeGameBundle)]
    [InlineData(TestConstants.BundleTypeSerialized)]
    [InlineData(TestConstants.BundleTypeProcessed)]
    [InlineData(TestConstants.BundleTypeResourceFile)]
    [InlineData(TestConstants.BundleTypeWebBundle)]
    [InlineData(TestConstants.BundleTypeUnknown)]
    public void SchemaV2_1_BundleHierarchy_BundleTypeEnumeration_Valid(string bundleType)
    {
        // Arrange
        var record = new BundleHierarchyRecord
        {
            ParentBundle = "parent",
            ChildBundle = "child",
            ChildBundleType = bundleType
        };

        // Act
        var json = JsonSerializer.Serialize(record);

        // Assert - Enum value should serialize correctly
        json.Should().Contain($"\"{bundleType}\"");
    }

    [Fact]
    public void SchemaV2_1_BundleHierarchy_DepthOptimization_ValidRange()
    {
        // Arrange - Test depth values from 0 (root) to 10 (deep nesting)
        var depths = Enumerable.Range(0, 11).ToList();

        // Act & Assert
        foreach (var depth in depths)
        {
            var record = new BundleHierarchyRecord
            {
                ParentBundle = "parent",
                ChildBundle = "child",
                ChildDepth = depth
            };

            var json = JsonSerializer.Serialize(record);
            var deserialized = JsonSerializer.Deserialize<BundleHierarchyRecord>(json);

            deserialized!.ChildDepth.Should().Be(depth);
        }
    }

    #endregion

    #region Assembly Dependencies v2.0

    [Fact]
    public void SchemaV2_1_AssemblyDependency_AllFieldsPresent()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            DependencyAssembly = "UnityEngine",
            Version = "0.0.0.0",
            SourceModule = "Assembly-CSharp.dll",
            PublicKeyToken = "null",
            Culture = "neutral",
            DependencyType = TestConstants.AssemblyDependencyTypeDirect,
            FailureReason = null
        };

        // Act
        var json = JsonSerializer.Serialize(record);
        var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        // Assert - All v2.0 metadata fields must be serializable
        root.TryGetProperty("sourceModule", out _).Should().BeTrue();
        root.TryGetProperty("publicKeyToken", out _).Should().BeTrue();
        root.TryGetProperty("culture", out _).Should().BeTrue();
        root.TryGetProperty("dependencyType", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData(TestConstants.AssemblyDependencyTypeDirect)]
    [InlineData(TestConstants.AssemblyDependencyTypeFramework)]
    [InlineData(TestConstants.AssemblyDependencyTypePlugin)]
    [InlineData(TestConstants.AssemblyDependencyTypeUnknown)]
    public void SchemaV2_1_AssemblyDependency_DependencyTypeEnumeration_Valid(string dependencyType)
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Source",
            DependencyAssembly = "Dependency",
            DependencyType = dependencyType
        };

        // Act
        var json = JsonSerializer.Serialize(record);

        // Assert - Enum value should serialize correctly
        json.Should().Contain($"\"{dependencyType}\"");
    }

    #endregion

    #region Cross-Model Integration Tests

    [Fact]
    public void SchemaV2_1_Integration_AllRelationsModels_Serialize()
    {
        // Arrange - Create one instance of each enhanced Relations model
        var collectionDep = new CollectionDependencyRecord
        {
            SourceCollection = "s1",
            TargetCollection = "t1",
            Resolved = true,
            Source = TestConstants.DependencySourceSerialized
        };

        var bundleHierarchy = new BundleHierarchyRecord
        {
            ParentBundle = "p1",
            ChildBundle = "c1",
            ParentName = "Parent",
            ChildBundleType = TestConstants.BundleTypeGameBundle,
            ChildDepth = 1
        };

        var assemblyDep = new AssemblyDependencyRecord
        {
            SourceAssembly = "s1",
            DependencyAssembly = "d1",
            DependencyType = TestConstants.AssemblyDependencyTypeDirect,
            PublicKeyToken = "null",
            Culture = "neutral"
        };

        // Act
        var json1 = JsonSerializer.Serialize(collectionDep);
        var json2 = JsonSerializer.Serialize(bundleHierarchy);
        var json3 = JsonSerializer.Serialize(assemblyDep);

        // Assert - All v2.0 models should serialize without errors
        json1.Should().NotBeNullOrEmpty();
        json2.Should().NotBeNullOrEmpty();
        json3.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SchemaV2_1_Integration_AllEnumerations_Distinct()
    {
        // Arrange - Collect all v2.0 enumeration values
        var dependencySources = new[]
        {
            TestConstants.DependencySourceSerialized,
            TestConstants.DependencySourceDynamic,
            TestConstants.DependencySourceBuiltin
        };

        var bundleTypes = new[]
        {
            TestConstants.BundleTypeGameBundle,
            TestConstants.BundleTypeSerialized,
            TestConstants.BundleTypeProcessed,
            TestConstants.BundleTypeResourceFile,
            TestConstants.BundleTypeWebBundle,
            TestConstants.BundleTypeUnknown
        };

        var assemblyDepTypes = new[]
        {
            TestConstants.AssemblyDependencyTypeDirect,
            TestConstants.AssemblyDependencyTypeFramework,
            TestConstants.AssemblyDependencyTypePlugin,
            TestConstants.AssemblyDependencyTypeUnknown
        };

        // Assert - All enumeration values should be distinct
        dependencySources.Should().OnlyHaveUniqueItems();
        bundleTypes.Should().OnlyHaveUniqueItems();
        assemblyDepTypes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SchemaV2_1_OptionalFields_NullHandling_Consistent()
    {
        // Arrange - Create records with all optional fields set to null
        var collectionDep = new CollectionDependencyRecord
        {
            SourceCollection = "s",
            TargetCollection = "t",
            Resolved = null,
            Source = null,
            FileIdentifier = null
        };

        var bundleHierarchy = new BundleHierarchyRecord
        {
            ParentBundle = "p",
            ChildBundle = "c",
            ParentName = null,
            ChildBundleType = null,
            ChildDepth = null
        };

        var assemblyDep = new AssemblyDependencyRecord
        {
            SourceAssembly = "s",
            DependencyAssembly = "d",
            Version = null,
            SourceModule = null,
            PublicKeyToken = null,
            Culture = null,
            DependencyType = null,
            FailureReason = null
        };

        // Act
        var json1 = JsonSerializer.Serialize(collectionDep);
        var json2 = JsonSerializer.Serialize(bundleHierarchy);
        var json3 = JsonSerializer.Serialize(assemblyDep);

        // Assert - Null optional fields should not appear in JSON
        json1.Should().NotContain("resolved");
        json1.Should().NotContain("source");
        json1.Should().NotContain("fileIdentifier");

        json2.Should().NotContain("parentName");
        json2.Should().NotContain("childBundleType");
        json2.Should().NotContain("childDepth");

        json3.Should().NotContain("version");
        json3.Should().NotContain("sourceModule");
        json3.Should().NotContain("publicKeyToken");
    }

    #endregion

    #region Performance Optimization Validation

    [Fact]
    public void SchemaV2_1_BundleHierarchy_QueryOptimizations_Validated()
    {
        // Arrange - Create hierarchical data with v2.0 optimization fields
        var records = new[]
        {
            new BundleHierarchyRecord
            {
                ParentBundle = null,
                ChildBundle = "root.bundle",
                ParentName = null,
                ChildBundleType = TestConstants.BundleTypeGameBundle,
                ChildDepth = 0
            },
            new BundleHierarchyRecord
            {
                ParentBundle = "root.bundle",
                ChildBundle = "level1.bundle",
                ParentName = "Root Bundle",
                ChildBundleType = TestConstants.BundleTypeGameBundle,
                ChildDepth = 1
            },
            new BundleHierarchyRecord
            {
                ParentBundle = "level1.bundle",
                ChildBundle = "resource1.bundle",
                ParentName = "Level 1 Bundle",
                ChildBundleType = TestConstants.BundleTypeResourceFile,
                ChildDepth = 2
            }
        };

        // Act - Perform optimized queries (no JOINs needed)
        var rootBundles = records.Where(r => r.ChildDepth == 0).ToList();
        var directChildren = records.Where(r => r.ParentName == "Root Bundle").ToList();
        var resourceBundles = records.Where(r => r.ChildBundleType == TestConstants.BundleTypeResourceFile).ToList();

        // Assert - All optimization queries should work correctly
        rootBundles.Should().HaveCount(1);
        rootBundles[0].ChildBundle.Should().Be("root.bundle");

        directChildren.Should().HaveCount(1);
        directChildren[0].ChildBundle.Should().Be("level1.bundle");

        resourceBundles.Should().HaveCount(1);
        resourceBundles[0].ChildDepth.Should().Be(2);
    }

    #endregion
}
