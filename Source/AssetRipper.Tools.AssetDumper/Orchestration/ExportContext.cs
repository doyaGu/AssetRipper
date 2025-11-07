using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Writers;

namespace AssetRipper.Tools.AssetDumper.Orchestration;

/// <summary>
/// Shared context for export operations containing configuration and accumulated results.
/// </summary>
public sealed class ExportContext
{
	public Options Options { get; }
	public GameData GameData { get; }
	public CompressionKind CompressionKind { get; }
	public bool EnableIndex { get; }
	public List<DomainExportResult> DomainResults { get; }
	public List<ShardDescriptor> AllShards { get; }
	public Dictionary<string, ManifestIndex> IndexRefs { get; }
	public KeyIndexGenerator? IndexGenerator { get; }

	public ExportContext(
		Options options,
		GameData gameData,
		CompressionKind compressionKind,
		bool enableIndex,
		KeyIndexGenerator? indexGenerator)
	{
		Options = options;
		GameData = gameData;
		CompressionKind = compressionKind;
		EnableIndex = enableIndex;
		IndexGenerator = indexGenerator;
		DomainResults = new List<DomainExportResult>();
		AllShards = new List<ShardDescriptor>();
		IndexRefs = new Dictionary<string, ManifestIndex>(System.StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Adds a domain result and captures its index if applicable.
	/// </summary>
	public void AddResult(DomainExportResult result)
	{
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
}
