using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Validation;
using AssetRipper.Tools.AssetDumper.Validation.Models;
using Moq;
using NUnit.Framework;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace AssetRipper.Tools.AssetDumper.Tests.Validation;

/// <summary>
/// Unit tests for ComprehensiveSchemaValidator.
/// </summary>
[TestFixture]
public class ComprehensiveSchemaValidatorTests
{
    private Mock<Options> _mockOptions;
    private ComprehensiveSchemaValidator _validator;
    private string _tempDirectory;

    [SetUp]
    public void SetUp()
    {
        _mockOptions = new Mock<Options>();
        _mockOptions.Setup(o => o.OutputPath).Returns(_tempDirectory);
        _mockOptions.Setup(o => o.Verbose).Returns(false);
        _mockOptions.Setup(o => o.Silent).Returns(true);

        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        _validator = new ComprehensiveSchemaValidator(_mockOptions.Object);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Test]
    public async Task ValidateAllAsync_WithValidData_ReturnsPassedResult()
    {
        // Arrange
        CreateValidAssetData();
        CreateValidTypeData();
        CreateValidDependencyData();

        var domainResults = CreateDomainResults();

        // Act
        var report = await _validator.ValidateAllAsync(domainResults);

        // Assert
        Assert.That(report.OverallResult, Is.EqualTo(ValidationResult.Passed));
        Assert.That(report.Errors, Is.Empty);
        Assert.That(report.TotalRecordsValidated, Is.GreaterThan(0));
    }

    [Test]
    public async Task ValidateAllAsync_WithInvalidData_ReturnsFailedResult()
    {
        // Arrange
        CreateInvalidAssetData();
        var domainResults = CreateDomainResults();

        // Act
        var report = await _validator.ValidateAllAsync(domainResults);

        // Assert
        Assert.That(report.OverallResult, Is.EqualTo(ValidationResult.Failed));
        Assert.That(report.Errors, Is.Not.Empty);
        Assert.That(report.Errors.Any(e => e.ErrorType == ValidationErrorType.Structural), Is.True);
    }

    [Test]
    public async Task ValidateAllAsync_WithMissingReferences_ReturnsReferenceErrors()
    {
        // Arrange
        CreateAssetDataWithMissingReferences();
        var domainResults = CreateDomainResults();

        // Act
        var report = await _validator.ValidateAllAsync(domainResults);

        // Assert
        Assert.That(report.OverallResult, Is.EqualTo(ValidationResult.Failed));
        Assert.That(report.Errors.Any(e => e.ErrorType == ValidationErrorType.Reference), Is.True);
    }

    [Test]
    public async Task ValidateAllAsync_WithCircularDependencies_ReturnsCircularDependencyError()
    {
        // Arrange
        CreateCircularDependencyData();
        var domainResults = CreateDomainResults();

        // Act
        var report = await _validator.ValidateAllAsync(domainResults);

        // Assert
        Assert.That(report.OverallResult, Is.EqualTo(ValidationResult.Failed));
        Assert.That(report.Errors.Any(e => e.ErrorType == ValidationErrorType.Reference && e.Message.Contains("circular")), Is.True);
    }

    [Test]
    public async Task ValidateAllAsync_WithInvalidCollectionId_ReturnsPatternError()
    {
        // Arrange
        CreateAssetDataWithInvalidCollectionId();
        var domainResults = CreateDomainResults();

        // Act
        var report = await _validator.ValidateAllAsync(domainResults);

        // Assert
        Assert.That(report.OverallResult, Is.EqualTo(ValidationResult.Failed));
        Assert.That(report.Errors.Any(e => e.ErrorType == ValidationErrorType.Pattern), Is.True);
    }

    [Test]
    public async Task ValidateAllAsync_WithInvalidUnityGuid_ReturnsPatternError()
    {
        // Arrange
        CreateSceneDataWithInvalidGuid();
        var domainResults = CreateDomainResults();

        // Act
        var report = await _validator.ValidateAllAsync(domainResults);

        // Assert
        Assert.That(report.OverallResult, Is.EqualTo(ValidationResult.Failed));
        Assert.That(report.Errors.Any(e => e.ErrorType == ValidationErrorType.Pattern), Is.True);
    }

    [Test]
    public async Task ValidateAllAsync_WithCorrectDataFieldStructure_Passes()
    {
        // Arrange: Create asset data with raw payload in data field (not wrapped in container)
        CreateAssetDataWithCorrectDataStructure();
        var domainResults = CreateDomainResults();

        // Act
        var report = await _validator.ValidateAllAsync(domainResults);

        // Assert: Should pass validation - data is raw payload, byteStart/byteSize at root
        Assert.That(report.OverallResult, Is.EqualTo(ValidationResult.Passed));
        Assert.That(report.Errors, Is.Empty);
    }

    [Test]
    public async Task ValidateAllAsync_WithOldDataContainerStructure_Fails()
    {
        // Arrange: Create asset data with OLD container structure (byteStart, byteSize, content)
        CreateAssetDataWithOldContainerStructure();
        var domainResults = CreateDomainResults();

        // Act
        var report = await _validator.ValidateAllAsync(domainResults);

        // Assert: Should fail - data field contains container object instead of raw payload
        // The schema expects raw asset data in the data field
        Assert.That(report.OverallResult, Is.EqualTo(ValidationResult.Failed));
        Assert.That(report.Errors.Any(e => e.Message.Contains("data")), Is.True);
    }

    [Test]
    public void ValidationReport_GeneratesCorrectJson()
    {
        // Arrange
        var report = new ValidationReport
        {
            OverallResult = ValidationResult.Failed,
            ValidationTime = TimeSpan.FromSeconds(5.5),
            TotalRecordsValidated = 1000,
            SchemasLoaded = 25,
            DataFilesProcessed = 10,
            Errors = new List<Validation.Models.ValidationError>
            {
                new Validation.Models.ValidationError
                {
                    ErrorType = ValidationErrorType.Structural,
                    Domain = "assets",
                    TableId = "assets",
                    LineNumber = 1,
                    Message = "Test error",
                    Severity = ValidationSeverity.Error
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        Assert.That(json, Does.Contain("\"overallResult\": \"Failed\""));
        Assert.That(json, Does.Contain("\"validationTime\""));
        Assert.That(json, Does.Contain("\"totalRecordsValidated\": 1000"));
        Assert.That(json, Does.Contain("\"errors\""));
        Assert.That(json, Does.Contain("\"Test error\""));
    }

    #region Helper Methods

    private List<DomainExportResult> CreateDomainResults()
    {
        return new List<DomainExportResult>
        {
            new DomainExportResult("assets", "ndjson", "Schemas/v2/facts/assets.schema.json", "Asset facts"),
            new DomainExportResult("types", "ndjson", "Schemas/v2/facts/types.schema.json", "Type facts"),
            new DomainExportResult("asset_dependencies", "ndjson", "Schemas/v2/relations/asset_dependencies.schema.json", "Asset dependency relations")
        };
    }

    private void CreateValidAssetData()
    {
        var assetsFile = Path.Combine(_tempDirectory, "assets.ndjson");
        var assets = new[]
        {
            new
            {
                domain = "assets",
                pk = new { collectionId = "sharedassets1.assets", pathId = 1 },
                pathId = 1,
                classKey = 1,
                className = "GameObject",
                name = "TestObject",
                unity = new { classId = 1, typeId = 1 }
            },
            new
            {
                domain = "assets",
                pk = new { collectionId = "sharedassets1.assets", pathId = 2 },
                pathId = 2,
                classKey = 4,
                className = "Transform",
                name = "TestTransform",
                unity = new { classId = 4, typeId = 4 }
            }
        };

        WriteNdjsonFile(assetsFile, assets);
    }

    private void CreateValidTypeData()
    {
        var typesFile = Path.Combine(_tempDirectory, "types.ndjson");
        var types = new[]
        {
            new
            {
                domain = "types",
                classKey = 1,
                classId = 1,
                className = "GameObject"
            },
            new
            {
                domain = "types",
                classKey = 4,
                classId = 4,
                className = "Transform"
            }
        };

        WriteNdjsonFile(typesFile, types);
    }

    private void CreateValidDependencyData()
    {
        var depsFile = Path.Combine(_tempDirectory, "asset_dependencies.ndjson");
        var deps = new[]
        {
            new
            {
                domain = "asset_dependencies",
                from = new { collectionId = "sharedassets1.assets", pathId = 1 },
                to = new { collectionId = "sharedassets1.assets", pathId = 2 },
                edge = new
                {
                    kind = "pptr",
                    field = "m_Transform",
                    fieldType = "PPtr<Transform>"
                }
            }
        };

        WriteNdjsonFile(depsFile, deps);
    }

    private void CreateInvalidAssetData()
    {
        var assetsFile = Path.Combine(_tempDirectory, "assets.ndjson");
        var assets = new[]
        {
            new
            {
                domain = "assets",
                // Missing required pk field
                classKey = 1,
                className = "GameObject"
            }
        };

        WriteNdjsonFile(assetsFile, assets);
    }

    private void CreateAssetDataWithMissingReferences()
    {
        var assetsFile = Path.Combine(_tempDirectory, "assets.ndjson");
        var depsFile = Path.Combine(_tempDirectory, "asset_dependencies.ndjson");

        var assets = new[]
        {
            new
            {
                domain = "assets",
                pk = new { collectionId = "sharedassets1.assets", pathId = 1 },
                pathId = 1,
                classKey = 1,
                className = "GameObject"
            }
        };

        var deps = new[]
        {
            new
            {
                domain = "asset_dependencies",
                from = new { collectionId = "sharedassets1.assets", pathId = 1 },
                to = new { collectionId = "sharedassets1.assets", pathId = 999 }, // Non-existent asset
                edge = new
                {
                    kind = "pptr",
                    field = "m_Transform"
                }
            }
        };

        WriteNdjsonFile(assetsFile, assets);
        WriteNdjsonFile(depsFile, deps);
    }

    private void CreateCircularDependencyData()
    {
        var assetsFile = Path.Combine(_tempDirectory, "assets.ndjson");
        var depsFile = Path.Combine(_tempDirectory, "asset_dependencies.ndjson");

        var assets = new[]
        {
            new
            {
                domain = "assets",
                pk = new { collectionId = "sharedassets1.assets", pathId = 1 },
                pathId = 1,
                classKey = 1,
                className = "GameObject"
            },
            new
            {
                domain = "assets",
                pk = new { collectionId = "sharedassets1.assets", pathId = 2 },
                pathId = 2,
                classKey = 1,
                className = "GameObject"
            }
        };

        var deps = new[]
        {
            new
            {
                domain = "asset_dependencies",
                from = new { collectionId = "sharedassets1.assets", pathId = 1 },
                to = new { collectionId = "sharedassets1.assets", pathId = 2 },
                edge = new { kind = "pptr", field = "m_Ref" }
            },
            new
            {
                domain = "asset_dependencies",
                from = new { collectionId = "sharedassets1.assets", pathId = 2 },
                to = new { collectionId = "sharedassets1.assets", pathId = 1 },
                edge = new { kind = "pptr", field = "m_Ref" }
            }
        };

        WriteNdjsonFile(assetsFile, assets);
        WriteNdjsonFile(depsFile, deps);
    }

    private void CreateAssetDataWithInvalidCollectionId()
    {
        var assetsFile = Path.Combine(_tempDirectory, "assets.ndjson");
        var assets = new[]
        {
            new
            {
                domain = "assets",
                pk = new { collectionId = "invalid@id", pathId = 1 }, // Invalid characters
                pathId = 1,
                classKey = 1,
                className = "GameObject"
            }
        };

        WriteNdjsonFile(assetsFile, assets);
    }

    private void CreateSceneDataWithInvalidGuid()
    {
        var scenesFile = Path.Combine(_tempDirectory, "scenes.ndjson");
        var scenes = new[]
        {
            new
            {
                domain = "scenes",
                sceneGuid = "invalid-guid", // Invalid GUID format
                sceneName = "TestScene"
            }
        };

        WriteNdjsonFile(scenesFile, scenes);
    }

    private void CreateAssetDataWithCorrectDataStructure()
    {
        var assetsFile = Path.Combine(_tempDirectory, "assets.ndjson");
        var typesFile = Path.Combine(_tempDirectory, "types.ndjson");

        var assets = new[]
        {
            new
            {
                domain = "assets",
                pk = new { collectionId = "sharedassets1.assets", pathId = 1 },
                pathId = 1L,
                classKey = 1,
                className = "GameObject",
                name = "TestObject",
                unity = new { classId = 1, typeId = 1 },
                byteStart = 12345L,  // At root level
                byteSize = 678,       // At root level
                data = new            // Raw asset payload directly
                {
                    m_Name = "TestObject",
                    m_IsActive = 1,
                    m_Layer = 0
                }
            }
        };

        var types = new[]
        {
            new
            {
                domain = "types",
                classKey = 1,
                classId = 1,
                className = "GameObject"
            }
        };

        WriteNdjsonFile(assetsFile, assets);
        WriteNdjsonFile(typesFile, types);
    }

    private void CreateAssetDataWithOldContainerStructure()
    {
        var assetsFile = Path.Combine(_tempDirectory, "assets.ndjson");
        var typesFile = Path.Combine(_tempDirectory, "types.ndjson");

        var assets = new[]
        {
            new
            {
                domain = "assets",
                pk = new { collectionId = "sharedassets1.assets", pathId = 1 },
                pathId = 1L,
                classKey = 1,
                className = "GameObject",
                name = "TestObject",
                unity = new { classId = 1, typeId = 1 },
                data = new  // OLD structure - wrapped in container
                {
                    byteStart = 12345L,
                    byteSize = 678,
                    content = new  // Actual payload nested inside
                    {
                        m_Name = "TestObject",
                        m_IsActive = 1,
                        m_Layer = 0
                    }
                }
            }
        };

        var types = new[]
        {
            new
            {
                domain = "types",
                classKey = 1,
                classId = 1,
                className = "GameObject"
            }
        };

        WriteNdjsonFile(assetsFile, assets);
        WriteNdjsonFile(typesFile, types);
    }

    private void WriteNdjsonFile<T>(string filePath, T[] data)
    {
        using var writer = new StreamWriter(filePath);
        foreach (var item in data)
        {
            var json = JsonSerializer.Serialize(item);
            writer.WriteLine(json);
        }
    }

    #endregion
}