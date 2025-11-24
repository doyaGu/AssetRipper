# assets.schema.json Reference

**Schema ID:** `https://schemas.assetripper.dev/assetdump/v2/facts/assets.schema.json`
**Title:** AssetDump v2 Asset Facts
**Domain:** `assets`
**Layer:** Facts
**Version:** 2.0

---

## Overview

The `assets.schema.json` defines per-asset fact records capturing identity, class information, and serialized data slices for individual Unity objects. This is the primary schema for asset-level data and contains detailed metadata about every IUnityObjectBase instance in the export.

### Purpose

- Record individual Unity object metadata (GameObject, MonoBehaviour, Texture2D, etc.)
- Capture Unity serialization details (ClassID, TypeID, type tree indices)
- Store asset naming and original path information
- Preserve complete hierarchical path from root to asset
- Include serialized object payload as JSON

### Key Characteristics

- **Primary Key:** Composite `{collectionId, pathId}` (AssetPK structure)
- **Domain Identifier:** `"assets"` (constant)
- **Cardinality:** One record per Unity asset (typically 10K-200K+ per project)
- **Size:** Variable (minimal records ~200 bytes, large records with data can be KB-MB)

---

## Schema Structure

### Required Fields

| Field | Type | Description |
|-------|------|-------------|
| domain | string (const: "assets") | Domain identifier for NDJSON routing |
| pk | AssetPK | Primary key: {collectionId, pathId} |
| classKey | integer | Join key into types.ndjson for type lookup |

### Optional Fields

| Field | Type | Description |
|-------|------|-------------|
| pathId | integer | PathID within containing collection (redundant with pk.pathId) |
| className | string | Unity class name (GameObject, MonoBehaviour, etc.) |
| name | string | Best-effort asset name (m_Name or INamed) |
| originalPath | string | Original Unity project path |
| originalDirectory | string | Original directory without filename |
| originalName | string | Original filename without extension |
| originalExtension | string | Original file extension (.png, .fbx) |
| assetBundleName | string | AssetBundle name if applicable |
| hierarchy | HierarchyPath | Complete path from root bundle |
| collectionName | string | Containing collection name (readability) |
| bundleName | string | Parent bundle name (readability) |
| sceneName | string | Scene name if asset belongs to one |
| unity | object | Unity serialization metadata |
| data | any | Serialized Unity object payload (JSON) |
| hash | string | Content hash for deduplication |

---

## Field Reference

### domain

**Type:** `string`
**Const:** `"assets"`
**Required:** ✓

**Description:**
Fixed domain identifier for asset records. Enables NDJSON streaming and record type identification.

**Example:**
```json
"domain": "assets"
```

**Validation:**
- Must exactly equal `"assets"`
- Case-sensitive

---

### pk

**Type:** `object` (AssetPK)
**Required:** ✓
**Reference:** `core.schema.json#AssetPK`

**Description:**
Primary key structure combining collectionId and pathId to create globally unique asset reference.

**Properties:**
- `collectionId` (CollectionID): Collection containing this asset
- `pathId` (integer): Unity m_PathID within collection

**Example:**
```json
"pk": {
  "collectionId": "sharedassets0",
  "pathId": 123
}
```

**Characteristics:**
- Globally unique within an export
- Deterministic (same asset always gets same PK)
- Corresponds to Unity's {SerializedFile, ObjectInfo} pairing

**Unity Correspondence:**
```csharp
// IUnityObjectBase identification
interface IUnityObjectBase {
    IAssetCollection Collection { get; }  // -> pk.collectionId
    long PathID { get; }                  // -> pk.pathId
}
```

---

### pathId

**Type:** `integer`
**Required:** ✗

**Description:**
PathID within the containing collection. Unique identifier for the asset in its collection. This field is redundant with `pk.pathId` but provided for convenience.

**Example:**
```json
"pathId": 123
```

**Range:**
- Positive: Regular assets (1 to max long)
- Negative: Special Unity assets (e.g., -1 for BuiltinExtra)
- Zero: Invalid (PathID 0 is reserved for null)

**Note:** When present, must equal `pk.pathId`

---

### classKey

**Type:** `integer`
**Required:** ✓

**Description:**
Join key into facts/types.ndjson for efficient type information lookup. This is a dense integer assigned by the exporter to map assets to their type definitions.

**Example:**
```json
"classKey": 1
```

**Characteristics:**
- Dense sequential integers (1, 2, 3, ...)
- Unique per (ClassID, MonoBehaviour script) combination
- Enables efficient type-based queries via by_class index

**Usage:**
```
1. Asset has classKey: 1
2. Query types.ndjson for classKey: 1
3. Get ClassID: 1, ClassName: "GameObject"
```

**Note:** For MonoBehaviour (ClassID 114), each unique script gets a different classKey.

---

### className

**Type:** `string`
**Required:** ✗

**Description:**
Unity class name providing human-readable type information. Derived from IUnityObjectBase.ClassName.

**Examples:**
```json
"className": "GameObject"
"className": "MonoBehaviour"
"className": "Texture2D"
"className": "PlayerController"  // MonoBehaviour script name
```

**Source:**
```csharp
string className = asset.ClassName;
```

**Special Cases:**
- MonoBehaviour: Script class name (e.g., "PlayerController")
- Unknown types: May be `"ClassID_{classId}"` format
- Null for unreadable objects

---

### name

**Type:** `string`
**Required:** ✗

**Description:**
Best-effort asset name extracted from Unity's m_Name field or INamed interface implementation.

**Examples:**
```json
"name": "Main Camera"
"name": "PlayerController"
"name": "logo"
"name": ""  // Some assets have empty names
```

**Source:**
```csharp
// Priority order:
1. INamed.Name (if implements INamed)
2. m_Name field (if exists in serialized data)
3. Empty string or null
```

**Characteristics:**
- May be empty string
- Not guaranteed unique
- Display name for UI/logging

---

### originalPath

**Type:** `string`
**Required:** ✗

**Description:**
Original path of the asset in the Unity project, typically in format `Assets/Path/To/File.ext`.

**Examples:**
```json
"originalPath": "Assets/Textures/logo.png"
"originalPath": "Assets/Scenes/MainMenu.unity"
"originalPath": "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader"
```

**Characteristics:**
- Unity-style forward slashes
- Typically starts with "Assets/" or "Packages/"
- Not available for all asset types

---

### originalDirectory

**Type:** `string`
**Required:** ✗

**Description:**
Original directory path without filename. Derived from originalPath.

**Examples:**
```json
"originalDirectory": "Assets/Textures"
"originalDirectory": "Assets/Scenes"
```

**Relationship:**
```
originalPath = originalDirectory + "/" + originalName + originalExtension
```

---

### originalName

**Type:** `string`
**Required:** ✗

**Description:**
Original filename without extension.

**Examples:**
```json
"originalName": "logo"
"originalName": "MainMenu"
"originalName": "PlayerController"
```

---

### originalExtension

**Type:** `string`
**Required:** ✗

**Description:**
Original file extension including the dot.

**Examples:**
```json
"originalExtension": ".png"
"originalExtension": ".fbx"
"originalExtension": ".unity"
"originalExtension": ".cs"
```

---

### assetBundleName

**Type:** `string`
**Required:** ✗

**Description:**
Name of the asset bundle containing this asset, if the asset was marked for AssetBundle packaging in Unity.

**Examples:**
```json
"assetBundleName": "characters"
"assetBundleName": "level1_assets"
```

**Note:** Only present for assets explicitly assigned to AssetBundles in Unity Editor

---

### hierarchy

**Type:** `object` (HierarchyPath)
**Required:** ✗
**Reference:** `core.schema.json#HierarchyPath`

**Description:**
Complete hierarchical path from root bundle to this asset. Includes bundle PKs, names, and depth.

**Properties:**
- `bundlePath`: Array of bundle PKs from root to containing bundle
- `bundleNames`: Human-readable bundle names
- `depth`: Hierarchy depth (root = 0)

**Example:**
```json
"hierarchy": {
  "bundlePath": ["00000000", "A1B2C3D4"],
  "bundleNames": ["GameBundle", "level0"],
  "depth": 1
}
```

**Usage:**
- Reconstruct full path for display
- Filter assets by bundle hierarchy
- Understand asset organization

---

### collectionName

**Type:** `string`
**Required:** ✗

**Description:**
Name of the containing collection for readability. Redundant with data in collections.ndjson but provided for convenience.

**Example:**
```json
"collectionName": "sharedassets0.assets"
```

---

### bundleName

**Type:** `string`
**Required:** ✗

**Description:**
Name of the parent bundle for readability.

**Example:**
```json
"bundleName": "level0"
```

---

### sceneName

**Type:** `string`
**Required:** ✗

**Description:**
Name of the scene if this asset belongs to one. Only present for assets in scene collections.

**Example:**
```json
"sceneName": "MainMenu"
```

---

### unity

**Type:** `object`
**Required:** ✗

**Description:**
Unity serialization metadata; all fields optional for version flexibility. Contains low-level Unity format details.

**Properties:**

| Field | Type | Description |
|-------|------|-------------|
| classId | integer | Unity ClassID (1=GameObject, 114=MonoBehaviour) |
| typeId | integer | SerializedType.TypeID when available |
| serializedTypeIndex | integer | Index into SerializedFile's type tree array |
| scriptTypeIndex | integer | Script type index for MonoBehaviour |
| isStripped | boolean | Type information was stripped during build |
| serializedVersion | integer | SerializedVersion of asset type |

**Example:**
```json
"unity": {
  "classId": 1,
  "typeId": 1,
  "serializedTypeIndex": 5,
  "scriptTypeIndex": -1,
  "isStripped": false,
  "serializedVersion": 6
}
```

**Unity Correspondence:**
```csharp
// SerializedFile.ObjectInfo
class ObjectInfo {
    int ClassID;              // -> classId
    int TypeID;               // -> typeId (Unity 5+)
    int SerializedTypeIndex;  // -> serializedTypeIndex
    int ScriptTypeIndex;      // -> scriptTypeIndex
    bool Stripped;            // -> isStripped
}
```

---

#### unity.classId

**Type:** `integer`

**Description:**
Unity ClassID identifying the native type. Standard values defined by Unity.

**Common Values:**

| ClassID | Type |
|---------|------|
| 1 | GameObject |
| 4 | Transform |
| 21 | Material |
| 28 | Texture2D |
| 43 | Mesh |
| 48 | Shader |
| 114 | MonoBehaviour |
| 115 | MonoScript |

**Example:**
```json
"classId": 1  // GameObject
```

---

#### unity.typeId

**Type:** `integer`

**Description:**
Type ID of the object from ObjectInfo.TypeID. For non-MonoBehaviour types, typically equals classId. For MonoBehaviour (ClassID 114), this is the script's unique identifier.

**Examples:**
```json
"typeId": 1    // GameObject
"typeId": 114  // MonoBehaviour base
```

---

#### unity.serializedTypeIndex

**Type:** `integer`

**Description:**
Index in the SerializedFile.Types array (Unity 5+). Points to the SerializedType definition in the type tree. -1 if not applicable or Unity 4 or earlier.

**Range:**
- 0-N: Valid index
- -1: Not applicable

---

#### unity.scriptTypeIndex

**Type:** `integer`

**Description:**
Script type index for MonoBehaviour types. Links to the MonoScript metadata. -1 if not a MonoBehaviour.

**Range:**
- 0-N: Valid script index
- -1: Not a MonoBehaviour

---

#### unity.isStripped

**Type:** `boolean`

**Description:**
True if the asset's type information was stripped during build. Stripped types have no type tree and may be unreadable.

**Examples:**
```json
"isStripped": false  // Type tree available
"isStripped": true   // Type tree stripped
```

---

#### unity.serializedVersion

**Type:** `integer`

**Description:**
SerializedVersion of the asset type. Unity uses this to handle format changes across Unity versions. Different Unity versions may have different serialized formats for the same type.

**Example:**
```json
"serializedVersion": 6
```

---

### data

**Type:** `object` | `array` | `string` | `number` | `boolean` | `null`
**Required:** ✗

**Description:**
Serialized Unity object payload emitted inline as JSON. Contains the deserialized asset data. Null for unreadable assets (UnreadableObject, UnknownObject).

**Characteristics:**
- Can be any valid JSON type
- Structure varies by asset ClassID
- Null if asset couldn't be read/deserialized
- May be very large (MB+) for complex assets

**Examples:**

**GameObject data:**
```json
"data": {
  "m_Name": "Main Camera",
  "m_IsActive": true,
  "m_Component": [
    {"component": {"collectionId": "sharedassets0", "pathId": 2}},
    {"component": {"collectionId": "sharedassets0", "pathId": 3}}
  ],
  "m_Layer": 0,
  "m_Tag": "MainCamera"
}
```

**Texture2D data:**
```json
"data": {
  "m_Name": "logo",
  "m_Width": 256,
  "m_Height": 256,
  "m_TextureFormat": 5,
  "m_MipMap": true
}
```

**Null data (unreadable):**
```json
"data": null
```

**Unity Correspondence:**
```csharp
// Serialized data extraction
var data = asset.SerializedData.ToJson();
```

---

### hash

**Type:** `string`
**Required:** ✗

**Description:**
Optional content hash (e.g., `sha1:<hex>`) for deduplication and integrity verification. Computed from serialized asset data.

**Format:** `<algorithm>:<hex-digest>`

**Examples:**
```json
"hash": "sha1:a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0"
"hash": "md5:1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d"
```

**Use Cases:**
- Detect duplicate assets across collections
- Verify data integrity
- Incremental export (detect changes)

---

## Complete Example

**Minimal Asset:**
```json
{
  "domain": "assets",
  "pk": {
    "collectionId": "sharedassets0",
    "pathId": 1
  },
  "classKey": 1
}
```

**Typical Asset:**
```json
{
  "domain": "assets",
  "pk": {
    "collectionId": "sharedassets0",
    "pathId": 1
  },
  "classKey": 1,
  "className": "GameObject",
  "name": "Main Camera",
  "hierarchy": {
    "bundlePath": ["00000000", "A1B2C3D4"],
    "bundleNames": ["GameBundle", "level0"],
    "depth": 1
  },
  "unity": {
    "classId": 1,
    "typeId": 1,
    "serializedTypeIndex": 5,
    "scriptTypeIndex": -1,
    "isStripped": false,
    "serializedVersion": 6
  },
  "data": {
    "m_Name": "Main Camera",
    "m_IsActive": true,
    "m_Component": [
      {"component": {"collectionId": "sharedassets0", "pathId": 2}}
    ],
    "m_Layer": 0,
    "m_Tag": "MainCamera"
  }
}
```

**Complete Asset with Original Path:**
```json
{
  "domain": "assets",
  "pk": {
    "collectionId": "sharedassets0",
    "pathId": 100
  },
  "pathId": 100,
  "classKey": 28,
  "className": "Texture2D",
  "name": "logo",
  "originalPath": "Assets/Textures/UI/logo.png",
  "originalDirectory": "Assets/Textures/UI",
  "originalName": "logo",
  "originalExtension": ".png",
  "assetBundleName": "ui_assets",
  "hierarchy": {
    "bundlePath": ["00000000", "A1B2C3D4", "E5F6G7H8"],
    "bundleNames": ["GameBundle", "level0", "Resources"],
    "depth": 2
  },
  "collectionName": "sharedassets0.assets",
  "bundleName": "level0",
  "unity": {
    "classId": 28,
    "typeId": 28,
    "serializedTypeIndex": 12,
    "isStripped": false,
    "serializedVersion": 4
  },
  "data": {
    "m_Name": "logo",
    "m_Width": 256,
    "m_Height": 256,
    "m_TextureFormat": 5,
    "m_MipMap": true,
    "m_IsReadable": false
  },
  "hash": "sha1:a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0"
}
```

---

## Usage Patterns

### Finding Assets by Type

**Step 1: Query by_class index**
```
Query: by_class.ndjson WHERE classKey = 1
Result: List of all GameObject AssetPKs
```

**Step 2: Fetch asset details**
```
Query: assets.ndjson WHERE pk IN (list from step 1)
Result: Full asset records
```

### Resolving Asset References

**From PPtr to Asset:**
```json
// PPtr reference
{"collectionId": "sharedassets0", "pathId": 123}

// Query assets.ndjson
WHERE pk.collectionId = "sharedassets0"
AND pk.pathId = 123
```

### Type Hierarchy Traversal

**Get all assets in a bundle:**
```json
// Query assets.ndjson
WHERE hierarchy.bundlePath CONTAINS "A1B2C3D4"
```

### Finding Assets by Name

**Pattern match on name field:**
```json
// Query assets.ndjson
WHERE name LIKE "Player%"
```

---

## Performance Considerations

### Index Strategy

**Primary Index:** `{collectionId, pathId}` (PK)
**Secondary Indexes:**
- `classKey` (type-based queries)
- `name` (name searches, if needed)
- `hierarchy.bundlePath[]` (bundle queries, if needed)

### Memory Usage

**Typical Sizes:**
- Minimal record: ~200 bytes
- Average record: ~500 bytes
- Large record (with data): 1KB - 1MB+

**Large Dataset (200K assets):**
- Minimal: ~40 MB
- Average: ~100 MB
- With data: 200 MB - 20 GB+

### Query Optimization

**Avoid:**
- Full table scans for type queries (use by_class index)
- Deserializing `data` field if not needed
- Repeated classKey lookups (cache type dictionary)

**Prefer:**
- Index-based lookups
- Streaming NDJSON processing
- Selective field projection

---

## Related Schemas

### Parent/Container

- **collections.schema.json**: Contains this asset (via pk.collectionId)
- **bundles.schema.json**: Hierarchical container (via hierarchy.bundlePath)

### Type Information

- **types.schema.json**: Type dictionary (via classKey)
- **type_definitions.schema.json**: .NET type details (for MonoBehaviour)

### Dependencies

- **asset_dependencies.schema.json**: Asset-to-asset references (from/to this asset)

### Indexes

- **by_class.schema.json**: Assets grouped by type (contains this asset's PK)
- **by_collection.schema.json**: Collection summary (includes this asset in count)

---

## Implementation Notes

### C# Model

**Source:** `AssetRipper.Tools.AssetDumper/Models/AssetRecord.cs`

```csharp
public class AssetRecord
{
    public required string Domain { get; init; } = "assets";
    public required AssetPK PK { get; init; }
    public required int ClassKey { get; init; }

    public int? PathId { get; init; }
    public string? ClassName { get; init; }
    public string? Name { get; init; }
    public string? OriginalPath { get; init; }
    public string? OriginalDirectory { get; init; }
    public string? OriginalName { get; init; }
    public string? OriginalExtension { get; init; }
    public string? AssetBundleName { get; init; }
    public HierarchyPath? Hierarchy { get; init; }
    public string? CollectionName { get; init; }
    public string? BundleName { get; init; }
    public string? SceneName { get; init; }
    public UnityMetadata? Unity { get; init; }
    public object? Data { get; init; }
    public string? Hash { get; init; }
}
```

### Exporter

**Source:** `AssetRipper.Tools.AssetDumper/Exporters/AssetExporter.cs`

**Key Logic:**
```csharp
foreach (var collection in bundle.Collections)
{
    foreach (var asset in collection.Assets)
    {
        var record = new AssetRecord
        {
            Domain = "assets",
            PK = new AssetPK
            {
                CollectionId = collection.CollectionId,
                PathId = asset.PathID
            },
            ClassKey = typeDict.GetClassKey(asset),
            ClassName = asset.ClassName,
            Name = GetAssetName(asset),
            Unity = ExtractUnityMetadata(asset),
            Data = SerializeAssetData(asset)
        };

        writer.WriteLine(JsonSerializer.Serialize(record));
    }
}
```

---

**End of assets.schema.json Reference**
