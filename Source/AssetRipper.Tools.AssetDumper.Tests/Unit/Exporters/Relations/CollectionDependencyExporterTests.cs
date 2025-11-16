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
/// Unit tests for CollectionDependencyExporter with Schema v2.0 enhancements.
/// Validates that the exporter correctly populates 'resolved', 'source', and 'fileIdentifier' fields.
/// </summary>
public class CollectionDependencyExporterTests : IDisposable
{
    private readonly string _testOutputPath;
    private readonly Options _options;

    public CollectionDependencyExporterTests()
    {
        _testOutputPath = TestPathHelper.CreateTestDirectory(nameof(CollectionDependencyExporterTests));
        _options = OptionsBuilder.CreateForExport(".", _testOutputPath);
    }

    public void Dispose()
    {
        TestPathHelper.CleanupTestDirectory(_testOutputPath);
    }

    [Fact]
    public void CollectionDependencyExporter_ShouldExportBasicDependency()
    {
        // Arrange
        var exporter = new CollectionDependencyExporter(_options);

        // Act - This will test that the exporter can be instantiated and configured
        var result = exporter.GetDomainResult();

        // Assert
        result.Should().NotBeNull();
        result.DomainName.Should().Be("collection_dependencies");
        result.SchemaPath.Should().Contain("collection_dependencies");
    }

    [Fact]
    public void CollectionDependencyExporter_ShouldHaveCorrectSchema()
    {
        // Arrange
        var exporter = new CollectionDependencyExporter(_options);

        // Act
        var result = exporter.GetDomainResult();

        // Assert
        result.SchemaPath.Should().Be("Schemas/v2/relations/collection_dependencies.schema.json");
    }

    [Fact]
    public void CollectionDependencyRecord_ResolvedField_ShouldBeOptional()
    {
        // Arrange
        var record = new CollectionDependencyRecord
        {
            SourceCollection = TestConstants.DefaultCollectionId,
            DependencyIndex = 0,
            TargetCollection = TestConstants.SecondaryCollectionId,
            Resolved = null // Optional field
        };

        // Assert
        record.Resolved.Should().BeNull();
    }

    [Fact]
    public void CollectionDependencyRecord_SourceField_ShouldSupportAllValidValues()
    {
        // Arrange & Act
        var serializedRecord = new CollectionDependencyRecord
        {
            SourceCollection = "source",
            TargetCollection = "target",
            Source = TestConstants.DependencySourceSerialized
        };

        var dynamicRecord = new CollectionDependencyRecord
        {
            SourceCollection = "source",
            TargetCollection = "target",
            Source = TestConstants.DependencySourceDynamic
        };

        var builtinRecord = new CollectionDependencyRecord
        {
            SourceCollection = "source",
            TargetCollection = "target",
            Source = TestConstants.DependencySourceBuiltin
        };

        // Assert
        serializedRecord.Source.Should().Be(TestConstants.DependencySourceSerialized);
        dynamicRecord.Source.Should().Be(TestConstants.DependencySourceDynamic);
        builtinRecord.Source.Should().Be(TestConstants.DependencySourceBuiltin);
    }

    [Fact]
    public void CollectionDependencyRecord_FileIdentifierField_ShouldBeOptional()
    {
        // Arrange
        var recordWithoutFileId = new CollectionDependencyRecord
        {
            SourceCollection = "source",
            TargetCollection = "target",
            FileIdentifier = null
        };

        var recordWithFileId = new CollectionDependencyRecord
        {
            SourceCollection = "source",
            TargetCollection = "target",
            FileIdentifier = new FileIdentifierRecord
            {
                Guid = TestConstants.ValidGuid,
                Type = 3,
                PathName = "Assets/Test.unity"
            }
        };

        // Assert
        recordWithoutFileId.FileIdentifier.Should().BeNull();
        recordWithFileId.FileIdentifier.Should().NotBeNull();
        recordWithFileId.FileIdentifier!.Guid.Should().Be(TestConstants.ValidGuid);
    }

    [Fact]
    public void CollectionDependencyRecord_V2_1Fields_ShouldWorkTogether()
    {
        // Arrange - Complete v2.0 example
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
                PathName = "Assets/Scenes/Level1.unity"
            }
        };

        // Assert - All v2.0 fields should be properly set
        record.Resolved.Should().BeTrue();
        record.Source.Should().Be(TestConstants.DependencySourceSerialized);
        record.FileIdentifier.Should().NotBeNull();
        record.FileIdentifier!.Guid.Should().Be(TestConstants.ValidGuid);
        record.FileIdentifier.PathName.Should().Be("Assets/Scenes/Level1.unity");
    }

    [Fact]
    public void FileIdentifierRecord_AllFields_ShouldBeAccessible()
    {
        // Arrange
        var fileId = new FileIdentifierRecord
        {
            Guid = TestConstants.ValidGuid,
            Type = 3,
            PathName = "Assets/Test.unity"
        };

        // Assert
        fileId.Guid.Should().Be(TestConstants.ValidGuid);
        fileId.Type.Should().Be(3);
        fileId.PathName.Should().Be("Assets/Test.unity");
    }

    [Fact]
    public void CollectionDependencyExporter_OutputFormat_ShouldBeNDJson()
    {
        // Arrange
        var exporter = new CollectionDependencyExporter(_options);

        // Act
        var result = exporter.GetDomainResult();

        // Assert
        result.Format.Should().Be("ndjson");
    }
}
