# core.schema.json Reference

**Schema ID:** `https://schemas.assetripper.dev/assetdump/v2/core.schema.json`
**Title:** AssetDump v2 Core Definitions
**Type:** Shared Type Library
**Version:** 2.0

---

## Overview

The `core.schema.json` file defines shared type definitions and schema fragments reused across all AssetDump v2 schemas. It serves as the foundation for type consistency and provides common data structures used throughout the Facts, Relations, Indexes, and Metrics layers.

### Purpose

- Define reusable type definitions (CollectionID, UnityGuid, Timestamp, etc.)
- Establish reference structures (AssetRef, BundleRef, SceneRef)
- Provide hierarchical path representation (HierarchyPath)
- Enable consistent validation patterns across all schemas

### Key Concepts

**Anchors:** Core types are exposed via JSON Schema `$anchor` for cross-schema references
- `#AssetPK` - Asset primary key structure
- `#AssetRef` - Asset reference with metadata
- `#NullableAssetRef` - Optional asset reference
- `#BundleRef` - Bundle reference
- `#SceneRef` - Scene reference
- `#HierarchyPath` - Complete hierarchical path

---

## Type Definitions

### CollectionID

**Type:** `string`
**Pattern:** `^[A-Za-z0-9:_-]{2,}$`
**Min Length:** 1

**Description:**
Stable identifier for a serialized collection (e.g., sharedassets1.assets, BUILTIN-EXTRA). Supports both uppercase and lowercase alphanumeric characters plus `:_-`.

**Characteristics:**
- Deterministic (derived from collection name via FNV-1a hash)
- Case-insensitive
- Sortable (lexicographic ordering)
- Human-readable for well-known collections

**Examples:**
```json
"sharedassets0.assets"
"level0"
"BUILTIN-EXTRA"
"A1B2C3D4"
```

**Validation Rules:**
- Minimum 2 characters
- Only alphanumeric plus `:_-` characters
- No whitespace or special characters

**Usage:**
- Primary key in `collections.schema.json`
- Foreign key in `assets.schema.json` (pk.collectionId)
- Index key in dependency arrays

---

### StableKey

**Type:** `string`
**Pattern:** `^[A-Za-z0-9:_-]+:-?\d+$`

**Description:**
Deterministic key in format `<collectionId>:<pathId>` used for lexicographic indexing and sorting. Ensures consistent ordering across exports.

**Format:** `collectionId:pathId`

**Examples:**
```json
"sharedassets0:100"
"level0:-1"
"BUILTIN-EXTRA:12"
"A1B2C3D4:523"
```

**Characteristics:**
- Lexicographically sortable
- Globally unique (within an export)
- Deterministic (same asset always gets same key)
- Human-parseable

**Usage:**
- Primary key in `script_metadata.schema.json`
- Reference field in `script_sources.schema.json`
- Asset identification in text-based queries

---

### Timestamp

**Type:** `string`
**Format:** `date-time` (ISO 8601)

**Description:**
UTC timestamp in extended ISO-8601 form.

**Format:** `YYYY-MM-DDTHH:MM:SSZ`

**Examples:**
```json
"2025-11-16T13:30:00Z"
"2025-01-15T08:45:23Z"
```

**Validation Rules:**
- Must be valid ISO 8601 date-time
- Must include timezone (Z for UTC)
- Extended format (with dashes and colons)

**Usage:**
- `scenes.schema.json` - exportedAt field
- Audit trails and versioning
- Export metadata

---

### UnityGuid

**Type:** `string`
**Pattern:** `^([0-9A-Fa-f]{32}|[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})$`

**Description:**
Unity GUID in 32 hex characters (no dashes) or canonical GUID format (with dashes). Used for scene references and asset identification.

**Formats:**

**32-hex format (Unity style):**
```json
"1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d"
```

**Canonical format (RFC 4122):**
```json
"1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d"
```

**Characteristics:**
- Both uppercase and lowercase hex accepted
- 128-bit identifier
- Globally unique
- Case-insensitive comparison

**Usage:**
- Scene identification (`scenes.schema.json`)
- Script GUID (`script_metadata.schema.json`)
- Assembly GUID (`script_sources.schema.json`)

---

### CompressionCodec

**Type:** `string`
**Enum:** `["none", "gzip", "zstd", "zstd-seekable"]`

**Description:**
Supported compression codecs for AssetDump shards.

**Values:**

| Value | Description | Use Case |
|-------|-------------|----------|
| `none` | No compression | Small datasets, debugging |
| `gzip` | Standard gzip | Wide compatibility |
| `zstd` | Zstandard compression | Best compression ratio |
| `zstd-seekable` | Seekable Zstandard | Random access + compression |

**Examples:**
```json
"none"
"gzip"
"zstd"
"zstd-seekable"
```

**Usage:**
- Export metadata
- Shard file headers
- Compression configuration

---

### NonEmptyString

**Type:** `string`
**Min Length:** 1

**Description:**
Simple string type with minimum length constraint.

**Examples:**
```json
"GameObject"
"Assembly-CSharp"
"m_Material"
```

**Usage:**
- Field names
- Type names
- General non-nullable text fields

---

## Reference Structures

### AssetRef

**Anchor:** `#AssetRef`
**Type:** `object`
**Additional Properties:** `false`

**Description:**
Structured reference to a Unity asset using collectionId + pathId. Corresponds to Unity's PPtr (Pointer) structure for cross-file asset references.

**Required Fields:**
- `collectionId` (CollectionID)
- `pathId` (integer)

**Field Reference:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| collectionId | CollectionID | ✓ | Collection containing the target asset |
| pathId | integer | ✓ | Unity m_PathID within the referenced collection |

**JSON Schema:**
```json
{
  "$anchor": "AssetRef",
  "type": "object",
  "additionalProperties": false,
  "required": ["collectionId", "pathId"],
  "properties": {
    "collectionId": {"$ref": "#/$defs/CollectionID"},
    "pathId": {
      "type": "integer",
      "description": "Unity m_PathID within the referenced collection."
    }
  }
}
```

**Example:**
```json
{
  "collectionId": "sharedassets0",
  "pathId": 123
}
```

**Unity Correspondence:**
```csharp
// Unity PPtr<T> structure
class PPtr<T> {
    int m_FileID;   // -> collectionId (via dependency array)
    long m_PathID;  // -> pathId
}
```

**Usage:**
- Scene hierarchy references (`scenes.schema.json`)
- Asset dependency targets (`asset_dependencies.schema.json`)
- Type definition script references (`type_definitions.schema.json`)

---

### NullableAssetRef

**Anchor:** `#NullableAssetRef`
**Type:** `object` or `null`

**Description:**
Optional Unity asset reference; null denotes unresolved or intentionally empty edges. Corresponds to Unity's null PPtr (FileID=0, PathID=0).

**Schema:**
```json
{
  "$anchor": "NullableAssetRef",
  "anyOf": [
    {"$ref": "#AssetRef"},
    {"type": "null"}
  ]
}
```

**Examples:**

**Valid reference:**
```json
{
  "collectionId": "sharedassets0",
  "pathId": 123
}
```

**Null reference:**
```json
null
```

**Unity Correspondence:**
```csharp
PPtr<GameObject> optionalObject;  // Can be null
// When null: m_FileID=0, m_PathID=0
```

**Usage:**
- Optional asset fields
- Unresolved dependencies
- Scene roots (may be null)

---

### BundleRef

**Anchor:** `#BundleRef`
**Type:** `object`
**Additional Properties:** `false`

**Description:**
Reference to a Bundle node using stable PK. Bundles represent container structures in AssetRipper's hierarchy (GameBundle, ProcessedBundle, etc.).

**Required Fields:**
- `bundlePk` (string, 8-char hex)

**Optional Fields:**
- `bundleName` (string)

**Field Reference:**

| Field | Type | Required | Pattern | Description |
|-------|------|----------|---------|-------------|
| bundlePk | string | ✓ | `^[A-F0-9]{8}$` | Stable 8-character hex identifier for the bundle node |
| bundleName | string | | | Optional human-readable bundle name for readability |

**JSON Schema:**
```json
{
  "$anchor": "BundleRef",
  "type": "object",
  "additionalProperties": false,
  "required": ["bundlePk"],
  "properties": {
    "bundlePk": {
      "type": "string",
      "pattern": "^[A-F0-9]{8}$"
    },
    "bundleName": {
      "type": "string"
    }
  }
}
```

**Examples:**

**Minimal (PK only):**
```json
{
  "bundlePk": "A1B2C3D4"
}
```

**With name:**
```json
{
  "bundlePk": "00000000",
  "bundleName": "GameBundle"
}
```

**Special Cases:**
- Root GameBundle always has `bundlePk: "00000000"`

**Usage:**
- Collection parent bundle (`collections.schema.json`)
- Scene bundle reference (`scenes.schema.json`)
- Bundle hierarchy edges (`bundle_hierarchy.schema.json`)

---

### SceneRef

**Anchor:** `#SceneRef`
**Type:** `object`
**Additional Properties:** `false`

**Description:**
Reference to a Scene using Unity GUID. GUID is the primary key as it's unique across the entire project.

**Required Fields:**
- `sceneGuid` (UnityGuid)

**Optional Fields:**
- `sceneName` (string)
- `scenePath` (string)

**Field Reference:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| sceneGuid | UnityGuid | ✓ | Unity GUID of the scene asset |
| sceneName | string | | Optional scene name for human readability |
| scenePath | string | | Optional scene path (e.g., Assets/Scenes/MainMenu.unity) |

**JSON Schema:**
```json
{
  "$anchor": "SceneRef",
  "type": "object",
  "additionalProperties": false,
  "required": ["sceneGuid"],
  "properties": {
    "sceneGuid": {"$ref": "#/$defs/UnityGuid"},
    "sceneName": {"type": "string"},
    "scenePath": {"type": "string"}
  }
}
```

**Examples:**

**Minimal (GUID only):**
```json
{
  "sceneGuid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d"
}
```

**Complete:**
```json
{
  "sceneGuid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
  "sceneName": "MainMenu",
  "scenePath": "Assets/Scenes/MainMenu.unity"
}
```

**Usage:**
- Bundle scene references (`bundles.schema.json`)
- Collection scene reference (`collections.schema.json`)
- Script metadata scene provenance (`script_metadata.schema.json`)

---

### HierarchyPath

**Anchor:** `#HierarchyPath`
**Type:** `object`
**Additional Properties:** `false`

**Description:**
Complete hierarchical path from root to target entity. Represents the full Bundle nesting structure in AssetRipper.

**Required Fields:**
- `bundlePath` (array of 8-char hex strings)
- `depth` (integer, minimum 0)

**Optional Fields:**
- `bundleNames` (array of strings)

**Field Reference:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| bundlePath | array | ✓ | Ordered list of bundle PKs from root to current bundle. First element is always the root GameBundle. |
| bundleNames | array | | Human-readable bundle names corresponding to bundlePath. Array length must match bundlePath. |
| depth | integer | ✓ | Hierarchy depth where root = 0. Equals bundlePath.length - 1. |

**JSON Schema:**
```json
{
  "$anchor": "HierarchyPath",
  "type": "object",
  "additionalProperties": false,
  "required": ["bundlePath", "depth"],
  "properties": {
    "bundlePath": {
      "type": "array",
      "items": {
        "type": "string",
        "pattern": "^[A-F0-9]{8}$"
      }
    },
    "bundleNames": {
      "type": "array",
      "items": {"type": "string"}
    },
    "depth": {
      "type": "integer",
      "minimum": 0
    }
  }
}
```

**Examples:**

**Root level (depth=0):**
```json
{
  "bundlePath": ["00000000"],
  "bundleNames": ["GameBundle"],
  "depth": 0
}
```

**Nested (depth=2):**
```json
{
  "bundlePath": ["00000000", "A1B2C3D4", "E5F6G7H8"],
  "bundleNames": ["GameBundle", "level0", "Resources"],
  "depth": 2
}
```

**Validation Rules:**
- `depth` must equal `bundlePath.length - 1`
- If `bundleNames` is present, `bundleNames.length` must equal `bundlePath.length`
- First element of `bundlePath` should be root GameBundle (`"00000000"`)

**Usage:**
- Asset hierarchy metadata (`assets.schema.json`)
- Bundle hierarchy tracking
- Path reconstruction

---

## AssetPK (Root Schema)

**Anchor:** `#AssetPK`
**Type:** `object`
**Additional Properties:** `false`

**Description:**
Primary key structure for Unity assets. Combines collection identifier with PathID to create a globally unique asset reference.

**Required Fields:**
- `collectionId` (CollectionID)
- `pathId` (integer)

**Field Reference:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| collectionId | CollectionID | ✓ | Stable identifier for the containing collection |
| pathId | integer | ✓ | Unity m_PathID that uniquely identifies an object inside the owning collection. Corresponds to IUnityObjectBase.PathID. |

**JSON Schema:**
```json
{
  "AssetPK": {
    "$anchor": "AssetPK",
    "type": "object",
    "additionalProperties": false,
    "required": ["collectionId", "pathId"]
  },
  "properties": {
    "collectionId": {"$ref": "#/$defs/CollectionID"},
    "pathId": {
      "type": "integer",
      "description": "Unity m_PathID"
    }
  }
}
```

**Examples:**
```json
{
  "collectionId": "sharedassets0",
  "pathId": 123
}
```

```json
{
  "collectionId": "BUILTIN-EXTRA",
  "pathId": -1
}
```

**Characteristics:**
- Globally unique within an export
- Deterministic (same asset always gets same PK)
- Sortable (by collectionId then pathId)
- Corresponds to Unity's asset identification system

**Usage:**
- Primary key in `assets.schema.json`
- Source/target in `asset_dependencies.schema.json`
- Index keys in `by_class.schema.json`

**Unity Correspondence:**
```csharp
// Unity asset identification
class ObjectInfo {
    long m_PathID;           // -> pathId
    string collectionName;   // -> collectionId (derived)
}
```

---

## Cross-Schema References

### Reference Syntax

Core types are referenced from other schemas using JSON Schema `$ref`:

**From same document:**
```json
{"$ref": "#/$defs/CollectionID"}
```

**From other schemas:**
```json
{"$ref": "https://schemas.assetripper.dev/assetdump/v2/core.schema.json#/$defs/CollectionID"}
```

**Using anchors:**
```json
{"$ref": "https://schemas.assetripper.dev/assetdump/v2/core.schema.json#AssetPK"}
```

### Schemas Using Core Types

**All schemas** reference core.schema.json for:
- CollectionID (collection identification)
- AssetPK/AssetRef (asset references)
- BundleRef (bundle references)
- UnityGuid (GUID fields)
- Timestamp (temporal metadata)

**Heavy users:**
- `assets.schema.json` - AssetPK, HierarchyPath, CollectionID
- `asset_dependencies.schema.json` - AssetPK (from/to fields)
- `scenes.schema.json` - UnityGuid, AssetRef, SceneRef, BundleRef
- `collections.schema.json` - CollectionID, BundleRef, SceneRef

---

## Validation Examples

### CollectionID Validation

**Valid:**
```json
"sharedassets0"
"level0"
"BUILTIN-EXTRA"
"A1B2C3D4"
"my_collection"
"collection-1"
```

**Invalid:**
```json
"a"           // Too short (min 2 chars)
""            // Empty string
"my collection"  // Contains space
"collection@1"   // Invalid character @
```

### StableKey Validation

**Valid:**
```json
"sharedassets0:100"
"level0:-1"
"BUILTIN-EXTRA:12"
```

**Invalid:**
```json
"sharedassets0"     // Missing :pathId
"sharedassets0:"    // Missing pathId value
":100"              // Missing collectionId
"sharedassets0:abc" // PathId not numeric
```

### UnityGuid Validation

**Valid:**
```json
"1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d"                    // 32-hex
"1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D"                    // Uppercase
"1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d"                // Canonical
```

**Invalid:**
```json
"1a2b3c4d"                                           // Too short
"1a2b3c4d-5e6f-7a8b-9c0d"                            // Incomplete canonical
"1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6dXX"                 // Too long
"1a2b3c4d-5e6f7a8b-9c0d-1e2f3a4b5c6d"                // Wrong dash placement
```

---

## Implementation Notes

### Type Safety

All core types have strict validation:
- **Pattern matching:** Regex patterns enforce format
- **Length constraints:** Min/max length where applicable
- **Required fields:** No optional required fields
- **Additional properties:** Disabled for strict typing

### Performance Considerations

**Identifier Lookups:**
- CollectionID: O(1) hash table lookup
- StableKey: Parse once, cache collectionId/pathId
- AssetPK: Composite key, use tuple or struct

**Memory Usage:**
- CollectionID: 8-32 bytes (short strings)
- UnityGuid: 32 bytes (hex string) or 16 bytes (binary)
- AssetPK: 16-24 bytes (depends on pathId size)

### Best Practices

1. **Cache parsed values:** Don't parse StableKey repeatedly
2. **Use binary GUIDs:** Convert hex strings to 16-byte binary for storage
3. **Index by AssetPK:** Use composite index (collectionId, pathId)
4. **Validate early:** Check patterns at ingestion time

---

## Related Schemas

### Direct Dependents

Every schema in the AssetDump v2 system references core.schema.json:

**Facts Layer:**
- assets.schema.json
- bundles.schema.json
- collections.schema.json
- scenes.schema.json
- script_metadata.schema.json
- script_sources.schema.json
- types.schema.json
- type_definitions.schema.json
- type_members.schema.json
- assemblies.schema.json

**Relations Layer:**
- asset_dependencies.schema.json
- collection_dependencies.schema.json
- bundle_hierarchy.schema.json
- assembly_dependencies.schema.json
- script_type_mapping.schema.json
- type_inheritance.schema.json

**Indexes Layer:**
- by_class.schema.json
- by_collection.schema.json

**Metrics Layer:**
- scene_stats.schema.json
- asset_distribution.schema.json
- dependency_stats.schema.json

---

## Change History

**Version 2.0 (2025-11-11):**
- Added domain field support across all schemas
- Enhanced UnityGuid to support both 32-hex and canonical formats
- Added CompressionCodec enumeration
- Improved documentation and examples

**Version 1.0:**
- Initial release
- Core type definitions established
- Reference structures defined

---

**End of core.schema.json Reference**
