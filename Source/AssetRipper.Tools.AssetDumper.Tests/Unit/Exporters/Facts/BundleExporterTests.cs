using System.IO;
using System.Reflection;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Facts;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Exporters.Facts;

public class BundleExporterTests : IDisposable
{
	private readonly string _testOutputPath;

	public BundleExporterTests()
	{
		_testOutputPath = Path.Combine(Path.GetTempPath(), $"AssetDumperTests_{Guid.NewGuid():N}");
		Directory.CreateDirectory(_testOutputPath);
	}

	public void Dispose()
	{
		if (Directory.Exists(_testOutputPath))
		{
			try
			{
				Directory.Delete(_testOutputPath, recursive: true);
			}
			catch
			{
				// Ignore cleanup errors.
			}
		}
	}

	[Fact]
	public void Constructor_ShouldSerializeEmptyChildBundlePks()
	{
		// Arrange
		var exporter = new BundleExporter(
			new Options { InputPath = "C:\\Input", OutputPath = _testOutputPath, Quiet = true },
			CompressionKind.None,
			enableIndex: false);
		var settingsField = typeof(BundleExporter).GetField("_jsonSettings", BindingFlags.Instance | BindingFlags.NonPublic);
		settingsField.Should().NotBeNull();
		var settings = (JsonSerializerSettings)settingsField!.GetValue(exporter)!;

		var record = new BundleRecord
		{
			Pk = "bundle-1",
			Name = "LeafBundle",
			BundleType = "ProcessedBundle",
			IsRoot = false,
			HierarchyDepth = 1,
			HierarchyPath = "GameBundle/LeafBundle",
			ChildBundlePks = new List<string>(),
			DirectCollectionCount = 0,
			TotalCollectionCount = 0,
			DirectSceneCollectionCount = 0,
			TotalSceneCollectionCount = 0,
			DirectChildBundleCount = 0,
			TotalChildBundleCount = 0,
			DirectResourceCount = 0,
			TotalResourceCount = 0,
			DirectFailedFileCount = 0,
			TotalFailedFileCount = 0,
			DirectAssetCount = 0,
			TotalAssetCount = 0
		};

		// Act
		string json = JsonConvert.SerializeObject(record, settings);

		// Assert
		json.Should().Contain("\"childBundlePks\":[]");
	}
}
