using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Helpers;

namespace AssetRipper.Tools.AssetDumper.Tests.TestInfrastructure.Builders;

/// <summary>
/// Fluent builder for creating Options instances in tests.
/// Provides sensible defaults and easy customization.
/// </summary>
public class OptionsBuilder
{
    private string? _inputPath;
    private string? _outputPath;
    private string _factTables = "all";
    private string _relationTables = "all";
    private string _exportDomains = "all";
    private bool _enableIndexing = false;
    private string _compression = "none";
    private bool _quiet = true;
    private bool _verbose = false;
    private long _shardSize = 0; // 0 = auto
    private int _parallelThreads = 1; // Single-threaded for tests by default
    private bool _incrementalMode = false;
    private bool _dryRun = false;
    private bool _validateSchemas = false;

    public OptionsBuilder()
    {
        // Defaults are set above
    }

    public OptionsBuilder WithInputPath(string path)
    {
        _inputPath = path;
        return this;
    }

    public OptionsBuilder WithOutputPath(string path)
    {
        _outputPath = path;
        return this;
    }

    public OptionsBuilder WithTempOutputPath(string? testName = null)
    {
        _outputPath = TestPathHelper.CreateTestDirectory(testName);
        return this;
    }

    public OptionsBuilder WithFactTables(string tables)
    {
        _factTables = tables;
        return this;
    }

    public OptionsBuilder WithRelationTables(string tables)
    {
        _relationTables = tables;
        return this;
    }

    public OptionsBuilder WithExportDomains(string domains)
    {
        _exportDomains = domains;
        return this;
    }

    public OptionsBuilder WithIndexing(bool enable = true)
    {
        _enableIndexing = enable;
        return this;
    }

    public OptionsBuilder WithCompression(string codec)
    {
        _compression = codec;
        return this;
    }

    public OptionsBuilder WithQuiet(bool quiet = true)
    {
        _quiet = quiet;
        return this;
    }

    public OptionsBuilder WithVerbose(bool verbose = true)
    {
        _verbose = verbose;
        _quiet = !verbose; // Verbose implies not quiet
        return this;
    }

    public OptionsBuilder WithShardSize(long size)
    {
        _shardSize = size;
        return this;
    }

    public OptionsBuilder WithParallelThreads(int threads)
    {
        _parallelThreads = threads;
        return this;
    }

    public OptionsBuilder WithIncrementalMode(bool enable = true)
    {
        _incrementalMode = enable;
        return this;
    }

    public OptionsBuilder WithDryRun(bool enable = true)
    {
        _dryRun = enable;
        return this;
    }

    public OptionsBuilder WithSchemaValidation(bool enable = true)
    {
        _validateSchemas = enable;
        return this;
    }

    /// <summary>
    /// Builds the Options instance with sensible test defaults.
    /// </summary>
    public Options Build()
    {
        // Ensure required paths are set
        if (string.IsNullOrEmpty(_inputPath))
        {
            _inputPath = Directory.GetCurrentDirectory(); // Safe default for tests
        }

        if (string.IsNullOrEmpty(_outputPath))
        {
            _outputPath = TestPathHelper.CreateTestDirectory();
        }

        return new Options
        {
            InputPath = _inputPath,
            OutputPath = _outputPath,
            FactTables = _factTables,
            RelationTables = _relationTables,
            ExportDomains = _exportDomains,
            EnableIndexing = _enableIndexing,
            Compression = _compression,
            Quiet = _quiet,
            Verbose = _verbose,
            ShardSize = _shardSize,
            ParallelThreads = _parallelThreads,
            IncrementalMode = _incrementalMode,
            DryRun = _dryRun,
            ValidateSchemas = _validateSchemas
        };
    }

    /// <summary>
    /// Creates a minimal Options instance for basic tests.
    /// </summary>
    public static Options CreateMinimal(string? testName = null)
    {
        return new OptionsBuilder()
            .WithTempOutputPath(testName)
            .WithExportDomains("none")
            .Build();
    }

    /// <summary>
    /// Creates an Options instance suitable for validation tests.
    /// </summary>
    public static Options CreateForValidation(string outputPath)
    {
        return new OptionsBuilder()
            .WithOutputPath(outputPath)
            .WithSchemaValidation(true)
            .WithQuiet(true)
            .Build();
    }

    /// <summary>
    /// Creates an Options instance suitable for export tests.
    /// </summary>
    public static Options CreateForExport(string inputPath, string outputPath)
    {
        return new OptionsBuilder()
            .WithInputPath(inputPath)
            .WithOutputPath(outputPath)
            .WithQuiet(true)
            .WithCompression("none") // Faster for tests
            .WithParallelThreads(1)   // Deterministic for tests
            .Build();
    }
}
