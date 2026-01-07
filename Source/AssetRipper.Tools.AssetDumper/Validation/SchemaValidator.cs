using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Constants;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Models.Common;
using AssetRipper.Tools.AssetDumper.Validation.Models;
using AssetRipper.Tools.AssetDumper.Writers;
using Json.Schema;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ZstdSharp;

namespace AssetRipper.Tools.AssetDumper.Validation;

/// <summary>
/// Comprehensive schema validation tool for AssetDumper that performs deep schema compliance evaluation.
/// Goes beyond basic schema validation to include structural, semantic, and cross-table validation.
/// </summary>
public sealed class SchemaValidator
{
    private static readonly ConcurrentDictionary<string, JsonSchema> SchemaFileCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly Options _options;
    private readonly ValidationContext _validationContext;
    private readonly EvaluationOptions _evaluationOptions;
    private readonly ConcurrentBag<Validation.Models.ValidationError> _validationErrors;
    private readonly Dictionary<string, JsonSchema> _loadedSchemas;
    private readonly Dictionary<string, List<JsonNode>> _loadedData;

    // Unity-specific validation rules
    private static readonly Dictionary<int, string> UnityClassIdNames = new()
    {
        { 1, "GameObject" },
        { 4, "Transform" },
        { 20, "Texture2D" },
        { 21, "Texture2DArray" },
        { 22, "Texture2DArray" },
        { 23, "TextureCube" },
        { 28, "Texture2D" },
        { 43, "Mesh" },
        { 48, "Shader" },
        { 89, "TextAsset" },
        { 90, "Rigidbody" },
        { 92, "Collider" },
        { 95, "AnimationClip" },
        { 114, "MonoBehaviour" },
        { 115, "MonoScript" },
        { 128, "Font" },
        { 129, "Material" },
        { 130, "Renderer" },
        { 136, "Camera" },
        { 157, "AudioSource" },
        { 195, "Animator" },
        { 196, "AnimatorController" },
        { 197, "AnimatorOverrideController" },
        { 205, "AudioClip" },
        { 213, "SpriteRenderer" },
        { 274, "SpriteAtlas" }
    };

    public SchemaValidator(Options options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _validationContext = new ValidationContext();
        _validationErrors = new ConcurrentBag<Validation.Models.ValidationError>();
        _loadedSchemas = new Dictionary<string, JsonSchema>();
        _loadedData = new Dictionary<string, List<JsonNode>>();
        
        _evaluationOptions = new EvaluationOptions
        {
            OutputFormat = OutputFormat.Flag,
            RequireFormatValidation = false
        };

        // JsonSchema.Net resolves $ref via the registry. Our v2 schemas often carry hosted $id values
        // (https://schemas.assetripper.dev/assetdump/v2/...), but in tests and local runs we validate
        // against the on-disk schema files. Provide a deterministic fetch that maps hosted URIs to
        // local paths so evaluation doesn't throw on unresolved refs.
        SchemaRegistry.Global.Fetch = FetchSchemaDocument;
    }

    private Json.Schema.IBaseDocument? FetchSchemaDocument(Uri uri, SchemaRegistry _)
    {
        // The registry may ask for a URI with fragment; retrieval should be by the document URI.
        Uri documentUri = uri.IsAbsoluteUri ? new Uri(uri.GetLeftPart(UriPartial.Path)) : uri;

        if (documentUri.IsFile)
        {
            var filePath = documentUri.LocalPath;
            if (File.Exists(filePath))
            {
                return JsonSchema.FromFile(filePath);
            }
        }

        if (string.Equals(documentUri.Host, "schemas.assetripper.dev", StringComparison.OrdinalIgnoreCase)
            && documentUri.AbsolutePath.StartsWith("/assetdump/v2/", StringComparison.OrdinalIgnoreCase))
        {
            string relative = documentUri.AbsolutePath["/assetdump/v2/".Length..].TrimStart('/');
            string schemaPath = $"Schemas/v2/{relative}";

            var fullPath = ResolveSchemaPath(schemaPath);
            if (fullPath != null && File.Exists(fullPath))
            {
                return JsonSchema.FromFile(fullPath);
            }
        }

        return null;
    }

    /// <summary>
    /// Validates all AssetDumper output files against their schemas with comprehensive analysis.
    /// </summary>
    /// <param name="domainResults">List of domain export results to validate</param>
    /// <returns>Comprehensive validation report</returns>
    public async Task<ValidationReport> ValidateAllAsync(IEnumerable<DomainExportResult> domainResults)
    {
        if (domainResults is null)
            throw new ArgumentNullException(nameof(domainResults));

        var stopwatch = Stopwatch.StartNew();
        var resultsList = domainResults.ToList();

        try
        {
            // Phase 1: Load all schemas and build validation context
            await LoadAllSchemasAsync();
            await LoadAllDataAsync(resultsList);
            BuildValidationContext();

            // Phase 2: Perform comprehensive validation
            await PerformStructuralValidationAsync(resultsList);
            await PerformDataTypeValidationAsync(resultsList);
            await PerformConstraintValidationAsync(resultsList);
            await PerformConditionalValidationAsync(resultsList);
            await PerformCrossTableValidationAsync();
            await PerformSemanticValidationAsync(resultsList);

            stopwatch.Stop();

            // Generate comprehensive report
            return GenerateValidationReport(stopwatch.Elapsed, resultsList);
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory.Export, $"Comprehensive validation failed: {ex.Message}");
            return new ValidationReport
            {
                OverallResult = ValidationResult.Failed,
                ValidationTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                Errors = _validationErrors.ToList()
            };
        }
    }

    /// <summary>
    /// Loads all v2 schemas and builds a comprehensive validation context.
    /// </summary>
    private async Task LoadAllSchemasAsync()
    {
        var schemaPaths = new[]
        {
            "Schemas/v2/core.schema.json",
            "Schemas/v2/facts/assets.schema.json",
            "Schemas/v2/facts/assemblies.schema.json",
            "Schemas/v2/facts/bundles.schema.json",
            "Schemas/v2/facts/collections.schema.json",
            "Schemas/v2/facts/scenes.schema.json",
            "Schemas/v2/facts/script_metadata.schema.json",
            "Schemas/v2/facts/script_sources.schema.json",
            "Schemas/v2/facts/type_definitions.schema.json",
            "Schemas/v2/facts/type_members.schema.json",
            "Schemas/v2/facts/types.schema.json",
            "Schemas/v2/relations/assembly_dependencies.schema.json",
            "Schemas/v2/relations/asset_dependencies.schema.json",
            "Schemas/v2/relations/bundle_hierarchy.schema.json",
            "Schemas/v2/relations/collection_dependencies.schema.json",
            "Schemas/v2/relations/script_type_mapping.schema.json",
            "Schemas/v2/relations/type_inheritance.schema.json",
            "Schemas/v2/indexes/by_class.schema.json",
            "Schemas/v2/indexes/by_collection.schema.json",
            "Schemas/v2/indexes/by_name.schema.json",
            "Schemas/v2/metrics/asset_distribution.schema.json",
            "Schemas/v2/metrics/dependency_stats.schema.json",
            "Schemas/v2/metrics/scene_stats.schema.json"
        };

        foreach (var schemaPath in schemaPaths)
        {
            try
            {
                var fullPath = ResolveSchemaPath(schemaPath);
                if (fullPath != null && File.Exists(fullPath))
                {
                    // JsonSchema.Net builds schemas into the global registry and registers anchors.
                    // Loading the same schema multiple times in one process can throw (duplicate anchors / overwriting).
                    // Cache by absolute file path to make schema loading idempotent across many validator instances.
                    var schema = SchemaFileCache.GetOrAdd(fullPath, static path => JsonSchema.FromFile(path));
                    _loadedSchemas[schemaPath] = schema;

                    // Ensure $ref resolution works when schemas reference the canonical hosted IDs
                    // (e.g., https://schemas.assetripper.dev/assetdump/v2/core.schema.json#AssetPK).
                    // JsonSchema.Net's SchemaRegistry can register by explicit base URI.
                    // Our schemas use hosted $id values, so register them under that hosted namespace.
                    if (schemaPath.StartsWith("Schemas/v2/", StringComparison.Ordinal))
                    {
                        string hostedRelative = schemaPath["Schemas/v2/".Length..];
                        var hostedUri = new Uri($"https://schemas.assetripper.dev/assetdump/v2/{hostedRelative}");
                        try
                        {
                            SchemaRegistry.Global.Register(hostedUri, schema);
                        }
                        catch
                        {
                            // Ignore duplicate registration across test runs / multiple validators.
                        }
                    }

                    // Also register by file URI to support any file:// refs.
                    var fileUri = new Uri(fullPath);
                    try
                    {
                        SchemaRegistry.Global.Register(fileUri, schema);
                    }
                    catch
                    {
                        // Ignore duplicate registration across test runs / multiple validators.
                    }

                    Logger.Info($"Loaded schema: {schemaPath}");
                }
                else
                {
                    Logger.Warning($"Schema file not found: {schemaPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load schema {schemaPath}: {ex.Message}");

                AddValidationError(
                    tableId: "schemas",
                    filePath: schemaPath,
                    lineNumber: 0,
                    errorType: ValidationErrorType.Structural,
                    message: $"Failed to load schema '{schemaPath}'.",
#if DEBUG
                    details: ex.ToString());
#else
                    details: $"{ex.GetType().Name}: {ex.Message}");
#endif
            }
        }

        // If schema discovery fails entirely, surface it as a validation error.
        // Silent schema load failures would make the validator effectively skip structural checks.
        if (_loadedSchemas.Count == 0)
        {
            string probe = "Schemas" + Path.DirectorySeparatorChar + "v2" + Path.DirectorySeparatorChar + "core.schema.json";
            string appCandidate = Path.Combine(AppContext.BaseDirectory, probe);
            string cwdCandidate = Path.Combine(Environment.CurrentDirectory, probe);

            AddValidationError(
                tableId: "schemas",
                filePath: "",
                lineNumber: 0,
                errorType: ValidationErrorType.Structural,
                message: "No schemas were loaded; schema path resolution failed.",
                details: $"AppContext.BaseDirectory='{AppContext.BaseDirectory}'; Environment.CurrentDirectory='{Environment.CurrentDirectory}'. core.schema.json candidates: app='{appCandidate}' (exists={File.Exists(appCandidate)}), cwd='{cwdCandidate}' (exists={File.Exists(cwdCandidate)}).");
        }
    }

    /// <summary>
    /// Loads all NDJSON data files for validation.
    /// </summary>
    private async Task LoadAllDataAsync(List<DomainExportResult> domainResults)
    {
        foreach (var result in domainResults)
        {
            if (string.Equals(result.Format, "ndjson", StringComparison.OrdinalIgnoreCase))
            {
                var dataList = new List<JsonNode>();

                if (result.HasShards)
                {
                    foreach (var shard in result.Shards)
                    {
                        await LoadDataFromFileAsync(shard.Shard, shard.Compression, dataList);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(result.EntryFile))
                {
                    await LoadDataFromFileAsync(result.EntryFile, "none", dataList);
                }

                _loadedData[result.Domain] = dataList;
                Logger.Info($"Loaded {dataList.Count} records for {result.Domain}");
            }
        }
    }

    /// <summary>
    /// Loads data from a single NDJSON file.
    /// </summary>
    private async Task LoadDataFromFileAsync(string filePath, string? compression, List<JsonNode> dataList)
    {
        var absolutePath = OutputPathHelper.ResolveAbsolutePath(_options.OutputPath, filePath);
        
        if (!File.Exists(absolutePath))
        {
            Logger.Warning($"Data file not found: {absolutePath}");
            return;
        }

        try
        {
            if (!TryOpenReader(absolutePath, compression, out StreamReader? reader))
                return;

            using (reader)
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var node = JsonNode.Parse(line);
                        if (node != null)
                            dataList.Add(node);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to parse JSON line in {filePath}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load data from {filePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the validation context with cross-references and semantic rules.
    /// </summary>
    private void BuildValidationContext()
    {
        // Build asset reference maps
        BuildAssetReferenceMaps();
        
        // Build type reference maps
        BuildTypeReferenceMaps();
        
        // Build dependency maps
        BuildDependencyMaps();
        
        // Build semantic validation rules
        BuildSemanticRules();
    }

    /// <summary>
    /// Performs structural compliance validation.
    /// </summary>
    private async Task PerformStructuralValidationAsync(List<DomainExportResult> domainResults)
    {
        foreach (var result in domainResults)
        {
            if (!_loadedSchemas.TryGetValue(result.SchemaPath, out var schema))
                continue;

            if (!_loadedData.TryGetValue(result.Domain, out var data))
                continue;

            for (int i = 0; i < data.Count; i++)
            {
                var node = data[i];
                var lineNumber = i + 1;

                // Basic schema validation
                EvaluationResults evaluation;
                try
                    {
                        // JsonSchema.Net evaluation is most reliable with JsonElement input.
                        // Some JsonNode-based evaluations can throw conversion exceptions.
                        JsonElement element = JsonSerializer.SerializeToElement(node);
                        evaluation = schema.Evaluate(element, _evaluationOptions);
                }
                catch (Exception ex)
                {
                    AddValidationError(result.Domain, result.EntryFile ?? string.Empty, lineNumber,
                        ValidationErrorType.Structural,
                        "Schema evaluation threw an exception",
                        ex.ToString());
                    continue;
                }

                if (!evaluation.IsValid)
                {
                    AddValidationError(result.Domain, result.EntryFile ?? string.Empty, lineNumber,
                        ValidationErrorType.Structural,
                        "Schema validation failed",
                        GetSchemaErrorDetails(evaluation));
                }

                // Additional structural checks
                await ValidateStructureAsync(result.Domain, node, lineNumber);
            }
        }
    }

    /// <summary>
    /// Performs data type adherence validation.
    /// </summary>
    private async Task PerformDataTypeValidationAsync(List<DomainExportResult> domainResults)
    {
        foreach (var result in domainResults)
        {
            if (!_loadedData.TryGetValue(result.Domain, out var data))
                continue;

            for (int i = 0; i < data.Count; i++)
            {
                var node = data[i];
                var lineNumber = i + 1;

                await ValidateDataTypesAsync(result.Domain, node, lineNumber);
            }
        }
    }

    /// <summary>
    /// Performs constraint violation detection.
    /// </summary>
    private async Task PerformConstraintValidationAsync(List<DomainExportResult> domainResults)
    {
        foreach (var result in domainResults)
        {
            if (!_loadedSchemas.TryGetValue(result.SchemaPath, out var schema))
                continue;

            if (!_loadedData.TryGetValue(result.Domain, out var data))
                continue;

            for (int i = 0; i < data.Count; i++)
            {
                var node = data[i];
                var lineNumber = i + 1;

                await ValidateConstraintsAsync(result.Domain, node, schema, lineNumber);
            }
        }
    }

    /// <summary>
    /// Performs conditional logic evaluation.
    /// </summary>
    private async Task PerformConditionalValidationAsync(List<DomainExportResult> domainResults)
    {
        foreach (var result in domainResults)
        {
            if (!_loadedData.TryGetValue(result.Domain, out var data))
                continue;

            for (int i = 0; i < data.Count; i++)
            {
                var node = data[i];
                var lineNumber = i + 1;

                await ValidateConditionalLogicAsync(result.Domain, node, lineNumber);
            }
        }
    }

    /// <summary>
    /// Performs cross-table reference validation.
    /// </summary>
    private async Task PerformCrossTableValidationAsync()
    {
        // Validate asset references across tables
        await ValidateAssetReferencesAsync();
        
        // Validate type references
        await ValidateTypeReferencesAsync();
        
        // Validate dependency consistency
        await ValidateDependencyConsistencyAsync();
        
        // Validate index consistency
        await ValidateIndexConsistencyAsync();
    }

    /// <summary>
    /// Performs semantic validation for Unity-specific constraints.
    /// </summary>
    private async Task PerformSemanticValidationAsync(List<DomainExportResult> domainResults)
    {
        foreach (var result in domainResults)
        {
            if (!_loadedData.TryGetValue(result.Domain, out var data))
                continue;

            for (int i = 0; i < data.Count; i++)
            {
                var node = data[i];
                var lineNumber = i + 1;

                await ValidateUnitySpecificRulesAsync(result.Domain, node, lineNumber);
            }
        }
    }

    #region Helper Methods

    /// <summary>
    /// Resolves schema path to absolute file path.
    /// </summary>
    private string? ResolveSchemaPath(string schemaRelativePath)
    {
        if (string.IsNullOrWhiteSpace(schemaRelativePath))
            return null;

        string normalized = schemaRelativePath.Replace('/', Path.DirectorySeparatorChar);

        foreach (string baseDirectory in EnumerateCandidateBaseDirectories())
        {
            // Direct relative (baseDirectory is already the tool root)
            string candidate = Path.Combine(baseDirectory, normalized);
            if (File.Exists(candidate))
                return candidate;

            // Common repo layout: ...\Source\AssetRipper.Tools.AssetDumper\Schemas\...
            candidate = Path.Combine(baseDirectory, "AssetRipper.Tools.AssetDumper", normalized);
            if (File.Exists(candidate))
                return candidate;

            // Common monorepo layout: ...\AssetRipper\Source\AssetRipper.Tools.AssetDumper\Schemas\...
            candidate = Path.Combine(baseDirectory, "AssetRipper", "Source", "AssetRipper.Tools.AssetDumper", normalized);
            if (File.Exists(candidate))
                return candidate;

            // Alternate layout: ...\Source\AssetRipper.Tools.AssetDumper\Schemas\...
            candidate = Path.Combine(baseDirectory, "Source", "AssetRipper.Tools.AssetDumper", normalized);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Enumerates candidate base directories for schema files.
    /// </summary>
    private static IEnumerable<string> EnumerateCandidateBaseDirectories()
    {
        HashSet<string> yielded = new(StringComparer.OrdinalIgnoreCase);

        foreach (string directory in AscendDirectories(AppContext.BaseDirectory, 8))
        {
            if (yielded.Add(directory))
                yield return directory;
        }

        foreach (string directory in AscendDirectories(Environment.CurrentDirectory, 8))
        {
            if (yielded.Add(directory))
                yield return directory;
        }
    }

    /// <summary>
    /// Ascends through directory hierarchy.
    /// </summary>
    private static IEnumerable<string> AscendDirectories(string? startDirectory, int limit)
    {
        string? current = startDirectory;
        for (int i = 0; i <= limit && !string.IsNullOrEmpty(current); i++)
        {
            yield return current;
            current = Path.GetDirectoryName(current);
        }
    }

    /// <summary>
    /// Opens a reader for the specified file with compression handling.
    /// </summary>
    private static bool TryOpenReader(string filePath, string? compression, [NotNullWhen(true)] out StreamReader? reader)
    {
        reader = null;
        Stream baseStream;

        try
        {
            baseStream = File.OpenRead(filePath);
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory.Export, $"Failed to open file for validation: {ex.Message}");
            return false;
        }

        string normalizedCompression = string.IsNullOrWhiteSpace(compression)
            ? "none"
            : compression.Trim().ToLowerInvariant();

        try
        {
            Stream effectiveStream = normalizedCompression switch
            {
                "none" => baseStream,
                "zstd" or "zstd-seekable" => new DecompressionStream(baseStream),
                _ => throw new NotSupportedException($"Unsupported compression '{compression}'.")
            };

            reader = new StreamReader(effectiveStream);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory.Export, $"Failed to initialize validation stream: {ex.Message}");
            baseStream.Dispose();
            return false;
        }
    }

    /// <summary>
    /// Adds a validation error to the error collection.
    /// </summary>
    private void AddValidationError(string tableId, string filePath, long lineNumber,
        ValidationErrorType errorType, string message, string? details = null)
    {
        var error = new Validation.Models.ValidationError
        {
            TableId = tableId,
            FilePath = filePath,
            LineNumber = lineNumber,
            ErrorType = errorType,
            Message = message,
            RuleDescription = details ?? message
        };

        _validationErrors.Add(error);
    }

    /// <summary>
    /// Extracts detailed error information from schema evaluation results.
    /// </summary>
    private string GetSchemaErrorDetails(EvaluationResults evaluation)
    {
        var details = new List<string>();
        
        if (evaluation.Errors?.Any() == true)
        {
            details.AddRange(evaluation.Errors.Select(e => e.ToString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        if (evaluation.Details?.Any() == true)
        {
            details.AddRange(evaluation.Details.Select(d => d.ToString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        return string.Join("; ", details);
    }

    #endregion

    #region Validation Context Builders

    /// <summary>
    /// Builds asset reference maps from loaded data.
    /// </summary>
    private void BuildAssetReferenceMaps()
    {
        if (_loadedData.TryGetValue("assets", out var assetData))
        {
            foreach (var node in assetData)
            {
                var pk = node["pk"]?.AsObject();
                var collectionId = pk?["collectionId"]?.GetValue<string>();
                var pathId = pk?["pathId"]?.GetValue<long>();
                var classKey = node["classKey"]?.GetValue<int>();
                var className = node["className"]?.GetValue<string>();
                var name = node["name"]?.GetValue<string>();

                if (collectionId != null && pathId.HasValue)
                {
                    var assetPk = $"{collectionId}:{pathId.Value}";
                    var assetRef = new AssetReference
                    {
                        AssetPk = assetPk,
                        CollectionId = collectionId,
                        PathId = pathId.Value,
                        TableId = "assets",
                        ClassKey = classKey ?? 0,
                        ClassName = className ?? string.Empty,
                        Name = name
                    };

                    if (!_validationContext.AssetReferences.ContainsKey(assetPk))
                        _validationContext.AssetReferences[assetPk] = new List<AssetReference>();
                    
                    _validationContext.AssetReferences[assetPk].Add(assetRef);
                }
            }
        }
    }

    /// <summary>
    /// Builds type reference maps from loaded data.
    /// </summary>
    private void BuildTypeReferenceMaps()
    {
        if (_loadedData.TryGetValue("types", out var typeData))
        {
            foreach (var node in typeData)
            {
                var classKey = node["classKey"]?.GetValue<int>();
                var classId = node["classId"]?.GetValue<int>();
                var className = node["className"]?.GetValue<string>();

                if (classKey.HasValue && classId.HasValue && className != null)
                {
                    var typeRef = new TypeReference
                    {
                        ClassKey = classKey.Value,
                        ClassId = classId.Value,
                        ClassName = className,
                        TypeId = node["typeId"]?.GetValue<int>(),
                        ScriptTypeIndex = node["scriptTypeIndex"]?.GetValue<int>(),
                        IsStripped = node["isStripped"]?.GetValue<bool>() ?? false,
                        BaseClassName = node["baseClassName"]?.GetValue<string>(),
                        IsAbstract = node["isAbstract"]?.GetValue<bool>() ?? false
                    };

                    _validationContext.TypeReferences[classKey.Value] = typeRef;
                }
            }
        }
    }

    /// <summary>
    /// Builds dependency maps from loaded data.
    /// </summary>
    private void BuildDependencyMaps()
    {
        if (_loadedData.TryGetValue("asset_dependencies", out var depData))
        {
            foreach (var node in depData)
            {
                var from = node["from"]?.AsObject();
                var to = node["to"]?.AsObject();
                var edge = node["edge"]?.AsObject();

                if (from != null && to != null && edge != null)
                {
                    var fromPk = $"{from["collectionId"]?.GetValue<string>()}:{from["pathId"]?.GetValue<long>()}";
                    var toPk = $"{to["collectionId"]?.GetValue<string>()}:{to["pathId"]?.GetValue<long>()}";
                    var kind = edge["kind"]?.GetValue<string>();
                    var field = edge["field"]?.GetValue<string>();

                    var depRef = new DependencyReference
                    {
                        FromAssetPk = fromPk,
                        ToAssetPk = toPk,
                        Kind = kind ?? string.Empty,
                        Field = field ?? string.Empty,
                        FieldType = edge["fieldType"]?.GetValue<string>(),
                        FileId = edge["fileId"]?.GetValue<int>(),
                        ArrayIndex = edge["arrayIndex"]?.GetValue<int>(),
                        IsNullable = edge["isNullable"]?.GetValue<bool>() ?? false,
                        Status = node["status"]?.GetValue<string>(),
                        TargetType = node["targetType"]?.GetValue<string>()
                    };

                    if (!_validationContext.AssetDependencies.ContainsKey(fromPk))
                        _validationContext.AssetDependencies[fromPk] = new List<DependencyReference>();
                    
                    _validationContext.AssetDependencies[fromPk].Add(depRef);
                }
            }
        }
    }

    /// <summary>
    /// Builds semantic validation rules.
    /// </summary>
    private void BuildSemanticRules()
    {
        // Unity-specific rules for common classes
        _validationContext.UnityRules.RequiredFields[1] = new List<string> { "m_Name" }; // GameObject
        _validationContext.UnityRules.RequiredFields[4] = new List<string> { "m_LocalPosition", "m_LocalRotation", "m_LocalScale" }; // Transform
        _validationContext.UnityRules.RequiredFields[114] = new List<string> { "m_Script" }; // MonoBehaviour

        // Reference rules for common Unity types
        _validationContext.UnityRules.ReferenceRules["GameObject"] = new List<ReferenceRule>
        {
            new ReferenceRule { SourceField = "m_Transform", AllowedClassIds = new List<int> { 4 }, Required = true },
            new ReferenceRule { SourceField = "m_Component", AllowedClassIds = new List<int> { 114 }, Required = false, Nullable = true }
        };
    }

    #endregion

    #region Validation Implementations

    /// <summary>
    /// Validates structure beyond basic schema validation.
    /// </summary>
    private async Task ValidateStructureAsync(string tableId, JsonNode node, long lineNumber)
    {
        // Check for unexpected fields
        var obj = node.AsObject();
        if (obj != null)
        {
            foreach (var property in obj)
            {
                if (IsUnexpectedField(tableId, property.Key))
                {
                    AddValidationError(tableId, "", lineNumber, ValidationErrorType.UnexpectedField,
                        $"Unexpected field '{property.Key}' found in {tableId} table");
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates data types with enhanced checking.
    /// </summary>
    private async Task ValidateDataTypesAsync(string tableId, JsonNode node, long lineNumber)
    {
        var obj = node.AsObject();
        if (obj == null) return;

        foreach (var property in obj)
        {
            await ValidateFieldDataType(tableId, property.Key, property.Value, lineNumber);
        }
    }

    /// <summary>
    /// Validates constraints like patterns, ranges, and lengths.
    /// </summary>
    private async Task ValidateConstraintsAsync(string tableId, JsonNode node, JsonSchema schema, long lineNumber)
    {
        var obj = node.AsObject();
        if (obj == null) return;

        // Validate CollectionID pattern
        string? collectionId = null;
        if (string.Equals(tableId, "assets", StringComparison.OrdinalIgnoreCase))
        {
            collectionId = obj["pk"]?["collectionId"]?.GetValue<string>();
        }
        else if (obj.ContainsKey("collectionId"))
        {
            collectionId = obj["collectionId"]?.GetValue<string>();
        }

            if (collectionId != null && !Regex.IsMatch(collectionId, @"^[A-Za-z0-9:._-]{2,}$"))
        {
            AddValidationError(tableId, "", lineNumber, ValidationErrorType.Pattern,
                $"CollectionID '{collectionId}' does not match required pattern");
        }

        // Validate Unity GUID pattern
        if (obj.ContainsKey("sceneGuid"))
        {
            var sceneGuid = obj["sceneGuid"]?.GetValue<string>();
            if (sceneGuid != null && !Regex.IsMatch(sceneGuid, @"^([0-9A-Fa-f]{32}|[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})$"))
            {
                AddValidationError(tableId, "", lineNumber, ValidationErrorType.Pattern,
                    $"Scene GUID '{sceneGuid}' does not match required pattern");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates conditional logic and field dependencies.
    /// </summary>
    private async Task ValidateConditionalLogicAsync(string tableId, JsonNode node, long lineNumber)
    {
        var obj = node.AsObject();
        if (obj == null) return;

        // Validate MonoBehaviour conditional requirements
        if (tableId == "assets")
        {
            var classId = obj["unity"]?["classId"]?.GetValue<int>();
            if (classId == 114) // MonoBehaviour
            {
                var scriptTypeIndex = obj["unity"]?["scriptTypeIndex"]?.GetValue<int>();
                if (!scriptTypeIndex.HasValue || scriptTypeIndex.Value < 0)
                {
                    AddValidationError(tableId, "", lineNumber, ValidationErrorType.Conditional,
                        "MonoBehaviour assets must have a valid scriptTypeIndex >= 0");
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates asset references across tables.
    /// </summary>
    private async Task ValidateAssetReferencesAsync()
    {
        foreach (var dependency in _validationContext.AssetDependencies.SelectMany(kvp => kvp.Value))
        {
            if (!_validationContext.AssetReferences.ContainsKey(dependency.ToAssetPk))
            {
                AddValidationError("asset_dependencies", "", 0, ValidationErrorType.Reference,
                    $"Reference to non-existent asset: {dependency.ToAssetPk}");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates type references.
    /// </summary>
    private async Task ValidateTypeReferencesAsync()
    {
        if (_loadedData.TryGetValue("assets", out var assetData))
        {
            foreach (var node in assetData)
            {
                var classKey = node["classKey"]?.GetValue<int>();
                if (classKey.HasValue && !_validationContext.TypeReferences.ContainsKey(classKey.Value))
                {
                    AddValidationError("assets", "", 0, ValidationErrorType.Reference,
                        $"Reference to non-existent type with classKey: {classKey.Value}");
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates dependency consistency.
    /// </summary>
    private async Task ValidateDependencyConsistencyAsync()
    {
        // Check for circular dependencies
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var assetPk in _validationContext.AssetDependencies.Keys)
        {
            if (HasCircularDependency(assetPk, visited, recursionStack))
            {
                AddValidationError("asset_dependencies", "", 0, ValidationErrorType.Reference,
                    $"Circular dependency detected involving asset: {assetPk}");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates index consistency.
    /// </summary>
    private async Task ValidateIndexConsistencyAsync()
    {
        // Validate by_class index consistency
        if (_loadedData.TryGetValue("by_class", out var indexData))
        {
            foreach (var indexNode in indexData)
            {
                var classKey = indexNode["classKey"]?.GetValue<int>();
                var assetsArray = indexNode["assets"]?.AsArray();
                var count = indexNode["count"]?.GetValue<int>();

                if (classKey.HasValue && assetsArray != null && count.HasValue)
                {
                    if (assetsArray.Count != count.Value)
                    {
                        AddValidationError("by_class", "", 0, ValidationErrorType.Structural,
                            $"Index count mismatch for classKey {classKey.Value}: expected {count.Value}, found {assetsArray.Count}");
                    }

                    // Verify all referenced assets exist
                    foreach (var assetRef in assetsArray)
                    {
                        if (assetRef is not JsonObject assetRefObject)
                            continue;

                        var collectionId = assetRefObject["collectionId"]?.GetValue<string>();
                        var pathId = assetRefObject["pathId"]?.GetValue<long>();
                        if (collectionId != null && pathId.HasValue)
                        {
                            var assetPk = $"{collectionId}:{pathId.Value}";
                            if (!_validationContext.AssetReferences.ContainsKey(assetPk))
                            {
                                AddValidationError("by_class", "", 0, ValidationErrorType.Reference,
                                    $"Index references non-existent asset: {assetPk}");
                            }
                        }
                    }
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates Unity-specific rules and constraints.
    /// </summary>
    private async Task ValidateUnitySpecificRulesAsync(string tableId, JsonNode node, long lineNumber)
    {
        var obj = node.AsObject();
        if (obj == null) return;

        if (tableId == "assets")
        {
            await ValidateAssetUnityRules(obj, lineNumber);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates Unity-specific rules for assets.
    /// </summary>
    private async Task ValidateAssetUnityRules(JsonObject obj, long lineNumber)
    {
        var unityObj = obj["unity"]?.AsObject();
        if (unityObj == null) return;

        var classId = unityObj["classId"]?.GetValue<int>();
        if (!classId.HasValue) return;

        // Detect legacy container structure inside data (byteStart/byteSize/content wrapper).
        var dataObj = obj["data"]?.AsObject();
        if (dataObj != null && dataObj.ContainsKey("content") && (dataObj.ContainsKey("byteStart") || dataObj.ContainsKey("byteSize")))
        {
            AddValidationError("assets", "", lineNumber, ValidationErrorType.Structural,
                "Asset 'data' uses legacy container structure (byteStart/byteSize/content); expected raw payload in 'data' with byteStart/byteSize at root");
        }

        // Check required fields for known Unity classes in the serialized payload.
        // Note: these rules apply only when 'data' is an object payload.
        if (dataObj != null && _validationContext.UnityRules.RequiredFields.TryGetValue(classId.Value, out var requiredFields))
        {
            foreach (var requiredField in requiredFields)
            {
                if (!dataObj.ContainsKey(requiredField))
                {
                    AddValidationError("assets", "", lineNumber, ValidationErrorType.MissingRequired,
                        $"Unity class {classId.Value} ({GetUnityClassName(classId.Value)}) is missing required payload field: {requiredField}");
                }
            }
        }

        // Validate MonoBehaviour specific rules
        if (classId.Value == 114)
        {
            await ValidateMonoBehaviourRules(obj, lineNumber);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates MonoBehaviour specific rules.
    /// </summary>
    private async Task ValidateMonoBehaviourRules(JsonObject obj, long lineNumber)
    {
        var scriptTypeIndex = obj["unity"]?["scriptTypeIndex"]?.GetValue<int>();
        if (scriptTypeIndex.HasValue && scriptTypeIndex.Value >= 0)
        {
            // Verify script type exists in types table
            if (!_validationContext.TypeReferences.Any(t => t.Value.ScriptTypeIndex == scriptTypeIndex.Value))
            {
                AddValidationError("assets", "", lineNumber, ValidationErrorType.Reference,
                    $"MonoBehaviour references non-existent script type index: {scriptTypeIndex.Value}");
            }
        }

        await Task.CompletedTask;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Checks if a field is unexpected for a given table.
    /// </summary>
    private bool IsUnexpectedField(string tableId, string fieldName)
    {
        var expectedFields = GetExpectedFieldsForTable(tableId);
        if (expectedFields.Count == 0)
        {
            // No explicit whitelist for this table; do not emit unexpected-field errors.
            return false;
        }

        return !expectedFields.Contains(fieldName);
    }

    /// <summary>
    /// Gets expected fields for a table.
    /// </summary>
    private HashSet<string> GetExpectedFieldsForTable(string tableId)
    {
        return tableId.ToLowerInvariant() switch
        {
            "assets" => new HashSet<string>
            {
                "domain",
                "pk",
                "classKey",
                "className",
                "name",
                "originalPath",
                "originalDirectory",
                "originalName",
                "originalExtension",
                "assetBundleName",
                "hierarchy",
                "collectionName",
                "bundleName",
                "sceneName",
                "unity",
                "byteStart",
                "byteSize",
                "data",
                "hash"
            },
            "types" => new HashSet<string> { "domain", "classKey", "classId", "className", "typeId", "scriptTypeIndex" },
            "asset_dependencies" => new HashSet<string> { "domain", "from", "to", "edge", "status" },
            "by_class" => new HashSet<string> { "domain", "classKey", "assets", "count", "className", "classId" },
            "by_collection" => new HashSet<string> { "domain", "collectionId", "name", "count", "isScene", "bundleName", "typeDistribution", "totalTypeCount" },
            "by_name" => new HashSet<string> { "domain", "name", "locations" },
            _ => new HashSet<string>()
        };
    }

    /// <summary>
    /// Validates data type for a specific field.
    /// </summary>
    private async Task ValidateFieldDataType(string tableId, string fieldName, JsonNode? value, long lineNumber)
    {
        if (value == null) return;

        var expectedType = GetExpectedFieldType(tableId, fieldName);
        if (expectedType == null) return;

        var actualType = GetJsonNodeType(value);
        if (!IsCompatibleType(expectedType, actualType))
        {
            AddValidationError(tableId, "", lineNumber, ValidationErrorType.DataType,
                $"Field '{fieldName}' has incorrect type: expected {expectedType}, got {actualType}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets expected field type for a table field.
    /// </summary>
    private string? GetExpectedFieldType(string tableId, string fieldName)
    {
        return (tableId.ToLowerInvariant(), fieldName) switch
        {
            ("assets", "domain") => "string",
            ("assets", "classKey") => "integer",
            ("assets", "pathId") => "integer",
            ("assets", "className") => "string",
            ("types", "domain") => "string",
            ("types", "classKey") => "integer",
            ("types", "classId") => "integer",
            _ => null
        };
    }

    /// <summary>
    /// Gets JSON node type as string.
    /// </summary>
    private string GetJsonNodeType(JsonNode node)
    {
        return node.GetValueKind() switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Array => "array",
            JsonValueKind.Object => "object",
            JsonValueKind.Null => "null",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Checks if actual type is compatible with expected type.
    /// </summary>
    private bool IsCompatibleType(string expectedType, string actualType)
    {
        return (expectedType, actualType) switch
        {
            ("string", "string") => true,
            ("integer", "number") => true,
            ("boolean", "boolean") => true,
            ("array", "array") => true,
            ("object", "object") => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks for circular dependencies using DFS.
    /// </summary>
    private bool HasCircularDependency(string assetPk, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(assetPk))
            return true;

        if (visited.Contains(assetPk))
            return false;

        visited.Add(assetPk);
        recursionStack.Add(assetPk);

        if (_validationContext.AssetDependencies.TryGetValue(assetPk, out var dependencies))
        {
            foreach (var dep in dependencies)
            {
                if (HasCircularDependency(dep.ToAssetPk, visited, recursionStack))
                    return true;
            }
        }

        recursionStack.Remove(assetPk);
        return false;
    }

    /// <summary>
    /// Gets Unity class name from ClassID.
    /// </summary>
    private string GetUnityClassName(int classId)
    {
        return UnityClassIdNames.TryGetValue(classId, out var name) ? name : $"ClassID_{classId}";
    }

    /// <summary>
    /// Generates comprehensive validation report.
    /// </summary>
    private ValidationReport GenerateValidationReport(TimeSpan elapsed, List<DomainExportResult> domainResults)
    {
        var report = new ValidationReport
        {
            OverallResult = _validationErrors.Any(e => e.Severity >= ValidationSeverity.Error) ? ValidationResult.Failed : ValidationResult.Passed,
            ValidationTime = elapsed,
            TotalRecordsValidated = _loadedData.Values.SelectMany(v => v).Count(),
            SchemasLoaded = _loadedSchemas.Count,
            DataFilesProcessed = domainResults.Count(r => r.Format == "ndjson"),
            Errors = _validationErrors.ToList(),
            Metadata = new ValidationMetadata
            {
                Performance = new ValidationPerformanceMetrics
                {
                    RecordsPerSecond = _loadedData.Values.SelectMany(v => v).Count() / Math.Max(1, elapsed.TotalSeconds),
                    PeakMemoryUsageMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0)
                }
            }
        };

        // Generate domain summaries
        foreach (var result in domainResults)
        {
            if (_loadedData.TryGetValue(result.Domain, out var data))
            {
                var domainErrors = _validationErrors.Where(e => e.TableId == result.TableId).ToList();
                
                report.DomainSummaries.Add(new DomainValidationSummary
                {
                    Domain = result.Domain,
                    TableId = result.TableId,
                    Result = domainErrors.Any(e => e.Severity >= ValidationSeverity.Error) ? ValidationResult.Failed : ValidationResult.Passed,
                    RecordsValidated = data.Count,
                    ErrorCount = domainErrors.Count(e => e.Severity >= ValidationSeverity.Error),
                    WarningCount = domainErrors.Count(e => e.Severity == ValidationSeverity.Warning),
                    SchemaPath = result.SchemaPath,
                    FilesProcessed = result.HasShards
                        ? result.Shards.Select(s => s.Shard).ToList()
                        : new List<string> { result.EntryFile ?? string.Empty }
                });
            }
        }

        return report;
    }

    #endregion
}
