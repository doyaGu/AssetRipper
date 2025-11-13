using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Records;
using AssetRipper.Tools.AssetDumper.Generators;
using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Pipeline for exporting script-code association metadata (assemblies, types, mappings).
/// Optionally generates AST files when linking source files.
/// </summary>
internal sealed class ScriptCodeExportPipeline
{
	private readonly ExportContext _context;
	private readonly AstGenerator? _astGenerator;

	public ScriptCodeExportPipeline(ExportContext context)
	{
		_context = context ?? throw new ArgumentNullException(nameof(context));
		
		// Initialize AST generator if AST generation is enabled
		if (_context.Options.GenerateAst)
		{
			_astGenerator = new AstGenerator(_context.Options);
		}
	}

	/// <summary>
	/// Executes the script-code association export pipeline.
	/// </summary>
	public void Execute()
	{
		try
		{
			// Phase A: Core exports (always execute if enabled)
			ExportAssemblyFacts();
			ExportTypeDefinitions();
			ExportScriptTypeMappings();

			// Phase B: Enhanced relationship exports
			ExportAssemblyDependencies();
			ExportTypeInheritance();

			// Optional: Link to source files if available
			if (_context.Options.LinkSourceFiles)
			{
				// Generate AST before linking sources if enabled
				if (_context.Options.GenerateAst && _astGenerator != null)
				{
					GenerateAst();
				}
				
				ExportScriptSources();
			}

			// Phase C: Optional type members (detailed analysis)
			if (_context.Options.ExportTypeMembers)
			{
				ExportTypeMembers();
			}
		}
		catch (Exception ex)
		{
			Logger.Error("Script-code association export failed", ex);
			throw;
		}
	}

	private void ExportAssemblyFacts()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting assembly facts...");
		}

		AssemblyFactsExporter exporter = new AssemblyFactsExporter(
			_context.Options,
			_context.CompressionKind,
			_context.EnableIndex);

		DomainExportResult result = exporter.ExportAssemblies(_context.GameData);
		_context.AddResult(result);
	}

	private void ExportTypeDefinitions()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting type definitions...");
		}

		TypeDefinitionRecordExporter exporter = new TypeDefinitionRecordExporter(
			_context.Options,
			_context.CompressionKind,
			_context.EnableIndex);

		DomainExportResult result = exporter.ExportTypes(_context.GameData);
		_context.AddResult(result);
	}

	private void ExportScriptTypeMappings()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting script-type mappings...");
		}

		ScriptTypeMappingExporter exporter = new ScriptTypeMappingExporter(
			_context.Options,
			_context.CompressionKind,
			_context.EnableIndex);

		DomainExportResult result = exporter.ExportMappings(_context.GameData);
		_context.AddResult(result);
	}

	private void ExportScriptSources()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Linking script sources...");
		}

		ScriptSourceExporter exporter = new ScriptSourceExporter(
			_context.Options,
			_context.CompressionKind,
			_context.EnableIndex);

		DomainExportResult result = exporter.ExportSources(_context.GameData);
		_context.AddResult(result);
	}

	private void GenerateAst()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Generating AST from decompiled scripts...");
		}

		string scriptsDir = Path.Combine(_context.Options.OutputPath, "Scripts");
		if (!Directory.Exists(scriptsDir))
		{
			Logger.Warning("Scripts directory not found. Skipping AST generation.");
			return;
		}

		// Use FilterManager for consistent filtering (if available in context)
		FilterManager filterManager = new FilterManager(_context.Options);
		_astGenerator?.GenerateAstFromScripts(scriptsDir, _context.Options.OutputPath, filterManager);
	}

	private void ExportTypeMembers()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting type members (V2 schema with enhanced metadata)...");
		}

		TypeMemberExporter exporter = new TypeMemberExporter(
			_context.Options,
			_context.CompressionKind,
			_context.EnableIndex);

		DomainExportResult result = exporter.ExportMembers(_context.GameData);
		_context.AddResult(result);
	}

	private void ExportAssemblyDependencies()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting assembly dependencies...");
		}

		AssemblyDependencyExporter exporter = new AssemblyDependencyExporter(
			_context.Options,
			_context.CompressionKind,
			_context.EnableIndex);

		DomainExportResult result = exporter.ExportDependencies(_context.GameData);
		_context.AddResult(result);
	}

	private void ExportTypeInheritance()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting type inheritance...");
		}

		TypeInheritanceExporter exporter = new TypeInheritanceExporter(
			_context.Options,
			_context.CompressionKind,
			_context.EnableIndex);

		DomainExportResult result = exporter.ExportInheritance(_context.GameData);
		_context.AddResult(result);
	}
}
