using AssetRipper.Import.Logging;

namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Validates Options and provides detailed error messages with suggestions.
/// </summary>
public sealed class OptionsValidator
{
    private readonly Options _options;
    private readonly List<ValidationError> _errors = new();
    private readonly List<ValidationWarning> _warnings = new();

    public OptionsValidator(Options options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Validates all options and returns whether validation passed.
    /// </summary>
    public bool Validate()
    {
        _errors.Clear();
        _warnings.Clear();

        ValidateInputPath();
        ValidateOutputPath();
        ValidateCompression();
        ValidateShardSize();
        ValidateParallelism();
        ValidateExportOptions();
        ValidateIncrementalOptions();

        // Log all errors and warnings
        foreach (ValidationError error in _errors)
        {
            Logger.Error($"Validation Error: {error.Message}");
            if (!string.IsNullOrEmpty(error.Suggestion))
            {
                Logger.Error($"  Suggestion: {error.Suggestion}");
            }
        }

        foreach (ValidationWarning warning in _warnings)
        {
            Logger.Warning($"Validation Warning: {warning.Message}");
            if (!string.IsNullOrEmpty(warning.Suggestion))
            {
                Logger.Warning($"  Suggestion: {warning.Suggestion}");
            }
        }

        return _errors.Count == 0;
    }

    /// <summary>
    /// Gets validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors => _errors;

    /// <summary>
    /// Gets validation warnings.
    /// </summary>
    public IReadOnlyList<ValidationWarning> Warnings => _warnings;

    private void ValidateInputPath()
    {
        if (string.IsNullOrWhiteSpace(_options.InputPath))
        {
            _errors.Add(new ValidationError
            {
                Field = "InputPath",
                Message = "Input path is required",
                Suggestion = "Specify the path to a Unity game folder or .unity3d file using the first positional argument"
            });
            return;
        }

        if (!Directory.Exists(_options.InputPath) && !File.Exists(_options.InputPath))
        {
            _errors.Add(new ValidationError
            {
                Field = "InputPath",
                Message = $"Input path does not exist: {_options.InputPath}",
                Suggestion = "Verify the path is correct and accessible. For Unity games, specify either:\n" +
                           "  - The game's Data folder (e.g., MyGame_Data)\n" +
                           "  - A .unity3d asset bundle file"
            });
        }
    }

    private void ValidateOutputPath()
    {
        if (string.IsNullOrWhiteSpace(_options.OutputPath))
        {
            _errors.Add(new ValidationError
            {
                Field = "OutputPath",
                Message = "Output path is required",
                Suggestion = "Specify the output directory using the second positional argument"
            });
            return;
        }

        // Check if output path is writable
        try
        {
            string testDir = Path.Combine(_options.OutputPath, ".test");
            Directory.CreateDirectory(testDir);
            Directory.Delete(testDir);
        }
        catch (Exception ex)
        {
            _errors.Add(new ValidationError
            {
                Field = "OutputPath",
                Message = $"Output path is not writable: {ex.Message}",
                Suggestion = "Ensure you have write permissions to the output directory"
            });
        }

        // Warn if output directory already exists and is not empty
        if (Directory.Exists(_options.OutputPath))
        {
            string[] existingFiles = Directory.GetFiles(_options.OutputPath, "*", SearchOption.AllDirectories);
            if (existingFiles.Length > 0)
            {
                _warnings.Add(new ValidationWarning
                {
                    Field = "OutputPath",
                    Message = $"Output directory already contains {existingFiles.Length} file(s)",
                    Suggestion = "Existing files may be overwritten. Consider using a clean directory or enable incremental mode (--incremental)"
                });
            }
        }
    }

    private void ValidateCompression()
    {
        if (string.IsNullOrWhiteSpace(_options.Compression))
        {
            return; // No compression specified, will default to none
        }

        string compression = _options.Compression.Trim().ToLowerInvariant();
        string[] validFormats = { "none", "gzip", "gz", "zstd", "zstd-seekable", "zstd_seekable", "zstdseekable" };

        if (!validFormats.Contains(compression))
        {
            _errors.Add(new ValidationError
            {
                Field = "Compression",
                Message = $"Unknown compression format: {_options.Compression}",
                Suggestion = "Valid compression formats are:\n" +
                           "  - none (no compression, fastest)\n" +
                           "  - gzip or gz (standard compression, best compatibility)\n" +
                           "  - zstd (fast compression with good ratio)\n" +
                           "  - zstd-seekable (allows random access to compressed data)"
            });
        }

        // Warn about compression trade-offs
        if (compression == "zstd-seekable" || compression == "zstd_seekable" || compression == "zstdseekable")
        {
            _warnings.Add(new ValidationWarning
            {
                Field = "Compression",
                Message = "Zstd-seekable compression creates larger files than regular zstd",
                Suggestion = "Use zstd-seekable only if you need random access to compressed data. Otherwise, use 'zstd' for better compression ratio."
            });
        }
    }

    private void ValidateShardSize()
    {
        if (_options.ShardSize <= 0)
        {
            return; // Will use default
        }

        if (_options.ShardSize < 100)
        {
            _warnings.Add(new ValidationWarning
            {
                Field = "ShardSize",
                Message = $"Shard size is very small ({_options.ShardSize} records per shard)",
                Suggestion = "Small shard sizes create many files and may impact performance. Recommended: 10,000 - 100,000 records per shard."
            });
        }
        else if (_options.ShardSize > 1_000_000)
        {
            _warnings.Add(new ValidationWarning
            {
                Field = "ShardSize",
                Message = $"Shard size is very large ({_options.ShardSize} records per shard)",
                Suggestion = "Large shard sizes may cause memory issues and slow processing. Recommended: 10,000 - 100,000 records per shard."
            });
        }
    }

    private void ValidateParallelism()
    {
        if (_options.ParallelDegree < 0)
        {
            _errors.Add(new ValidationError
            {
                Field = "ParallelDegree",
                Message = "Parallel degree cannot be negative",
                Suggestion = "Use 0 for automatic detection, 1 for sequential, or higher values for parallel processing."
            });
            return;
        }

        if (_options.ParallelDegree == 0)
        {
            return; // Will use default (auto-detect)
        }

        int cpuCount = Environment.ProcessorCount;

        if (_options.ParallelDegree > cpuCount * 2)
        {
            _warnings.Add(new ValidationWarning
            {
                Field = "ParallelDegree",
                Message = $"Parallel degree ({_options.ParallelDegree}) is much higher than CPU count ({cpuCount})",
                Suggestion = $"High parallelism may not improve performance and could increase memory usage. Consider using automatic detection (0) or a value between 1 and {cpuCount * 2}."
            });
        }

        if (_options.ParallelDegree == 1)
        {
            _warnings.Add(new ValidationWarning
            {
                Field = "ParallelDegree",
                Message = "Parallel degree is set to 1 (single-threaded mode)",
                Suggestion = "Single-threaded processing may be slow for large projects. Consider using automatic detection (0) or higher values for better performance."
            });
        }
    }

    private void ValidateExportOptions()
    {
        // Check if at least one export option is enabled
        bool hasAnyExport = _options.ExportFacts || 
                           _options.ExportRelations || 
                           _options.ExportScenes || 
                           _options.ExportScripts;

        if (!hasAnyExport)
        {
            _warnings.Add(new ValidationWarning
            {
                Field = "ExportOptions",
                Message = "All export options are disabled",
                Suggestion = "Enable at least one export option:\n" +
                           "  --facts (export asset facts)\n" +
                           "  --relations (export dependencies)\n" +
                           "  --scenes (export scene hierarchy)\n" +
                           "  --scripts (export and decompile scripts)"
            });
        }

        // Validate index options
        if (_options.EnableIndex && !_options.ExportIndexes)
        {
            _warnings.Add(new ValidationWarning
            {
                Field = "EnableIndex",
                Message = "Index generation is enabled but index export is disabled",
                Suggestion = "Set --indexes=true to export the generated indexes, or set --enable-index=false to skip index generation entirely"
            });
        }
    }

    private void ValidateIncrementalOptions()
    {
        if (!_options.IncrementalProcessing)
        {
            return;
        }

        // Check if manifest exists for incremental processing
        if (!string.IsNullOrWhiteSpace(_options.OutputPath))
        {
            string manifestPath = Path.Combine(_options.OutputPath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                _warnings.Add(new ValidationWarning
                {
                    Field = "Incremental",
                    Message = "Incremental mode is enabled but no existing manifest found",
                    Suggestion = "First export will be a full export. Subsequent exports will be incremental."
                });
            }
        }
    }
}

/// <summary>
/// Represents a validation error that prevents operation.
/// </summary>
public sealed class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
}

/// <summary>
/// Represents a validation warning that should be reviewed but doesn't prevent operation.
/// </summary>
public sealed class ValidationWarning
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
}
