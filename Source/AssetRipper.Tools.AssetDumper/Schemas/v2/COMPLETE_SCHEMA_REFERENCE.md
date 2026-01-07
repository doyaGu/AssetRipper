# AssetRipper AssetDump v2 Schema - Complete Reference Documentation

**Version:** 2.0
**Schema Standard:** JSON Schema Draft 2020-12
**Last Updated:** 2025-11-16

---

## Table of Contents

1. [Introduction](#introduction)
2. [Architecture Overview](#architecture-overview)
3. [Core Concepts](#core-concepts)
4. [Shared Type Definitions](#shared-type-definitions)
5. [Schema Layers](#schema-layers)
   - [Facts Layer](#facts-layer)
   - [Relations Layer](#relations-layer)
   - [Indexes Layer](#indexes-layer)
   - [Metrics Layer](#metrics-layer)
6. [Hierarchy Model](#hierarchy-model)
7. [Identifier System](#identifier-system)
8. [Usage Patterns](#usage-patterns)
9. [Query Examples](#query-examples)
10. [Implementation Notes](#implementation-notes)
11. [Schema Index](#schema-index)

---

## Introduction

The AssetRipper AssetDump v2 schema system provides a comprehensive, structured format for representing Unity game assets extracted by AssetRipper. This reference documentation describes all 23 schemas that make up the complete data model, designed for developers implementing tools, analyzers, and queries against AssetRipper export data.

### Design Goals

- **Self-contained:** All schemas reference shared core definitions; no external dependencies
- **Type-safe:** Strong typing with JSON Schema validation rules
- **Queryable:** Optimized structure for graph queries and analysis
- **Stable:** Deterministic identifiers for consistent cross-export references
- **Scalable:** Tested on projects with 200K+ assets

### Key Features

- **4-layer architecture:** Facts, Relations, Indexes, and Metrics
- **Domain field:** Every record includes a `domain` identifier for NDJSON streaming
- **Stable identifiers:** FNV-1a hash-based IDs for reproducible exports
- **Comprehensive coverage:** 10 fact schemas, 6 relation schemas, 3 indexes, 3 metrics
- **Unity metadata:** Complete preservation of Unity serialization details

---

## Architecture Overview

The schema system is organized into four distinct layers:

```
┌─────────────────────────────────────────────────────┐
│                  METRICS LAYER                      │
│  ┌─────────────┐ ┌─────────────┐ ┌──────────────┐ │
│  │Scene Stats  │ │Asset Distrib│ │Dependency    │ │
│  │             │ │             │ │Stats         │ │
│  └─────────────┘ └─────────────┘ └──────────────┘ │
└─────────────────────────────────────────────────────┘
                         ↑
┌─────────────────────────────────────────────────────┐
│                  INDEXES LAYER                      │
│  ┌─────────────────┐  ┌──────────────────────────┐ │
│  │ By Class        │  │ By Collection            │ │
│  └─────────────────┘  └──────────────────────────┘ │
└─────────────────────────────────────────────────────┘
                         ↑
┌─────────────────────────────────────────────────────┐
│                  RELATIONS LAYER                    │
│  ┌──────────────┐ ┌─────────────┐ ┌─────────────┐ │
│  │Asset Deps    │ │Bundle Hier  │ │Assembly Deps│ │
│  │Collection Dep│ │Script Type  │ │Type Inherit │ │
│  └──────────────┘ └─────────────┘ └─────────────┘ │
└─────────────────────────────────────────────────────┘
                         ↑
┌─────────────────────────────────────────────────────┐
│                   FACTS LAYER                       │
│  ┌─────────┐ ┌─────────┐ ┌───────┐ ┌────────────┐ │
│  │Assets   │ │Bundles  │ │Collect│ │Scenes      │ │
│  ├─────────┤ ├─────────┤ ├───────┤ ├────────────┤ │
│  │Scripts  │ │Types    │ │Type   │ │Assemblies  │ │
│  │Metadata │ │         │ │Members│ │            │ │
│  └─────────┘ └─────────┘ └───────┘ └────────────┘ │
└─────────────────────────────────────────────────────┘
```

### Layer Responsibilities

**Facts Layer** (10 schemas)
- Primary entity data: assets, bundles, collections, scenes
- Type system: types, type definitions, type members, assemblies
- Script metadata: MonoScript information, source code references
- Single source of truth for all entity attributes

**Relations Layer** (6 schemas)
- Edges connecting entities: dependencies, hierarchy, inheritance
- Asset-to-asset references (PPtr structure)
- Bundle parent-child relationships
- Assembly and type relationships

**Indexes Layer** (3 schemas)
- Query acceleration structures
- Grouped asset lists (by class, by collection, by name)
- Pre-computed aggregations for fast lookups

**Metrics Layer** (3 schemas)
- Derived statistics and analytics
- Scene complexity metrics
- Asset distribution analysis
- Dependency graph health

---

## Core Concepts

### Domain Field

Every schema includes a required `domain` field with a constant string value:

```json
{
  "domain": "assets",
  "pk": {...},
  ...
}
```

**Purpose:**
- Enables mixed NDJSON streaming (multiple schema types in one file)
- Provides type identification for dynamic dispatch
- Supports query routing and filtering

### Primary Keys

Each schema defines its primary key structure:

| Schema Type | Primary Key Format | Example |
|-------------|-------------------|---------|
| Assets | `{collectionId, pathId}` | `{collectionId: "A1B2C3D4", pathId: 1}` |
| Bundles | `pk` (8-char hex) | `"00000000"` |
| Collections | `collectionId` | `"sharedassets0.assets"` |
| Scenes | `sceneGuid` (Unity GUID) | `"1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p"` |
| Types | `classKey` (integer) | `1` |
| Type Definitions | `pk` (composite) | `"Assembly-CSharp::Game.Controllers::PlayerController"` |

### Versioning

All schemas use semantic versioning embedded in the `$id` field:

```json
"$id": "https://schemas.assetripper.dev/assetdump/v2/facts/assets.schema.json"
```

---

## Shared Type Definitions

The `core.schema.json` file defines common types used across all schemas.

### CollectionID

```json
{
  "type": "string",
  "description": "Stable identifier for a serialized collection",
  "minLength": 1,
  "pattern": "^[A-Za-z0-9:_-]{2,}$"
}
```

**Usage:** Identifies asset files (sharedassets1.assets, level0, BUILTIN-EXTRA)
**Properties:** Case-insensitive, supports alphanumeric + `:_-`

### StableKey

```json
{
  "type": "string",
  "description": "Deterministic key '<collectionId>:<pathId>'",
  "pattern": "^[A-Za-z0-9:_-]+:-?\\d+$"
}
```

**Format:** `<collectionId>:<pathId>`
**Example:** `"sharedassets1:100"`, `"level0:-1"`
**Purpose:** Lexicographic sorting, consistent ordering

### UnityGuid

```json
{
  "type": "string",
  "description": "Unity GUID in 32 hex or canonical format",
  "pattern": "^([0-9A-Fa-f]{32}|[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})$"
}
```

**Formats:**
- 32 hex: `"1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d"`
- Canonical: `"1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d"`

### AssetPK (Asset Primary Key)

```json
{
  "type": "object",
  "required": ["collectionId", "pathId"],
  "properties": {
    "collectionId": {"$ref": "#/$defs/CollectionID"},
    "pathId": {
      "type": "integer",
      "description": "Unity m_PathID"
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

### AssetRef (Asset Reference)

```json
{
  "type": "object",
  "required": ["collectionId", "pathId"],
  "properties": {
    "collectionId": {"$ref": "#/$defs/CollectionID"},
    "pathId": {"type": "integer"}
  }
}
```

**Corresponds to:** Unity PPtr<T> structure
**Usage:** Cross-file asset references

### BundleRef (Bundle Reference)

```json
{
  "type": "object",
  "required": ["bundlePk"],
  "properties": {
    "bundlePk": {
      "type": "string",
      "pattern": "^[A-F0-9]{8}$"
    },
    "bundleName": {"type": "string"}
  }
}
```

### SceneRef (Scene Reference)

```json
{
  "type": "object",
  "required": ["sceneGuid"],
  "properties": {
    "sceneGuid": {"$ref": "#/$defs/UnityGuid"},
    "sceneName": {"type": "string"},
    "scenePath": {"type": "string"}
  }
}
```

### HierarchyPath

```json
{
  "type": "object",
  "required": ["bundlePath", "depth"],
  "properties": {
    "bundlePath": {
      "type": "array",
      "items": {"type": "string", "pattern": "^[A-F0-9]{8}$"}
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

**Example:**
```json
{
  "bundlePath": ["00000000", "A1B2C3D4", "E5F6G7H8"],
  "bundleNames": ["GameBundle", "level0", "Resources"],
  "depth": 2
}
```

### Timestamp

```json
{
  "type": "string",
  "format": "date-time",
  "description": "UTC timestamp in ISO-8601 format"
}
```

**Example:** `"2025-11-16T13:30:00Z"`

### CompressionCodec

```json
{
  "type": "string",
  "enum": ["none", "gzip", "zstd", "zstd-seekable"]
}
```

---

## Schema Layers

## Facts Layer

The Facts layer contains 10 schemas representing core entity data.

### 1. assets.schema.json

**Purpose:** Individual Unity object metadata and serialized data

**Domain:** `"assets"`

**Primary Key:** `pk` (AssetPK structure)

**Required Fields:**
- `domain` (const: "assets")
- `pk` (AssetPK)
- `classKey` (integer)

**Optional Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `pathId` | integer | PathID within collection |
| `className` | string | Unity class name (GameObject, MonoBehaviour, etc.) |
| `name` | string | Asset name (m_Name or INamed) |
| `originalPath` | string | Original Unity project path |
| `originalDirectory` | string | Original directory path |
| `originalName` | string | Original filename without extension |
| `originalExtension` | string | Original file extension (.png, .fbx) |
| `assetBundleName` | string | AssetBundle name if applicable |
| `hierarchy` | HierarchyPath | Complete bundle path |
| `collectionName` | string | Containing collection name |
| `bundleName` | string | Parent bundle name |
| `sceneName` | string | Scene name if applicable |
| `unity` | object | Unity metadata (classId, typeId, serializedTypeIndex, etc.) |
| `data` | any | Serialized Unity object payload (JSON) |
| `hash` | string | Content hash (sha1:<hex>) |

**Unity Metadata Object:**
```json
{
  "classId": 1,
  "typeId": 1,
  "serializedTypeIndex": 5,
  "scriptTypeIndex": -1,
  "isStripped": false,
  "serializedVersion": 6
}
```

**Example:**
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
    "isStripped": false
  }
}
```

**Links To:**
- `types.schema.json` via `classKey`
- `collections.schema.json` via `pk.collectionId`

---

### 2. bundles.schema.json

**Purpose:** Bundle metadata capturing hierarchy and aggregated counts

**Domain:** `"bundles"`

**Primary Key:** `pk` (8-char hex string)

**Required Fields:** (28 total)
- `domain`, `pk`, `name`, `bundleType`, `isRoot`, `hierarchyDepth`, `hierarchyPath`
- `childBundlePks`, `directCollectionCount`, `totalCollectionCount`
- `directSceneCollectionCount`, `totalSceneCollectionCount`
- `directChildBundleCount`, `totalChildBundleCount`
- `directResourceCount`, `totalResourceCount`
- `directFailedFileCount`, `totalFailedFileCount`
- `directAssetCount`, `totalAssetCount`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `pk` | string | Stable 8-char hex ID derived from lineage |
| `bundleType` | string | GameBundle, ProcessedBundle, etc. |
| `isRoot` | boolean | True for root GameBundle (pk="00000000") |
| `hierarchyDepth` | integer | Depth in tree (root = 0) |
| `parentPk` | string | Parent bundle PK (required if isRoot=false) |
| `ancestorPath` | array | Ordered ancestor PKs from root to parent |
| `childBundlePks` | array | Direct child bundle PKs |
| `collectionIds` | array | Direct collection IDs |
| `resources` | array | Resource file objects |
| `failedFiles` | array | Failed file objects |
| `scenes` | array | Scene references |

**Counter Fields:**
- Direct vs Total: "Direct" counts immediate children, "Total" includes entire subtree
- Collection counts, scene counts, child bundle counts, resource counts, failed file counts, asset counts

**Conditional Requirement:**
- If `isRoot: false`, then `parentPk` and `bundleIndex` are required

**Example:**
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

---

### 3. collections.schema.json

**Purpose:** AssetCollection metadata (Unity SerializedFiles and processed collections)

**Domain:** `"collections"`

**Primary Key:** `collectionId`

**Required Fields:**
- `domain`, `collectionId`, `name`, `platform`, `unityVersion`, `endian`
- `bundle`, `dependencies`, `assetCount`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `collectionId` | CollectionID | Unique collection identifier |
| `name` | string | Filename (sharedassets1.assets) |
| `collectionType` | enum | Serialized / Processed / Virtual |
| `friendlyName` | string | Human-friendly scene identifier |
| `bundleName` | string | AssetBundle name (if from bundle) |
| `filePath` | string | Relative file path |
| `platform` | string | Unity build target/platform |
| `unityVersion` | string | Current Unity version |
| `originalUnityVersion` | string | Original version before processing |
| `formatVersion` | integer | SerializedFile format version |
| `endian` | enum | LittleEndian / BigEndian |
| `flagsRaw` | string | Bitflag string |
| `flags` | array | Parsed flag set |
| `isSceneCollection` | boolean | True for .unity files |
| `bundle` | BundleRef | Parent bundle reference |
| `scene` | SceneRef | Scene reference (if applicable) |
| `collectionIndex` | integer | Index within parent bundle |
| `dependencies` | array | Ordered CollectionID dependencies |
| `dependencyIndices` | object | Map: CollectionID → index |
| `assetCount` | integer | Total assets in collection |

**Dependencies:**
- Index 0 is always self-reference (Unity convention)
- `null` entries indicate unresolved dependencies (schema explicitly allows `anyOf: [CollectionID, null]`)
- `dependencyIndices` only contains resolved entries (excludes null positions)

**Source Object:**
```json
{
  "source": {
    "uri": "file://path/to/collection",
    "offset": 1024,
    "size": 524288
  }
}
```

**Unity Metadata:**
```json
{
  "unity": {
    "builtInClassification": "BUILTIN-EXTRA"
  }
}
```

**Example:**
```json
{
  "domain": "collections",
  "collectionId": "sharedassets0",
  "name": "sharedassets0.assets",
  "collectionType": "Serialized",
  "platform": "StandaloneWindows64",
  "unityVersion": "2021.3.5f1",
  "endian": "LittleEndian",
  "isSceneCollection": false,
  "bundle": {
    "bundlePk": "A1B2C3D4",
    "bundleName": "level0"
  },
  "dependencies": [
    "sharedassets0",
    "sharedassets1",
    "BUILTIN-EXTRA"
  ],
  "dependencyIndices": {
    "sharedassets0": 0,
    "sharedassets1": 1,
    "BUILTIN-EXTRA": 2
  },
  "assetCount": 523
}
```

---

### 4. scenes.schema.json

**Purpose:** Scene aggregation with GameObject hierarchy and component counts

**Domain:** `"scenes"`

**Primary Key:** `sceneGuid`

**Required Fields:** (19 total including counts)
- `domain`, `name`, `sceneGuid`, `scenePath`, `exportedAt`, `version`, `platform`
- `sceneCollectionCount`, `collectionIds`, `assetCount`, `gameObjectCount`
- `componentCount`, `managerCount`, `prefabInstanceCount`, `dependencyCount`
- `rootGameObjectCount`, `strippedAssetCount`, `hiddenAssetCount`, `hasSceneRoots`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `sceneGuid` | UnityGuid | Primary key (stable across exports) |
| `scenePath` | string | Project-relative path |
| `exportedAt` | Timestamp | Export timestamp |
| `version` | string | Unity version |
| `primaryCollectionId` | CollectionID | First collection in scene |
| `collectionIds` | array | All collection IDs for this scene |
| `collectionDetails` | array | Detailed metadata per collection |
| `bundle` | BundleRef | Bundle containing primary collection |
| `hierarchy` | AssetRef | SceneHierarchyObject reference |

**Collection Details:**
```json
{
  "collectionDetails": [
    {
      "collectionId": "level0",
      "bundle": {"bundlePk": "A1B2C3D4", "bundleName": "level0"},
      "isPrimary": true,
      "assetCount": 1523
    }
  ]
}
```

**Counts:**
- `assetCount`: Total assets across all collections
- `gameObjectCount`: Number of GameObjects
- `componentCount`: Number of Components
- `managerCount`: Number of LevelGameManager assets
- `prefabInstanceCount`: Number of PrefabInstance assets
- `rootGameObjectCount`: Root GameObjects without parent
- `strippedAssetCount`: Assets with stripped type info
- `hiddenAssetCount`: Assets excluded from YAML export

**Asset Lists (optional):**
- `sceneRoots`: SceneRoots asset references
- `rootGameObjects`: Root GameObject references
- `gameObjects`: All GameObject references
- `components`: All Component references
- `managers`: LevelGameManager references
- `prefabInstances`: PrefabInstance references
- `strippedAssets`: Stripped asset references
- `hiddenAssets`: Hidden asset references

**Example:**
```json
{
  "domain": "scenes",
  "name": "MainMenu",
  "sceneGuid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
  "scenePath": "Assets/Scenes/MainMenu.unity",
  "exportedAt": "2025-11-16T13:30:00Z",
  "version": "2021.3.5f1",
  "platform": "StandaloneWindows64",
  "sceneCollectionCount": 1,
  "collectionIds": ["level0"],
  "primaryCollectionId": "level0",
  "bundle": {
    "bundlePk": "A1B2C3D4",
    "bundleName": "level0"
  },
  "assetCount": 1523,
  "gameObjectCount": 245,
  "componentCount": 687,
  "managerCount": 5,
  "prefabInstanceCount": 12,
  "dependencyCount": 89,
  "rootGameObjectCount": 15,
  "strippedAssetCount": 0,
  "hiddenAssetCount": 0,
  "hasSceneRoots": true
}
```

---

### 5. script_metadata.schema.json

**Purpose:** MonoScript asset metadata with assembly linkage and resolution status

**Domain:** `"script_metadata"`

**Primary Key:** `pk` (StableKey format)

**Required Fields:**
- `domain`, `pk`, `collectionId`, `pathId`, `classId`, `className`
- `fullName`, `assemblyName`, `isPresent`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `pk` | StableKey | `<collectionId>:<pathId>` format |
| `collectionId` | CollectionID | Collection containing MonoScript |
| `pathId` | integer | Unity m_PathID for MonoScript |
| `classId` | integer | Unity ClassID (typically 115) |
| `className` | string | Short class name |
| `fullName` | string | Fully qualified type name |
| `namespace` | string | Namespace |
| `assemblyName` | string | Assembly name (fixed via FixAssemblyName) |
| `assemblyNameRaw` | string | Original assembly name |
| `isPresent` | boolean | Script type found in assemblies |
| `isGeneric` | boolean | Generic type definition |
| `genericParameterCount` | integer | Number of generic parameters |
| `executionOrder` | integer | Script execution order |
| `scriptGuid` | UnityGuid | Script GUID |
| `assemblyGuid` | UnityGuid | Assembly GUID |
| `scriptFileId` | integer | MonoScript file identifier |
| `propertiesHash` | string | m_PropertiesHash (8 or 32 hex chars) |

**Scene Provenance:**
```json
{
  "scene": {
    "name": "MainMenu",
    "path": "Assets/Scenes/MainMenu.unity",
    "guid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d"
  }
}
```

**Example:**
```json
{
  "domain": "script_metadata",
  "pk": "sharedassets0:100",
  "collectionId": "sharedassets0",
  "pathId": 100,
  "classId": 115,
  "className": "PlayerController",
  "fullName": "Game.Controllers.PlayerController",
  "namespace": "Game.Controllers",
  "assemblyName": "Assembly-CSharp",
  "assemblyNameRaw": "Assembly-CSharp",
  "isPresent": true,
  "isGeneric": false,
  "scriptGuid": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"
}
```

---

### 6. script_sources.schema.json

**Purpose:** Links MonoScripts to decompiled source files with metadata

**Domain:** `"script_sources"`

**Primary Key:** `pk` (Unity GUID from ScriptHashing)

**Required Fields:**
- `domain`, `pk`, `scriptPk`, `assemblyGuid`, `sourcePath`
- `sourceSize`, `lineCount`, `sha256`, `language`, `decompiler`, `decompilationStatus`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `pk` | UnityGuid | Script GUID |
| `scriptPk` | StableKey | MonoScript reference |
| `assemblyGuid` | string | Assembly GUID (32 hex) |
| `sourcePath` | string | Relative path to decompiled file |
| `sourceSize` | integer | File size in bytes |
| `lineCount` | integer | Number of lines |
| `characterCount` | integer | Total characters (optional) |
| `sha256` | string | SHA256 hash (64 hex chars) |
| `language` | enum | CSharp / UnityShader / HLSL / UnityScript |
| `decompiler` | string | Decompiler name (typically "ILSpy") |
| `decompilerVersion` | string | Decompiler version |
| `decompilationStatus` | enum | success / failed / empty / skipped |
| `isEmpty` | boolean | EmptyScript placeholder |
| `errorMessage` | string | Error if decompilation failed |
| `isPresent` | boolean | Script type exists in assembly |
| `isGeneric` | boolean | Generic type |

**Future Fields:**
- `hasAst`: AST file exists (not currently implemented)
- `astPath`: Path to AST JSON (not currently implemented)

**Example:**
```json
{
  "domain": "script_sources",
  "pk": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6",
  "scriptPk": "sharedassets0:100",
  "assemblyGuid": "1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D",
  "sourcePath": "Scripts/PlayerController.cs",
  "sourceSize": 4521,
  "lineCount": 125,
  "sha256": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2",
  "language": "CSharp",
  "decompiler": "ILSpy",
  "decompilerVersion": "7.2.1.6856",
  "decompilationStatus": "success",
  "isEmpty": false,
  "isPresent": true,
  "isGeneric": false
}
```

---

### 7. types.schema.json

**Purpose:** Type dictionary mapping classKey to Unity ClassID/ClassName

**Domain:** `"types"`

**Primary Key:** `classKey`

**Required Fields:**
- `domain`, `classKey`, `classId`, `className`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `classKey` | integer | Stable integer identifier (assigned by exporter) |
| `classId` | integer | Unity ClassID (114=MonoBehaviour, etc.) |
| `className` | string | Unity type name |
| `typeId` | integer | SerializedType.TypeID |
| `serializedTypeIndex` | integer | Index in SerializedFile.Types array (-1 if N/A) |
| `scriptTypeIndex` | integer | Script type index for MonoBehaviour (-1 if N/A) |
| `isStripped` | boolean | Type definition stripped from build |
| `originalClassName` | string | Original Unity name before processing |
| `baseClassName` | string | Base class name |
| `isAbstract` | boolean | Abstract class |
| `isEditorOnly` | boolean | Editor-only class |
| `isReleaseOnly` | boolean | Release-only class |

**MonoScript Metadata (ClassID 114 only):**
```json
{
  "monoScript": {
    "assemblyName": "Assembly-CSharp",
    "namespace": "Game.Controllers",
    "className": "PlayerController",
    "scriptGuid": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"
  }
}
```

**Example:**
```json
{
  "domain": "types",
  "classKey": 1,
  "classId": 1,
  "className": "GameObject",
  "typeId": 1,
  "serializedTypeIndex": 5,
  "scriptTypeIndex": -1,
  "isStripped": false,
  "isAbstract": false,
  "isEditorOnly": false,
  "isReleaseOnly": false
}
```

**MonoBehaviour Example:**
```json
{
  "domain": "types",
  "classKey": 150,
  "classId": 114,
  "className": "PlayerController",
  "typeId": 114,
  "scriptTypeIndex": 25,
  "isStripped": false,
  "monoScript": {
    "assemblyName": "Assembly-CSharp",
    "namespace": "Game.Controllers",
    "className": "PlayerController",
    "scriptGuid": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"
  }
}
```

---

### 8. type_definitions.schema.json

**Purpose:** Complete .NET type definitions from assemblies

**Domain:** `"type_definitions"`

**Primary Key:** `pk` (composite: `ASSEMBLY::NAMESPACE::TYPENAME`)

**Required Fields:**
- `domain`, `pk`, `assemblyGuid`, `assemblyName`, `typeName`, `fullName`
- `isClass`, `isStruct`, `isInterface`, `isEnum`, `isAbstract`, `isSealed`, `isGeneric`, `visibility`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `pk` | string | Composite key (:: separator) |
| `assemblyGuid` | string | Assembly GUID (16-char hex) |
| `assemblyName` | string | Assembly name |
| `namespace` | string | Type namespace (empty for global) |
| `typeName` | string | Simple type name |
| `fullName` | string | Fully qualified type name |
| `isClass` | boolean | Type is a class |
| `isStruct` | boolean | Type is a struct |
| `isInterface` | boolean | Type is an interface |
| `isEnum` | boolean | Type is an enum |
| `isAbstract` | boolean | Abstract type |
| `isSealed` | boolean | Sealed type |
| `isGeneric` | boolean | Generic type |
| `genericParameterCount` | integer | Number of generic parameters |
| `visibility` | enum | public / internal / private / protected / etc. |
| `baseType` | string | Fully qualified base type name |
| `isNested` | boolean | Nested type |
| `declaringType` | string | Declaring type for nested types |
| `interfaces` | array | Implemented interface names |
| `fieldCount` | integer | Number of fields |
| `methodCount` | integer | Number of methods |
| `propertyCount` | integer | Number of properties |
| `isMonoBehaviour` | boolean | Derives from MonoBehaviour |
| `isScriptableObject` | boolean | Derives from ScriptableObject |
| `isSerializable` | boolean | Serializable by Unity |

**Script Reference:**
```json
{
  "scriptRef": {
    "collectionId": "sharedassets0",
    "pathId": 100,
    "scriptGuid": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"
  }
}
```

**Example:**
```json
{
  "domain": "type_definitions",
  "pk": "Assembly-CSharp::Game.Controllers::PlayerController",
  "assemblyGuid": "1A2B3C4D5E6F7A8B",
  "assemblyName": "Assembly-CSharp",
  "namespace": "Game.Controllers",
  "typeName": "PlayerController",
  "fullName": "Game.Controllers.PlayerController",
  "isClass": true,
  "isStruct": false,
  "isInterface": false,
  "isEnum": false,
  "isAbstract": false,
  "isSealed": false,
  "isGeneric": false,
  "visibility": "public",
  "baseType": "UnityEngine.MonoBehaviour",
  "isNested": false,
  "interfaces": [],
  "fieldCount": 8,
  "methodCount": 15,
  "propertyCount": 3,
  "isMonoBehaviour": true,
  "isScriptableObject": false,
  "isSerializable": true
}
```

---

### 9. type_members.schema.json

**Purpose:** Detailed type member information (fields, properties, methods)

**Domain:** `"type_members"`

**Primary Key:** `pk` (composite: `ASSEMBLY::NAMESPACE::TYPENAME::MEMBERNAME`)

**Required Fields:**
- `domain`, `pk`, `assemblyGuid`, `typeFullName`, `memberName`
- `memberKind`, `memberType`, `visibility`, `isStatic`, `serialized`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `pk` | string | Composite key with :: separator |
| `assemblyGuid` | string | Assembly GUID (16-char hex) |
| `typeFullName` | string | Owner type full name |
| `memberName` | string | Member name |
| `memberKind` | enum | field / property / method / event / constructor / nestedType |
| `memberType` | string | Member type (field type, return type, etc.) |
| `visibility` | enum | public / private / protected / internal / etc. |
| `isStatic` | boolean | Static member |
| `isVirtual` | boolean | Virtual (methods/properties) |
| `isOverride` | boolean | Overrides base member |
| `isSealed` | boolean | Sealed (prevents override) |
| `serialized` | boolean | Unity serializes this member |
| `attributes` | array | Applied C# attributes |
| `documentationString` | string | XML documentation |
| `obsoleteMessage` | string | Obsolete attribute message |
| `nativeName` | string | Unity native name |
| `isCompilerGenerated` | boolean | Compiler-generated member |

**Property-specific:**
- `hasGetter` / `hasSetter` / `hasParameters`

**Field-specific:**
- `isConst` / `isReadOnly` / `constantValue`
- `serializeField` / `hideInInspector`

**Method-specific:**
- `parameterCount` / `parameters` / `isAbstract` / `isGeneric` / `genericParameterCount`

**Parameter Object:**
```json
{
  "name": "value",
  "type": "System.Int32",
  "isOptional": false,
  "defaultValue": null
}
```

**Example:**
```json
{
  "domain": "type_members",
  "pk": "Assembly-CSharp::Game.Controllers::PlayerController::currentHealth",
  "assemblyGuid": "1A2B3C4D5E6F7A8B",
  "typeFullName": "Game.Controllers.PlayerController",
  "memberName": "currentHealth",
  "memberKind": "field",
  "memberType": "System.Int32",
  "visibility": "private",
  "isStatic": false,
  "serialized": true,
  "serializeField": true,
  "hideInInspector": false,
  "isReadOnly": false,
  "attributes": ["UnityEngine.SerializeField"]
}
```

---

### 10. assemblies.schema.json

**Purpose:** Assembly metadata including DLL paths and type counts

**Domain:** `"assemblies"`

**Primary Key:** `pk` (32-char hex Assembly GUID)

**Required Fields:**
- `domain`, `pk`, `name`, `fullName`, `scriptingBackend`
- `typeCount`, `scriptCount`, `isDynamic`, `isEditor`, `assemblyType`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `pk` | string | Assembly GUID (32-char uppercase hex) |
| `name` | string | Assembly simple name |
| `fullName` | string | Fully qualified name with version/culture/token |
| `version` | string | Assembly version (0.0.0.0 format) |
| `targetFramework` | string | Target framework (netstandard2.1, etc.) |
| `scriptingBackend` | enum | Mono / IL2CPP / Unknown |
| `runtime` | string | Runtime version description |
| `assemblyType` | enum | Predefined / UnityEngine / UnityExtension / User / System |
| `dllPath` | string | Relative path to exported DLL |
| `dllSize` | integer | DLL file size in bytes |
| `dllSha256` | string | SHA256 hash (64 hex chars) |
| `typeCount` | integer | Number of types in assembly |
| `scriptCount` | integer | Number of MonoScripts referencing assembly |
| `isDynamic` | boolean | Dynamically generated assembly |
| `isEditor` | boolean | Editor-only assembly |
| `platform` | string | Target platform |
| `mscorlibVersion` | integer | Mscorlib version (2 or 4, if mscorlib) |
| `references` | array | Referenced assembly names |
| `exportType` | enum | Decompile / Save / Skip |
| `isModified` | boolean | Modified by AssetRipper |

**Example:**
```json
{
  "domain": "assemblies",
  "pk": "1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D",
  "name": "Assembly-CSharp",
  "fullName": "Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
  "version": "0.0.0.0",
  "targetFramework": "netstandard2.1",
  "scriptingBackend": "IL2CPP",
  "runtime": ".NET Standard 2.1",
  "assemblyType": "Predefined",
  "dllPath": "Scripts/Assembly-CSharp.dll",
  "dllSize": 524288,
  "dllSha256": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2",
  "typeCount": 234,
  "scriptCount": 189,
  "isDynamic": false,
  "isEditor": false,
  "platform": "StandaloneWindows64",
  "references": ["UnityEngine", "UnityEngine.CoreModule"],
  "exportType": "Decompile",
  "isModified": false
}
```

---

## Relations Layer

The Relations layer contains 6 schemas representing edges between entities.

### 1. asset_dependencies.schema.json

**Purpose:** Asset-to-asset PPtr references (Unity dependency graph)

**Domain:** `"asset_dependencies"`

**Required Fields:**
- `domain`, `from`, `to`, `edge`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `from` | AssetPK | Source asset (owns reference) |
| `to` | AssetPK | Target asset (being referenced) |
| `edge` | object | Edge metadata |
| `status` | enum | Resolution status |
| `targetType` | string | Expected target class name |
| `notes` | string | Diagnostic information |

**Edge Object:**

| Field | Type | Description |
|-------|------|-------------|
| `kind` | enum | pptr / external / internal / array_element / dictionary_key / dictionary_value |
| `field` | string | Field path (m_Material, m_Materials[2], etc.) |
| `fieldType` | string | Field type (PPtr<Material>, etc.) |
| `fileId` | integer | Unity FileID (0=same file, >0=dependency index, <0=builtin) |
| `arrayIndex` | integer | Array index if applicable |
| `isNullable` | boolean | Field can legally be null |

**Status Values:**
- `Resolved`: Target asset found
- `Missing`: Target not found
- `External`: Cross-collection reference
- `SelfReference`: from==to
- `Null`: PathID==0 (intentional null)
- `InvalidFileID`: FileID out of bounds
- `TypeMismatch`: Target type doesn't match expected

**Example:**
```json
{
  "domain": "asset_dependencies",
  "from": {
    "collectionId": "sharedassets0",
    "pathId": 1
  },
  "to": {
    "collectionId": "sharedassets1",
    "pathId": 100
  },
  "edge": {
    "kind": "pptr",
    "field": "m_Material",
    "fieldType": "PPtr<Material>",
    "fileId": 1
  },
  "status": "Resolved",
  "targetType": "Material"
}
```

---

### 2. collection_dependencies.schema.json

**Purpose:** Collection-level dependencies (SerializedFile.Dependencies)

**Domain:** `"collection_dependencies"`

**Required Fields:**
- `domain`, `sourceCollection`, `dependencyIndex`, `targetCollection`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `sourceCollection` | CollectionID | Declaring collection |
| `dependencyIndex` | integer | Index in dependency list (0=self) |
| `targetCollection` | CollectionID or null | Target collection (null if unresolved) |
| `resolved` | boolean | Successfully resolved |
| `source` | enum | serialized / dynamic / builtin |

**FileIdentifier Object:**
```json
{
  "fileIdentifier": {
    "guid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
    "type": 0,
    "pathName": "Assets/Scenes/level0.unity"
  }
}
```

**FileIdentifier Type:**
- 0 = Asset
- 1 = Serialized
- 2 = Meta
- 3 = BuiltinExtra
- 4 = Unknown

**Example:**
```json
{
  "domain": "collection_dependencies",
  "sourceCollection": "sharedassets0",
  "dependencyIndex": 1,
  "targetCollection": "sharedassets1",
  "resolved": true,
  "source": "serialized",
  "fileIdentifier": {
    "guid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
    "type": 0,
    "pathName": "sharedassets1.assets"
  }
}
```

---

### 3. bundle_hierarchy.schema.json

**Purpose:** Parent-child relationships between bundles

**Domain:** `"bundle_hierarchy"`

**Required Fields:**
- `domain`, `parentPk`, `childPk`, `childIndex`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `parentPk` | string | Parent bundle PK (8-char hex) |
| `parentName` | string | Parent bundle name |
| `childPk` | string | Child bundle PK (8-char hex) |
| `childIndex` | integer | Index in parent's child list |
| `childName` | string | Child bundle name |
| `childBundleType` | enum | GameBundle / SerializedBundle / ProcessedBundle / etc. |
| `childDepth` | integer | Depth in hierarchy (root=0) |

**Example:**
```json
{
  "domain": "bundle_hierarchy",
  "parentPk": "00000000",
  "parentName": "GameBundle",
  "childPk": "A1B2C3D4",
  "childIndex": 0,
  "childName": "level0",
  "childBundleType": "SerializedBundle",
  "childDepth": 1
}
```

---

### 4. assembly_dependencies.schema.json

**Purpose:** Assembly-to-assembly dependency references

**Domain:** `"assembly_dependencies"`

**Required Fields:**
- `domain`, `sourceAssembly`, `targetName`, `isResolved`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `sourceAssembly` | string | Source assembly GUID (32-char hex) |
| `sourceModule` | string | Module declaring reference |
| `targetAssembly` | string or null | Target assembly GUID (null if unresolved) |
| `targetName` | string | Target assembly name |
| `version` | string | Required version (Major.Minor.Build.Revision) |
| `publicKeyToken` | string | Public key token (16-char hex) |
| `culture` | string | Culture (neutral, en-US, etc.) |
| `isResolved` | boolean | Successfully resolved |
| `dependencyType` | enum | direct / framework / plugin / unknown |
| `isFrameworkAssembly` | boolean | .NET framework/reference assembly |
| `failureReason` | string | Resolution failure reason |

**Example:**
```json
{
  "domain": "assembly_dependencies",
  "sourceAssembly": "1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D",
  "sourceModule": "Assembly-CSharp.dll",
  "targetAssembly": "2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D7E",
  "targetName": "UnityEngine.CoreModule",
  "version": "0.0.0.0",
  "culture": "neutral",
  "isResolved": true,
  "dependencyType": "direct",
  "isFrameworkAssembly": false
}
```

---

### 5. script_type_mapping.schema.json

**Purpose:** MonoScript to .NET TypeDefinition mapping with validation

**Domain:** `"script_type_mapping"`

**Required Fields:**
- `domain`, `scriptPk`, `scriptGuid`, `typeFullName`, `assemblyGuid`, `assemblyName`, `isValid`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `scriptPk` | StableKey | MonoScript PK (collectionId:pathId) |
| `scriptGuid` | UnityGuid | Script GUID |
| `typeFullName` | string | Fully qualified .NET type name |
| `assemblyGuid` | string | Assembly GUID (32-char hex) |
| `assemblyName` | string | Assembly name (fixed) |
| `namespace` | string | Type namespace |
| `className` | string | Simple class name |
| `isValid` | boolean | TypeDefinition successfully resolved |
| `failureReason` | string | Resolution failure reason (if isValid=false) |
| `isGeneric` | boolean | Generic type |
| `genericParameterCount` | integer | Generic parameter count |
| `scriptIdentifier` | string | ScriptIdentifier.UniqueName for debugging |

**Conditional:** If `isValid: false`, then `failureReason` is required

**Example:**
```json
{
  "domain": "script_type_mapping",
  "scriptPk": "sharedassets0:100",
  "scriptGuid": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6",
  "typeFullName": "Game.Controllers.PlayerController",
  "assemblyGuid": "1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D",
  "assemblyName": "Assembly-CSharp",
  "namespace": "Game.Controllers",
  "className": "PlayerController",
  "isValid": true,
  "isGeneric": false,
  "genericParameterCount": 0,
  "scriptIdentifier": "Assembly-CSharp::Game.Controllers::PlayerController"
}
```

---

### 6. type_inheritance.schema.json

**Purpose:** Type inheritance relationships for hierarchy analysis

**Domain:** `"type_inheritance"`

**Required Fields:**
- `domain`, `derivedType`, `derivedAssembly`, `baseType`, `baseAssembly`, `relationshipType`, `inheritanceDistance`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `derivedType` | string | Fully qualified derived type name |
| `derivedAssembly` | string | Assembly containing derived type |
| `baseType` | string | Fully qualified base type name |
| `baseAssembly` | string | Assembly containing base type |
| `relationshipType` | enum | class_inheritance / interface_implementation |
| `inheritanceDistance` | integer | Distance in chain (1=direct) |
| `inheritanceDepth` | integer | Depth from root (0=root) |
| `baseTypeArguments` | array | Type arguments if base is generic |
| `descendantCount` | integer | Total descendants (including self) |

**Example:**
```json
{
  "domain": "type_inheritance",
  "derivedType": "Game.Controllers.PlayerController",
  "derivedAssembly": "Assembly-CSharp",
  "baseType": "UnityEngine.MonoBehaviour",
  "baseAssembly": "UnityEngine.CoreModule",
  "relationshipType": "class_inheritance",
  "inheritanceDistance": 1,
  "inheritanceDepth": 3,
  "descendantCount": 1
}
```

---

## Indexes Layer

### 1. by_class.schema.json

**Purpose:** Assets grouped by classKey for type-based queries

**Domain:** `"by_class"`

**Required Fields:**
- `domain`, `classKey`, `assets`, `count`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `classKey` | integer | Dense class key (min: 1) |
| `assets` | array | Array of AssetPK objects |
| `count` | integer | Number of assets (must equal assets.length) |
| `className` | string | Unity type name (human-readable) |
| `classId` | integer | Unity ClassID (min: -2) |

**Example:**
```json
{
  "domain": "by_class",
  "classKey": 1,
  "className": "GameObject",
  "classId": 1,
  "assets": [
    {"collectionId": "sharedassets0", "pathId": 1},
    {"collectionId": "sharedassets0", "pathId": 5},
    {"collectionId": "level0", "pathId": 2}
  ],
  "count": 3
}
```

---

### 2. by_collection.schema.json

**Purpose:** Collection-level asset summaries with type distribution

**Domain:** `"by_collection"`

**Format:** NDJSON (one record per line)

**Required Fields:**
- `domain`, `collectionId`, `count`

**Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `domain` | string | "by_collection" |
| `collectionId` | CollectionID | Collection identifier |
| `name` | string | Collection name |
| `count` | integer | Total assets in collection |
| `isScene` | boolean | Scene collection |
| `bundleName` | string | Parent bundle name |
| `typeDistribution` | array | Top 10 types by count |
| `totalTypeCount` | integer | Total distinct types |

**Type Distribution Item:**
```json
{
  "classKey": 1,
  "className": "GameObject",
  "classId": 1,
  "count": 245
}
```

**Example (single NDJSON record):**
```json
{"domain":"by_collection","collectionId":"sharedassets0","name":"sharedassets0.assets","count":523,"isScene":false,"bundleName":"level0","typeDistribution":[{"classKey":1,"className":"GameObject","classId":1,"count":245}],"totalTypeCount":25}
```

---

### 3. by_name.schema.json

**Purpose:** Assets grouped by name for name-based queries

**Domain:** `"by_name"`

**Format:** NDJSON (one record per line)

**Required Fields:**
- `domain`, `name`, `locations`

**Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `domain` | string | "by_name" |
| `name` | string | Asset name key |
| `locations` | array | Asset locations sharing this name |

**Location Item:**
```json
{
  "collectionId": "sharedassets0",
  "pathId": 123,
  "classId": 28,
  "className": "Texture2D"
}
```

**Example:**
```json
{"domain":"by_name","name":"Main Camera","locations":[{"collectionId":"sharedassets0","pathId":1,"classId":20,"className":"Camera"}]}
```

---

## Metrics Layer

### 1. scene_stats.schema.json

**Purpose:** Scene-level statistics from SceneHierarchyObject

**Domain:** `"scene_stats"`

**Required Fields:**
- `domain`, `sceneGuid`, `sceneName`, `counts`

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `sceneGuid` | UnityGuid | Scene GUID (primary key) |
| `sceneName` | string | Scene name |
| `scenePath` | string | Project-relative path |
| `hierarchyAssetPk` | AssetPK | SceneHierarchyObject reference |
| `counts` | object | Count object (required) |
| `hasSceneRoots` | boolean | Has SceneRoots asset |
| `notes` | string | Optional notes |

**Counts Object (all required):**
```json
{
  "gameObjects": 245,
  "components": 687,
  "prefabInstances": 12,
  "managers": 5,
  "rootGameObjects": 15,
  "strippedAssets": 0,
  "hiddenAssets": 0,
  "collections": 1
}
```

**Example:**
```json
{
  "domain": "scene_stats",
  "sceneGuid": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
  "sceneName": "MainMenu",
  "scenePath": "Assets/Scenes/MainMenu.unity",
  "hierarchyAssetPk": {
    "collectionId": "level0",
    "pathId": 1
  },
  "counts": {
    "gameObjects": 245,
    "components": 687,
    "prefabInstances": 12,
    "managers": 5,
    "rootGameObjects": 15,
    "strippedAssets": 0,
    "hiddenAssets": 0,
    "collections": 1
  },
  "hasSceneRoots": true
}
```

---

### 2. asset_distribution.schema.json

**Purpose:** Asset distribution statistics by class and bundle

**Domain:** `"asset_distribution"`

**Required Fields:**
- `domain`, `summary`, `byClass`, `byBundle`

**Summary Object:**
```json
{
  "totalAssets": 201543,
  "totalBytes": 524288000,
  "uniqueClasses": 125,
  "totalCollections": 45,
  "totalBundles": 8,
  "assetsWithByteSize": 198234
}
```

**By Class Item:**
```json
{
  "classKey": 1,
  "classId": 1,
  "className": "GameObject",
  "count": 25000,
  "countWithByteSize": 24850,
  "totalBytes": 12500000,
  "averageBytes": 503,
  "minBytes": 100,
  "maxBytes": 5000,
  "medianBytes": 450
}
```

**By Bundle Item:**
```json
{
  "bundleName": "level0",
  "collections": 15,
  "totalAssets": 50000,
  "assetsWithByteSize": 49500,
  "totalBytes": 125000000,
  "averageBytes": 2525,
  "uniqueClasses": 45,
  "byClass": [
    {
      "classKey": 1,
      "classId": 1,
      "className": "GameObject",
      "count": 8000,
      "countWithByteSize": 7950,
      "totalBytes": 4000000,
      "averageBytes": 503,
      "minBytes": 100,
      "maxBytes": 2000,
      "medianBytes": 450
    }
  ]
}
```

---

### 3. dependency_stats.schema.json

**Purpose:** Dependency graph analytics and health metrics

**Domain:** `"dependency_stats"`

**Required Fields:**
- `domain`, `edges`, `degree`, `health`

**Edges Object:**
```json
{
  "total": 523456,
  "averagePerAsset": 2.6,
  "internalReferences": 450000,
  "externalReferences": 65000,
  "crossBundleReferences": 8456,
  "nullReferences": 1234,
  "unresolvedReferences": 567
}
```

**Degree Object:**
```json
{
  "outgoing": {
    "average": 2.6,
    "min": 0,
    "max": 234,
    "median": 2
  },
  "incoming": {
    "average": 2.6,
    "min": 0,
    "max": 1523,
    "median": 1
  }
}
```

**Health Object:**
```json
{
  "totalAssets": 201543,
  "noOutgoingRefs": 85000,
  "noIncomingRefs": 12000,
  "completelyIsolated": 150
}
```

**By Type Item:**
```json
{
  "classId": 28,
  "className": "Texture2D",
  "count": 15000,
  "averageOutDegree": 0.0,
  "averageInDegree": 5.2,
  "maxOutDegree": 0,
  "maxInDegree": 523
}
```

---

## Hierarchy Model

AssetRipper models Unity's asset structure as a 4-level hierarchy:

```
┌─────────────────────────────────────┐
│      GameBundle (Root)              │  Level 0
│      PK: "00000000"                 │
└──────────┬──────────────────────────┘
           │
           ├─► Bundle (Container)        Level 1+
           │   PK: 8-char hex
           │   Type: SerializedBundle,
           │         ProcessedBundle, etc.
           │   ┌──────────────────┐
           │   │ Can nest         │
           │   └──────────────────┘
           │         │
           │         ├─► AssetCollection  Level N
           │         │   (SerializedFile)
           │         │   ID: CollectionID
           │         │   ┌────────────┐
           │         │   │ sharedassets0.assets
           │         │   │ level0.unity
           │         │   └────────────┘
           │         │         │
           │         │         ├─► IUnityObjectBase (Asset)  Level N+1
           │         │         │   PK: {collectionId, pathId}
           │         │         │   ┌────────────┐
           │         │         │   │ GameObject
           │         │         │   │ MonoBehaviour
           │         │         │   │ Texture2D
           │         │         │   └────────────┘
```

### Level Characteristics

**Level 0: GameBundle (Root)**
- Always PK = `"00000000"`
- `isRoot: true`
- `hierarchyDepth: 0`
- Contains all other bundles
- Zero direct assets/collections

**Level 1+: Bundle Containers**
- Can nest recursively
- Types: SerializedBundle, ProcessedBundle, ResourceFile, WebBundle
- Track direct + total counts (collections, assets, children)
- Maintain ancestor paths

**Level N: AssetCollection**
- SerializedFile or ProcessedCollection
- Contains Unity assets (objects)
- Tracks dependencies to other collections
- Scene collections vs non-scene collections

**Level N+1: IUnityObjectBase (Assets)**
- Individual Unity objects
- GameObject, MonoBehaviour, Texture2D, etc.
- Reference other assets via PPtr
- Contain serialized data payload

---

## Identifier System

### CollectionID Generation

**Algorithm:** FNV-1a hash of collection name

**Format:** 8-character uppercase hex string

**Example:**
```
Input: "sharedassets0.assets"
FNV-1a Hash: 0xA1B2C3D4
CollectionID: "A1B2C3D4"
```

**Special Cases:**
- `BUILTIN-EXTRA`: Unity built-in resources
- `BUILTIN-DEFAULT`: Unity default resources
- `BUILTIN-EDITOR`: Unity editor resources

**Properties:**
- Deterministic (same input → same output)
- Collision-resistant (8-char hex = 4 billion values)
- Sortable (lexicographic order)
- Case-insensitive

### BundlePK Generation

**Algorithm:** FNV-1a hash of lineage path

**Lineage Path Format:** Concatenation of parent bundle names from root

**Example:**
```
Root: "GameBundle"
Child: "level0"
Grandchild: "Resources"

Lineage for "Resources":
  "GameBundle" + "level0" + "Resources"

FNV-1a Hash: 0xE5F6G7H8
BundlePK: "E5F6G7H8"
```

**Special Case:**
- Root GameBundle always has PK = `"00000000"`

### StableKey Format

**Format:** `<collectionId>:<pathId>`

**Examples:**
- `"sharedassets0:100"`
- `"level0:-1"`
- `"BUILTIN-EXTRA:12"`

**Purpose:**
- Lexicographic sorting (dictionary order)
- Consistent ordering across exports
- Human-readable asset reference

### Assembly GUID

**Algorithm:** FNV-1a hash of AssemblyDefinition.FullName

**Format:** 32-character uppercase hex string

**Example:**
```
Input: "Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
FNV-1a Hash: 0x1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D
Assembly GUID: "1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D"
```

### Script GUID

**Source:** MonoScript.m_PropertiesHash or ScriptHashing.CalculateScriptGuid()

**Format:** Unity GUID (32 hex or canonical)

**Purpose:** Links MonoScript assets to decompiled source files

---

## Usage Patterns

### Finding All Assets of a Type

**Using by_class Index:**
```json
// Query by_class.ndjson for classKey=1 (GameObject)
{
  "domain": "by_class",
  "classKey": 1,
  "className": "GameObject",
  "assets": [
    {"collectionId": "sharedassets0", "pathId": 1},
    {"collectionId": "level0", "pathId": 5}
  ],
  "count": 2
}

// Then fetch assets using AssetPK references
```

### Resolving Asset Dependencies

**Step 1: Get asset dependencies**
```json
{
  "domain": "asset_dependencies",
  "from": {"collectionId": "sharedassets0", "pathId": 1},
  "to": {"collectionId": "sharedassets1", "pathId": 100},
  "edge": {
    "kind": "pptr",
    "field": "m_Material",
    "fileId": 1
  },
  "status": "Resolved"
}
```

**Step 2: Resolve target asset**
```json
// Fetch from assets.ndjson
{
  "domain": "assets",
  "pk": {"collectionId": "sharedassets1", "pathId": 100},
  "className": "Material",
  "name": "PlayerMaterial"
}
```

**Step 3: Get type information**
```json
// Fetch from types.ndjson using classKey
{
  "domain": "types",
  "classKey": 21,
  "classId": 21,
  "className": "Material"
}
```

### Traversing Bundle Hierarchy

**Step 1: Start at root**
```json
{
  "domain": "bundles",
  "pk": "00000000",
  "isRoot": true,
  "childBundlePks": ["A1B2C3D4", "E5F6G7H8"]
}
```

**Step 2: Get hierarchy edges**
```json
{
  "domain": "bundle_hierarchy",
  "parentPk": "00000000",
  "childPk": "A1B2C3D4",
  "childIndex": 0,
  "childName": "level0",
  "childDepth": 1
}
```

**Step 3: Fetch child bundles recursively**

### Analyzing Script Types

**Step 1: Find MonoScript assets**
```json
// Query assets.ndjson for classId=115
{
  "domain": "assets",
  "pk": {"collectionId": "sharedassets0", "pathId": 100},
  "classId": 115,
  "className": "MonoScript"
}
```

**Step 2: Get script metadata**
```json
{
  "domain": "script_metadata",
  "pk": "sharedassets0:100",
  "fullName": "Game.Controllers.PlayerController",
  "assemblyName": "Assembly-CSharp",
  "isPresent": true
}
```

**Step 3: Find type definition**
```json
{
  "domain": "type_definitions",
  "pk": "Assembly-CSharp::Game.Controllers::PlayerController",
  "isMonoBehaviour": true,
  "fieldCount": 8,
  "methodCount": 15
}
```

**Step 4: Get type members**
```json
{
  "domain": "type_members",
  "pk": "Assembly-CSharp::Game.Controllers::PlayerController::currentHealth",
  "memberKind": "field",
  "memberType": "System.Int32",
  "serialized": true
}
```

---

## Query Examples

### Example 1: Find All Textures Over 1MB

```
1. Query by_class.ndjson for classId=28 (Texture2D)
2. Get asset list from index
3. For each asset in list:
   - Fetch from assets.ndjson
   - Check unity.byteSize field
   - Filter byteSize > 1048576
```

### Example 2: Find Missing Script References

```
1. Query script_metadata.ndjson
2. Filter where isPresent == false
3. For each missing script:
   - Get fullName and assemblyName
   - Find asset dependencies referencing this script
   - Report usage locations
```

### Example 3: Analyze Scene Complexity

```
1. Query scenes.ndjson for target scene
2. Get sceneGuid
3. Query scene_stats.ndjson with sceneGuid
4. Extract counts object:
   - gameObjectCount (scene complexity)
   - componentCount (component usage)
   - prefabInstanceCount (prefab usage)
   - rootGameObjectCount (hierarchy breadth)
```

### Example 4: Find Circular Dependencies

```
1. Build dependency graph from asset_dependencies.ndjson
2. For each asset:
   - Perform depth-first search
   - Track visited nodes
   - Detect back edges (cycles)
3. Report circular dependency chains
```

### Example 5: Calculate Asset Reuse

```
1. Query dependency_stats.ndjson
2. Get degree.incoming statistics
3. Query by Type section:
   - Find types with high averageInDegree
   - Identify most-shared assets
4. Query asset_dependencies.ndjson:
   - Count incoming edges per asset
   - Rank by reuse frequency
```

---

## Implementation Notes

### File Organization

Recommended directory structure:

```
export/
├── schemas/
│   └── v2/
│       ├── core.schema.json
│       ├── facts/
│       │   ├── assets.schema.json
│       │   ├── bundles.schema.json
│       │   └── ...
│       ├── relations/
│       ├── indexes/
│       └── metrics/
├── data/
│   ├── assets.ndjson
│   ├── bundles.ndjson
│   ├── collections.ndjson
│   └── ...
└── README.md
```

### NDJSON Format

All data is exported as **Newline Delimited JSON (NDJSON)**:

```json
{"domain":"assets","pk":{"collectionId":"sharedassets0","pathId":1},...}
{"domain":"assets","pk":{"collectionId":"sharedassets0","pathId":2},...}
{"domain":"assets","pk":{"collectionId":"sharedassets0","pathId":3},...}
```

**Benefits:**
- Streamable (process line-by-line)
- Grepable (search with text tools)
- Appendable (add records incrementally)
- Splittable (parallel processing)

### Compression

Supported compression codecs (from core.schema.json):
- `none`: Uncompressed NDJSON
- `gzip`: Standard gzip compression
- `zstd`: Zstandard compression (better ratio)
- `zstd-seekable`: Seekable zstd for random access

### Validation

All schemas conform to **JSON Schema Draft 2020-12**.

Validation tools:
- [ajv](https://ajv.js.org/) (JavaScript)
- [jsonschema](https://python-jsonschema.readthedocs.io/) (Python)
- [NJsonSchema](https://github.com/RicoSuter/NJsonSchema) (C#)

### Performance Considerations

**Large Datasets (200K+ assets):**
- Use indexes (by_class, by_collection) for type queries
- Stream NDJSON files line-by-line (don't load entire file)
- Build in-memory lookup tables for frequently accessed data
- Use compressed formats (zstd) for storage

**Graph Queries:**
- Build adjacency lists from asset_dependencies.ndjson
- Use breadth-first search for shortest paths
- Cache dependency subgraphs for repeated queries

---

## Schema Index

### Facts Layer (10 schemas)

| Schema | Domain | Primary Key | Purpose |
|--------|--------|-------------|---------|
| assets.schema.json | assets | {collectionId, pathId} | Individual Unity objects |
| bundles.schema.json | bundles | pk (8-char hex) | Bundle hierarchy nodes |
| collections.schema.json | collections | collectionId | AssetCollection metadata |
| scenes.schema.json | scenes | sceneGuid | Scene aggregations |
| script_metadata.schema.json | script_metadata | pk (StableKey) | MonoScript metadata |
| script_sources.schema.json | script_sources | pk (UnityGuid) | Decompiled source files |
| types.schema.json | types | classKey | Type dictionary |
| type_definitions.schema.json | type_definitions | pk (composite) | .NET type definitions |
| type_members.schema.json | type_members | pk (composite) | Type member details |
| assemblies.schema.json | assemblies | pk (32-char hex) | Assembly metadata |

### Relations Layer (6 schemas)

| Schema | Domain | Purpose |
|--------|--------|---------|
| asset_dependencies.schema.json | asset_dependencies | Asset-to-asset PPtr references |
| collection_dependencies.schema.json | collection_dependencies | Collection-level dependencies |
| bundle_hierarchy.schema.json | bundle_hierarchy | Bundle parent-child edges |
| assembly_dependencies.schema.json | assembly_dependencies | Assembly dependency graph |
| script_type_mapping.schema.json | script_type_mapping | MonoScript to TypeDefinition mapping |
| type_inheritance.schema.json | type_inheritance | Type inheritance relationships |

### Indexes Layer (3 schemas)

| Schema | Domain | Purpose |
|--------|--------|---------|
| by_class.schema.json | by_class | Assets grouped by type |
| by_collection.schema.json | by_collection | Collection summaries |
| by_name.schema.json | by_name | Assets grouped by name |

### Metrics Layer (3 schemas)

| Schema | Domain | Purpose |
|--------|--------|---------|
| scene_stats.schema.json | scene_stats | Scene complexity metrics |
| asset_distribution.schema.json | asset_distribution | Asset type and size distribution |
| dependency_stats.schema.json | dependency_stats | Dependency graph analytics |

---

## Appendix: Full Field Reference

For detailed field-by-field documentation of each schema, please refer to the individual schema reference pages in the `Reference/` directory:

- [core.schema.json Reference](Reference/core.md)
- [assets.schema.json Reference](Reference/assets.md)
- [bundles.schema.json Reference](Reference/bundles.md)
- [collections.schema.json Reference](Reference/collections.md)
- [scenes.schema.json Reference](Reference/scenes.md)
- [script_metadata.schema.json Reference](Reference/script_metadata.md)
- [script_sources.schema.json Reference](Reference/script_sources.md)
- [types.schema.json Reference](Reference/types.md)
- [type_definitions.schema.json Reference](Reference/type_definitions.md)
- [type_members.schema.json Reference](Reference/type_members.md)
- [assemblies.schema.json Reference](Reference/assemblies.md)
- [asset_dependencies.schema.json Reference](Reference/asset_dependencies.md)
- [collection_dependencies.schema.json Reference](Reference/collection_dependencies.md)
- [bundle_hierarchy.schema.json Reference](Reference/bundle_hierarchy.md)
- [assembly_dependencies.schema.json Reference](Reference/assembly_dependencies.md)
- [script_type_mapping.schema.json Reference](Reference/script_type_mapping.md)
- [type_inheritance.schema.json Reference](Reference/type_inheritance.md)
- [by_class.schema.json Reference](Reference/by_class.md)
- [by_collection.schema.json Reference](Reference/by_collection.md)
- [scene_stats.schema.json Reference](Reference/scene_stats.md)
- [asset_distribution.schema.json Reference](Reference/asset_distribution.md)
- [dependency_stats.schema.json Reference](Reference/dependency_stats.md)

---

**End of Complete Schema Reference**
