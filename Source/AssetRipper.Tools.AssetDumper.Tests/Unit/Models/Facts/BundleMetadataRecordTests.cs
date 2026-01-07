using AssetRipper.Tools.AssetDumper.Models.Facts;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Models.Facts;

/// <summary>
/// Unit tests for BundleMetadataRecord model class.
/// Tests bundle hierarchy fields: childBundlePks, ancestorPath, hierarchyPath, etc.
/// </summary>
public class BundleMetadataRecordTests
{
	[Fact]
	public void BundleMetadataRecord_RootBundle_ShouldHaveExpectedFields()
	{
		// Arrange & Act
		var record = new BundleRecord
		{
			Domain = "bundles",
			Pk = "00000001",
			Name = "RootBundle",
			BundleType = "GameBundle",
			ParentPk = null,  // Root has no parent
			IsRoot = true,
			HierarchyDepth = 0,
			HierarchyPath = "/",
			ChildBundlePks = new List<string> { "00000002", "00000003" },
			ChildBundleNames = new List<string> { "Level1", "Level2" },
			BundleIndex = null,  // Root has no index
			AncestorPath = new List<string>(),
			CollectionIds = new List<string> { "A1B2C3D4", "B2C3D4E5" }
		};

		// Assert
		record.IsRoot.Should().BeTrue();
		record.ParentPk.Should().BeNull();
		record.BundleIndex.Should().BeNull();
		record.HierarchyDepth.Should().Be(0);
		record.HierarchyPath.Should().Be("/");
		record.AncestorPath.Should().BeEmpty();
		record.ChildBundlePks.Should().HaveCount(2);
	}

	[Fact]
	public void BundleMetadataRecord_ChildBundle_ShouldHaveParentReference()
	{
		// Arrange & Act
		var record = new BundleRecord
		{
			Domain = "bundles",
			Pk = "00000002",
			Name = "Level1",
			BundleType = "Bundle",
			ParentPk = "00000001",  // References root
			IsRoot = false,
			HierarchyDepth = 1,
			HierarchyPath = "/Level1",
			BundleIndex = 0,  // First child of parent
			AncestorPath = new List<string> { "00000001" },
			CollectionIds = new List<string> { "A1B2C3D4" }
		};

		// Assert
		record.IsRoot.Should().BeFalse();
		record.ParentPk.Should().Be("00000001");
		record.BundleIndex.Should().Be(0);
		record.HierarchyDepth.Should().Be(1);
		record.AncestorPath.Should().ContainSingle().Which.Should().Be("00000001");
	}

	[Fact]
	public void BundleMetadataRecord_NestedBundle_ShouldHaveFullAncestorPath()
	{
		// Arrange - Bundle nested 3 levels deep
		var record = new BundleRecord
		{
			Pk = "00000004",
			Name = "Sublevel",
			ParentPk = "00000003",
			IsRoot = false,
			HierarchyDepth = 3,
			HierarchyPath = "/Level1/Level2/Sublevel",
			BundleIndex = 0,
			AncestorPath = new List<string> { "00000001", "00000002", "00000003" }  // Full lineage
		};

		// Assert
		record.HierarchyDepth.Should().Be(3);
		record.AncestorPath.Should().HaveCount(3);
	}

	[Fact]
	public void ChildBundlePks_ShouldCorrespondToChildBundleNames()
	{
		// Arrange
		var record = new BundleRecord
		{
			ChildBundlePks = new List<string> { "00000002", "00000003", "00000004" },
			ChildBundleNames = new List<string> { "Level1", "Level2", "SharedAssets" }
		};

		// Assert
		record.ChildBundlePks.Should().HaveCount(3);
		record.ChildBundleNames.Should().HaveCount(3);
		record.ChildBundlePks.Count.Should().Be(record.ChildBundleNames.Count);
	}

	[Fact]
	public void BundleIndex_ShouldReflectPositionInParent()
	{
		// Arrange - Multiple siblings
		var firstChild = new BundleRecord
		{
			Name = "Level1",
			BundleIndex = 0,
			ParentPk = "00000001"
		};

		var secondChild = new BundleRecord
		{
			Name = "Level2",
			BundleIndex = 1,
			ParentPk = "00000001"
		};

		var thirdChild = new BundleRecord
		{
			Name = "Level3",
			BundleIndex = 2,
			ParentPk = "00000001"
		};

		// Assert
		firstChild.BundleIndex.Should().Be(0);
		secondChild.BundleIndex.Should().Be(1);
		thirdChild.BundleIndex.Should().Be(2);
	}

	[Fact]
	public void HierarchyPath_ShouldBuildFromRoot()
	{
		// Arrange - Path shows full hierarchy
		var record = new BundleRecord
		{
			HierarchyPath = "/RootBundle/Level1/Sublevel"
		};

		// Assert
		record.HierarchyPath.Should().StartWith("/");
		record.HierarchyPath.Split('/').Should().HaveCountGreaterThan(1);
	}

	[Fact]
	public void CollectionIds_ShouldListAllCollections()
	{
		// Arrange
		var record = new BundleRecord
		{
			CollectionIds = new List<string> 
			{ 
				"A1B2C3D4", 
				"B2C3D4E5", 
				"C3D4E5F6" 
			}
		};

		// Assert
		record.CollectionIds.Should().HaveCount(3);
		record.CollectionIds.Should().OnlyHaveUniqueItems();
	}

	[Fact]
	public void Resources_ShouldListResourceFiles()
	{
		// Arrange
		var record = new BundleRecord
		{
			Resources = new List<BundleResourceRecord>
			{
				new BundleResourceRecord { Name = "resource.assets" },
				new BundleResourceRecord { Name = "resource.resS" }
			}
		};

		// Assert
		record.Resources.Should().HaveCount(2);
	}

	[Fact]
	public void FailedFiles_ShouldTrackLoadFailures()
	{
		// Arrange
		var record = new BundleRecord
		{
			FailedFiles = new List<BundleFailedFileRecord>
			{
				new BundleFailedFileRecord { Name = "corrupted.assets" }
			}
		};

		// Assert
		record.FailedFiles.Should().HaveCount(1);
		record.FailedFiles![0].Name.Should().Be("corrupted.assets");
	}

	[Fact]
	public void Scenes_ShouldListSceneNames()
	{
		// Arrange
		var record = new BundleRecord
		{
			Scenes = new List<SceneRefRecord>
			{
				new SceneRefRecord { SceneGuid = "guid1", SceneName = "MainScene" },
				new SceneRefRecord { SceneGuid = "guid2", SceneName = "SubScene" }
			}
		};

		// Assert
		record.Scenes.Should().HaveCount(2);
	}

	[Theory]
	[InlineData("GameBundle")]
	[InlineData("Bundle")]
	[InlineData("AssetBundle")]
	public void BundleType_ShouldReflectUnityBundleType(string bundleType)
	{
		// Arrange
		var record = new BundleRecord
		{
			BundleType = bundleType
		};

		// Assert
		record.BundleType.Should().Be(bundleType);
	}

	[Fact]
	public void ConditionalValidation_RootBundle_ShouldNotRequireParentPk()
	{
		// Arrange - Root bundle
		var record = new BundleRecord
		{
			IsRoot = true,
			ParentPk = null,
			BundleIndex = null
		};

		// Assert
		record.ParentPk.Should().BeNull();
		record.BundleIndex.Should().BeNull();
	}

	[Fact]
	public void ConditionalValidation_NonRootBundle_ShouldRequireParentPkAndIndex()
	{
		// Arrange - Non-root bundle must have parent and index
		var record = new BundleRecord
		{
			IsRoot = false,
			ParentPk = "00000001",
			BundleIndex = 0
		};

		// Assert
		record.ParentPk.Should().NotBeNull();
		record.BundleIndex.Should().NotBeNull();
	}

	[Fact]
	public void HierarchyDepth_ShouldMatchPathLevel()
	{
		// Arrange - Test depth consistency
		var rootBundle = new BundleRecord
		{
			HierarchyDepth = 0,
			HierarchyPath = "/"
		};

		var level1Bundle = new BundleRecord
		{
			HierarchyDepth = 1,
			HierarchyPath = "/Level1"
		};

		var level2Bundle = new BundleRecord
		{
			HierarchyDepth = 2,
			HierarchyPath = "/Level1/Sublevel"
		};

		// Assert
		rootBundle.HierarchyPath.Count(c => c == '/').Should().Be(1);  // Just root slash
		level1Bundle.HierarchyPath.Count(c => c == '/').Should().Be(1);  // Root + 1 level
		level2Bundle.HierarchyPath.Count(c => c == '/').Should().Be(2);  // Root + 2 levels
	}
}
