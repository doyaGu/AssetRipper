using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Models.Common;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Models.Facts;

/// <summary>
/// Unit tests for SceneRecord model class.
/// Tests primaryCollectionId logic, collectionDetails, and optional hierarchy fields.
/// </summary>
public class SceneRecordTests
{
	[Fact]
	public void SceneRecord_AllFieldsAssignable_ShouldSucceed()
	{
		// Arrange & Act
		var record = new SceneRecord
		{
			Domain = "scenes",
			Name = "MainScene",
			SceneGuid = "abc123",
			PrimaryCollectionId = "A1B2C3D4",
			Bundle = new BundleRef { BundlePk = "00000001", BundleName = "Level1" },
			PathId = 1,
			ClassId = 142,
			ClassName = "SceneAsset",
			GameObjectCount = 100,
			ComponentCount = 500,
			HasSceneRoots = true,
			CollectionDetails = new List<SceneCollectionDetail>
			{
				new SceneCollectionDetail
				{
					CollectionId = "A1B2C3D4",
					Bundle = new BundleRef { BundlePk = "00000001", BundleName = "Level1" },
					IsPrimary = true,
					AssetCount = 1234
				}
			}
		};

		// Assert
		record.Domain.Should().Be("scenes");
		record.Name.Should().Be("MainScene");
		record.PrimaryCollectionId.Should().Be("A1B2C3D4");
		record.CollectionDetails.Should().HaveCount(1);
	}

	[Fact]
	public void PrimaryCollectionId_ShouldReferenceFirstCollection()
	{
		// Arrange - Primary collection is the first in Collections list
		var record = new SceneRecord
		{
			PrimaryCollectionId = "A1B2C3D4",
			CollectionDetails = new List<SceneCollectionDetail>
			{
				new SceneCollectionDetail
				{
					CollectionId = "A1B2C3D4",
					IsPrimary = true
				},
				new SceneCollectionDetail
				{
					CollectionId = "B2C3D4E5",
					IsPrimary = false
				}
			}
		};

		// Act
		var primaryDetail = record.CollectionDetails.FirstOrDefault(d => d.IsPrimary == true);

		// Assert
		primaryDetail.Should().NotBeNull();
		primaryDetail!.CollectionId.Should().Be(record.PrimaryCollectionId);
		record.CollectionDetails[0].CollectionId.Should().Be(record.PrimaryCollectionId);
	}

	[Fact]
	public void CollectionDetails_CrossBundleScene_ShouldHandleMultipleBundles()
	{
		// Arrange - Scene spanning multiple bundles
		var record = new SceneRecord
		{
			PrimaryCollectionId = "A1B2C3D4",
			CollectionDetails = new List<SceneCollectionDetail>
			{
				new SceneCollectionDetail
				{
					CollectionId = "A1B2C3D4",
					Bundle = new BundleRef { BundlePk = "00000001", BundleName = "Level1" },
					IsPrimary = true,
					AssetCount = 1234
				},
				new SceneCollectionDetail
				{
					CollectionId = "B2C3D4E5",
					Bundle = new BundleRef { BundlePk = "00000002", BundleName = "SharedAssets" },
					IsPrimary = false,
					AssetCount = 567
				}
			}
		};

		// Assert
		record.CollectionDetails.Should().HaveCount(2);
		record.CollectionDetails[0].Bundle.BundlePk.Should().Be("00000001");
		record.CollectionDetails[1].Bundle.BundlePk.Should().Be("00000002");
		record.CollectionDetails.Select(d => d.Bundle.BundlePk).Should().OnlyHaveUniqueItems();
	}

	[Fact]
	public void OptionalHierarchyFields_BeforeProcessing_ShouldBeNullable()
	{
		// Arrange - Before SceneHierarchyObject creation
		var record = new SceneRecord
		{
			Name = "UnprocessedScene",
			PathId = null,
			ClassId = null,
			ClassName = null,
			GameObjects = null,
			Components = null
		};

		// Assert
		record.PathId.Should().BeNull();
		record.ClassId.Should().BeNull();
		record.ClassName.Should().BeNull();
		record.GameObjects.Should().BeNull();
		record.Components.Should().BeNull();
	}

	[Fact]
	public void OptionalHierarchyFields_AfterProcessing_ShouldHaveValues()
	{
		// Arrange - After SceneHierarchyObject creation
		var record = new SceneRecord
		{
			Name = "ProcessedScene",
			PathId = 1,
			ClassId = 142,
			ClassName = "SceneAsset",
			GameObjectCount = 50,
			ComponentCount = 200,
			GameObjects = new List<AssetRef>
			{
				new AssetRef("A1B2C3D4", 100),
				new AssetRef("A1B2C3D4", 101)
			},
			Components = new List<AssetRef>
			{
				new AssetRef("A1B2C3D4", 200),
				new AssetRef("A1B2C3D4", 201)
			}
		};

		// Assert
		record.PathId.Should().Be(1);
		record.ClassId.Should().Be(142);
		record.ClassName.Should().Be("SceneAsset");
		record.GameObjects.Should().HaveCount(2);
		record.Components.Should().HaveCount(2);
	}

	[Fact]
	public void MinimalOutput_ShouldOmitObjectLists()
	{
		// Arrange - MinimalOutput mode (lists not populated)
		var record = new SceneRecord
		{
			Name = "MinimalScene",
			GameObjectCount = 1000, // Count still present
			ComponentCount = 5000,   // Count still present
			GameObjects = null,      // List omitted
			Components = null        // List omitted
		};

		// Assert
		record.GameObjectCount.Should().Be(1000);
		record.ComponentCount.Should().Be(5000);
		record.GameObjects.Should().BeNull();
		record.Components.Should().BeNull();
	}

	[Fact]
	public void HasSceneRoots_ShouldIndicateRootObjectPresence()
	{
		// Arrange
		var recordWithRoots = new SceneRecord
		{
			HasSceneRoots = true,
			SceneRoots = new List<AssetRef>
			{
				new AssetRef("A1B2C3D4", 1)
			}
		};

		var recordWithoutRoots = new SceneRecord
		{
			HasSceneRoots = false,
			SceneRoots = null
		};

		// Assert
		recordWithRoots.HasSceneRoots.Should().BeTrue();
		recordWithRoots.SceneRoots.Should().NotBeNull();
		recordWithoutRoots.HasSceneRoots.Should().BeFalse();
		recordWithoutRoots.SceneRoots.Should().BeNull();
	}

	[Fact]
	public void CollectionDetails_AssetCount_ShouldSumToTotal()
	{
		// Arrange
		var record = new SceneRecord
		{
			CollectionDetails = new List<SceneCollectionDetail>
			{
				new SceneCollectionDetail { AssetCount = 1000 },
				new SceneCollectionDetail { AssetCount = 500 },
				new SceneCollectionDetail { AssetCount = 300 }
			}
		};

		// Act
		int totalAssets = record.CollectionDetails.Sum(d => d.AssetCount ?? 0);

		// Assert
		totalAssets.Should().Be(1800);
	}

	[Fact]
	public void Bundle_ShouldReferenceTopLevelBundle()
	{
		// Arrange
		var record = new SceneRecord
		{
			PrimaryCollectionId = "A1B2C3D4",
			Bundle = new BundleRef { BundlePk = "00000001", BundleName = "Level1" }
		};

		// Assert
		record.Bundle.Should().NotBeNull();
		record.Bundle.BundlePk.Should().Be("00000001");
		record.Bundle.BundleName.Should().Be("Level1");
	}
}
