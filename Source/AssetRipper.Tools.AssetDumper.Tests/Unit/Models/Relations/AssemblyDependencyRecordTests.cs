using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Constants;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Models.Relations;

/// <summary>
/// Unit tests for AssemblyDependencyRecord with Schema v2.0 enhancements.
/// Tests the new 'sourceModule', 'publicKeyToken', 'culture', 'dependencyType', and 'failureReason' fields.
/// </summary>
public class AssemblyDependencyRecordTests
{
    [Fact]
    public void AssemblyDependencyRecord_ShouldSerializeAllFields()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            DependencyAssembly = "UnityEngine.CoreModule",
            Version = "0.0.0.0",
            SourceModule = "Assembly-CSharp.dll",
            PublicKeyToken = "null",
            Culture = "neutral",
            DependencyType = TestConstants.AssemblyDependencyTypeDirect,
            FailureReason = null
        };

        // Act
        var json = JsonSerializer.Serialize(record);
        var deserialized = JsonSerializer.Deserialize<AssemblyDependencyRecord>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.SourceAssembly.Should().Be("Assembly-CSharp");
        deserialized.DependencyAssembly.Should().Be("UnityEngine.CoreModule");
        deserialized.Version.Should().Be("0.0.0.0");
        deserialized.SourceModule.Should().Be("Assembly-CSharp.dll");
        deserialized.PublicKeyToken.Should().Be("null");
        deserialized.Culture.Should().Be("neutral");
        deserialized.DependencyType.Should().Be(TestConstants.AssemblyDependencyTypeDirect);
    }

    [Fact]
    public void AssemblyDependencyRecord_WithNullOptionalFields_ShouldSerializeCorrectly()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            DependencyAssembly = "UnityEngine.CoreModule",
            Version = null,
            SourceModule = null,
            PublicKeyToken = null,
            Culture = null,
            DependencyType = null,
            FailureReason = null
        };

        // Act
        var json = JsonSerializer.Serialize(record);

        // Assert
        json.Should().Contain("sourceAssembly");
        json.Should().Contain("dependencyAssembly");
        json.Should().NotContain("version");
        json.Should().NotContain("sourceModule");
        json.Should().NotContain("publicKeyToken");
        json.Should().NotContain("culture");
        json.Should().NotContain("dependencyType");
        json.Should().NotContain("failureReason");
    }

    [Theory]
    [InlineData(TestConstants.AssemblyDependencyTypeDirect)]
    [InlineData(TestConstants.AssemblyDependencyTypeFramework)]
    [InlineData(TestConstants.AssemblyDependencyTypePlugin)]
    [InlineData(TestConstants.AssemblyDependencyTypeUnknown)]
    public void AssemblyDependencyRecord_ShouldSupportAllDependencyTypes(string dependencyType)
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            DependencyAssembly = "SomeDependency",
            DependencyType = dependencyType
        };

        // Act
        var json = JsonSerializer.Serialize(record);
        var deserialized = JsonSerializer.Deserialize<AssemblyDependencyRecord>(json);

        // Assert
        deserialized!.DependencyType.Should().Be(dependencyType);
    }

    [Fact]
    public void AssemblyDependencyRecord_DirectDependency_ShouldHaveCorrectType()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            DependencyAssembly = "UnityEngine.CoreModule",
            DependencyType = TestConstants.AssemblyDependencyTypeDirect
        };

        // Act & Assert
        record.DependencyType.Should().Be(TestConstants.AssemblyDependencyTypeDirect);
    }

    [Fact]
    public void AssemblyDependencyRecord_FrameworkDependency_ShouldHaveCorrectType()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            DependencyAssembly = "System.Runtime",
            DependencyType = TestConstants.AssemblyDependencyTypeFramework,
            PublicKeyToken = "b03f5f7f11d50a3a"
        };

        // Act & Assert
        record.DependencyType.Should().Be(TestConstants.AssemblyDependencyTypeFramework);
        record.PublicKeyToken.Should().Be("b03f5f7f11d50a3a");
    }

    [Fact]
    public void AssemblyDependencyRecord_WithPublicKeyToken_ShouldSerializeCorrectly()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            DependencyAssembly = "StrongNamedAssembly",
            PublicKeyToken = "89845dcd8080cc91"
        };

        // Act
        var json = JsonSerializer.Serialize(record);
        var deserialized = JsonSerializer.Deserialize<AssemblyDependencyRecord>(json);

        // Assert
        deserialized!.PublicKeyToken.Should().Be("89845dcd8080cc91");
    }

    [Fact]
    public void AssemblyDependencyRecord_WithCulture_ShouldSerializeCorrectly()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            DependencyAssembly = "LocalizedAssembly",
            Culture = "en-US"
        };

        // Act
        var json = JsonSerializer.Serialize(record);
        var deserialized = JsonSerializer.Deserialize<AssemblyDependencyRecord>(json);

        // Assert
        deserialized!.Culture.Should().Be("en-US");
    }

    [Fact]
    public void AssemblyDependencyRecord_NeutralCulture_ShouldUseNeutralValue()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            DependencyAssembly = "CultureNeutralAssembly",
            Culture = "neutral"
        };

        // Act & Assert
        record.Culture.Should().Be("neutral");
    }

    [Fact]
    public void AssemblyDependencyRecord_WithSourceModule_ShouldIndicateOriginModule()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            DependencyAssembly = "Dependency",
            SourceModule = "MyAssembly.Module1.dll"
        };

        // Act
        var json = JsonSerializer.Serialize(record);
        var deserialized = JsonSerializer.Deserialize<AssemblyDependencyRecord>(json);

        // Assert
        deserialized!.SourceModule.Should().Be("MyAssembly.Module1.dll");
    }

    [Fact]
    public void AssemblyDependencyRecord_WithFailureReason_ShouldIndicateResolutionFailure()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            DependencyAssembly = "MissingDependency",
            DependencyType = TestConstants.AssemblyDependencyTypeUnknown,
            FailureReason = "Assembly not found in search paths"
        };

        // Act
        var json = JsonSerializer.Serialize(record);
        var deserialized = JsonSerializer.Deserialize<AssemblyDependencyRecord>(json);

        // Assert
        deserialized!.FailureReason.Should().Be("Assembly not found in search paths");
        deserialized.DependencyType.Should().Be(TestConstants.AssemblyDependencyTypeUnknown);
    }

    [Fact]
    public void AssemblyDependencyRecord_SuccessfulResolution_ShouldHaveNullFailureReason()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            DependencyAssembly = "ValidDependency",
            DependencyType = TestConstants.AssemblyDependencyTypeDirect,
            FailureReason = null
        };

        // Act & Assert
        record.FailureReason.Should().BeNull();
    }

    [Fact]
    public void AssemblyDependencyRecord_CompleteMetadata_ShouldSerializeAllEnhancedFields()
    {
        // Arrange - Complete v2.0 metadata example
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "Assembly-CSharp",
            DependencyAssembly = "UnityEngine.CoreModule",
            Version = "0.0.0.0",
            SourceModule = "Assembly-CSharp.dll",
            PublicKeyToken = "null",
            Culture = "neutral",
            DependencyType = TestConstants.AssemblyDependencyTypeFramework,
            FailureReason = null
        };

        // Act
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });

        // Assert - All v2.0 fields should be present
        json.Should().Contain("sourceModule");
        json.Should().Contain("publicKeyToken");
        json.Should().Contain("culture");
        json.Should().Contain("dependencyType");
        json.Should().Contain(TestConstants.AssemblyDependencyTypeFramework);
    }

    [Fact]
    public void AssemblyDependencyRecord_VersionString_ShouldSupportStandardFormat()
    {
        // Arrange
        var record = new AssemblyDependencyRecord
        {
            SourceAssembly = "MyAssembly",
            DependencyAssembly = "VersionedDependency",
            Version = "1.2.3.4"
        };

        // Act
        var json = JsonSerializer.Serialize(record);
        var deserialized = JsonSerializer.Deserialize<AssemblyDependencyRecord>(json);

        // Assert
        deserialized!.Version.Should().Be("1.2.3.4");
    }
}
