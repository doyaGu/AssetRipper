using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Processing;
using AssetRipper.Processing.Prefabs;
using AssetRipper.Processing.Scenes;
using AssetRipper.Processing.ScriptableObject;

namespace AssetRipper.Tools.AssetDumper;

internal sealed class AssetDumperExportHandler : ExportHandler
{
	public AssetDumperExportHandler(FullConfiguration settings) : base(settings)
	{
	}

	protected override IEnumerable<IAssetProcessor> GetProcessors()
	{
		// Restrict processing to the stages required for metadata exports.
		yield return new SceneDefinitionProcessor();
		yield return new MainAssetProcessor();
		yield return new PrefabProcessor();
		yield return new ScriptableObjectProcessor();
	}
}
