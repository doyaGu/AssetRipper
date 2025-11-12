using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Records;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Pipeline for exporting script-code association metadata (assemblies, types, mappings).
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
