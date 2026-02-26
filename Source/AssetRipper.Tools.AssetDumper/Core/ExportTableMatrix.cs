using System.Linq;

namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Canonical pipeline owners for export tables.
/// </summary>
public enum ExportPipelineOwner
{
	Unknown = 0,
	FactsCore = 1,
	FactsOptional = 2,
	Relations = 3,
	ScriptCode = 4,
	Metrics = 5
}

/// <summary>
/// Selector domains driven by command-line table options.
/// </summary>
public enum ExportSelectorDomain
{
	Facts = 1,
	Relations = 2,
	CodeAnalysis = 3
}

/// <summary>
/// Resolved table selection for one export run.
/// </summary>
public sealed class ExportTableSelection
{
	private readonly HashSet<string> _selectedTables;
	private readonly Dictionary<ExportPipelineOwner, HashSet<string>> _tablesByOwner;
	private readonly Dictionary<ExportSelectorDomain, HashSet<string>> _tablesBySelector;

	internal ExportTableSelection(
		HashSet<string> selectedTables,
		Dictionary<ExportPipelineOwner, HashSet<string>> tablesByOwner,
		Dictionary<ExportSelectorDomain, HashSet<string>> tablesBySelector)
	{
		_selectedTables = selectedTables;
		_tablesByOwner = tablesByOwner;
		_tablesBySelector = tablesBySelector;
	}

	public IReadOnlyCollection<string> SelectedTables => _selectedTables;

	public bool IsTableSelected(string tableId)
	{
		if (string.IsNullOrWhiteSpace(tableId))
		{
			return false;
		}

		return _selectedTables.Contains(tableId);
	}

	public bool HasTablesForOwner(ExportPipelineOwner owner)
	{
		return _tablesByOwner.TryGetValue(owner, out HashSet<string>? tables) && tables.Count > 0;
	}

	public IReadOnlyCollection<string> GetTablesForOwner(ExportPipelineOwner owner)
	{
		if (_tablesByOwner.TryGetValue(owner, out HashSet<string>? tables))
		{
			return tables;
		}

		return Array.Empty<string>();
	}

	public bool HasSelectionsForSelector(ExportSelectorDomain selector)
	{
		return _tablesBySelector.TryGetValue(selector, out HashSet<string>? tables) && tables.Count > 0;
	}
}

/// <summary>
/// Centralized table ownership matrix and table-selection resolver.
/// </summary>
public static class ExportTableMatrix
{
	private static readonly IReadOnlyDictionary<string, ExportPipelineOwner> TableOwners =
		new Dictionary<string, ExportPipelineOwner>(StringComparer.OrdinalIgnoreCase)
		{
			["facts/assets"] = ExportPipelineOwner.FactsCore,
			["facts/collections"] = ExportPipelineOwner.FactsCore,
			["facts/types"] = ExportPipelineOwner.FactsCore,
			["facts/scenes"] = ExportPipelineOwner.FactsOptional,
			["facts/bundles"] = ExportPipelineOwner.FactsOptional,
			["facts/script_metadata"] = ExportPipelineOwner.FactsOptional,
			["relations/asset_dependencies"] = ExportPipelineOwner.Relations,
			["relations/collection_dependencies"] = ExportPipelineOwner.Relations,
			["relations/bundle_hierarchy"] = ExportPipelineOwner.Relations,
			["relations/script_type_mapping"] = ExportPipelineOwner.Relations,
			["facts/assemblies"] = ExportPipelineOwner.ScriptCode,
			["facts/type_definitions"] = ExportPipelineOwner.ScriptCode,
			["facts/type_members"] = ExportPipelineOwner.ScriptCode,
			["facts/script_sources"] = ExportPipelineOwner.ScriptCode,
			["relations/assembly_dependencies"] = ExportPipelineOwner.ScriptCode,
			["relations/type_inheritance"] = ExportPipelineOwner.ScriptCode
		};

	private static readonly IReadOnlyDictionary<string, string[]> FactsTokenMap =
		new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			// facts/types are derived from the asset scan and exported alongside facts/assets.
			["assets"] = ["facts/assets", "facts/types"],
			["types"] = ["facts/assets", "facts/types"],
			["collections"] = ["facts/collections"],
			["scenes"] = ["facts/scenes"],
			["scripts"] = ["facts/script_metadata"],
			["script_metadata"] = ["facts/script_metadata"],
			["bundles"] = ["facts/bundles"]
		};

	private static readonly IReadOnlyDictionary<string, string[]> RelationsTokenMap =
		new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			["dependencies"] = ["relations/asset_dependencies", "relations/collection_dependencies"],
			["asset_dependencies"] = ["relations/asset_dependencies"],
			["collection_dependencies"] = ["relations/collection_dependencies"],
			["hierarchy"] = ["relations/bundle_hierarchy"],
			["bundle_hierarchy"] = ["relations/bundle_hierarchy"],
			["mappings"] = ["relations/script_type_mapping"],
			["mapping"] = ["relations/script_type_mapping"],
			["script_type_mapping"] = ["relations/script_type_mapping"],
			["script-type-mapping"] = ["relations/script_type_mapping"]
		};

	private static readonly IReadOnlyDictionary<string, string[]> CodeAnalysisTokenMap =
		new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			// facts/assemblies are exported as shared baseline metadata whenever code-analysis is enabled.
			["types"] = ["facts/assemblies", "facts/type_definitions"],
			["type_definitions"] = ["facts/assemblies", "facts/type_definitions"],
			["members"] = ["facts/assemblies", "facts/type_members"],
			["type_members"] = ["facts/assemblies", "facts/type_members"],
			["inheritance"] = ["facts/assemblies", "relations/type_inheritance"],
			["type_inheritance"] = ["facts/assemblies", "relations/type_inheritance"],
			["mappings"] = ["facts/assemblies", "relations/script_type_mapping"],
			["mapping"] = ["facts/assemblies", "relations/script_type_mapping"],
			["script_type_mapping"] = ["facts/assemblies", "relations/script_type_mapping"],
			["script-type-mapping"] = ["facts/assemblies", "relations/script_type_mapping"],
			["dependencies"] = ["facts/assemblies", "relations/assembly_dependencies"],
			["assembly_dependencies"] = ["facts/assemblies", "relations/assembly_dependencies"],
			["sources"] = ["facts/assemblies", "facts/script_sources"],
			["script_sources"] = ["facts/assemblies", "facts/script_sources"]
		};

	public static bool TryGetOwner(string tableId, out ExportPipelineOwner owner)
	{
		return TableOwners.TryGetValue(tableId, out owner);
	}

	public static ExportPipelineOwner GetOwnerOrUnknown(string tableId)
	{
		return TryGetOwner(tableId, out ExportPipelineOwner owner)
			? owner
			: ExportPipelineOwner.Unknown;
	}

	public static ExportTableSelection Resolve(Options options)
	{
		if (options is null)
		{
			throw new ArgumentNullException(nameof(options));
		}

		HashSet<string> exportDomains = ParseCsv(options.ExportDomains);

		bool factsEnabled = IsDomainEnabled(exportDomains, "facts");
		bool relationsEnabled = IsDomainEnabled(exportDomains, "relations");
		bool codeAnalysisEnabled = IsDomainEnabled(exportDomains, "code-analysis", "codeanalysis");

		HashSet<string> factsTables = ResolveSelectorTables(
			domainEnabled: factsEnabled,
			tableSelection: options.FactTables,
			tokenMap: FactsTokenMap);

		HashSet<string> relationTables = ResolveSelectorTables(
			domainEnabled: relationsEnabled,
			tableSelection: options.RelationTables,
			tokenMap: RelationsTokenMap);

		HashSet<string> codeAnalysisTables = ResolveSelectorTables(
			domainEnabled: codeAnalysisEnabled,
			tableSelection: options.CodeAnalysisTables,
			tokenMap: CodeAnalysisTokenMap);

		HashSet<string> allSelected = new(StringComparer.OrdinalIgnoreCase);
		allSelected.UnionWith(factsTables);
		allSelected.UnionWith(relationTables);
		allSelected.UnionWith(codeAnalysisTables);

		Dictionary<ExportPipelineOwner, HashSet<string>> byOwner = new();
		foreach (string tableId in allSelected)
		{
			if (!TryGetOwner(tableId, out ExportPipelineOwner owner))
			{
				continue;
			}

			if (!byOwner.TryGetValue(owner, out HashSet<string>? tables))
			{
				tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				byOwner[owner] = tables;
			}

			tables.Add(tableId);
		}

		Dictionary<ExportSelectorDomain, HashSet<string>> bySelector = new()
		{
			[ExportSelectorDomain.Facts] = factsTables,
			[ExportSelectorDomain.Relations] = relationTables,
			[ExportSelectorDomain.CodeAnalysis] = codeAnalysisTables
		};

		return new ExportTableSelection(allSelected, byOwner, bySelector);
	}

	private static HashSet<string> ResolveSelectorTables(
		bool domainEnabled,
		string? tableSelection,
		IReadOnlyDictionary<string, string[]> tokenMap)
	{
		HashSet<string> tables = new(StringComparer.OrdinalIgnoreCase);
		if (!domainEnabled)
		{
			return tables;
		}

		HashSet<string> tokens = ParseCsv(tableSelection);
		if (tokens.Count == 0)
		{
			return tables;
		}

		bool hasAll = tokens.Contains("all");
		bool hasNone = tokens.Contains("none");
		if (hasNone && !hasAll)
		{
			return tables;
		}

		if (hasAll)
		{
			foreach (string[] mappedTables in tokenMap.Values)
			{
				tables.UnionWith(mappedTables);
			}
			return tables;
		}

		foreach (string token in tokens)
		{
			if (tokenMap.TryGetValue(token, out string[]? mappedTables))
			{
				tables.UnionWith(mappedTables);
				continue;
			}

			if (TryResolveDirectTableToken(token, out string? resolvedTable))
			{
				tables.Add(resolvedTable);
			}
		}

		return tables;
	}

	private static bool TryResolveDirectTableToken(string token, out string tableId)
	{
		tableId = string.Empty;
		if (string.IsNullOrWhiteSpace(token))
		{
			return false;
		}

		if (token.Contains('/') && TableOwners.ContainsKey(token))
		{
			tableId = token;
			return true;
		}

		List<string> matches = TableOwners.Keys
			.Where(table => table.EndsWith($"/{token}", StringComparison.OrdinalIgnoreCase))
			.ToList();
		if (matches.Count == 1)
		{
			tableId = matches[0];
			return true;
		}

		return false;
	}

	private static bool IsDomainEnabled(HashSet<string> exportDomains, params string[] aliases)
	{
		if (exportDomains.Count == 0)
		{
			return false;
		}

		bool hasAll = exportDomains.Contains("all");
		bool hasNone = exportDomains.Contains("none");
		if (hasNone && !hasAll)
		{
			return false;
		}

		if (hasAll)
		{
			return true;
		}

		foreach (string alias in aliases)
		{
			if (exportDomains.Contains(alias))
			{
				return true;
			}
		}

		return false;
	}

	private static HashSet<string> ParseCsv(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}

		return value
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(static token => !string.IsNullOrWhiteSpace(token))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}
}
