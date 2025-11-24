# scenes.schema.json Reference

**Schema ID:** `https://schemas.assetripper.dev/assetdump/v2/facts/scenes.schema.json`
**Title:** AssetDump Scenes Records
**Domain:** `scenes`
**Layer:** Facts
**Version:** 2.0

---

## Overview

The `scenes.schema.json` defines per-scene aggregation records capturing Unity scene metadata, GameObject hierarchy, and component statistics. Scenes represent complete Unity scenes with all their GameObjects, components, and dependencies.

### Purpose

- Aggregate scene-level metadata from multiple collections
- Track GameObject hierarchy and component counts
- Link scenes to their constituent collections
- Provide scene statistics (prefabs, managers, stripped assets)
- Reference SceneHierarchyObject and SceneRoots assets

### Key Characteristics

- **Primary Key:** `sceneGuid` (Unity GUID)
- **Domain Identifier:** `"scenes"` (constant)
- **Cardinality:** One record per Unity scene (typically 5-50 per project)
- **Multi-Collection:** Scenes can span multiple collections

---

## Required Fields (19 total)

| Field | Type | Description |
|-------|------|-------------|
| domain | string (const) | Domain identifier: "scenes" |
| name | string | Scene name |
| sceneGuid | UnityGuid | Scene GUID (primary key) |
| scenePath | string | Project-relative path |
| exportedAt | Timestamp | Export timestamp (ISO 8601) |
| version | string | Unity version |
| platform | string | Build platform |
| sceneCollectionCount | integer | Number of collections (min 1) |
| collectionIds | array | All collection IDs (min 1) |
| assetCount | integer | Total assets across collections |
| gameObjectCount | integer | Number of GameObjects |
| componentCount | integer | Number of Components |
| managerCount | integer | Number of LevelGameManagers |
| prefabInstanceCount | integer | Number of PrefabInstances |
| dependencyCount | integer | Number of dependencies |
| rootGameObjectCount | integer | Root GameObjects without parent |
| strippedAssetCount | integer | Assets with stripped types |
| hiddenAssetCount | integer | Hidden assets |
| hasSceneRoots | boolean | Has SceneRoots asset |

---

## Field Reference

### domain

**Type:** `string`
**Const:** `"scenes"`
**Required:** ✓

**Example:**
```json
"domain": "scenes"
```

---

### type

**Type:** `string`
**Const:** `"Scene"`
**Required:** ✗

**Description:**
Type discriminator for scene records.

**Example:**
```json
"type": "Scene"
```

---

### name

**Type:** `string`
**Required:** ✓

**Description:**
Scene name (typically filename without extension).

**Examples:**
```json
"name": "MainMenu"
"name": "Level1"
"name": "LoadingScreen"
```

---

### sceneGuid

**Type:** `UnityGuid`
**Required:** ✓
**Reference:** `core.schema.json#/$defs/UnityGuid`

**Description:**
Unity GUID uniquely identifying the scene across the entire project. More stable than AssetPK as it persists across Unity versions and rebuilds.

**Example:**
```json
"sceneGuid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d"
```

**Characteristics:**
- Globally unique within Unity project
- Persistent across rebuilds
- Assigned by Unity Editor in .meta files

---

### scenePath

**Type:** `string`
**Required:** ✓

**Description:**
Scene path relative to project root (typically Assets/Scenes/...).

**Examples:**
```json
"scenePath": "Assets/Scenes/MainMenu.unity"
"scenePath": "Assets/Levels/Level1.unity"
```

---

### exportedAt

**Type:** `Timestamp`
**Required:** ✓
**Reference:** `core.schema.json#/$defs/Timestamp`

**Description:**
UTC timestamp when the scene was exported in ISO 8601 format.

**Example:**
```json
"exportedAt": "2025-11-16T13:30:00Z"
```

---

### version

**Type:** `string`
**Required:** ✓

**Description:**
Unity version that created/last saved the scene.

**Example:**
```json
"version": "2021.3.5f1"
```

---

### platform

**Type:** `string`
**Required:** ✓

**Description:**
Build platform/target for the scene.

**Examples:**
```json
"platform": "StandaloneWindows64"
"platform": "Android"
"platform": "iOS"
```

---

### flags

**Type:** `string`
**Required:** ✗

**Description:**
SerializedFile flags for the scene collection.

**Example:**
```json
"flags": "0x00000004"
```

---

### endianType

**Type:** `string`
**Required:** ✗

**Description:**
Byte order for the scene's serialized data.

**Examples:**
```json
"endianType": "LittleEndian"
"endianType": "BigEndian"
```

---

### bundleName

**Type:** `string`
**Required:** ✗

**Description:**
Name of the bundle containing the primary scene collection.

**Example:**
```json
"bundleName": "level1"
```

---

### sceneCollectionCount

**Type:** `integer`
**Minimum:** 1
**Required:** ✓

**Description:**
Number of AssetCollections that make up this scene. Scenes can span multiple collections.

**Examples:**
```json
"sceneCollectionCount": 1   // Simple scene
"sceneCollectionCount": 3   // Complex scene with dependencies
```

**Note:** Always ≥ 1 (at least the primary scene collection)

---

### collectionIds

**Type:** `array` of CollectionID
**Min Items:** 1
**Unique Items:** true
**Required:** ✓

**Description:**
All collection IDs that compose this scene. First element is the primary collection.

**Example:**
```json
"collectionIds": [
  "level1",
  "sharedassets0.assets",
  "sharedassets1.assets"
]
```

**Invariant:**
- `collectionIds.length == sceneCollectionCount`
- `collectionIds[0]` is the primary scene collection

---

### collections (DEPRECATED)

**Type:** `array`
**Required:** ✗

**Description:**
**Deprecated:** Use `collectionDetails` instead. Legacy list of collection names.

**Example:**
```json
"collections": [
  {
    "collectionId": "level1",
    "name": "level1"
  }
]
```

---

### primaryCollectionId

**Type:** `CollectionID`
**Required:** ✗
**Reference:** `core.schema.json#/$defs/CollectionID`

**Description:**
The first collection added to this scene, used as the primary collection for export purposes. Equals `collectionIds[0]`.

**Example:**
```json
"primaryCollectionId": "level1"
```

---

### bundle

**Type:** `BundleRef`
**Required:** ✗
**Reference:** `core.schema.json#BundleRef`

**Description:**
The bundle containing the primary (first) collection. **Note:** Different collections may belong to different bundles.

**Example:**
```json
"bundle": {
  "bundlePk": "A1B2C3D4",
  "bundleName": "level1"
}
```

---

### collectionDetails

**Type:** `array` of objects
**Required:** ✗

**Description:**
Detailed metadata for each collection composing this scene, including their respective bundles.

**Item Schema:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| collectionId | CollectionID | ✓ | Collection identifier |
| bundle | BundleRef | ✓ | Bundle containing this collection |
| isPrimary | boolean | ✗ | True if first collection in scene |
| assetCount | integer | ✗ | Number of assets in this collection |

**Example:**
```json
"collectionDetails": [
  {
    "collectionId": "level1",
    "bundle": {
      "bundlePk": "A1B2C3D4",
      "bundleName": "level1"
    },
    "isPrimary": true,
    "assetCount": 1523
  },
  {
    "collectionId": "sharedassets0.assets",
    "bundle": {
      "bundlePk": "E5F6G7H8",
      "bundleName": "sharedassets"
    },
    "isPrimary": false,
    "assetCount": 234
  }
]
```

---

### hierarchy

**Type:** `AssetRef`
**Required:** ✗
**Reference:** `core.schema.json#AssetRef`

**Description:**
Reference to the SceneHierarchyObject asset. Only present after scene processing.

**Example:**
```json
"hierarchy": {
  "collectionId": "level1",
  "pathId": 1
}
```

---

### hierarchyAssetId

**Type:** `StableKey`
**Required:** ✗
**Reference:** `core.schema.json#/$defs/StableKey`

**Description:**
Stable key of the SceneHierarchyObject asset. Only present after scene processing.

**Example:**
```json
"hierarchyAssetId": "level1:1"
```

---

### pathID

**Type:** `integer`
**Required:** ✗

**Description:**
PathID of the SceneHierarchyObject asset. Only present after scene processing.

**Example:**
```json
"pathID": 1
```

---

### classID

**Type:** `integer`
**Required:** ✗

**Description:**
ClassID of the SceneHierarchyObject asset. Only present after scene processing.

**Example:**
```json
"classID": 1032  // SceneHierarchyObject
```

---

### className

**Type:** `string`
**Required:** ✗

**Description:**
Class name of the SceneHierarchyObject asset. Only present after scene processing.

**Example:**
```json
"className": "SceneHierarchyObject"
```

---

## Count Fields

### assetCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Total number of assets across all collections in the scene.

**Example:**
```json
"assetCount": 15234
```

---

### gameObjectCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of GameObject assets in the scene. From SceneHierarchyObject.GameObjects.

**Example:**
```json
"gameObjectCount": 245
```

---

### componentCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of Component assets in the scene. From SceneHierarchyObject.Components.

**Example:**
```json
"componentCount": 687
```

---

### managerCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of LevelGameManager assets. From SceneHierarchyObject.Managers.

**Example:**
```json
"managerCount": 5
```

---

### prefabInstanceCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of PrefabInstance assets. From SceneHierarchyObject.PrefabInstances.

**Example:**
```json
"prefabInstanceCount": 12
```

---

### dependencyCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of external dependencies referenced by the scene.

**Example:**
```json
"dependencyCount": 89
```

---

### hasSceneRoots

**Type:** `boolean`
**Required:** ✓

**Description:**
Whether the scene has a SceneRoots asset. From SceneHierarchyObject.SceneRoots.

**Examples:**
```json
"hasSceneRoots": true
"hasSceneRoots": false
```

---

### rootGameObjectCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of root GameObjects without parent. From SceneHierarchyObject.GetRoots().

**Example:**
```json
"rootGameObjectCount": 15
```

---

### strippedAssetCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of assets with stripped type information. From GameObjectHierarchyObject.StrippedAssets.

**Example:**
```json
"strippedAssetCount": 0
```

---

### hiddenAssetCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of hidden assets not included in YAML export. From GameObjectHierarchyObject.HiddenAssets.

**Example:**
```json
"hiddenAssetCount": 0
```

---

## Asset Lists (Optional)

All asset list fields are optional arrays of AssetRef objects.

### sceneRootsAsset

**Type:** `NullableAssetRef`
**Required:** ✗

**Description:**
Reference to the SceneRoots asset if it exists.

**Example:**
```json
"sceneRootsAsset": {
  "collectionId": "level1",
  "pathId": 2
}
```

---

### sceneRoots

**Type:** `array` of AssetRef
**Default:** `[]`
**Required:** ✗

**Description:**
References to scene root objects.

---

### rootGameObjects

**Type:** `array` of AssetRef
**Default:** `[]`
**Required:** ✗

**Description:**
References to root GameObject assets (without parent).

---

### gameObjects

**Type:** `array` of AssetRef
**Default:** `[]`
**Required:** ✗

**Description:**
References to all GameObject assets in the scene.

---

### components

**Type:** `array` of AssetRef
**Default:** `[]`
**Required:** ✗

**Description:**
References to all Component assets in the scene.

---

### managers

**Type:** `array` of AssetRef
**Default:** `[]`
**Required:** ✗

**Description:**
References to all LevelGameManager assets.

---

### prefabInstances

**Type:** `array` of AssetRef
**Default:** `[]`
**Required:** ✗

**Description:**
References to all PrefabInstance assets.

---

### strippedAssets

**Type:** `array` of AssetRef
**Default:** `[]`
**Required:** ✗

**Description:**
References to assets with stripped type information.

---

### hiddenAssets

**Type:** `array` of AssetRef
**Default:** `[]`
**Required:** ✗

**Description:**
References to hidden assets excluded from YAML export.

---

### notes

**Type:** `string`
**Required:** ✗

**Description:**
Optional notes or metadata about the scene.

**Example:**
```json
"notes": "Main menu scene with UI and background"
```

---

## Complete Example

```json
{
  "domain": "scenes",
  "type": "Scene",
  "name": "MainMenu",
  "sceneGuid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
  "scenePath": "Assets/Scenes/MainMenu.unity",
  "exportedAt": "2025-11-16T13:30:00Z",
  "version": "2021.3.5f1",
  "platform": "StandaloneWindows64",
  "flags": "0x00000004",
  "endianType": "LittleEndian",
  "bundleName": "mainmenu",
  "sceneCollectionCount": 2,
  "collectionIds": [
    "mainmenu",
    "sharedassets0.assets"
  ],
  "primaryCollectionId": "mainmenu",
  "bundle": {
    "bundlePk": "A1B2C3D4",
    "bundleName": "mainmenu"
  },
  "collectionDetails": [
    {
      "collectionId": "mainmenu",
      "bundle": {
        "bundlePk": "A1B2C3D4",
        "bundleName": "mainmenu"
      },
      "isPrimary": true,
      "assetCount": 1523
    },
    {
      "collectionId": "sharedassets0.assets",
      "bundle": {
        "bundlePk": "E5F6G7H8",
        "bundleName": "sharedassets"
      },
      "isPrimary": false,
      "assetCount": 234
    }
  ],
  "hierarchy": {
    "collectionId": "mainmenu",
    "pathId": 1
  },
  "hierarchyAssetId": "mainmenu:1",
  "pathID": 1,
  "classID": 1032,
  "className": "SceneHierarchyObject",
  "assetCount": 1757,
  "gameObjectCount": 245,
  "componentCount": 687,
  "managerCount": 5,
  "prefabInstanceCount": 12,
  "dependencyCount": 89,
  "hasSceneRoots": true,
  "rootGameObjectCount": 15,
  "strippedAssetCount": 0,
  "hiddenAssetCount": 0,
  "rootGameObjects": [
    {"collectionId": "mainmenu", "pathId": 10},
    {"collectionId": "mainmenu", "pathId": 15},
    {"collectionId": "mainmenu", "pathId": 20}
  ],
  "notes": "Main menu scene with UI and background"
}
```

---

## Usage Patterns

### Finding Scene by GUID

```python
def get_scene_by_guid(guid):
    """Find scene by Unity GUID"""
    # Query scenes.ndjson
    # WHERE sceneGuid == guid
    pass
```

### Analyzing Scene Complexity

```python
def analyze_scene(scene):
    """Analyze scene complexity"""
    return {
        "total_objects": scene.assetCount,
        "gameobjects": scene.gameObjectCount,
        "components": scene.componentCount,
        "avg_components_per_go": scene.componentCount / max(scene.gameObjectCount, 1),
        "prefabs": scene.prefabInstanceCount,
        "root_objects": scene.rootGameObjectCount,
        "hierarchy_depth": estimate_depth(scene)
    }
```

### Finding Large Scenes

```python
def find_large_scenes(all_scenes, threshold=10000):
    """Find scenes with many assets"""
    return [s for s in all_scenes if s.assetCount > threshold]
```

---

## Related Schemas

### Container References

- **collections.schema.json**: Scene collections (via collectionIds)
- **bundles.schema.json**: Bundles containing scene (via bundle field)

### Asset References

- **assets.schema.json**: Assets in scene (GameObjects, Components, etc.)

### Statistics

- **scene_stats.schema.json**: Derived scene statistics

---

## Implementation Notes

### C# Model

```csharp
public class SceneRecord
{
    public required string Domain { get; init; } = "scenes";
    public string? Type { get; init; } = "Scene";
    public required string Name { get; init; }
    public required string SceneGuid { get; init; }
    public required string ScenePath { get; init; }
    public required string ExportedAt { get; init; }
    public required string Version { get; init; }
    public required string Platform { get; init; }
    public string? Flags { get; init; }
    public string? EndianType { get; init; }
    public string? BundleName { get; init; }
    public required int SceneCollectionCount { get; init; }
    public required string[] CollectionIds { get; init; }
    public string? PrimaryCollectionId { get; init; }
    public BundleRef? Bundle { get; init; }
    public CollectionDetail[]? CollectionDetails { get; init; }

    // Counts (all required)
    public required int AssetCount { get; init; }
    public required int GameObjectCount { get; init; }
    public required int ComponentCount { get; init; }
    public required int ManagerCount { get; init; }
    public required int PrefabInstanceCount { get; init; }
    public required int DependencyCount { get; init; }
    public required bool HasSceneRoots { get; init; }
    public required int RootGameObjectCount { get; init; }
    public required int StrippedAssetCount { get; init; }
    public required int HiddenAssetCount { get; init; }

    // Optional asset lists
    public AssetRef? SceneRootsAsset { get; init; }
    public AssetRef[]? RootGameObjects { get; init; }
    public string? Notes { get; init; }
}
```

---

**End of scenes.schema.json Reference**
