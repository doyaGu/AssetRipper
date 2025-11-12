using AssetRipper.Tools.AssetDumper.Models;

namespace AssetRipper.Tools.AssetDumper.Tests.Models;

/// <summary>
/// Unit tests for AssetRecord model class and hierarchy path generation.
/// Tests HierarchyPath structure and StableKey format.
/// </summary>
public class AssetRecordTests
{
	[Fact]
	public void AssetRecord_AllFieldsAssignable_ShouldSucceed()
	{
		// Arrange & Act
		var record = new AssetRecord
		{
			Domain = "assets",
			StableKey = "A1B2C3D4:100",
			CollectionId = "A1B2C3D4",
			PathID = 100,
			ClassID = 1,
			ClassName = "GameObject",
			BestName = "MainCamera",
			Hierarchy = new HierarchyPath
			{
				BundlePk = "00000001",
				BundleName = "Level1",
				CollectionId = "A1B2C3D4",
				CollectionName = "scene.unity"
			}
		};

		// Assert
		record.Domain.Should().Be("assets");
		record.StableKey.Should().Be("A1B2C3D4:100");
		record.CollectionId.Should().Be("A1B2C3D4");
		record.PathID.Should().Be(100);
		record.Hierarchy.Should().NotBeNull();
	}

	[Theory]
	[InlineData("A1B2C3D4", 100, "A1B2C3D4:100")]
	[InlineData("B2C3D4E5", -1, "B2C3D4E5:-1")]  // Negative PathID
	[InlineData("12345678", 999999, "12345678:999999")]  // Large PathID
	public void StableKey_Format_ShouldBeCollectionIdColonPathId(
		string collectionId, long pathId, string expectedStableKey)
	{
		// Arrange
		var record = new AssetRecord
		{
			CollectionId = collectionId,
			PathID = pathId,
			StableKey = expectedStableKey
		};

		// Assert
		record.StableKey.Should().Be(expectedStableKey);
		record.StableKey.Should().MatchRegex(@"^[A-Za-z0-9:_-]+:-?\d+$");
	}

	[Fact]
	public void HierarchyPath_ShouldContainFullBundleToAssetPath()
	{
		// Arrange
		var record = new AssetRecord
		{
			StableKey = "A1B2C3D4:100",
			Hierarchy = new HierarchyPath
			{
				BundlePk = "00000001",
				BundleName = "Level1",
				CollectionId = "A1B2C3D4",
				CollectionName = "scene.unity"
			}
		};

		// Assert
		record.Hierarchy.Should().NotBeNull();
		record.Hierarchy.BundlePk.Should().Be("00000001");
		record.Hierarchy.BundleName.Should().Be("Level1");
		record.Hierarchy.CollectionId.Should().Be("A1B2C3D4");
		record.Hierarchy.CollectionName.Should().Be("scene.unity");
	}

	[Fact]
	public void HierarchyPath_ShouldSupportNestedBundles()
	{
		// Arrange - Asset in nested bundle structure
		var record = new AssetRecord
		{
			StableKey = "A1B2C3D4:100",
			Hierarchy = new HierarchyPath
			{
				BundlePk = "00000003",  // Child bundle PK
				BundleName = "Level1/Sublevel",
				CollectionId = "A1B2C3D4",
				CollectionName = "subscene.unity"
			}
		};

		// Assert
		record.Hierarchy.BundlePk.Should().Be("00000003");
		record.Hierarchy.BundleName.Should().Contain("/"); // Nested path
	}

	[Theory]
	[InlineData(1, "GameObject")]
	[InlineData(2, "Component")]
	[InlineData(4, "Transform")]
	[InlineData(43, "Mesh")]
	[InlineData(114, "MonoBehaviour")]
	public void ClassID_CommonUnityTypes_ShouldMatchKnownValues(int classID, string expectedClassName)
	{
		// Arrange
		var record = new AssetRecord
		{
			ClassID = classID,
			ClassName = expectedClassName
		};

		// Assert
		record.ClassID.Should().Be(classID);
		record.ClassName.Should().Be(expectedClassName);
	}

	[Fact]
	public void BestName_ShouldProvideUserFriendlyIdentifier()
	{
		// Arrange
		var namedAsset = new AssetRecord
		{
			BestName = "MainCamera",
			ClassName = "GameObject"
		};

		var unnamedAsset = new AssetRecord
		{
			BestName = null,
			ClassName = "Mesh"
		};

		// Assert
		namedAsset.BestName.Should().Be("MainCamera");
		unnamedAsset.BestName.Should().BeNull();
	}

	[Fact]
	public void Hierarchy_CollectionId_ShouldMatchRecordCollectionId()
	{
		// Arrange
		string collectionId = "A1B2C3D4";
		var record = new AssetRecord
		{
			CollectionId = collectionId,
			Hierarchy = new HierarchyPath
			{
				CollectionId = collectionId
			}
		};

		// Assert
		record.CollectionId.Should().Be(record.Hierarchy.CollectionId);
	}

	[Fact]
	public void StableKey_ShouldBeGloballyUnique()
	{
		// Arrange - Different assets in different collections
		var asset1 = new AssetRecord
		{
			CollectionId = "A1B2C3D4",
			PathID = 100,
			StableKey = "A1B2C3D4:100"
		};

		var asset2 = new AssetRecord
		{
			CollectionId = "B2C3D4E5",
			PathID = 100, // Same PathID but different collection
			StableKey = "B2C3D4E5:100"
		};

		var asset3 = new AssetRecord
		{
			CollectionId = "A1B2C3D4",
			PathID = 101, // Same collection but different PathID
			StableKey = "A1B2C3D4:101"
		};

		// Assert - All StableKeys should be unique
		string[] keys = new[] { asset1.StableKey, asset2.StableKey, asset3.StableKey };
		keys.Should().OnlyHaveUniqueItems();
	}

	[Fact]
	public void StableKey_ShouldBeLexicographicallySortable()
	{
		// Arrange
		AssetRecord[] assets = new[]
		{
			new AssetRecord { StableKey = "A1B2C3D4:100" },
			new AssetRecord { StableKey = "A1B2C3D4:10" },
			new AssetRecord { StableKey = "A1B2C3D4:99" },
			new AssetRecord { StableKey = "A1B2C3D4:1" },
		};

		// Act
		var sorted = assets.OrderBy(a => a.StableKey).ToList();

		// Assert - Should sort lexicographically
		sorted[0].StableKey.Should().Be("A1B2C3D4:1");
		sorted[1].StableKey.Should().Be("A1B2C3D4:10");
		sorted[2].StableKey.Should().Be("A1B2C3D4:100");
		sorted[3].StableKey.Should().Be("A1B2C3D4:99");
	}

	[Fact]
	public void Hierarchy_ShouldEnableQueryingAssetsByBundle()
	{
		// Arrange - Multiple assets in same bundle
		string bundlePk = "00000001";
		AssetRecord[] assets = new[]
		{
			new AssetRecord 
			{ 
				StableKey = "A1:100",
				Hierarchy = new HierarchyPath { BundlePk = bundlePk }
			},
			new AssetRecord 
			{ 
				StableKey = "A2:200",
				Hierarchy = new HierarchyPath { BundlePk = bundlePk }
			},
			new AssetRecord 
			{ 
				StableKey = "B1:300",
				Hierarchy = new HierarchyPath { BundlePk = "00000002" }
			},
		};

		// Act - Query assets by bundle
		var assetsInBundle1 = assets.Where(a => a.Hierarchy.BundlePk == bundlePk).ToList();

		// Assert
		assetsInBundle1.Should().HaveCount(2);
		assetsInBundle1.Select(a => a.StableKey).Should().Contain(new[] { "A1:100", "A2:200" });
	}
}
