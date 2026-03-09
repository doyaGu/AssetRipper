using System.IO;
using System.Reflection;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Exporters.Facts;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Exporters.Facts;

public class ScriptMetadataExporterTests : IDisposable
{
	private readonly string _testOutputPath;

	public ScriptMetadataExporterTests()
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
	public void Constructor_ShouldSerializeIsPresentWhenFalse()
	{
		// Arrange
		var exporter = new ScriptMetadataExporter(
			new Options { InputPath = "C:\\Input", OutputPath = _testOutputPath, Quiet = true },
			CompressionKind.None,
			enableIndex: false);
		var settingsField = typeof(ScriptMetadataExporter).GetField("_jsonSettings", BindingFlags.Instance | BindingFlags.NonPublic);
		settingsField.Should().NotBeNull();
		var settings = (JsonSerializerSettings)settingsField!.GetValue(exporter)!;

		var record = new ScriptMetadataRecord
		{
			Pk = "collection:1",
			CollectionId = "collection",
			PathId = 1,
			ClassId = 115,
			ClassName = "MonoScript",
			FullName = "Example.MonoScript",
			AssemblyName = "Assembly-CSharp",
			IsPresent = false
		};

		// Act
		string json = JsonConvert.SerializeObject(record, settings);

		// Assert
		json.Should().Contain("\"isPresent\":false");
	}
}
