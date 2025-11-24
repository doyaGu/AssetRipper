# bundles.schema.json Reference

**Schema ID:** `https://schemas.assetripper.dev/assetdump/v2/facts/bundles.schema.json`
**Title:** AssetDump v2 Bundle Metadata Facts
**Domain:** `bundles`
**Layer:** Facts
**Version:** 2.0

---

## Overview

The `bundles.schema.json` defines per-bundle metadata records capturing hierarchy relationships, collection coverage, and aggregate counters. Bundles represent container structures in AssetRipper's hierarchy system, organizing collections and assets into a tree structure.

### Purpose

- Model bundle hierarchy (GameBundle, SerializedBundle, ProcessedBundle, etc.)
- Track parent-child relationships between bundles
- Aggregate statistics (direct vs total counts)
- Link bundles to their collections and scenes
- Maintain ancestor paths for efficient traversal

### Key Characteristics

- **Primary Key:** `pk` (8-character uppercase hex string)
- **Domain Identifier:** `"bundles"` (constant)
- **Cardinality:** One record per Bundle instance (typically 5-50 per project)
- **Root Bundle:** Always has `pk: "00000000"` and `isRoot: true`

---

## Schema Structure

### Required Fields (28 total)

| Field | Type | Description |
|-------|------|-------------|
| domain | string (const) | Domain identifier: "bundles" |
| pk | string | 8-char hex bundle identifier |
| name | string | Bundle display name |
| bundleType | string | Runtime type name |
| isRoot | boolean | True for root GameBundle |
| hierarchyDepth | integer | Depth in tree (root=0) |
| hierarchyPath | string | Human-readable path |
| childBundlePks | array | Direct child bundle PKs |
| directCollectionCount | integer | Collections directly attached |
| totalCollectionCount | integer | Collections in subtree |
| directSceneCollectionCount | integer | Scene collections direct |
| totalSceneCollectionCount | integer | Scene collections in subtree |
| directChildBundleCount | integer | Immediate children |
| totalChildBundleCount | integer | Total descendants |
| directResourceCount | integer | Direct resource files |
| totalResourceCount | integer | Resources in subtree |
| directFailedFileCount | integer | Direct failed files |
| totalFailedFileCount | integer | Failed files in subtree |
| directAssetCount | integer | Assets in direct collections |
| totalAssetCount | integer | Assets in subtree |

### Conditional Required Fields

**If `isRoot: false`, then required:**
- `parentPk` (string): Parent bundle PK
- `bundleIndex` (integer): Index in parent's child list

---

## Field Reference

### domain

**Type:** `string`
**Const:** `"bundles"`
**Required:** ✓

**Description:**
Fixed domain identifier for bundle records.

**Example:**
```json
"domain": "bundles"
```

---

### pk

**Type:** `string`
**Pattern:** `^[A-F0-9]{8}$`
**Required:** ✓

**Description:**
Stable 8-character uppercase hex identifier for the bundle node, derived from its hierarchical lineage path using FNV-1a hashing.

**Examples:**
```json
"pk": "00000000"  // Root GameBundle
"pk": "A1B2C3D4"  // Child bundle
"pk": "E5F6G7H8"  // Grandchild bundle
```

**Generation:**
```
Lineage = concatenate(ancestor names from root)
PK = FNV1a(Lineage) -> 8-char hex
```

**Special Case:**
- Root GameBundle always has `pk: "00000000"`

---

### name

**Type:** `string`
**Required:** ✓

**Description:**
Bundle display name. Human-readable identifier for the bundle.

**Examples:**
```json
"name": "GameBundle"
"name": "level0"
"name": "Resources"
"name": "sharedassets"
```

**Source:**
```csharp
string name = bundle.Name;
```

---

### bundleType

**Type:** `string`
**Required:** ✓

**Description:**
Runtime type name of the bundle, indicating the C# class type in AssetRipper.

**Common Values:**
- `"GameBundle"`: Root container for entire game
- `"SerializedBundle"`: Bundle loaded from serialized file
- `"ProcessedBundle"`: Bundle created during processing
- `"ResourceFile"`: Resource file bundle
- `"WebBundle"`: Web-loaded bundle

**Example:**
```json
"bundleType": "SerializedBundle"
```

---

### parentPk

**Type:** `string`
**Pattern:** `^[A-F0-9]{8}$`
**Required:** ✓ (if `isRoot: false`)

**Description:**
Stable identifier of the parent bundle. Only present for non-root bundles.

**Example:**
```json
"parentPk": "00000000"
```

**Validation:**
- Must be a valid 8-char hex PK
- Must reference an existing bundle
- Not present when `isRoot: true`

---

### isRoot

**Type:** `boolean`
**Required:** ✓

**Description:**
True when the bundle represents the root game bundle. Only one bundle should have `isRoot: true` per export.

**Examples:**
```json
"isRoot": true   // Root GameBundle
"isRoot": false  // All other bundles
```

**Invariants:**
- Exactly one bundle with `isRoot: true`
- Root bundle has `pk: "00000000"`
- Root bundle has `hierarchyDepth: 0`
- Root bundle has no `parentPk`

---

### hierarchyDepth

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Depth within the bundle hierarchy, where root = 0.

**Examples:**
```json
"hierarchyDepth": 0  // Root
"hierarchyDepth": 1  // Direct child of root
"hierarchyDepth": 2  // Grandchild
```

**Calculation:**
```
depth = number of ancestors (excluding self)
depth = ancestorPath.length
depth = hierarchyPath.split('/').length - 1
```

---

### hierarchyPath

**Type:** `string`
**Required:** ✓

**Description:**
Human-readable path constructed from ancestor bundle names, separated by forward slashes.

**Examples:**
```json
"hierarchyPath": "GameBundle"
"hierarchyPath": "GameBundle/level0"
"hierarchyPath": "GameBundle/level0/Resources"
```

**Format:**
```
hierarchyPath = join('/', [ancestor names from root to self])
```

---

### childBundlePks

**Type:** `array` of strings
**Pattern:** `^[A-F0-9]{8}$`
**Required:** ✓

**Description:**
Stable PKs of direct child bundles in declaration order. Empty array if no children.

**Example:**
```json
"childBundlePks": ["A1B2C3D4", "E5F6G7H8", "1A2B3C4D"]
```

**Characteristics:**
- Ordered (maintains insertion/declaration order)
- Direct children only (not transitive)
- Can be empty array

---

### childBundleNames

**Type:** `array` of strings
**Required:** ✗

**Description:**
Names of child bundles for human readability. Corresponds to childBundlePks.

**Example:**
```json
"childBundleNames": ["level0", "level1", "sharedassets"]
```

**Invariant:**
- If present, `childBundleNames.length == childBundlePks.length`

---

### bundleIndex

**Type:** `integer`
**Minimum:** 0
**Required:** ✓ (if `isRoot: false`)

**Description:**
Index of this bundle within its parent's child list (0-based). Used for maintaining bundle order.

**Examples:**
```json
"bundleIndex": 0  // First child
"bundleIndex": 1  // Second child
"bundleIndex": 5  // Sixth child
```

**Usage:**
```
parent.childBundlePks[bundleIndex] == this.pk
```

---

### ancestorPath

**Type:** `array` of strings
**Pattern:** `^[A-F0-9]{8}$`
**Required:** ✗

**Description:**
Ordered list of ancestor bundle PKs from root to parent (excludes self).

**Examples:**

**Root:**
```json
"ancestorPath": []  // No ancestors
```

**Direct child:**
```json
"ancestorPath": ["00000000"]  // Parent is root
```

**Grandchild:**
```json
"ancestorPath": ["00000000", "A1B2C3D4"]  // Root -> Parent
```

**Calculation:**
```
ancestorPath = hierarchyPath - self
ancestorPath.length == hierarchyDepth
```

---

### collectionIds

**Type:** `array` of CollectionID
**Unique Items:** true
**Required:** ✗

**Description:**
Direct collection identifiers hosted by this bundle. Does not include collections from child bundles.

**Example:**
```json
"collectionIds": [
  "sharedassets0.assets",
  "sharedassets1.assets",
  "level0"
]
```

**Characteristics:**
- Unique values (no duplicates)
- Direct collections only
- Can be empty array

---

### resources

**Type:** `array` of objects
**Required:** ✗

**Description:**
Direct resource files referenced by the bundle.

**Item Schema:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | ✓ | Resource file name |
| filePath | string | ✗ | Original file path when known |

**Example:**
```json
"resources": [
  {
    "name": "unity_builtin_extra",
    "filePath": "Resources/unity_builtin_extra"
  },
  {
    "name": "globalgamemanagers.assets"
  }
]
```

---

### failedFiles

**Type:** `array` of objects
**Required:** ✗

**Description:**
Direct failed files captured on the bundle. Corresponds to Bundle.FailedFiles.

**Item Schema:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | ✓ | Failed file name |
| filePath | string | ✗ | Original file path when known |
| error | string | ✗ | Error message describing failure |

**Example:**
```json
"failedFiles": [
  {
    "name": "corrupted.assets",
    "filePath": "Data/corrupted.assets",
    "error": "Failed to parse SerializedFile header"
  }
]
```

---

### scenes

**Type:** `array` of SceneRef
**Required:** ✗

**Description:**
All scenes contained in this bundle's collections. Corresponds to Bundle.Scenes property.

**Example:**
```json
"scenes": [
  {
    "sceneGuid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
    "sceneName": "MainMenu",
    "scenePath": "Assets/Scenes/MainMenu.unity"
  },
  {
    "sceneGuid": "2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e",
    "sceneName": "Level1"
  }
]
```

---

## Counter Fields

All counter fields follow the "direct vs total" pattern:
- **Direct**: Count of items directly attached to this bundle
- **Total**: Count of items in entire subtree (this bundle + all descendants)

### directCollectionCount / totalCollectionCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of AssetCollection instances attached to the bundle.

**Examples:**
```json
"directCollectionCount": 3
"totalCollectionCount": 45
```

**Invariant:**
```
totalCollectionCount >= directCollectionCount
totalCollectionCount == directCollectionCount + sum(child.totalCollectionCount)
```

---

### directSceneCollectionCount / totalSceneCollectionCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of scene collections (collections where `isSceneCollection: true`).

**Examples:**
```json
"directSceneCollectionCount": 1
"totalSceneCollectionCount": 12
```

---

### directChildBundleCount / totalChildBundleCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of child bundles.

**Examples:**
```json
"directChildBundleCount": 3
"totalChildBundleCount": 15
```

**Invariant:**
```
directChildBundleCount == childBundlePks.length
totalChildBundleCount >= directChildBundleCount
```

---

### directResourceCount / totalResourceCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of resource files.

**Examples:**
```json
"directResourceCount": 2
"totalResourceCount": 8
```

---

### directFailedFileCount / totalFailedFileCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Number of failed files that couldn't be loaded.

**Examples:**
```json
"directFailedFileCount": 0
"totalFailedFileCount": 0
```

**Health Indicator:**
- 0: All files loaded successfully
- >0: Some files failed (check failedFiles array)

---

### directAssetCount / totalAssetCount

**Type:** `integer`
**Minimum:** 0
**Required:** ✓

**Description:**
Total number of Unity assets across collections.

**Examples:**
```json
"directAssetCount": 5234
"totalAssetCount": 201543
```

**Calculation:**
```
directAssetCount = sum(collection.assetCount for collection in direct collections)
totalAssetCount = directAssetCount + sum(child.totalAssetCount)
```

---

## Complete Examples

### Root GameBundle

```json
{
  "domain": "bundles",
  "pk": "00000000",
  "name": "GameBundle",
  "bundleType": "GameBundle",
  "isRoot": true,
  "hierarchyDepth": 0,
  "hierarchyPath": "GameBundle",
  "childBundlePks": ["A1B2C3D4", "E5F6G7H8"],
  "childBundleNames": ["level0", "sharedassets"],
  "collectionIds": [],
  "resources": [],
  "failedFiles": [],
  "scenes": [],
  "directCollectionCount": 0,
  "totalCollectionCount": 45,
  "directSceneCollectionCount": 0,
  "totalSceneCollectionCount": 12,
  "directChildBundleCount": 2,
  "totalChildBundleCount": 8,
  "directResourceCount": 0,
  "totalResourceCount": 15,
  "directFailedFileCount": 0,
  "totalFailedFileCount": 0,
  "directAssetCount": 0,
  "totalAssetCount": 201543
}
```

### Child Bundle

```json
{
  "domain": "bundles",
  "pk": "A1B2C3D4",
  "name": "level0",
  "bundleType": "SerializedBundle",
  "parentPk": "00000000",
  "isRoot": false,
  "hierarchyDepth": 1,
  "hierarchyPath": "GameBundle/level0",
  "bundleIndex": 0,
  "ancestorPath": ["00000000"],
  "childBundlePks": ["1A2B3C4D"],
  "childBundleNames": ["Resources"],
  "collectionIds": [
    "level0",
    "sharedassets0.assets"
  ],
  "resources": [
    {
      "name": "unity_builtin_extra",
      "filePath": "Resources/unity_builtin_extra"
    }
  ],
  "failedFiles": [],
  "scenes": [
    {
      "sceneGuid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
      "sceneName": "Level0",
      "scenePath": "Assets/Scenes/Level0.unity"
    }
  ],
  "directCollectionCount": 2,
  "totalCollectionCount": 15,
  "directSceneCollectionCount": 1,
  "totalSceneCollectionCount": 5,
  "directChildBundleCount": 1,
  "totalChildBundleCount": 3,
  "directResourceCount": 1,
  "totalResourceCount": 4,
  "directFailedFileCount": 0,
  "totalFailedFileCount": 0,
  "directAssetCount": 12345,
  "totalAssetCount": 89012
}
```

---

## Usage Patterns

### Traversing Bundle Hierarchy

**Top-down (breadth-first):**
```python
def traverse_bundles(root_pk):
    queue = [root_pk]
    while queue:
        current_pk = queue.pop(0)
        bundle = get_bundle(current_pk)
        process(bundle)
        queue.extend(bundle.childBundlePks)
```

**Bottom-up (ancestor traversal):**
```python
def get_ancestors(bundle_pk):
    ancestors = []
    bundle = get_bundle(bundle_pk)
    for ancestor_pk in bundle.ancestorPath:
        ancestors.append(get_bundle(ancestor_pk))
    return ancestors
```

### Finding Bundles by Depth

```python
def get_bundles_at_depth(depth):
    return [b for b in all_bundles if b.hierarchyDepth == depth]
```

### Calculating Bundle Statistics

```python
def get_bundle_stats(bundle_pk):
    bundle = get_bundle(bundle_pk)
    return {
        "total_assets": bundle.totalAssetCount,
        "total_collections": bundle.totalCollectionCount,
        "total_scenes": bundle.totalSceneCollectionCount,
        "descendant_bundles": bundle.totalChildBundleCount
    }
```

---

## Related Schemas

### Hierarchy Relationships

- **bundle_hierarchy.schema.json**: Explicit parent-child edges

### Container Relationships

- **collections.schema.json**: Collections belong to bundles (via bundle field)
- **scenes.schema.json**: Scenes reference bundles (via bundle field)

### Aggregation Sources

- **assets.schema.json**: Asset counts aggregated from collections
- **collections.schema.json**: Collection counts

---

## Implementation Notes

### C# Model

**Source:** `AssetRipper.Tools.AssetDumper/Models/BundleRecord.cs`

```csharp
public class BundleRecord
{
    public required string Domain { get; init; } = "bundles";
    public required string PK { get; init; }
    public required string Name { get; init; }
    public required string BundleType { get; init; }
    public string? ParentPk { get; init; }
    public required bool IsRoot { get; init; }
    public required int HierarchyDepth { get; init; }
    public required string HierarchyPath { get; init; }
    public int? BundleIndex { get; init; }
    public string[]? AncestorPath { get; init; }
    public required string[] ChildBundlePks { get; init; }
    public string[]? ChildBundleNames { get; init; }
    public string[]? CollectionIds { get; init; }
    public ResourceFile[]? Resources { get; init; }
    public FailedFile[]? FailedFiles { get; init; }
    public SceneRef[]? Scenes { get; init; }

    // Counters (all required)
    public required int DirectCollectionCount { get; init; }
    public required int TotalCollectionCount { get; init; }
    // ... (all other counter fields)
}
```

### PK Generation

```csharp
public static string ComputeBundlePK(Bundle bundle)
{
    if (bundle.IsRoot)
        return "00000000";

    var lineage = string.Join("", GetAncestorNames(bundle)) + bundle.Name;
    var hash = FNV1a.ComputeHash(lineage);
    return hash.ToString("X8");  // 8-char uppercase hex
}
```

---

**End of bundles.schema.json Reference**
