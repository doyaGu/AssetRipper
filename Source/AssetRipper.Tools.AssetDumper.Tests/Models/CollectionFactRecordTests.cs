using AssetRipper.Tools.AssetDumper.Models;

namespace AssetRipper.Tools.AssetDumper.Tests.Models;

/// <summary>
/// Unit tests for CollectionFactRecord model class.
/// Tests field assignment, nullable handling, and dependency indices logic.
/// </summary>
public class CollectionFactRecordTests
{
	[Fact]
	public void CollectionFactRecord_AllFieldsAssignable_ShouldSucceed()
	{
		// Arrange & Act
		var record = new CollectionFactRecord
		{
			Domain = "collections",
			CollectionId = "A1B2C3D4",
			Name = "TestCollection",
			CollectionType = "Serialized",
			FriendlyName = "Test Collection",
			FilePath = "Assets/Test.asset",
			BundleName = "TestBundle",
			Platform = "StandaloneWindows64",
			UnityVersion = "2020.3.0f1",
			OriginalUnityVersion = "2019.4.0f1",
			FormatVersion = 22,
			Endian = "LittleEndian",
			FlagsRaw = "0x00000001",
			Flags = new List<string> { "Flag1" },
			IsSceneCollection = true,
			Bundle = new BundleRef { BundlePk = "00000001", BundleName = "TestBundle" },
			Scene = new SceneRef { SceneName = "TestScene" },
			CollectionIndex = 0,
			Dependencies = new List<string> { "A1B2C3D4", "B2C3D4E5" },
			DependencyIndices = new Dictionary<string, int> { { "A1B2C3D4", 0 }, { "B2C3D4E5", 1 } },
			AssetCount = 100,
			Source = new CollectionSourceRecord { NormalizedPath = "/normalized/path" },
			Unity = new CollectionUnityRecord { Version = "2020.3.0f1" }
		};

		// Assert
		record.Domain.Should().Be("collections");
		record.CollectionId.Should().Be("A1B2C3D4");
		record.Name.Should().Be("TestCollection");
		record.CollectionType.Should().Be("Serialized");
		record.AssetCount.Should().Be(100);
	}

	[Theory]
	[InlineData("Serialized")]
	[InlineData("Processed")]
	[InlineData("Virtual")]
	public void CollectionType_WithValidTypes_ShouldAssign(string collectionType)
	{
		// Arrange & Act
		var record = new CollectionFactRecord
		{
			CollectionType = collectionType
		};

		// Assert
		record.CollectionType.Should().Be(collectionType);
	}

	[Fact]
	public void OriginalUnityVersion_WhenNull_ShouldBeNullable()
	{
		// Arrange & Act
		var record = new CollectionFactRecord
		{
			UnityVersion = "2020.3.0f1",
			OriginalUnityVersion = null // Same as current, should be null
		};

		// Assert
		record.OriginalUnityVersion.Should().BeNull();
	}

	[Fact]
	public void Dependencies_WithEmptyString_ShouldHandleUnresolvedDependencies()
	{
		// Arrange - Index 1 is unresolved (empty string placeholder)
		var record = new CollectionFactRecord
		{
			Dependencies = new List<string> { "A1B2C3D4", "", "B2C3D4E5" },
			DependencyIndices = new Dictionary<string, int> { { "A1B2C3D4", 0 }, { "B2C3D4E5", 2 } }
		};

		// Act & Assert
		record.Dependencies.Should().HaveCount(3);
		record.Dependencies[1].Should().BeEmpty();
		record.DependencyIndices.Should().HaveCount(2); // Empty string not indexed
	}

	[Fact]
	public void DependencyIndices_SelfReference_ShouldBeIndexZero()
	{
		// Arrange - Unity convention: index 0 is always self-reference
		var record = new CollectionFactRecord
		{
			CollectionId = "A1B2C3D4",
			Dependencies = new List<string> { "A1B2C3D4", "B2C3D4E5", "C3D4E5F6" },
			DependencyIndices = new Dictionary<string, int> 
			{ 
				{ "A1B2C3D4", 0 }, // Self-reference at index 0
				{ "B2C3D4E5", 1 },
				{ "C3D4E5F6", 2 }
			}
		};

		// Act & Assert
		record.DependencyIndices["A1B2C3D4"].Should().Be(0);
		record.Dependencies[0].Should().Be(record.CollectionId);
	}

	[Fact]
	public void FormatVersion_OnlyForSerializedCollections_ShouldBeNullable()
	{
		// Arrange - Processed/Virtual collections have no FormatVersion
		var processedRecord = new CollectionFactRecord
		{
			CollectionType = "Processed",
			FormatVersion = null
		};

		var serializedRecord = new CollectionFactRecord
		{
			CollectionType = "Serialized",
			FormatVersion = 22
		};

		// Assert
		processedRecord.FormatVersion.Should().BeNull();
		serializedRecord.FormatVersion.Should().Be(22);
	}

	[Fact]
	public void Bundle_ShouldContainBundlePkAndName()
	{
		// Arrange
		var record = new CollectionFactRecord
		{
			Bundle = new BundleRef 
			{ 
				BundlePk = "00000001", 
				BundleName = "TestBundle" 
			}
		};

		// Assert
		record.Bundle.Should().NotBeNull();
		record.Bundle.BundlePk.Should().Be("00000001");
		record.Bundle.BundleName.Should().Be("TestBundle");
	}

	[Fact]
	public void Scene_ForSceneCollections_ShouldBePresent()
	{
		// Arrange
		var sceneRecord = new CollectionFactRecord
		{
			IsSceneCollection = true,
			Scene = new SceneRef { SceneName = "MainScene" }
		};

		var nonSceneRecord = new CollectionFactRecord
		{
			IsSceneCollection = false,
			Scene = null
		};

		// Assert
		sceneRecord.Scene.Should().NotBeNull();
		sceneRecord.Scene!.SceneName.Should().Be("MainScene");
		nonSceneRecord.Scene.Should().BeNull();
	}

	[Fact]
	public void CollectionIndex_ShouldReflectPositionInBundle()
	{
		// Arrange
		var record = new CollectionFactRecord
		{
			CollectionIndex = 3 // 4th collection in bundle (0-indexed)
		};

		// Assert
		record.CollectionIndex.Should().Be(3);
	}
}
