# Comprehensive Schema Validator for AssetDumper

A robust, extensible validation framework for AssetDumper v2 schema compliance that goes beyond basic JSON schema validation to provide deep structural, semantic, and cross-table analysis.

## Features

### 🔍 **Comprehensive Validation Types**

1. **Structural Compliance**
   - Validates all required fields are present
   - Detects unexpected additional fields
   - Ensures correct nesting and structure
   - Verifies JSON schema compliance

2. **Data Type Adherence**
   - Validates each data element's type matches schema definitions exactly
   - Checks for type compatibility across references
   - Validates array and object structures

3. **Constraint Violation Detection**
   - Regex pattern validation (GUIDs, CollectionIDs, etc.)
   - Min/max range checking
   - Array length limits
   - Enum value validation

4. **Conditional Logic Evaluation**
   - Validates if/then/else logic in schemas
   - Field dependencies and conditional requirements
   - Unity-specific conditional rules

5. **Cross-Table Reference Validation**
   - Foreign key relationship verification
   - Asset reference integrity across all tables
   - Index consistency validation
   - Dependency graph analysis

6. **Semantic Validation**
   - Unity-specific constraints and business rules
   - MonoBehaviour script type validation
   - GameObject hierarchy validation
   - Asset bundle consistency checks

### 📊 **Detailed Reporting**

- **JSONPath Locations**: Precise location of each violation
- **Expected vs Actual Values**: Clear comparison of what was expected vs found
- **Error Categorization**: Grouped by type, domain, and severity
- **Performance Metrics**: Validation time, records per second, memory usage
- **Comprehensive Statistics**: Domain summaries, error counts, validation coverage

### 🛠 **Usage**

#### As Standalone Tool

```bash
# Basic validation
ValidationTool --output ./export

# With custom report path and verbose logging
ValidationTool -o ./export -r report.json -v

# Validate specific domains only
ValidationTool -o ./export -d assets,types,relations

# Continue on errors with error limit
ValidationTool -o ./export -c -m 50
```

#### Integrated into Pipeline

```csharp
var validator = new ComprehensiveSchemaValidator(options);
var report = await validator.ValidateAllAsync(domainResults);

if (report.OverallResult == ValidationResult.Failed)
{
    // Handle validation errors
    foreach (var error in report.Errors)
    {
        Logger.Error($"[{error.ErrorType}] {error.Domain}:{error.LineNumber} - {error.Message}");
    }
}
```

### 📁 **File Structure**

```
Validation/
├── ComprehensiveSchemaValidator.cs    # Main validation engine
├── ValidationTool.cs              # Standalone CLI tool
├── Models/
│   ├── ValidationReport.cs         # Report data structures
│   ├── ValidationError.cs         # Error and warning models
│   └── ValidationContext.cs       # Cross-reference context
└── README.md                    # This documentation
```

### 🔧 **Configuration**

The validator supports extensive configuration through command-line options:

- `--output, -o`: AssetDumper output directory (required)
- `--report, -r`: Validation report output path
- `--schemas, -s`: Schema directory path
- `--verbose, -v`: Enable detailed logging
- `--quiet, -q`: Suppress non-error output
- `--continue-on-error, -c`: Continue validation despite errors
- `--max-errors, -m`: Limit number of reported errors
- `--domains, -d`: Validate specific domains only

### 📋 **Validation Domains**

The validator supports all AssetDumper v2 domains:

#### Facts Tables
- `assets` - Asset facts and metadata
- `types` - Type dictionary and class information
- `assemblies` - Assembly information
- `bundles` - Bundle hierarchy data
- `collections` - Collection metadata
- `scenes` - Scene information
- `script_metadata` - MonoBehaviour script data
- `script_sources` - Source code content
- `type_definitions` - Type definition details
- `type_members` - Type member information

#### Relations Tables
- `asset_dependencies` - Asset reference relationships
- `assembly_dependencies` - Assembly dependency graph
- `bundle_hierarchy` - Bundle nesting structure
- `collection_dependencies` - Collection relationships
- `script_type_mapping` - Script to type mappings
- `type_inheritance` - Type inheritance hierarchy

#### Index Tables
- `by_class` - Assets grouped by type
- `by_collection` - Assets grouped by collection
- `by_name` - Assets grouped by name

#### Metrics Tables
- `asset_distribution` - Asset statistics by type and bundle
- `dependency_stats` - Dependency analysis metrics
- `scene_stats` - Scene-specific statistics

### 🎯 **Unity-Specific Rules**

The validator includes extensive Unity-specific validation:

#### GameObject Rules
- Required fields: `m_Name`, `m_Transform`
- Component reference validation
- Hierarchy consistency

#### MonoBehaviour Rules
- Script type index validation
- Assembly reference verification
- Script GUID consistency

#### Transform Rules
- Position, rotation, scale validation
- Parent-child relationship checks
- Hierarchy depth validation

#### Asset Reference Rules
- PPtr structure validation
- FileID range checking
- Cross-collection reference verification

### 📈 **Performance Characteristics**

- **Scalable**: Processes millions of records efficiently
- **Memory-Efficient**: Streaming NDJSON processing
- **Parallel**: Multi-threaded validation where possible
- **Incremental**: Can validate partial exports

### 🔍 **Error Detection Examples**

#### Structural Errors
```json
{
  "errorType": "MissingRequired",
  "message": "Required field 'pk' is missing",
  "jsonPath": "$",
  "expectedValue": "AssetPK object",
  "actualValue": null
}
```

#### Reference Errors
```json
{
  "errorType": "Reference",
  "message": "Reference to non-existent asset",
  "jsonPath": "$.to",
  "details": {
    "referencedAsset": "sharedassets1.assets:999",
    "validAssets": ["sharedassets1.assets:1", "sharedassets1.assets:2"]
  }
}
```

#### Constraint Errors
```json
{
  "errorType": "Pattern",
  "message": "CollectionID does not match required pattern",
  "jsonPath": "$.pk.collectionId",
  "constraint": "^[A-Za-z0-9:_-]{2,}$",
  "actualValue": "invalid@id"
}
```

### 🧪 **Testing**

Comprehensive unit tests ensure validator reliability:

```bash
# Run all validation tests
dotnet test --filter "Category=Validation"

# Run specific test categories
dotnet test --filter "Category=Structural"
dotnet test --filter "Category=Reference"
dotnet test --filter "Category=Semantic"
```

### 🔧 **Integration**

The validator is designed to integrate seamlessly with existing AssetDumper workflows:

#### During Export
```csharp
// Add validation to export pipeline
var validator = new ComprehensiveSchemaValidator(options);
var report = await validator.ValidateAllAsync(results);

// Fail fast on critical errors
if (report.OverallResult == ValidationResult.Failed)
{
    throw new AssetDumperValidationException(report);
}
```

#### Post-Export Analysis
```bash
# Validate completed export
ValidationTool -o ./export -r validation.json

# Analyze results programmatically
jq '.overallResult' validation.json
jq '.errors | length' validation.json
jq '.errors[] | select(.severity == "Critical")' validation.json
```

### 🚀 **Advanced Features**

#### Custom Validation Rules
Extend the validator with domain-specific rules:

```csharp
// Add Unity-specific rules
validator.AddSemanticRule("GameObject", new ValidationRule
{
    Condition = asset => asset.ClassId == 1,
    Validation = asset => asset.Components.Any(c => c.ClassId == 4), // Must have Transform
    ErrorMessage = "GameObject must have Transform component"
});
```

#### Performance Optimization
- Batch processing for large datasets
- Streaming validation for memory efficiency
- Parallel validation of independent domains
- Incremental validation for CI/CD pipelines

### 📚 **API Reference**

#### ComprehensiveSchemaValidator
```csharp
// Main validation class
public class ComprehensiveSchemaValidator
{
    public ComprehensiveSchemaValidator(Options options);
    public Task<ValidationReport> ValidateAllAsync(IEnumerable<DomainExportResult> results);
}
```

#### ValidationReport
```csharp
// Complete validation results
public class ValidationReport
{
    public ValidationResult OverallResult { get; set; }
    public TimeSpan ValidationTime { get; set; }
    public List<ValidationError> Errors { get; set; }
    public List<ValidationWarning> Warnings { get; set; }
    public List<DomainValidationSummary> DomainSummaries { get; set; }
    // ... additional properties
}
```

#### ValidationError
```csharp
// Individual validation error
public class ValidationError
{
    public ValidationErrorType ErrorType { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string Domain { get; set; }
    public string JsonPath { get; set; }
    public string Message { get; set; }
    public object? ExpectedValue { get; set; }
    public object? ActualValue { get; set; }
    // ... additional properties
}
```

### 🤝 **Contributing**

To extend the validator:

1. **Add New Validation Types**: Extend `ValidationErrorType` enum
2. **Implement Validation Logic**: Add methods to `ComprehensiveSchemaValidator`
3. **Update Context**: Extend `ValidationContext` for new cross-references
4. **Add Tests**: Create comprehensive unit tests
5. **Update Documentation**: Keep this README current

### 📄 **License**

This validation framework is part of AssetRipper.Tools.AssetDumper and follows the same licensing terms.

---

**Note**: This validator is designed specifically for AssetDumper v2 schemas and may not work with other schema versions without modification.