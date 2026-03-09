using AssetRipper.Tools.AssetDumper.Exporters.Facts;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper;
using Newtonsoft.Json;
using System.Reflection;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Exporters.Facts;

public class SceneExporterTests
{
	[Fact]
	public void IsPrimaryCollection_ShouldMatchOnlyTheResolvedPrimaryCollectionId()
	{
		// Arrange
		const string hierarchyCollectionId = "hierarchy-collection";
		const string resourceCollectionId = "resource-collection";

		// Act
		bool hierarchyIsPrimary = SceneExporter.IsPrimaryCollection(hierarchyCollectionId, hierarchyCollectionId);
		bool resourceIsPrimary = SceneExporter.IsPrimaryCollection(resourceCollectionId, hierarchyCollectionId);

		// Assert
		hierarchyIsPrimary.Should().BeTrue();
		resourceIsPrimary.Should().BeFalse();
	}

	[Fact]
	public void IsPrimaryCollection_ShouldNotDependOnCollectionOrdering()
	{
		// Arrange
		const string firstCollectionId = "sharedassets0";
		const string resolvedPrimaryCollectionId = "level0-hierarchy";

		// Act
		bool firstCollectionIsPrimary = SceneExporter.IsPrimaryCollection(firstCollectionId, resolvedPrimaryCollectionId);
		bool resolvedPrimaryIsPrimary = SceneExporter.IsPrimaryCollection(resolvedPrimaryCollectionId, resolvedPrimaryCollectionId);

		// Assert
		firstCollectionIsPrimary.Should().BeFalse();
		resolvedPrimaryIsPrimary.Should().BeTrue();
	}

	[Fact]
	public void SceneSerialization_ShouldPreserveRequiredZeroAndFalseFields()
	{
		// Arrange
		var exporter = new SceneExporter(
			new Options { InputPath = "C:\\Input", OutputPath = "C:\\Output" },
			CompressionKind.None,
			enableIndex: false);
		var settingsField = typeof(SceneExporter).GetField("_jsonSettings", BindingFlags.Instance | BindingFlags.NonPublic);
		settingsField.Should().NotBeNull();
		var settings = (JsonSerializerSettings)settingsField!.GetValue(exporter)!;

		var record = new SceneRecord
		{
			Name = "TestScene",
			SceneGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
			ScenePath = "Assets/TestScene.unity",
			ExportedAt = "2026-03-08T00:00:00Z",
			Version = "2021.3.0f1",
			Platform = "StandaloneWindows64",
			SceneCollectionCount = 1,
			PrefabInstanceCount = 0,
			StrippedAssetCount = 0,
			HiddenAssetCount = 0,
			HasSceneRoots = false
		};

		// Act
		string json = JsonConvert.SerializeObject(record, settings);

		// Assert
		json.Should().Contain("\"prefabInstanceCount\":0");
		json.Should().Contain("\"strippedAssetCount\":0");
		json.Should().Contain("\"hiddenAssetCount\":0");
		json.Should().Contain("\"hasSceneRoots\":false");
	}
}
