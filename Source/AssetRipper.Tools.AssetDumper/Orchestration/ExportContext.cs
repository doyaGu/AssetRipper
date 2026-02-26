using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Models.Common;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Generators;
using AssetRipper.Tools.AssetDumper.Writers;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Shared context for export operations containing configuration and accumulated results.
/// </summary>
public sealed class ExportContext
{
	public Options Options { get; }
	public ExportTableSelection TableSelection { get; }
	public GameData GameData { get; }
	public CompressionKind CompressionKind { get; }
	public bool EnableIndex { get; }
	public List<DomainExportResult> DomainResults { get; }
	public List<ShardDescriptor> AllShards { get; }
	public Dictionary<string, ManifestIndex> IndexRefs { get; }
	public KeyIndexGenerator? IndexGenerator { get; }
	private readonly Dictionary<string, ExportPipelineOwner> _emittedTablesByOwner;

	public ExportContext(
		Options options,
		ExportTableSelection tableSelection,
		GameData gameData,
		CompressionKind compressionKind,
		bool enableIndex,
		KeyIndexGenerator? indexGenerator)
	{
		Options = options;
		TableSelection = tableSelection;
		GameData = gameData;
		CompressionKind = compressionKind;
		EnableIndex = enableIndex;
		IndexGenerator = indexGenerator;
		DomainResults = new List<DomainExportResult>();
		AllShards = new List<ShardDescriptor>();
		IndexRefs = new Dictionary<string, ManifestIndex>(System.StringComparer.OrdinalIgnoreCase);
		_emittedTablesByOwner = new Dictionary<string, ExportPipelineOwner>(System.StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Adds a domain result and captures its index if applicable.
	/// </summary>
	public void AddResult(DomainExportResult result, ExportPipelineOwner owner)
	{
		if (ExportTableMatrix.TryGetOwner(result.TableId, out ExportPipelineOwner expectedOwner)
			&& owner != expectedOwner)
		{
			throw new InvalidOperationException(
				$"Table '{result.TableId}' is owned by '{expectedOwner}' but emitted by '{owner}'.");
		}

		if (_emittedTablesByOwner.TryGetValue(result.TableId, out ExportPipelineOwner existingOwner))
		{
			throw new InvalidOperationException(
				$"Duplicate table emission detected for '{result.TableId}'. Existing owner='{existingOwner}', new owner='{owner}'.");
		}

		_emittedTablesByOwner[result.TableId] = owner;
		DomainResults.Add(result);
		AllShards.AddRange(result.Shards);

		if (EnableIndex && IndexGenerator != null && result.HasIndex)
		{
			ManifestIndex? reference = IndexGenerator.Write(result.Domain, result.IndexEntries, CompressionKind);
			if (reference != null)
			{
				IndexRefs[result.Domain] = reference;
			}
		}
	}

	public void AddResult(DomainExportResult result)
	{
		AddResult(result, ExportPipelineOwner.Unknown);
	}
}
