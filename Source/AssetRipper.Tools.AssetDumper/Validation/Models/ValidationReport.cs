using System.Text.Json.Serialization;

namespace AssetRipper.Tools.AssetDumper.Validation.Models;

/// <summary>
/// Comprehensive validation report containing all validation results and statistics.
/// </summary>
public class ValidationReport
{
    /// <summary>
    /// Overall validation result (PASS/FAIL).
    /// </summary>
    [JsonPropertyName("overallResult")]
    public ValidationResult OverallResult { get; set; }

    /// <summary>
    /// Total time taken for validation.
    /// </summary>
    [JsonPropertyName("validationTime")]
    public TimeSpan ValidationTime { get; set; }

    /// <summary>
    /// Timestamp when validation was performed.
    /// </summary>
    [JsonPropertyName("validationTimestamp")]
    public DateTime ValidationTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Summary statistics for each domain.
    /// </summary>
    [JsonPropertyName("domainSummaries")]
    public List<DomainValidationSummary> DomainSummaries { get; set; } = new();

    /// <summary>
    /// All validation errors found.
    /// </summary>
    [JsonPropertyName("errors")]
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// All validation warnings found.
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Total number of records validated across all domains.
    /// </summary>
    [JsonPropertyName("totalRecordsValidated")]
    public int TotalRecordsValidated { get; set; }

    /// <summary>
    /// Number of schemas loaded and used for validation.
    /// </summary>
    [JsonPropertyName("schemasLoaded")]
    public int SchemasLoaded { get; set; }

    /// <summary>
    /// Number of data files processed.
    /// </summary>
    [JsonPropertyName("dataFilesProcessed")]
    public int DataFilesProcessed { get; set; }

    /// <summary>
    /// Error message if validation failed catastrophically.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional metadata about the validation process.
    /// </summary>
    [JsonPropertyName("metadata")]
    public ValidationMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Validation result enumeration.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationResult
{
    /// <summary>
    /// Validation passed with no errors.
    /// </summary>
    Passed,

    /// <summary>
    /// Validation failed with errors.
    /// </summary>
    Failed,

    /// <summary>
    /// Validation completed with warnings but no errors.
    /// </summary>
    PassedWithWarnings,

    /// <summary>
    /// Validation could not be completed due to system errors.
    /// </summary>
    Incomplete
}

/// <summary>
/// Summary of validation results for a specific domain.
/// </summary>
public class DomainValidationSummary
{
    /// <summary>
    /// Domain identifier (e.g., "assets", "relations", "indexes").
    /// </summary>
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Table identifier within the domain.
    /// </summary>
    [JsonPropertyName("tableId")]
    public string TableId { get; set; } = string.Empty;

    /// <summary>
    /// Validation result for this domain.
    /// </summary>
    [JsonPropertyName("result")]
    public ValidationResult Result { get; set; }

    /// <summary>
    /// Number of records validated in this domain.
    /// </summary>
    [JsonPropertyName("recordsValidated")]
    public int RecordsValidated { get; set; }

    /// <summary>
    /// Number of errors found in this domain.
    /// </summary>
    [JsonPropertyName("errorCount")]
    public int ErrorCount { get; set; }

    /// <summary>
    /// Number of warnings found in this domain.
    /// </summary>
    [JsonPropertyName("warningCount")]
    public int WarningCount { get; set; }

    /// <summary>
    /// Schema used for validation.
    /// </summary>
    [JsonPropertyName("schemaPath")]
    public string SchemaPath { get; set; } = string.Empty;

    /// <summary>
    /// Files processed for this domain.
    /// </summary>
    [JsonPropertyName("filesProcessed")]
    public List<string> FilesProcessed { get; set; } = new();
}

/// <summary>
/// Metadata about the validation process.
/// </summary>
public class ValidationMetadata
{
    /// <summary>
    /// Version of the validation framework.
    /// </summary>
    [JsonPropertyName("validatorVersion")]
    public string ValidatorVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Schema version used for validation.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "v2";

    /// <summary>
    /// Types of validation performed.
    /// </summary>
    [JsonPropertyName("validationTypes")]
    public List<string> ValidationTypes { get; set; } = new()
    {
        "Structural",
        "DataType",
        "Constraint",
        "Conditional",
        "CrossTable",
        "Semantic"
    };

    /// <summary>
    /// Performance metrics.
    /// </summary>
    [JsonPropertyName("performance")]
    public ValidationPerformanceMetrics Performance { get; set; } = new();
}

/// <summary>
/// Performance metrics for the validation process.
/// </summary>
public class ValidationPerformanceMetrics
{
    /// <summary>
    /// Records validated per second.
    /// </summary>
    [JsonPropertyName("recordsPerSecond")]
    public double RecordsPerSecond { get; set; }

    /// <summary>
    /// Memory usage peak in MB.
    /// </summary>
    [JsonPropertyName("peakMemoryUsageMB")]
    public double PeakMemoryUsageMB { get; set; }

    /// <summary>
    /// Time spent on each validation phase.
    /// </summary>
    [JsonPropertyName("phaseBreakdown")]
    public Dictionary<string, TimeSpan> PhaseBreakdown { get; set; } = new();
}