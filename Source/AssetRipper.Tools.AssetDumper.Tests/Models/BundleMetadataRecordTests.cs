using AssetRipper.Tools.AssetDumper.Models;

namespace AssetRipper.Tools.AssetDumper.Tests.Models;

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
		var record = new BundleMetadataRecord
		{
			Domain = "bundles",
			BundlePk = "00000001",
			Name = "RootBundle",
			BundleType = "GameBundle",
			ParentPk = null,  // Root has no parent
			IsRoot = true,
			HierarchyDepth = 0,
			HierarchyPath = "/",
			ChildBundlePks = new List<string> { "00000002", "00000003" },
			ChildBundleNames = new List<string> { "Level1", "Level2" },
			BundleIndex = null,  // Root has no index
			AncestorPath = "",
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
		var record = new BundleMetadataRecord
		{
			Domain = "bundles",
			BundlePk = "00000002",
			Name = "Level1",
			BundleType = "Bundle",
			ParentPk = "00000001",  // References root
			IsRoot = false,
			HierarchyDepth = 1,
			HierarchyPath = "/Level1",
			BundleIndex = 0,  // First child of parent
			AncestorPath = "00000001",
			CollectionIds = new List<string> { "A1B2C3D4" }
		};

		// Assert
		record.IsRoot.Should().BeFalse();
		record.ParentPk.Should().Be("00000001");
		record.BundleIndex.Should().Be(0);
		record.HierarchyDepth.Should().Be(1);
		record.AncestorPath.Should().Be("00000001");
	}

	[Fact]
	public void BundleMetadataRecord_NestedBundle_ShouldHaveFullAncestorPath()
	{
		// Arrange - Bundle nested 3 levels deep
		var record = new BundleMetadataRecord
		{
			BundlePk = "00000004",
			Name = "Sublevel",
			ParentPk = "00000003",
			IsRoot = false,
			HierarchyDepth = 3,
			HierarchyPath = "/Level1/Level2/Sublevel",
			BundleIndex = 0,
			AncestorPath = "00000001/00000002/00000003"  // Full lineage
		};

		// Assert
		record.HierarchyDepth.Should().Be(3);
		record.AncestorPath.Should().Contain("/");
		record.AncestorPath.Split('/').Should().HaveCount(3);
	}

	[Fact]
	public void ChildBundlePks_ShouldCorrespondToChildBundleNames()
	{
		// Arrange
		var record = new BundleMetadataRecord
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
		var firstChild = new BundleMetadataRecord
		{
			Name = "Level1",
			BundleIndex = 0,
			ParentPk = "00000001"
		};

		var secondChild = new BundleMetadataRecord
		{
			Name = "Level2",
			BundleIndex = 1,
			ParentPk = "00000001"
		};

		var thirdChild = new BundleMetadataRecord
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
		var record = new BundleMetadataRecord
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
		var record = new BundleMetadataRecord
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
		var record = new BundleMetadataRecord
		{
			Resources = new List<string> 
			{ 
				"resource.assets", 
				"resource.resS" 
			}
		};

		// Assert
		record.Resources.Should().HaveCount(2);
	}

	[Fact]
	public void FailedFiles_ShouldTrackLoadFailures()
	{
		// Arrange
		var record = new BundleMetadataRecord
		{
			FailedFiles = new List<string> 
			{ 
				"corrupted.assets" 
			}
		};

		// Assert
		record.FailedFiles.Should().HaveCount(1);
		record.FailedFiles[0].Should().Be("corrupted.assets");
	}

	[Fact]
	public void Scenes_ShouldListSceneNames()
	{
		// Arrange
		var record = new BundleMetadataRecord
		{
			Scenes = new List<string> 
			{ 
				"MainScene", 
				"SubScene" 
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
		var record = new BundleMetadataRecord
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
		var record = new BundleMetadataRecord
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
		var record = new BundleMetadataRecord
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
		var rootBundle = new BundleMetadataRecord
		{
			HierarchyDepth = 0,
			HierarchyPath = "/"
		};

		var level1Bundle = new BundleMetadataRecord
		{
			HierarchyDepth = 1,
			HierarchyPath = "/Level1"
		};

		var level2Bundle = new BundleMetadataRecord
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
