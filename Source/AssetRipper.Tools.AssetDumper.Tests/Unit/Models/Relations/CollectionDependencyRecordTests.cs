using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Constants;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Models.Relations;

/// <summary>
/// Unit tests for CollectionDependencyRecord with Schema v2.0 enhancements.
/// Tests the new 'resolved', 'source', and 'fileIdentifier' fields.
/// </summary>
public class CollectionDependencyRecordTests
{
    [Fact]
    public void CollectionDependencyRecord_ShouldSerializeAllFields()
    {
        // Arrange
        var record = new CollectionDependencyRecord
        {
            SourceCollection = "sharedassets1.assets",
            DependencyIndex = 0,
            TargetCollection = "level1.unity",
            Resolved = true,
            Source = TestConstants.DependencySourceSerialized,
            FileIdentifier = new FileIdentifierRecord
            {
                Guid = TestConstants.ValidGuid,
                Type = 3,
                PathName = "Assets/Scenes/Level1.unity"
            }
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var deserialized = JsonConvert.DeserializeObject<CollectionDependencyRecord>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.SourceCollection.Should().Be("sharedassets1.assets");
        deserialized.TargetCollection.Should().Be("level1.unity");
        deserialized.Resolved.Should().BeTrue();
        deserialized.Source.Should().Be(TestConstants.DependencySourceSerialized);
        deserialized.FileIdentifier.Should().NotBeNull();
        deserialized.FileIdentifier!.Guid.Should().Be(TestConstants.ValidGuid);
    }

    [Fact]
    public void CollectionDependencyRecord_WithNullOptionalFields_ShouldSerializeCorrectly()
    {
        // Arrange
        var record = new CollectionDependencyRecord
        {
            SourceCollection = "sharedassets1.assets",
            DependencyIndex = 0,
            TargetCollection = "level1.unity",
            Resolved = null,
            Source = null,
            FileIdentifier = null
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        // Assert
        json.Should().NotContain("\"resolved\"");
        json.Should().NotContain("\"source\"");
        json.Should().NotContain("\"fileIdentifier\"");
    }

    [Theory]
    [InlineData(TestConstants.DependencySourceSerialized)]
    [InlineData(TestConstants.DependencySourceDynamic)]
    [InlineData(TestConstants.DependencySourceBuiltin)]
    public void CollectionDependencyRecord_ShouldSupportAllSourceTypes(string sourceType)
    {
        // Arrange
        var record = new CollectionDependencyRecord
        {
            SourceCollection = "sharedassets1.assets",
            DependencyIndex = 0,
            TargetCollection = "level1.unity",
            Source = sourceType
        };

        // Act
        var json = JsonConvert.SerializeObject(record, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var deserialized = JsonConvert.DeserializeObject<CollectionDependencyRecord>(json);

        // Assert
        deserialized!.Source.Should().Be(sourceType);
    }

    [Fact]
    public void CollectionDependencyRecord_ResolvedTrue_ShouldIndicateSuccessfulResolution()
    {
        // Arrange
        var record = new CollectionDependencyRecord
        {
            SourceCollection = "sharedassets1.assets",
            DependencyIndex = 0,
            TargetCollection = "level1.unity",
            Resolved = true,
            FileIdentifier = new FileIdentifierRecord
            {
                Guid = TestConstants.ValidGuid,
                Type = 3
            }
        };

        // Act & Assert
        record.Resolved.Should().BeTrue();
        record.FileIdentifier.Should().NotBeNull();
    }

    [Fact]
    public void CollectionDependencyRecord_ResolvedFalse_ShouldIndicateUnresolvedDependency()
    {
        // Arrange
        var record = new CollectionDependencyRecord
        {
            SourceCollection = "sharedassets1.assets",
            DependencyIndex = 0,
            TargetCollection = "missing.unity",
            Resolved = false,
            Source = TestConstants.DependencySourceSerialized
        };

        // Act & Assert
        record.Resolved.Should().BeFalse();
    }

    [Fact]
    public void FileIdentifierRecord_ShouldSerializeAllFields()
    {
        // Arrange
        var fileId = new FileIdentifierRecord
        {
            Guid = TestConstants.ValidGuid,
            Type = 3,
            PathName = "Assets/Scenes/Level1.unity"
        };

        // Act
        var json = JsonConvert.SerializeObject(fileId, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var deserialized = JsonConvert.DeserializeObject<FileIdentifierRecord>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Guid.Should().Be(TestConstants.ValidGuid);
        deserialized.Type.Should().Be(3);
        deserialized.PathName.Should().Be("Assets/Scenes/Level1.unity");
    }

    [Fact]
    public void FileIdentifierRecord_WithNullOptionalFields_ShouldSerializeCorrectly()
    {
        // Arrange
        var fileId = new FileIdentifierRecord
        {
            Guid = TestConstants.ValidGuid,
            Type = 3,
            PathName = null
        };

        // Act
        var json = JsonConvert.SerializeObject(fileId, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        // Assert
        json.Should().Contain("\"guid\"");
        json.Should().Contain("\"type\"");
        json.Should().NotContain("\"pathName\"");
    }
}
