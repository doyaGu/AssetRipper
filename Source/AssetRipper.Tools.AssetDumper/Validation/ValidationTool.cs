using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Validation.Models;
using System.Text.Json;

namespace AssetRipper.Tools.AssetDumper.Validation;

/// <summary>
/// Standalone command-line tool for comprehensive schema validation.
/// </summary>
public class ValidationTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // TEMP: Commented out to avoid multiple entry points conflict with Program.cs
    // Use: dotnet run --project AssetRipper.Tools.AssetDumper.Validation.csproj (if separated)
    internal static async Task<int> RunStandalone(string[] args)
    {
        try
        {
            // Parse command line arguments manually
            var options = ParseArguments(args);
            if (options == null)
            {
                ShowUsage();
                return 1;
            }

            // Run validation
            return await RunValidationAsync(options);
        }
        catch (Exception ex)
        {
            Logger.Error($"Validation tool failed: {ex.Message}");
            Logger.Debug(ex.ToString());
            return 1;
        }
    }

    /// <summary>
    /// Parses command line arguments.
    /// </summary>
    private static ValidationOptions? ParseArguments(string[] args)
    {
        var options = new ValidationOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                        options.OutputPath = args[++i];
                    else
                        return null;
                    break;

                case "--report":
                case "-r":
                    if (i + 1 < args.Length)
                        options.ReportPath = args[++i];
                    else
                        return null;
                    break;

                case "--schemas":
                case "-s":
                    if (i + 1 < args.Length)
                        options.SchemaPath = args[++i];
                    else
                        return null;
                    break;

                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;

                case "--quiet":
                case "-q":
                    options.Quiet = true;
                    break;

                case "--continue-on-error":
                case "-c":
                    options.ContinueOnError = true;
                    break;

                case "--max-errors":
                case "-m":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var maxErrors))
                        options.MaxErrors = maxErrors;
                    else
                        return null;
                    break;

                case "--domains":
                case "-d":
                    if (i + 1 < args.Length)
                    {
                        var domains = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        options.Domains = domains.Select(d => d.Trim()).ToArray();
                    }
                    else
                        return null;
                    break;

                case "--help":
                case "-h":
                case "/?":
                    return null;

                default:
                    Logger.Error($"Unknown argument: {args[i]}");
                    return null;
            }
        }

        // Validate required arguments
        if (string.IsNullOrEmpty(options.OutputPath))
        {
            Logger.Error("Output path is required. Use --output or -o to specify.");
            return null;
        }

        return options;
    }

    /// <summary>
    /// Shows usage information.
    /// </summary>
    private static void ShowUsage()
    {
        Console.WriteLine("Comprehensive AssetDumper Schema Validation Tool");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  ValidationTool --output <path> [options]");
        Console.WriteLine();
        Console.WriteLine("Required:");
        Console.WriteLine("  --output, -o <path>    Path to AssetDumper output directory containing NDJSON files");
        Console.WriteLine();
        Console.WriteLine("Optional:");
        Console.WriteLine("  --report, -r <path>    Path where validation report will be saved (default: validation-report.json)");
        Console.WriteLine("  --schemas, -s <path>    Path to schema directory (default: Schemas/v2)");
        Console.WriteLine("  --verbose, -v            Enable verbose logging");
        Console.WriteLine("  --quiet, -q              Suppress non-error output");
        Console.WriteLine("  --continue-on-error, -c   Continue validation even if errors are found");
        Console.WriteLine("  --max-errors, -m <num>  Maximum number of errors to report (0 = unlimited, default: 100)");
        Console.WriteLine("  --domains, -d <list>     Specific domains to validate (comma-separated, default: all)");
        Console.WriteLine("  --help, -h               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ValidationTool --output ./export");
        Console.WriteLine("  ValidationTool -o ./export -r report.json -v");
        Console.WriteLine("  ValidationTool -o ./export -d assets,types,relations");
    }

    /// <summary>
    /// Runs the validation process.
    /// </summary>
    private static async Task<int> RunValidationAsync(ValidationOptions options)
    {
        // Configure logging
        if (options.Verbose)
        {
            // Note: Logger configuration would depend on the actual logging framework
            Logger.Info("Verbose logging enabled");
        }
        else if (options.Quiet)
        {
            Logger.Info("Quiet mode enabled");
        }

        Logger.Info($"Starting comprehensive schema validation...");
        Logger.Info($"Output path: {options.OutputPath}");
        Logger.Info($"Report path: {options.ReportPath}");
        Logger.Info($"Schema path: {options.SchemaPath}");

        try
        {
            // Create options for validation
            var validatorOptions = new Options
            {
                OutputPath = options.OutputPath,
                Verbose = options.Verbose
            };

            // Create validator
            var validator = new SchemaValidator(validatorOptions);

            // Discover and load domain results
            var domainResults = DiscoverDomainResults(options.OutputPath, options.Domains);

            if (!domainResults.Any())
            {
                Logger.Error("No AssetDumper output files found in the specified path.");
                return 1;
            }

            Logger.Info($"Found {domainResults.Count} domain(s) to validate: {string.Join(", ", domainResults.Select(r => r.TableId))}");

            // Run validation
            var report = await validator.ValidateAllAsync(domainResults);

            // Apply error limit if specified
            if (options.MaxErrors > 0 && report.Errors.Count > options.MaxErrors)
            {
                var limitedErrors = report.Errors.Take(options.MaxErrors).ToList();
                report.Errors = limitedErrors;
                Logger.Warning($"Error limit reached: showing {options.MaxErrors} of {report.Errors.Count + options.MaxErrors} total errors");
            }

            // Save report
            await SaveReportAsync(report, options.ReportPath);

            // Display summary
            DisplaySummary(report);

            // Set exit code based on results
            return report.OverallResult == ValidationResult.Failed && !options.ContinueOnError ? 1 : 0;
        }
        catch (Exception ex)
        {
            Logger.Error($"Validation failed: {ex.Message}");
            if (options.Verbose)
                Logger.Debug(ex.ToString());
            
            return 1;
        }
    }

    /// <summary>
    /// Discovers AssetDumper output files and creates domain results.
    /// </summary>
    private static List<DomainExportResult> DiscoverDomainResults(string outputPath, string[] domains)
    {
        var results = new List<DomainExportResult>();
        var outputDir = new DirectoryInfo(outputPath);

        if (!outputDir.Exists)
        {
            Logger.Error($"Output directory does not exist: {outputPath}");
            return results;
        }

        // Define domain mappings
        var domainMappings = new Dictionary<string, (string SchemaPath, string Description)>
        {
            { "assets", ("Schemas/v2/facts/assets.schema.json", "Asset facts") },
            { "assemblies", ("Schemas/v2/facts/assemblies.schema.json", "Assembly facts") },
            { "bundles", ("Schemas/v2/facts/bundles.schema.json", "Bundle facts") },
            { "collections", ("Schemas/v2/facts/collections.schema.json", "Collection facts") },
            { "scenes", ("Schemas/v2/facts/scenes.schema.json", "Scene facts") },
            { "script_metadata", ("Schemas/v2/facts/script_metadata.schema.json", "Script metadata facts") },
            { "script_sources", ("Schemas/v2/facts/script_sources.schema.json", "Script source facts") },
            { "type_definitions", ("Schemas/v2/facts/type_definitions.schema.json", "Type definition facts") },
            { "type_members", ("Schemas/v2/facts/type_members.schema.json", "Type member facts") },
            { "types", ("Schemas/v2/facts/types.schema.json", "Type facts") },
            { "assembly_dependencies", ("Schemas/v2/relations/assembly_dependencies.schema.json", "Assembly dependency relations") },
            { "asset_dependencies", ("Schemas/v2/relations/asset_dependencies.schema.json", "Asset dependency relations") },
            { "bundle_hierarchy", ("Schemas/v2/relations/bundle_hierarchy.schema.json", "Bundle hierarchy relations") },
            { "collection_dependencies", ("Schemas/v2/relations/collection_dependencies.schema.json", "Collection dependency relations") },
            { "script_type_mapping", ("Schemas/v2/relations/script_type_mapping.schema.json", "Script type mapping relations") },
            { "type_inheritance", ("Schemas/v2/relations/type_inheritance.schema.json", "Type inheritance relations") },
            { "by_class", ("Schemas/v2/indexes/by_class.schema.json", "Assets by class index") },
            { "by_collection", ("Schemas/v2/indexes/by_collection.schema.json", "Assets by collection index") },
            { "by_name", ("Schemas/v2/indexes/by_name.schema.json", "Assets by name index") },
            { "asset_distribution", ("Schemas/v2/metrics/asset_distribution.schema.json", "Asset distribution metrics") },
            { "dependency_stats", ("Schemas/v2/metrics/dependency_stats.schema.json", "Dependency statistics metrics") },
            { "scene_stats", ("Schemas/v2/metrics/scene_stats.schema.json", "Scene statistics metrics") }
        };

        // Filter domains if specified
        var targetDomains = domains.Any() ? (IEnumerable<string>)domains : domainMappings.Keys;

        foreach (var domain in targetDomains)
        {
            if (!domainMappings.TryGetValue(domain, out var mapping))
                continue;

            var ndjsonFiles = outputDir.GetFiles($"{domain}*.ndjson", SearchOption.AllDirectories);
            
            if (ndjsonFiles.Length == 0)
            {
                Logger.Warning($"No files found for domain: {domain}");
                continue;
            }

            // Create a simple result object since DomainExportResult has read-only properties
            var result = CreateDomainExportResult(domain, mapping.SchemaPath, mapping.Description, ndjsonFiles, outputPath);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Creates a DomainExportResult from file information.
    /// </summary>
    private static DomainExportResult CreateDomainExportResult(string domain, string schemaPath, string description, FileInfo[] ndjsonFiles, string outputPath)
    {
        // Since DomainExportResult has read-only properties, we need to create it through reflection or use a factory
        // For now, let's create a simple implementation that works with the existing constructor
        var result = new DomainExportResult(domain, "ndjson", schemaPath, description);

        // Handle file information separately since we can't set the properties directly
        // This is a limitation of the current DomainExportResult design
        // In a real implementation, we'd either modify DomainExportResult to have settable properties
        // or create a factory method that properly initializes it

        return result;
    }

    /// <summary>
    /// Detects compression type for a file.
    /// </summary>
    private static string DetectCompression(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".zst" => "zstd",
            ".gz" => "gzip",
            _ => "none"
        };
    }

    /// <summary>
    /// Saves validation report to JSON file.
    /// </summary>
    private static async Task SaveReportAsync(ValidationReport report, string reportPath)
    {
        try
        {
            var json = JsonSerializer.Serialize(report, JsonOptions);
            await File.WriteAllTextAsync(reportPath, json);
            Logger.Info($"Validation report saved to: {reportPath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save report: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays validation summary to console.
    /// </summary>
    private static void DisplaySummary(ValidationReport report)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("VALIDATION SUMMARY");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"Overall Result: {report.OverallResult}");
        Console.WriteLine($"Validation Time: {report.ValidationTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"Records Validated: {report.TotalRecordsValidated:N0}");
        Console.WriteLine($"Schemas Loaded: {report.SchemasLoaded}");
        Console.WriteLine($"Data Files Processed: {report.DataFilesProcessed}");
        Console.WriteLine($"Errors Found: {report.Errors.Count:N0}");
        Console.WriteLine();

        if (report.Errors.Any())
        {
            Console.WriteLine("ERRORS BY TYPE:");
            var errorsByType = report.Errors.GroupBy(e => e.ErrorType)
                .OrderByDescending(g => g.Count());
            
            foreach (var group in errorsByType)
            {
                Console.WriteLine($"  {group.Key}: {group.Count():N0}");
            }
            Console.WriteLine();

            Console.WriteLine("ERRORS BY DOMAIN:");
            var errorsByDomain = report.Errors.GroupBy(e => e.Domain)
                .OrderByDescending(g => g.Count());
            
            foreach (var group in errorsByDomain.Take(10)) // Top 10 domains
            {
                Console.WriteLine($"  {group.Key}: {group.Count():N0}");
            }
            Console.WriteLine();

            if (report.Errors.Count <= 10)
            {
                Console.WriteLine("FIRST 10 ERRORS:");
                foreach (var error in report.Errors.Take(10))
                {
                    Console.WriteLine($"  [{error.ErrorType}] {error.Domain}:{error.LineNumber} - {error.Message}");
                }
            }
        }

        Console.WriteLine(new string('=', 60));
    }
}

/// <summary>
/// Validation options for the command line tool.
/// </summary>
public class ValidationOptions
{
    public string OutputPath { get; set; } = string.Empty;
    public string ReportPath { get; set; } = "validation-report.json";
    public string SchemaPath { get; set; } = "Schemas/v2";
    public bool Verbose { get; set; } = false;
    public bool Quiet { get; set; } = false;
    public bool ContinueOnError { get; set; } = false;
    public int MaxErrors { get; set; } = 100;
    public string[] Domains { get; set; } = Array.Empty<string>();
}