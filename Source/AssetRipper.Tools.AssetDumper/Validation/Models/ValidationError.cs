using System.Text.Json.Serialization;

namespace AssetRipper.Tools.AssetDumper.Validation.Models;

/// <summary>
/// Represents a validation error found during schema validation.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Unique identifier for this error.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of validation error.
    /// </summary>
    [JsonPropertyName("errorType")]
    public ValidationErrorType ErrorType { get; set; }

    /// <summary>
    /// Severity level of the error.
    /// </summary>
    [JsonPropertyName("severity")]
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

    /// <summary>
    /// Domain where the error occurred (e.g., "assets", "relations").
    /// </summary>
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Table identifier where the error occurred.
    /// </summary>
    [JsonPropertyName("tableId")]
    public string TableId { get; set; } = string.Empty;

    /// <summary>
    /// File path where the error occurred.
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number in the file where the error occurred.
    /// </summary>
    [JsonPropertyName("lineNumber")]
    public long LineNumber { get; set; }

    /// <summary>
    /// JSONPath to the exact location of the error within the JSON document.
    /// </summary>
    [JsonPropertyName("jsonPath")]
    public string JsonPath { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the validation rule that was violated.
    /// </summary>
    [JsonPropertyName("ruleDescription")]
    public string RuleDescription { get; set; } = string.Empty;

    /// <summary>
    /// Expected value according to the schema.
    /// </summary>
    [JsonPropertyName("expectedValue")]
    public object? ExpectedValue { get; set; }

    /// <summary>
    /// Actual value found in the data.
    /// </summary>
    [JsonPropertyName("actualValue")]
    public object? ActualValue { get; set; }

    /// <summary>
    /// Schema constraint that was violated.
    /// </summary>
    [JsonPropertyName("constraint")]
    public string Constraint { get; set; } = string.Empty;

    /// <summary>
    /// Additional context or suggestions for fixing the error.
    /// </summary>
    [JsonPropertyName("suggestion")]
    public string Suggestion { get; set; } = string.Empty;

    /// <summary>
    /// Category of the error for grouping and filtering.
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the error was detected.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional error-specific data.
    /// </summary>
    [JsonPropertyName("details")]
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Represents a validation warning found during schema validation.
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// Unique identifier for this warning.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of validation warning.
    /// </summary>
    [JsonPropertyName("warningType")]
    public ValidationWarningType WarningType { get; set; }

    /// <summary>
    /// Severity level of the warning.
    /// </summary>
    [JsonPropertyName("severity")]
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Warning;

    /// <summary>
    /// Domain where the warning occurred.
    /// </summary>
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Table identifier where the warning occurred.
    /// </summary>
    [JsonPropertyName("tableId")]
    public string TableId { get; set; } = string.Empty;

    /// <summary>
    /// File path where the warning occurred.
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number in the file where the warning occurred.
    /// </summary>
    [JsonPropertyName("lineNumber")]
    public long LineNumber { get; set; }

    /// <summary>
    /// JSONPath to the exact location of the warning within the JSON document.
    /// </summary>
    [JsonPropertyName("jsonPath")]
    public string JsonPath { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable warning message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the validation rule that triggered the warning.
    /// </summary>
    [JsonPropertyName("ruleDescription")]
    public string RuleDescription { get; set; } = string.Empty;

    /// <summary>
    /// Value that triggered the warning.
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    /// <summary>
    /// Additional context or suggestions.
    /// </summary>
    [JsonPropertyName("suggestion")]
    public string Suggestion { get; set; } = string.Empty;

    /// <summary>
    /// Category of the warning for grouping and filtering.
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the warning was detected.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional warning-specific data.
    /// </summary>
    [JsonPropertyName("details")]
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Types of validation errors.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationErrorType
{
    /// <summary>
    /// Basic JSON schema validation failed.
    /// </summary>
    Structural,

    /// <summary>
    /// Data type mismatch.
    /// </summary>
    DataType,

    /// <summary>
    /// Constraint violation (regex, range, etc.).
    /// </summary>
    Constraint,

    /// <summary>
    /// Conditional logic violation.
    /// </summary>
    Conditional,

    /// <summary>
    /// Cross-table reference error.
    /// </summary>
    CrossTable,

    /// <summary>
    /// Unity-specific semantic rule violation.
    /// </summary>
    Semantic,

    /// <summary>
    /// Missing required field.
    /// </summary>
    MissingRequired,

    /// <summary>
    /// Unexpected additional field.
    /// </summary>
    UnexpectedField,

    /// <summary>
    /// Invalid enum value.
    /// </summary>
    InvalidEnum,

    /// <summary>
    /// Format validation failed.
    /// </summary>
    Format,

    /// <summary>
    /// Pattern matching failed.
    /// </summary>
    Pattern,

    /// <summary>
    /// Range validation failed.
    /// </summary>
    Range,

    /// <summary>
    /// Length validation failed.
    /// </summary>
    Length,

    /// <summary>
    /// Unique constraint violation.
    /// </summary>
    Unique,

    /// <summary>
    /// Reference integrity violation.
    /// </summary>
    Reference
}

/// <summary>
/// Types of validation warnings.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationWarningType
{
    /// <summary>
    /// Deprecated field usage.
    /// </summary>
    Deprecated,

    /// <summary>
    /// Unusual but valid data pattern.
    /// </summary>
    UnusualPattern,

    /// <summary>
    /// Performance concern.
    /// </summary>
    Performance,

    /// <summary>
    /// Missing optional but recommended field.
    /// </summary>
    MissingOptional,

    /// <summary>
    /// Inconsistent naming convention.
    /// </summary>
    Naming,

    /// <summary>
    /// Potential data quality issue.
    /// </summary>
    DataQuality,

    /// <summary>
    /// Version compatibility warning.
    /// </summary>
    Version,

    /// <summary>
    /// Redundant data detected.
    /// </summary>
    Redundant,

    /// <summary>
    /// Incomplete data.
    /// </summary>
    Incomplete,

    /// <summary>
    /// Inconsistent data across records.
    /// </summary>
    Inconsistent
}

/// <summary>
/// Severity levels for validation issues.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message only.
    /// </summary>
    Info,

    /// <summary>
    /// Warning that should be reviewed.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that must be fixed.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that blocks processing.
    /// </summary>
    Critical
}