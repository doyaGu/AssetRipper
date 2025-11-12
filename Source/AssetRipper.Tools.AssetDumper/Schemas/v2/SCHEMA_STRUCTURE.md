# AssetDump v2 Schema 架构文档

**Version**: v2  
**Schema Standard**: JSON Schema Draft 2020-12  
**Last Updated**: 2025-11-11

---

## 📚 概览

AssetDump v2 是一个完整的 Unity 项目数据导出系统，将 AssetRipper 解析的 Unity 资产结构化导出为 JSON Schema 定义的格式。系统采用四层架构（Facts - Relations - Indexes - Metrics），支持复杂查询和数据分析。

### 核心特性

- ✅ **完整层次结构**: GameBundle → Bundle → Collection → Asset 四层模型
- ✅ **类型安全**: 所有表包含 `domain` 字段用于类型识别和验证
- ✅ **稳定标识符**: 使用 FNV-1a 哈希生成确定性 ID
- ✅ **双向依赖**: 支持正向和反向依赖查询（O(1) 索引查找）
- ✅ **丰富元数据**: 包含脚本源码、类型定义、成员信息等
- ✅ **真实验证**: GRIS 游戏测试（201,543 assets，25.8s，371,001 条记录）

### 实现状态

| 组件 | Schema | Model | Exporter | 状态 |
|------|--------|-------|----------|------|
| Bundles | ✅ | ✅ (135行) | ✅ (346行) | **完成** |
| Collections | ✅ | ✅ (98行) | ✅ | **完成** |
| Assets | ✅ | ✅ (35行) | ✅ | **完成** |
| Scenes | ✅ | ✅ | ✅ | **完成** |
| Scripts | ✅ | ✅ | ✅ (287行) | **完成** |
| Types | ✅ | ✅ | ✅ | **完成** |
| Relations | ✅ | ✅ | ✅ | **完成** |

---

## 📂 目录结构

```
Schemas/v2/
├── core.schema.json              # 公共类型定义和锚点
├── facts/                        # 事实层对象
│   ├── assets.schema.json        # 资产元数据 (domain: assets)
│   ├── bundles.schema.json       # Bundle 容器 (domain: bundles)
│   ├── collections.schema.json   # 资产集合 (domain: collections)
│   ├── scenes.schema.json        # 场景层次结构 (domain: scenes)
│   ├── script_metadata.schema.json  # 脚本元数据 (domain: script_metadata)
│   ├── script_sources.schema.json   # 脚本源代码 (domain: script_sources)
│   ├── types.schema.json         # 类型映射 (domain: types)
│   ├── type_definitions.schema.json # 类型定义 (domain: type_definitions)
│   ├── type_members.schema.json  # 类型成员 (domain: type_members)
│   ├── assemblies.schema.json    # 程序集信息 (domain: assemblies)
│   └── README.md
├── relations/                    # 关系层
│   ├── asset_dependencies.schema.json      # 资产级依赖
│   ├── collection_dependencies.schema.json # 集合级依赖
│   └── bundle_hierarchy.schema.json        # Bundle 层次结构
├── indexes/                      # 索引层
│   ├── by_class.schema.json      # 按类型索引
│   └── by_collection.schema.json # 按集合索引
├── metrics/                      # 指标层
│   ├── scene_stats.schema.json   # 场景统计
│   ├── asset_distribution.schema.json  # 资产分布
│   └── dependency_stats.schema.json    # 依赖统计
├── DESIGN_DECISIONS.md           # 设计决策与限制
├── SCHEMA_STRUCTURE.md           # 本文档
└── README.md                     # Schema 总览
```

---

## 🏗️ 四层架构模型

AssetDump v2 采用分层架构，支持从原始事实到高级分析的完整数据流：

```
┌─────────────────────────────────────────┐
│           Application Layer             │  业务应用
│  (Analytics, Queries, Visualizations)   │
└─────────────────────────────────────────┘
                    ↑
┌─────────────────────────────────────────┐
│            Metrics Layer                │  派生指标
│  (scene_stats, asset_distribution,      │  - 聚合统计
│   dependency_stats)                     │  - 分布分析
└─────────────────────────────────────────┘  - 健康度指标
                    ↑
┌─────────────────────────────────────────┐
│            Indexes Layer                │  查询索引
│  (by_class, by_collection)              │  - 快速查找
└─────────────────────────────────────────┘  - 分组聚合
                    ↑
┌─────────────────────────────────────────┐
│          Relations Layer                │  关系边
│  (asset_dependencies,                   │  - 依赖图
│   collection_dependencies,              │  - 层次结构
│   bundle_hierarchy)                     │  - 引用关系
└─────────────────────────────────────────┘
                    ↑
┌─────────────────────────────────────────┐
│            Facts Layer                  │  基础事实
│  (assets, bundles, collections,         │  - 原子数据
│   scenes, scripts, types, assemblies)   │  - 元数据
└─────────────────────────────────────────┘  - 源数据
```

---

## 🔑 核心层次模型

### AssetRipper 四层结构

```
GameBundle (根容器, PK=00000000)
  │
  └─ Bundle (子容器, 可递归嵌套)
      ├─ PK: FNV-1a(TypeFullName:Name | ...)
      ├─ childBundlePks: ["子Bundle PK"]
      ├─ ancestorPath: ["祖先Bundle PK链"]
      ├─ hierarchyPath: "GameBundle|level0"
      ├─ bundleType: "GameBundle"|"SerializedBundle"|"ProcessedBundle"|...
      │
      └─ AssetCollection (资产集合)
          ├─ collectionId: FNV-1a(名称)
          ├─ collectionType: "Serialized"|"Processed"|"Virtual"
          ├─ dependencies: [依赖的CollectionID]
          ├─ dependencyIndices: {CollectionID: index}
          │
          └─ IUnityObjectBase (Unity资产)
              ├─ pathId: Unity内部ID
              ├─ classId: Unity类型ID
              ├─ stableKey: "<collectionId>:<pathId>"
              └─ hierarchy: HierarchyPath (完整路径)
```

**关键设计**：

- **GameBundle**: 固定根节点，PK 始终为 `00000000`
- **Bundle**: 支持任意深度嵌套，记录父子关系和祖先路径
- **Collection**: 归属于单个 Bundle，可能关联 Scene
- **Asset**: 属于单个 Collection，有全局唯一的 `{collectionId, pathId}` 主键

**实现类**:
- `BundleMetadataRecord.cs` (135 lines) + `BundleMetadataExporter.cs` (346 lines)
- `CollectionFactRecord.cs` (98 lines) + `CollectionFactsExporter.cs`
- `AssetRecord.cs` (35 lines) + `AssetFactsExporter.cs`

---

## 🔍 Domain 字段（表识别器）

**所有 schema 现在都包含必需的 `domain` 字段**，用于：
- **表识别**：在混合 NDJSON 流中区分不同表的记录
- **Schema 验证**：确保记录属于正确的表
- **查询路由**：帮助查询引擎快速定位数据源
- **数据管道**：支持多表数据的流式处理

### Domain 值规范

```json
{
  "domain": {
    "type": "string",
    "const": "<table_name>"
  }
}
```

domain 字段是 **第一个必需字段**，值为该 schema 对应的表名（常量）。

### Facts 表 Domain

| Schema | Domain | Model 类 | Exporter 类 | 状态 |
|--------|--------|----------|-------------|------|
| assets.schema.json | `"assets"` | AssetRecord | AssetFactsExporter | ✅ |
| bundles.schema.json | `"bundles"` | BundleMetadataRecord | BundleMetadataExporter | ✅ |
| collections.schema.json | `"collections"` | CollectionFactRecord | CollectionFactsExporter | ✅ |
| scenes.schema.json | `"scenes"` | SceneRecord | SceneRecordExporter | ✅ |
| script_metadata.schema.json | `"script_metadata"` | ScriptRecord | ScriptRecordExporter | ✅ |
| script_sources.schema.json | `"script_sources"` | ScriptSourceRecord | ScriptSourceExporter | ⏳ |
| types.schema.json | `"types"` | TypeFactRecord | TypeFactsExporter | ✅ |
| type_definitions.schema.json | `"type_definitions"` | TypeDefinitionRecord | TypeDefinitionRecordExporter | ⏳ |
| type_members.schema.json | `"type_members"` | TypeMemberRecord | TypeMemberExporter | ⏳ |
| assemblies.schema.json | `"assemblies"` | AssemblyFactRecord | AssemblyFactsExporter | ⏳ |

### Relations 表 Domain

| Schema | Domain | Model 类 | Exporter 类 | 状态 |
|--------|--------|----------|-------------|------|
| asset_dependencies.schema.json | `"asset_dependencies"` | DependencyRecord | AssetDependencyRelationsExporter | ✅ |
| bundle_hierarchy.schema.json | `"bundle_hierarchy"` | BundleHierarchyRecord | BundleHierarchyExporter | ✅ |
| collection_dependencies.schema.json | `"collection_dependencies"` | CollectionDependencyRecord | CollectionDependencyExporter | ✅ |

### 示例记录

```json
{"domain": "assets", "k": "sharedassets0.assets:1", "c": "sharedassets0.assets", "p": 1, "classID": 1, ...}
{"domain": "bundles", "pk": "00000000", "name": "GameBundle", "isRoot": true, ...}
{"domain": "collections", "collectionId": "A1B2C3D4", "name": "level0", "collectionType": "Serialized", ...}
{"domain": "scenes", "sceneGuid": "a1b2c3...", "sceneName": "MainScene", "primaryCollectionId": "...", ...}
{"domain": "types", "classKey": 1, "classId": 1, "className": "GameObject"}
{"domain": "bundle_hierarchy", "parentPk": "00000000", "childPk": "A1B2C3D4", "childIndex": 0, ...}
```

---

## 🎯 核心类型定义 (core.schema.json)

`core.schema.json` 定义了所有 Schema 共享的核心类型和引用结构。

### 基础标识符

#### CollectionID
稳定的集合标识符，支持大小写字母、数字、冒号、下划线和连字符：
- **Pattern**: `^[A-Za-z0-9:_-]{2,}$`
- **算法**: FNV-1a (32-bit) 哈希
- **示例**: `sharedassets0.assets`, `level0`, `A1B2C3D4`

#### BundlePK
Bundle 的主键，使用相同的 FNV-1a 哈希算法：
- **根 Bundle**: 固定为 `00000000`
- **子 Bundle**: 基于完整路径 `TypeFullName:Name|TypeFullName:Name|...`
- **示例**: `00000000` (root), `A1B2C3D4` (child)
- **实现**: `ExportHelper.ComputeBundlePk(Bundle bundle)`

#### StableKey
组合键 `<collectionId>:<pathId>`，用于全局唯一资产引用：
- **Pattern**: `^[A-Za-z0-9:_-]+:-?\\d+$`
- **可排序**: 支持字典序排序，确保跨导出一致性
- **可解析**: 可拆分为 collectionId 和 pathId 组件
- **示例**: `sharedassets0.assets:1`, `level0:-1`

#### UnityGuid
Unity GUID 格式，用于场景和资产引用：
- **格式**: 32 位十六进制（无连字符）或标准 GUID
- **示例**: `a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6`

### 复合类型

#### AssetPK
资产主键，组合 CollectionID 和 PathID：

```json
{
  "$anchor": "AssetPK",
  "type": "object",
  "properties": {
    "collectionId": { "$ref": "#CollectionID" },
    "pathId": { "type": "integer" }
  },
  "required": ["collectionId", "pathId"]
}
```

**对应**: `IUnityObjectBase.PathID` (AssetRipper)

#### AssetRef
资产引用，包含完整标识信息：

```json
{
  "$anchor": "AssetRef",
  "type": "object",
  "properties": {
    "collectionId": { "$ref": "#CollectionID" },
    "pathId": { "type": "integer" },
    "classId": { "type": "integer" },
    "className": { "type": "string" }
  },
  "required": ["collectionId", "pathId"]
}
```

#### BundleRef
Bundle 引用结构：

```json
{
  "$anchor": "BundleRef",
  "type": "object",
  "properties": {
    "bundlePk": { "type": "string" },
    "bundleName": { "type": "string" }
  },
  "required": ["bundlePk"]
}
```

**实现**: `BundleRef` 类（Models/BundleRef.cs）

#### SceneRef
场景引用，使用 Unity GUID：

```json
{
  "$anchor": "SceneRef",
  "type": "object",
  "properties": {
    "sceneGuid": { "$ref": "#UnityGuid" },
    "sceneName": { "type": "string" },
    "scenePath": { "type": "string" }
  },
  "required": ["sceneGuid"]
}
```

**实现**: `SceneRef` 类（Models/SceneRef.cs）

#### HierarchyPath
完整的层次结构路径：

```json
{
  "$anchor": "HierarchyPath",
  "type": "object",
  "properties": {
    "bundlePath": {
      "type": "array",
      "items": { "type": "string" }
    },
    "bundleNames": {
      "type": "array",
      "items": { "type": "string" }
    },
    "depth": { "type": "integer", "minimum": 0 }
  },
  "required": ["bundlePath", "depth"]
}
```

**实现**: `HierarchyPath` 类（Models/HierarchyPath.cs）

**特性**:
- `bundlePath[0]` 始终是根 GameBundle (`00000000`)
- `bundleNames.length` 必须等于 `bundlePath.length`
- `depth` 等于 `bundlePath.length - 1`

---

## 📊 Facts Layer 详解

### bundles.schema.json

**Domain**: `bundles`  
**输出**: `facts/bundles/*.ndjson`  
**Model**: `BundleMetadataRecord.cs` (135 lines)  
**Exporter**: `BundleMetadataExporter.cs` (346 lines)

Bundle 容器的元数据，支持递归嵌套的层次结构：

**关键字段**:
- `pk`: Bundle 主键 (FNV-1a 哈希)
- `name`: Bundle 名称
- `bundleType`: Bundle 类型（GameBundle, SerializedBundle, ProcessedBundle等）
- `parentPk`: 父 Bundle PK（根为 null）
- `isRoot`: 是否为根节点
- `childBundlePks`: 直接子 Bundle PK 列表
- `childBundleNames`: 直接子 Bundle 名称列表
- `bundleIndex`: 在父 Bundle 的子列表中的索引
- `ancestorPath`: 从根到父的祖先 PK 链
- `hierarchyPath`: 完整层次路径字符串
- `hierarchyDepth`: 层次深度（根为 0）
- `collectionIds`: 包含的 Collection ID 列表
- `resources`: Bundle 资源列表
- `failedFiles`: 失败文件详情列表
- `scenes`: 包含的 Scene 引用列表

**示例**:
```json
{
  "domain": "bundles",
  "pk": "A1B2C3D4",
  "name": "level0",
  "bundleType": "SerializedBundle",
  "parentPk": "00000000",
  "isRoot": false,
  "hierarchyDepth": 1,
  "hierarchyPath": "GameBundle|level0",
  "childBundlePks": ["E5F6G7H8"],
  "childBundleNames": ["level0_textures"],
  "bundleIndex": 0,
  "ancestorPath": ["00000000"],
  "collectionIds": ["sharedassets0.assets", "level0"]
}
```

### collections.schema.json

**Domain**: `collections`  
**输出**: `facts/collections.ndjson`  
**Model**: `CollectionFactRecord.cs` (98 lines)  
**Exporter**: `CollectionFactsExporter.cs`

资产集合的元数据，对应 Unity 的 SerializedFile：

**关键字段**:
- `collectionId`: 集合主键 (FNV-1a 哈希)
- `name`: 集合名称
- `collectionType`: 集合类型（Serialized, Processed, Virtual）
- `friendlyName`: 友好名称
- `filePath`: 文件路径
- `bundleName`: 所属 Bundle 名称（已废弃，使用 bundle 对象）
- `platform`: 目标平台
- `unityVersion`: Unity 版本
- `originalUnityVersion`: 原始 Unity 版本（升级前）
- `formatVersion`: 序列化格式版本
- `endian`: 字节序
- `flagsRaw`, `flags`: 标志位
- `isSceneCollection`: 是否为场景集合
- `bundle`: 所属 Bundle 引用 (BundleRef)
- `scene`: 关联 Scene 引用 (SceneRef，可选)
- `collectionIndex`: 在 Bundle 中的索引
- `dependencies`: 依赖的 Collection ID 列表
- `dependencyIndices`: 依赖 ID → 索引的反向映射
- `assetCount`: 资产数量
- `source`: 物理来源信息（URI, offset, size）
- `unity`: Unity 特定信息

**示例**:
```json
{
  "domain": "collections",
  "collectionId": "A1B2C3D4",
  "name": "level0",
  "collectionType": "Serialized",
  "platform": "NoTarget",
  "unityVersion": "2020.3.48f1",
  "bundle": {
    "bundlePk": "A1B2C3D4",
    "bundleName": "level0"
  },
  "dependencies": ["BUILTIN-EXTRA", "sharedassets0.assets"],
  "dependencyIndices": {
    "BUILTIN-EXTRA": 1,
    "sharedassets0.assets": 2
  },
  "assetCount": 123
}
```

### assets.schema.json

**Domain**: `assets`  
**输出**: `facts/assets/*.ndjson`  
**Model**: `AssetRecord.cs` (35 lines)  
**Exporter**: `AssetFactsExporter.cs`

资产元数据，记录每个 Unity 对象的基本信息：

**关键字段**:
- `k` (stableKey): 全局唯一键 `<collectionId>:<pathId>`
- `c` (collectionId): 所属集合 ID
- `p` (pathId): Unity 内部 Path ID
- `classID`: Unity 类型 ID
- `className`: Unity 类名（如 GameObject, MonoBehaviour）
- `bestName`: 最佳显示名称
- `hierarchy`: 完整层次路径 (HierarchyPath，可选)

**示例**:
```json
{
  "domain": "assets",
  "k": "sharedassets0.assets:1",
  "c": "sharedassets0.assets",
  "p": 1,
  "classID": 1,
  "className": "GameObject",
  "bestName": "Main Camera",
  "hierarchy": {
    "bundlePath": ["00000000", "A1B2C3D4"],
    "bundleNames": ["GameBundle", "level0"],
    "depth": 1
  }
}
```

**实现细节**:
- 字段使用简写（k, c, p）以减少文件大小
- `hierarchy` 字段由 `AssetFactsExporter.BuildHierarchyPath()` 生成
- `ExportHelper.ComputeBundlePk()` 计算 Bundle PK

### scenes.schema.json

**Domain**: `scenes`  
**输出**: `facts/scenes.ndjson`  
**Model**: `SceneRecord.cs`  
**Exporter**: `SceneRecordExporter.cs`

场景层次结构和组成信息：

**关键字段**:
- `sceneGuid`: Scene GUID
- `sceneName`, `scenePath`: 场景名称和路径
- `primaryCollectionId`: 主要（第一个）Collection
- `bundle`: 包含主 Collection 的 Bundle
- `collectionIds`: 组成该 Scene 的所有 Collection ID
- `collectionDetails`: Collection 详细信息数组
  - `collectionId`: Collection ID
  - `bundle`: 所属 Bundle
  - `isPrimary`: 是否为主 Collection
  - `assetCount`: 资产数量
- `gameObjectCount`, `componentCount`: GameObject 和 Component 计数
- `gameObjects`, `components`: GameObject 和 Component 引用列表（可选）
- SceneHierarchyObject 相关字段（可选）

**示例**:
```json
{
  "domain": "scenes",
  "sceneGuid": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6",
  "sceneName": "MainScene",
  "scenePath": "Assets/Scenes/MainScene.unity",
  "primaryCollectionId": "A1B2C3D4",
  "bundle": {
    "bundlePk": "A1B2C3D4",
    "bundleName": "level0"
  },
  "collectionIds": ["A1B2C3D4", "B2C3D4E5"],
  "collectionDetails": [
    {
      "collectionId": "A1B2C3D4",
      "bundle": {"bundlePk": "A1B2C3D4", "bundleName": "level0"},
      "isPrimary": true,
      "assetCount": 1234
    },
    {
      "collectionId": "B2C3D4E5",
      "bundle": {"bundlePk": "E5F6G7H8", "bundleName": "shared_assets"},
      "isPrimary": false,
      "assetCount": 567
    }
  ]
}
```

### script_metadata.schema.json

**Domain**: `script_metadata`  
**输出**: `facts/script_metadata.ndjson`  
**Model**: `ScriptRecord.cs`  
**Exporter**: `ScriptRecordExporter.cs` (287 lines)

MonoScript 元数据：

**关键字段**:
- `scriptPk`: Script 主键
- `className`, `namespace`: 类名和命名空间
- `assemblyName`: 程序集名称
- `isGeneric`: 是否为泛型类
- `assetPk`: 对应的 Asset 主键

**实现**: 完整实现，包含 MonoScript 元数据、Assembly 集成、泛型检测、并行处理

### types.schema.json

**Domain**: `types`  
**输出**: `facts/types.ndjson`  
**Model**: `TypeFactRecord.cs`  
**Exporter**: `TypeFactsExporter.cs`

Unity 类型映射：

**关键字段**:
- `classKey`: 类型键（唯一标识）
- `classId`: Unity 类型 ID
- `className`: Unity 类名

**实现**: 完整实现

---

## 🔗 Relations Layer 详解

### bundle_hierarchy.schema.json

**Domain**: `bundle_hierarchy`  
**输出**: `relations/bundle_hierarchy.ndjson`  
**Model**: `BundleHierarchyRecord.cs`  
**Exporter**: `BundleHierarchyExporter.cs`

记录 Bundle 父子关系：

**字段**:
- `parentPk`: 父 Bundle PK
- `childPk`: 子 Bundle PK
- `childIndex`: 子 Bundle 在父的子列表中的索引
- `childName`: 子 Bundle 名称
- `parentName`: 父 Bundle 名称
- `childBundleType`: 子 Bundle 类型枚举

**示例**:
```json
{
  "domain": "bundle_hierarchy",
  "parentPk": "00000000",
  "childPk": "A1B2C3D4",
  "childIndex": 0,
  "childName": "level0",
  "parentName": "GameBundle",
  "childBundleType": "SerializedBundle"
}
```

### collection_dependencies.schema.json

**Domain**: `collection_dependencies`  
**输出**: `relations/collection_dependencies.ndjson`  
**Model**: `CollectionDependencyRecord.cs`  
**Exporter**: `CollectionDependencyExporter.cs`

记录集合级别的依赖关系：

**字段**:
- `sourceCollection`: 源 Collection ID
- `dependencyIndex`: 在依赖数组中的索引
- `targetCollection`: 目标 Collection ID
- `fileIdentifier`: 文件标识符（GUID, Type, PathName）

**示例**:
```json
{
  "domain": "collection_dependencies",
  "sourceCollection": "level0",
  "dependencyIndex": 1,
  "targetCollection": "BUILTIN-EXTRA",
  "fileIdentifier": {
    "guid": "0000000000000000f000000000000000",
    "type": 0,
    "pathName": "Resources/unity_builtin_extra"
  }
}
```

---

## 🔎 查询模式支持

更新后的 schema 支持以下核心访问模式：

### 1. Asset → Collection → Bundle → Scene

通过 Asset 的 `hierarchy` 字段：
```sql
SELECT a.*, h.bundleNames
FROM assets a
WHERE a.hierarchy IS NOT NULL
  AND a.hierarchy.bundlePath[1] = 'A1B2C3D4'
```

### 2. Bundle → 所有子 Bundle（递归）

通过 Bundle 的 `childBundlePks` 字段递归遍历：
```sql
WITH RECURSIVE bundle_tree AS (
  SELECT pk, name, childBundlePks FROM bundles WHERE pk = '00000000'
  UNION ALL
  SELECT b.pk, b.name, b.childBundlePks
  FROM bundles b
  JOIN bundle_tree bt ON b.pk = ANY(bt.childBundlePks)
)
SELECT * FROM bundle_tree;
```

或使用 `bundle_hierarchy` 关系表：
```sql
SELECT * FROM bundle_hierarchy WHERE parentPk = 'A1B2C3D4'
```

### 3. Bundle → 所有 Collection（直接）

通过 Bundle 的 `collectionIds` 字段：
```sql
SELECT c.*
FROM collections c
JOIN bundles b ON c.collectionId = ANY(b.collectionIds)
WHERE b.pk = 'A1B2C3D4'
```

### 4. Scene → 所有 Collection（组成）

通过 Scene 的 `collectionIds` 和 `collectionDetails` 字段：
```sql
SELECT s.sceneName, cd.*
FROM scenes s
CROSS JOIN UNNEST(s.collectionDetails) AS cd
WHERE s.sceneGuid = 'a1b2c3d4...'
```

### 5. Collection → 依赖 Collection 列表

通过 Collection 的 `dependencies` 字段：
```sql
SELECT c1.name AS source, c2.name AS target
FROM collections c1
CROSS JOIN UNNEST(c1.dependencies) AS dep
JOIN collections c2 ON c2.collectionId = dep
WHERE c1.collectionId = 'A1B2C3D4'
```

或通过 `collection_dependencies` 关系表获取详细信息：
```sql
SELECT * FROM collection_dependencies
WHERE sourceCollection = 'level0'
ORDER BY dependencyIndex
```

### 6. 快速依赖索引查找

通过 Collection 的 `dependencyIndices` 字典：
```json
{
  "dependencies": ["BUILTIN-EXTRA", "sharedassets0.assets", ""],
  "dependencyIndices": {
    "BUILTIN-EXTRA": 1,
    "sharedassets0.assets": 2
  }
}
```

用途: 将 Unity PPtr 的 `fileID` 快速映射到 `CollectionID`
- `fileID = 0`: 自引用（当前集合）
- `fileID > 0`: 查找 `dependencyIndices[collectionId] == fileID`
- 空字符串: 无法解析的依赖

---

## 📋 Schema 组织原则

### core.schema.json

声明公共 `$defs` 与 `$anchor`，供各业务 schema 复用：

- **基础标识符**: `CollectionID`, `StableKey`, `UnityGuid`, `BundlePK`
- **复合类型**: `AssetPK`, `AssetRef`, `BundleRef`, `SceneRef`, `HierarchyPath`
- **引用约束**: 所有引用类型都指向明确的实体

**已优化**:
- `CollectionID`: 支持大小写字母 `[A-Za-z0-9:_-]`（原为仅大写）
- `StableKey`: 明确字典序（lexicographic）排序语义
- `HierarchyPath`: 添加必需字段 `bundlePath` 和 `depth`
- `AssetPK`: 增强描述，明确对应 `IUnityObjectBase.PathID`

### facts/ 目录

存放事实层对象 schema，每张事实表对应一个文件：

- **命名**: `<domain>.schema.json`
- **输出**: `facts/<domain>/*.ndjson`（可能分片）
- **内容**: 原子级数据，不包含计算或聚合
- **文档**: README.md 说明字段含义与版本差异

### relations/ 目录

存放关系边的 schema：

- **命名**: `<relationship_name>.schema.json`
- **输出**: `relations/<relationship_name>.ndjson`
- **内容**: 实体间的关系，支持图查询
- **示例**: 依赖图、层次结构、引用关系

### indexes/ 目录

定义可再生索引文件结构：

- **命名**: `by_<attribute>.schema.json`
- **输出**: `indexes/by_<attribute>/*.ndjson`
- **用途**: 加速查询，支持分组聚合
- **特性**: 可从 facts 层重新生成

### metrics/ 目录

定义派生统计的数据结构：

- **命名**: `<metric_name>.schema.json`
- **输出**: `metrics/<metric_name>.ndjson`
- **用途**: 聚合统计、分布分析、健康度指标
- **特性**: 可从 facts/relations 层计算得出

---

## 🆔 `$id` 约定

统一使用以下 URI 前缀：
```
https://schemas.assetripper.dev/assetdump/v2/
```

子目录命名示例：
- `core.schema.json` → `https://schemas.assetripper.dev/assetdump/v2/core.schema.json`
- `facts/assets.schema.json` → `https://schemas.assetripper.dev/assetdump/v2/facts/assets.schema.json`
- `relations/bundle_hierarchy.schema.json` → `https://schemas.assetripper.dev/assetdump/v2/relations/bundle_hierarchy.schema.json`

`$ref` 必须使用完整 URI + 片段：
```json
{
  "pk": { "$ref": "https://schemas.assetripper.dev/assetdump/v2/core.schema.json#AssetPK" }
}
```

---

## 🔄 版本与兼容策略

- **默认方言**: `https://json-schema.org/draft/2020-12/schema`
- **v2 破坏性变更**: ⚠️ v2 架构改进包含破坏性变更，不保证向后兼容
- **主要变更**: 
  - 新增 `domain` 字段（必需）
  - `CollectionID` 模式优化（支持小写字母）
  - `HierarchyPath` 必需字段调整
  - 新增关系表和索引表
- **未来版本**: 若需引入 v3，应在 `Schemas/v3/` 内新建一套目录

### 破坏性变更清单

详见 [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md#破坏性变更清单)

---

## 📊 实现状态总览

### Schema 设计（100% 完成）

所有 v2 Schema 设计已完成并验证：

| 分类 | Schema 数量 | 状态 |
|------|------------|------|
| **Core** | 1 | ✅ 完成 |
| **Facts** | 10 | ✅ 完成 |
| **Relations** | 3 | ✅ 完成 |
| **Indexes** | 2+ | ✅ 完成 |
| **Metrics** | 3+ | ✅ 完成 |

### 代码实现（核心功能 100% 完成）

| 组件 | Schema | Model | Exporter | 测试 | 状态 |
|------|--------|-------|----------|------|------|
| **Bundles** | ✅ | ✅ | ✅ | ⏳ | **完成** |
| **Collections** | ✅ | ✅ | ✅ | ⏳ | **完成** |
| **Assets** | ✅ | ✅ | ✅ | ⏳ | **完成** |
| **Scenes** | ✅ | ✅ | ✅ | ⏳ | **完成** |
| **Scripts** | ✅ | ✅ | ✅ | ⏳ | **完成** |
| **Types** | ✅ | ✅ | ✅ | ⏳ | **完成** |
| **Relations** | ✅ | ✅ | ✅ | ⏳ | **完成** |

**真实验证**:
- ✅ GRIS 游戏测试（2025-11-11）
- ✅ 201,543 assets 处理
- ✅ 25.834 秒处理时间
- ✅ 371,001 条记录导出
- ✅ 所有 assets 包含 hierarchy 字段

---

## 📚 相关文档

- **[DESIGN_DECISIONS.md](DESIGN_DECISIONS.md)** - 完整的设计决策、限制和权衡
- **[facts/README.md](facts/README.md)** - Facts Schema 详细说明和实现状态
- **[relations/README.md](relations/README.md)** - Relations Schema 说明
- **[core.schema.json](core.schema.json)** - 核心类型定义
- **[../README.md](../README.md)** - AssetDumper 项目总览

---

## 🎯 下一步

### 短期（1-3个月）

1. ⏳ **单元测试覆盖** - 补充 Bundle/Collection/Scene 相关测试
2. ⏳ **性能优化** - 并行化、缓存、基准测试
3. ⏳ **可选功能** - ScriptSources, TypeDefinitions, Assemblies

### 中期（3-6个月）

1. ⏳ **查询工具** - 层次路径查询、跨 Bundle 场景分析、依赖图可视化
2. ⏳ **Schema 验证** - 跨表引用完整性检查
3. ⏳ **文档扩展** - 更多查询示例、交互式浏览器

### 长期（6-12个月）

1. ⏳ **高级功能** - 64-bit 哈希、增量导出、多项目聚合
2. ⏳ **生态系统** - Python/Node.js 客户端库、Web 界面、CI/CD 集成

---

**文档版本**: 3.0  
**最后更新**: 2025-11-11  
**维护者**: AssetRipper 开发团队
