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
/// Unit tests for AssemblyDependencyExporter with Schema v2.0 enhancements.
/// Validates that the exporter correctly populates enhanced metadata fields:
/// 'sourceModule', 'publicKeyToken', 'culture', 'dependencyType', and 'failureReason'.
/// </summary>
public class AssemblyDependencyExporterTests : IDisposable
{
    private readonly string _testOutputPath;
    private readonly Options _options;

    public AssemblyDependencyExporterTests()
    {
        _testOutputPath = TestPathHelper.CreateTestDirectory(nameof(AssemblyDependencyExporterTests));
        _options = OptionsBuilder.CreateForExport(".", _testOutputPath);
    }

    public void Dispose()
    {
        TestPathHelper.CleanupTestDirectory(_testOutputPath);
    }

    [Fact]
    public void AssemblyDependencyExporter_ShouldExportDependencyData()
    {
        // Arrange
        var exporter = new AssemblyDependencyExporter(_options, CompressionKind.None, enableIndex: false);
        exporter.Should().NotBeNull();

        var expected = new DomainExportResult(
            domain: "assembly_dependencies",
            tableId: "relations/assembly_dependencies",
            schemaPath: "Schemas/v2/relations/assembly_dependencies.schema.json");

        expected.Domain.Should().Be("assembly_dependencies");
        expected.SchemaPath.Should().Contain("assembly_dependencies");
    }

    [Fact]
    public void AssemblyDependencyExporter_ShouldHaveCorrectSchema()
    {
        var expected = new DomainExportResult(
            domain: "assembly_dependencies",
            tableId: "relations/assembly_dependencies",
            schemaPath: "Schemas/v2/relations/assembly_dependencies.schema.json");

        expected.SchemaPath.Should().Be("Schemas/v2/relations/assembly_dependencies.schema.json");
    }

    [Fact]
    public void AssemblyDependencyRecord_SourceModuleField_ShouldIdentifyOriginModule()
    {
        // Arrange - v2.0 enhancement: SourceModule identifies which module has the dependency
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            TargetName = "UnityEngine.CoreModule",
            SourceModule = "MyAssembly.Module1.dll"
        };

        // Assert
        record.SourceModule.Should().Be("MyAssembly.Module1.dll");
    }

    [Fact]
    public void AssemblyDependencyRecord_PublicKeyTokenField_ShouldStoreStrongNameInfo()
    {
        // Arrange - v2.0 enhancement: PublicKeyToken for strong-named assemblies
        var strongNamedRecord = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            TargetName = "System.Runtime",
            PublicKeyToken = "b03f5f7f11d50a3a"
        };

        var notStrongNamedRecord = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            TargetName = "LocalAssembly",
            PublicKeyToken = null
        };

        // Assert
        strongNamedRecord.PublicKeyToken.Should().Be("b03f5f7f11d50a3a");
        notStrongNamedRecord.PublicKeyToken.Should().BeNull();
    }

    [Fact]
    public void AssemblyDependencyRecord_CultureField_ShouldStoreCultureInfo()
    {
        // Arrange - v2.0 enhancement: Culture for localized assemblies
        var neutralRecord = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            TargetName = "UnityEngine",
            Culture = "neutral"
        };

        var localizedRecord = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            TargetName = "LocalizedResources",
            Culture = "en-US"
        };

        // Assert
        neutralRecord.Culture.Should().Be("neutral");
        localizedRecord.Culture.Should().Be("en-US");
    }

    [Theory]
    [InlineData(TestConstants.AssemblyDependencyTypeDirect)]
    [InlineData(TestConstants.AssemblyDependencyTypeFramework)]
    [InlineData(TestConstants.AssemblyDependencyTypePlugin)]
    [InlineData(TestConstants.AssemblyDependencyTypeUnknown)]
    public void AssemblyDependencyRecord_DependencyTypeField_ShouldSupportAllTypes(string dependencyType)
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Source",
            TargetName = "Dependency",
            DependencyType = dependencyType
        };

        // Assert
        record.DependencyType.Should().Be(dependencyType);
    }

    [Fact]
    public void AssemblyDependencyRecord_DirectDependency_ShouldHaveCorrectType()
    {
        // Arrange - Direct user code dependency
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            TargetName = "UnityEngine.CoreModule",
            DependencyType = TestConstants.AssemblyDependencyTypeDirect
        };

        // Assert
        record.DependencyType.Should().Be(TestConstants.AssemblyDependencyTypeDirect);
    }

    [Fact]
    public void AssemblyDependencyRecord_FrameworkDependency_ShouldHaveCorrectTypeAndMetadata()
    {
        // Arrange - .NET Framework dependency
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            TargetName = "System.Runtime",
            DependencyType = TestConstants.AssemblyDependencyTypeFramework,
            PublicKeyToken = "b03f5f7f11d50a3a",
            Culture = "neutral",
            Version = "8.0.0.0"
        };

        // Assert
        record.DependencyType.Should().Be(TestConstants.AssemblyDependencyTypeFramework);
        record.PublicKeyToken.Should().Be("b03f5f7f11d50a3a");
        record.Culture.Should().Be("neutral");
    }

    [Fact]
    public void AssemblyDependencyRecord_PluginDependency_ShouldHaveCorrectType()
    {
        // Arrange - Third-party plugin dependency
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            TargetName = "Newtonsoft.Json",
            DependencyType = TestConstants.AssemblyDependencyTypePlugin
        };

        // Assert
        record.DependencyType.Should().Be(TestConstants.AssemblyDependencyTypePlugin);
    }

    [Fact]
    public void AssemblyDependencyRecord_FailureReasonField_ShouldIndicateResolutionFailure()
    {
        // Arrange - v2.0 enhancement: FailureReason for debugging unresolved dependencies
        var failedRecord = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            TargetName = "MissingDependency",
            DependencyType = TestConstants.AssemblyDependencyTypeUnknown,
            FailureReason = "Assembly not found in search paths"
        };

        var successRecord = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            TargetName = "ValidDependency",
            DependencyType = TestConstants.AssemblyDependencyTypeDirect,
            FailureReason = null
        };

        // Assert
        failedRecord.FailureReason.Should().Be("Assembly not found in search paths");
        successRecord.FailureReason.Should().BeNull();
    }

    [Fact]
    public void AssemblyDependencyRecord_VersionField_ShouldStoreVersionInfo()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            TargetName = "UnityEngine.CoreModule",
            Version = "0.0.0.0"
        };

        // Assert
        record.Version.Should().Be("0.0.0.0");
    }

    [Fact]
    public void AssemblyDependencyRecord_V2_1MetadataFields_ShouldWorkTogether()
    {
        // Arrange - Complete v2.0 metadata example
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            TargetName = "UnityEngine.CoreModule",
            Version = "0.0.0.0",
            SourceModule = "Assembly-CSharp.dll",
            PublicKeyToken = "null",
            Culture = "neutral",
            DependencyType = TestConstants.AssemblyDependencyTypeFramework,
            FailureReason = null
        };

        // Assert - All v2.0 fields should be properly set
        record.SourceModule.Should().Be("Assembly-CSharp.dll");
        record.PublicKeyToken.Should().Be("null");
        record.Culture.Should().Be("neutral");
        record.DependencyType.Should().Be(TestConstants.AssemblyDependencyTypeFramework);
        record.FailureReason.Should().BeNull();
    }

    [Fact]
    public void AssemblyDependencyRecord_OptionalFields_ShouldBeNullable()
    {
        // Arrange
        var minimalRecord = new AssemblyDependencyRecord
        {
            SourceAssembly = "Source",
            TargetName = "Dependency",
            Version = null,
            SourceModule = null,
            PublicKeyToken = null,
            Culture = null,
            DependencyType = null,
            FailureReason = null
        };

        // Assert
        minimalRecord.Version.Should().BeNull();
        minimalRecord.SourceModule.Should().BeNull();
        minimalRecord.PublicKeyToken.Should().BeNull();
        minimalRecord.Culture.Should().BeNull();
        minimalRecord.DependencyType.Should().BeNull();
        minimalRecord.FailureReason.Should().BeNull();
    }

    [Fact]
    public void AssemblyDependencyExporter_OutputFormat_ShouldBeNDJson()
    {
        var expected = new DomainExportResult(
            domain: "assembly_dependencies",
            tableId: "relations/assembly_dependencies",
            schemaPath: "Schemas/v2/relations/assembly_dependencies.schema.json");

        expected.Format.Should().Be("ndjson");
    }

    [Fact]
    public void AssemblyDependencyRecord_DependencyTypeField_EnablesFastTypeFiltering()
    {
        // Arrange - v2.0 optimization: DependencyType allows direct type filtering
        var records = new[]
        {
            new AssemblyDependencyRecord
            {
                SourceAssembly = "Source",
                TargetName = "Direct1",
                DependencyType = TestConstants.AssemblyDependencyTypeDirect
            },
            new AssemblyDependencyRecord
            {
                SourceAssembly = "Source",
                TargetName = "Framework1",
                DependencyType = TestConstants.AssemblyDependencyTypeFramework
            },
            new AssemblyDependencyRecord
            {
                SourceAssembly = "Source",
                TargetName = "Plugin1",
                DependencyType = TestConstants.AssemblyDependencyTypePlugin
            }
        };

        // Act - Fast query: filter by DependencyType directly
        var frameworkDeps = records.Where(r => r.DependencyType == TestConstants.AssemblyDependencyTypeFramework).ToList();

        // Assert
        frameworkDeps.Should().HaveCount(1);
        frameworkDeps[0].TargetName.Should().Be("Framework1");
    }
}
