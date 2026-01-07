using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Constants;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var root = JObject.Parse(json);

        // Assert - All v2.0 fields must be serializable
        root.Property("resolved").Should().NotBeNull();
        root.Property("source").Should().NotBeNull();
        root.Property("fileIdentifier").Should().NotBeNull();
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
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

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
            ParentPk = "main.bundle",
            ChildPk = "child.bundle",
            ChildIndex = 0,
            ParentName = "Main Bundle",
            ChildBundleType = TestConstants.BundleTypeGameBundle,
            ChildDepth = 1
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var root = JObject.Parse(json);

        // Assert - All v2.0 optimization fields must be serializable
        root.Property("parentName").Should().NotBeNull();
        root.Property("childBundleType").Should().NotBeNull();
        root.Property("childDepth").Should().NotBeNull();
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
            ParentPk = "parent",
            ChildPk = "child",
            ChildIndex = 0,
            ChildBundleType = bundleType
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

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
                ParentPk = "parent",
                ChildPk = "child",
                ChildIndex = 0,
                ChildDepth = depth
            };

            var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var deserialized = JsonConvert.DeserializeObject<BundleHierarchyRecord>(json);

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
            TargetName = "UnityEngine",
            Version = "0.0.0.0",
            SourceModule = "Assembly-CSharp.dll",
            PublicKeyToken = "null",
            Culture = "neutral",
            DependencyType = TestConstants.AssemblyDependencyTypeDirect,
            FailureReason = null
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var root = JObject.Parse(json);

        // Assert - All v2.0 metadata fields must be serializable
        root.Property("sourceModule").Should().NotBeNull();
        root.Property("publicKeyToken").Should().NotBeNull();
        root.Property("culture").Should().NotBeNull();
        root.Property("dependencyType").Should().NotBeNull();
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
            TargetName = "Dependency",
            DependencyType = dependencyType
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

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
            ParentPk = "p1",
            ChildPk = "c1",
            ChildIndex = 0,
            ParentName = "Parent",
            ChildBundleType = TestConstants.BundleTypeGameBundle,
            ChildDepth = 1
        };

        var assemblyDep = new AssemblyDependencyRecord
        {
            SourceAssembly = "s1",
            TargetName = "d1",
            DependencyType = TestConstants.AssemblyDependencyTypeDirect,
            PublicKeyToken = "null",
            Culture = "neutral"
        };

        // Act
        var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        var json1 = JsonConvert.SerializeObject(collectionDep, settings);
        var json2 = JsonConvert.SerializeObject(bundleHierarchy, settings);
        var json3 = JsonConvert.SerializeObject(assemblyDep, settings);

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
            ParentPk = "p",
            ChildPk = "c",
            ChildIndex = 0,
            ParentName = null,
            ChildBundleType = null,
            ChildDepth = null
        };

        var assemblyDep = new AssemblyDependencyRecord
        {
            SourceAssembly = "s",
            TargetName = "d",
            Version = null,
            SourceModule = null,
            PublicKeyToken = null,
            Culture = null,
            DependencyType = null,
            FailureReason = null
        };

        // Act
        var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        var json1 = JsonConvert.SerializeObject(collectionDep, settings);
        var json2 = JsonConvert.SerializeObject(bundleHierarchy, settings);
        var json3 = JsonConvert.SerializeObject(assemblyDep, settings);

        // Assert - Null optional fields should not appear in JSON
        json1.Should().NotContain("\"resolved\"");
        json1.Should().NotContain("\"source\"");
        json1.Should().NotContain("\"fileIdentifier\"");

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
                ParentPk = string.Empty,
                ChildPk = "root.bundle",
                ChildIndex = 0,
                ParentName = null,
                ChildBundleType = TestConstants.BundleTypeGameBundle,
                ChildDepth = 0
            },
            new BundleHierarchyRecord
            {
                ParentPk = "root.bundle",
                ChildPk = "level1.bundle",
                ChildIndex = 0,
                ParentName = "Root Bundle",
                ChildBundleType = TestConstants.BundleTypeGameBundle,
                ChildDepth = 1
            },
            new BundleHierarchyRecord
            {
                ParentPk = "level1.bundle",
                ChildPk = "resource1.bundle",
                ChildIndex = 0,
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
        rootBundles[0].ChildPk.Should().Be("root.bundle");

        directChildren.Should().HaveCount(1);
        directChildren[0].ChildPk.Should().Be("level1.bundle");

        resourceBundles.Should().HaveCount(1);
        resourceBundles[0].ChildDepth.Should().Be(2);
    }

    #endregion
}
