using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Facts;
using AssetRipper.Tools.AssetDumper.Exporters.Relations;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Pipeline for exporting script-code association metadata (assemblies, type metadata and code relationships).
/// Optionally generates AST files when linking source files.
/// </summary>
internal sealed class ScriptCodeExportPipeline
{
	private readonly ExportContext _context;

	public ScriptCodeExportPipeline(ExportContext context)
	{
		_context = context ?? throw new ArgumentNullException(nameof(context));
	}

	/// <summary>
	/// Executes the script-code association export pipeline.
	/// </summary>
	public void Execute()
	{
		try
		{
			// Phase A: Core exports
			if (_context.Options.ExportAssemblyFacts)
			{
				ExportAssemblyFacts();
			}
			if (_context.Options.ExportTypeDefinitions)
			{
				ExportTypeDefinitions();
			}

			// Phase B: Enhanced relationship exports
			if (_context.Options.ExportAssemblyDependencies)
			{
				ExportAssemblyDependencies();
			}
			if (_context.Options.ExportTypeInheritance)
			{
				ExportTypeInheritance();
			}

			// Optional: Link to source files if available
			if (_context.Options.LinkSourceFiles)
			{
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

		AssemblyExporter exporter = new AssemblyExporter(
			_context.Options,
			_context.CompressionKind,
			_context.EnableIndex);

		DomainExportResult result = exporter.ExportAssemblies(_context.GameData);
		_context.AddResult(result, ExportPipelineOwner.ScriptCode);
	}

	private void ExportTypeDefinitions()
	{
		if (!_context.Options.Silent)
		{
			Logger.Info("Exporting type definitions...");
		}

		TypeDefinitionExporter exporter = new TypeDefinitionExporter(
			_context.Options,
			_context.CompressionKind,
			_context.EnableIndex);

		DomainExportResult result = exporter.ExportTypes(_context.GameData);
		_context.AddResult(result, ExportPipelineOwner.ScriptCode);
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
		_context.AddResult(result, ExportPipelineOwner.ScriptCode);
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
		_context.AddResult(result, ExportPipelineOwner.ScriptCode);
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
		_context.AddResult(result, ExportPipelineOwner.ScriptCode);
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
		_context.AddResult(result, ExportPipelineOwner.ScriptCode);
	}
}
