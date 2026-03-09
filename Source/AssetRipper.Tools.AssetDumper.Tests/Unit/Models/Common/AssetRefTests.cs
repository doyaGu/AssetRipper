using AssetRipper.Tools.AssetDumper.Models.Common;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Models.Common;

public class AssetRefTests
{
	[Fact]
	public void Serialize_ShouldUseSchemaFieldNames()
	{
		// Arrange
		var assetRef = new AssetRef("sharedassets0.assets", 42);

		// Act
		string json = JsonConvert.SerializeObject(assetRef);

		// Assert
		json.Should().Contain("\"collectionId\":\"sharedassets0.assets\"");
		json.Should().Contain("\"pathId\":42");
		json.Should().NotContain("\"c\":");
		json.Should().NotContain("\"p\":");
	}
}
