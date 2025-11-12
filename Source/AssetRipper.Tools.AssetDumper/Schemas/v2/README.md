# AssetDump v2 Schemas# AssetDump v2 Schemas

**Version**: v2 **Version**: v2

**Schema Standard**: JSON Schema Draft 2020-12 **Schema Standard**: JSON Schema Draft 2020-12

**Last Updated**: 2025-11-11**Last Updated**: 2025-11-11

---

## 📚 Quick Overview## 📚 Overview

AssetDump v2 将 AssetRipper 解析的 Unity 资产导出为结构化 JSON Schema 格式，采用四层架构（Facts - Relations - Indexes - Metrics）。AssetDump v2 是一个完整的 Unity 项目数据导出系统，将 AssetRipper 解析的 Unity 资产结构化导出为 JSON Schema 定义的格式。系统采用四层架构（Facts - Relations - Indexes - Metrics），支持增量导出、复杂查询和数据分析。

### 核心特性### 核心特性

- ✅ 四层层次结构 (GameBundle → Bundle → Collection → Asset)- ✅ **完整层次结构**: GameBundle → Bundle → Collection → Asset 四层模型

- ✅ 类型安全 (所有表包含 `domain` 字段)- ✅ **类型安全**: 所有表包含 `domain` 字段用于类型识别和验证

- ✅ 稳定标识符 (FNV-1a 哈希)- ✅ **稳定标识符**: 使用 FNV-1a 哈希生成确定性 ID

- ✅ 双向依赖查询- ✅ **双向依赖**: 支持正向和反向依赖查询（O(1) 索引查找）

- ✅ 丰富元数据- ✅ **丰富元数据**: 包含脚本源码、类型定义、成员信息等

---

## 📂 Directory Structure## 📂 Directory Structure

````

v2/Schemas/v2/

├── core.schema.json              # 共享类型定义├── core.schema.json              # 公共类型定义和锚点

├── facts/                        # 事实层 (10 schemas)├── facts/                        # 事实层对象

│   └── README.md│   ├── assets.schema.json        # 资产元数据

├── relations/                    # 关系层 (6 schemas)│   ├── bundles.schema.json       # Bundle 容器

│   └── README.md│   ├── collections.schema.json   # 资产集合

├── indexes/                      # 索引层 (2 schemas)│   ├── scenes.schema.json        # 场景层次结构

│   └── README.md│   ├── script_metadata.schema.json  # 脚本元数据

└── metrics/                      # 指标层 (3 schemas)│   ├── script_sources.schema.json   # 脚本源代码

    └── README.md│   ├── types.schema.json         # 类型映射

```│   ├── type_definitions.schema.json # 类型定义

│   ├── type_members.schema.json  # 类型成员

### Schema Layers│   └── assemblies.schema.json    # 程序集信息

├── relations/                    # 关系层

| Layer | Schemas | Purpose | Details |│   ├── asset_dependencies.schema.json      # 资产级依赖

|-------|---------|---------|---------|│   ├── collection_dependencies.schema.json # 集合级依赖

| **[Facts](facts/README.md)** | 10 | 基础事实数据 | assets, bundles, collections, scenes, scripts, types, assemblies |│   └── bundle_hierarchy.schema.json        # Bundle 层次结构

| **[Relations](relations/README.md)** | 6 | 实体间关系 | dependencies, hierarchy, type mapping |├── indexes/                      # 索引层

| **[Indexes](indexes/README.md)** | 2 | 查询加速 | by_class, by_collection |│   ├── by_class.schema.json      # 按类型索引

| **[Metrics](metrics/README.md)** | 3 | 派生统计 | scene_stats, asset_distribution, dependency_stats |│   └── by_collection.schema.json # 按集合索引

├── metrics/                      # 指标层

---│   ├── scene_stats.schema.json   # 场景统计

│   ├── asset_distribution.schema.json  # 资产分布

## 🔑 Core Concepts│   └── dependency_stats.schema.json    # 依赖统计

└── README.md                     # 本文档

### Domain Field```

所有 schema 包含必需的 `domain` 字段用于表识别：

```json---

{"domain": "assets", "pk": {...}, ...}

```## 🏗️ Architecture



### Stable Identifiers### Four-Layer Model

- **CollectionID**: FNV-1a 哈希 (8字符十六进制)

- **BundlePK**: Bundle主键 (根节点=`00000000`)AssetDump v2 采用分层架构，支持从原始事实到高级分析的完整数据流：

- **StableKey**: `<collectionId>:<pathId>` (全局唯一)

```

### Four-Layer Architecture┌─────────────────────────────────────────┐

│           Application Layer             │  业务应用

```│  (Analytics, Queries, Visualizations)   │

Metrics Layer (派生指标)└─────────────────────────────────────────┘

     ↓                    ↑

Indexes Layer (查询索引)┌─────────────────────────────────────────┐

     ↓│            Metrics Layer                │  派生指标

Relations Layer (关系边)│  (scene_stats, asset_distribution,      │  - 聚合统计

     ↓│   dependency_stats)                     │  - 分布分析

Facts Layer (基础事实)└─────────────────────────────────────────┘  - 健康度指标

```                    ↑

┌─────────────────────────────────────────┐

---│            Indexes Layer                │  查询索引

│  (by_class, by_collection)              │  - 快速查找

## 📖 Documentation└─────────────────────────────────────────┘  - 分组聚合

                    ↑

### Quick Start┌─────────────────────────────────────────┐

- **[Facts Layer](facts/README.md)** - 10 个基础 schemas 详解│          Relations Layer                │  关系边

- **[Relations Layer](relations/README.md)** - 6 个关系 schemas 详解│  (asset_dependencies,                   │  - 依赖图

- **[Indexes Layer](indexes/README.md)** - 2 个索引 schemas 详解│   collection_dependencies,              │  - 层次结构

- **[Metrics Layer](metrics/README.md)** - 3 个指标 schemas 详解│   bundle_hierarchy)                     │  - 引用关系

└─────────────────────────────────────────┘

### Reference Docs                    ↑

- **[core.schema.json](core.schema.json)** - 共享类型定义┌─────────────────────────────────────────┐

- **[ARCHITECTURE_DETAILED.md](ARCHITECTURE_DETAILED.md)** - 完整架构文档│            Facts Layer                  │  基础事实

- **[DESIGN_DECISIONS.md](DESIGN_DECISIONS.md)** - 设计决策与权衡│  (assets, bundles, collections,         │  - 原子数据

- **[VALIDATION_NOTES.md](VALIDATION_NOTES.md)** - Schema验证规则│   scenes, scripts, types, assemblies)   │  - 元数据

└─────────────────────────────────────────┘  - 源数据

### Additional```

- **[CONSOLIDATION_PLAN_V2.md](CONSOLIDATION_PLAN_V2.md)** - 文档整合计划

- **[INTEGRATION_RECOMMENDATION.md](INTEGRATION_RECOMMENDATION.md)** - 整合建议### Hierarchy Model



---AssetRipper 的四层层次结构：



## 🚀 Quick Examples```

GameBundle (根容器, PK=00000000)

### Query All GameObject Assets  └─ Bundle (子容器, 可递归嵌套)

```sql      ├─ childBundlePks: ["子Bundle PK"]

SELECT * FROM by_class WHERE classId = 1;      ├─ ancestorPath: ["祖先Bundle PK链"]

```      └─ AssetCollection (资产集合)

          ├─ collectionId: FNV-1a 哈希

### Find Asset Dependencies          ├─ dependencies: [依赖的CollectionID]

```sql          └─ IUnityObjectBase (Unity资产)

SELECT * FROM asset_dependencies WHERE from.collectionId = 'CAB-1234';              ├─ pathId: Unity内部ID

```              ├─ classId: Unity类型ID

              └─ hierarchy: 完整层次路径

### Scene Statistics```

```sql

SELECT * FROM scene_stats;**关键设计**：

```- **GameBundle**: 固定根节点，PK 始终为 `00000000`

- **Bundle**: 支持任意深度嵌套，记录父子关系和祖先路径

---- **Collection**: 归属于单个 Bundle，可能关联 Scene

- **Asset**: 属于单个 Collection，有全局唯一的 `{collectionId, pathId}` 主键

## 🔧 Usage

---

### Export Pipeline

```csharp## 🔑 Core Concepts

var exporter = new AssetDumperPipeline();

exporter.Options.OutputDirectory = "output/";### Domain Field

await exporter.ExportAsync(gameData);

```所有 Schema 包含必需的 `domain` 字段作为表识别器：



### Output Structure```json

```{

output/  "domain": {

├── facts/    "type": "string",

│   ├── assets.ndjson    "const": "<table_name>"

│   ├── bundles.ndjson  }

│   └── collections.ndjson}

├── relations/```

│   └── asset_dependencies.ndjson

├── indexes/**用途**：

│   └── by_class.ndjson- **混合流支持**: 在单个 NDJSON 流中区分不同表的记录

└── metrics/- **Schema 验证**: 确保记录属于正确的表

    └── scene_stats.json- **查询路由**: 帮助查询引擎快速定位数据源

```- **数据管道**: 支持多表数据的流式处理



---**示例**：

```json

## 📄 Schema Summary{"domain": "assets", "pk": {"collectionId": "...", "pathId": 1}, ...}

{"domain": "types", "classKey": 1, "classId": 1, "className": "GameObject"}

| Schema | Domain | Output | Purpose |{"domain": "asset_dependencies", "from": {...}, "to": {...}, ...}

|--------|--------|--------|---------|```

| **Facts Layer** |

| assets | `assets` | assets.ndjson | 资产元数据 |### Stable Identifiers

| bundles | `bundles` | bundles.ndjson | Bundle容器 |

| collections | `collections` | collections.ndjson | 资产集合 |#### CollectionID

| scenes | `scenes` | scenes.ndjson | 场景层次结构 |使用 FNV-1a (32-bit) 哈希生成 8 字符十六进制 ID：

| script_metadata | `script_metadata` | script_metadata.ndjson | 脚本元数据 |- **Pattern**: `^[A-Za-z0-9:_-]{2,}$`

| script_sources | `script_sources` | script_sources.ndjson | 脚本源码 |- **稳定性**: 基于集合名称和路径，跨导出保持一致

| types | `types` | types.ndjson | 类型映射 |- **紧凑性**: 8 字符，平均节省 ~40 字节

| type_definitions | `type_definitions` | type_definitions.ndjson | 类型定义 |- **示例**: `sharedassets0.assets`, `level0`, `A1B2C3D4`

| type_members | `type_members` | type_members.ndjson | 类型成员 |

| assemblies | `assemblies` | assemblies.ndjson | 程序集信息 |#### BundlePK

| **Relations Layer** |Bundle 的主键，使用相同的 FNV-1a 哈希算法：

| asset_dependencies | `asset_dependencies` | asset_dependencies.ndjson | 资产级依赖 |- **根Bundle**: 固定为 `00000000`

| collection_dependencies | `collection_dependencies` | collection_dependencies.ndjson | 集合级依赖 |- **子Bundle**: 基于 Bundle 名称计算

| bundle_hierarchy | `bundle_hierarchy` | bundle_hierarchy.ndjson | Bundle层次 |- **示例**: `00000000` (root), `A1B2C3D4` (child)

| assembly_dependencies | `assembly_dependencies` | assembly_dependencies.ndjson | 程序集依赖 |

| script_type_mapping | `script_type_mapping` | script_type_mapping.ndjson | 脚本类型映射 |#### StableKey

| type_inheritance | `type_inheritance` | type_inheritance.ndjson | 类型继承 |组合键 `<collectionId>:<pathId>`，用于全局唯一资产引用：

| **Indexes Layer** |- **Pattern**: `^[A-Za-z0-9:_-]+:-?\\d+$`

| by_class | `by_class` | by_class.ndjson | 按类型索引 |- **可排序**: 支持字典序排序，确保跨导出一致性

| by_collection | `by_collection` | by_collection.ndjson | 按集合索引 |- **可解析**: 可拆分为 collectionId 和 pathId 组件

| **Metrics Layer** |- **示例**: `sharedassets0.assets:1`, `level0:-1`

| scene_stats | `scene_stats` | scene_stats.json | 场景统计 |

| asset_distribution | `asset_distribution` | asset_distribution.json | 资产分布 |#### UnityGuid

| dependency_stats | `dependency_stats` | dependency_stats.json | 依赖统计 |Unity GUID 格式，用于场景和资产引用：

- **格式**: 32 位十六进制（无连字符）或标准 GUID

---- **示例**: `a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6`



## 📝 License---



AssetDump v2 Schemas are part of the AssetRipper project.  ## 📋 Core Definitions (core.schema.json)

Licensed under the GNU General Public License v3.0.

`core.schema.json` 定义了所有 Schema 共享的核心类型：

---

### AssetPK

**For detailed documentation, see [ARCHITECTURE_DETAILED.md](ARCHITECTURE_DETAILED.md) and layer-specific READMEs.**资产主键，组合 CollectionID 和 PathID：

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

### AssetRef
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

### BundleRef
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

### SceneRef
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

### HierarchyPath
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

---

## 📊 Schema Categories

### Facts Layer (facts/)

存放原子级事实数据，每个实体一条记录。

#### assets.schema.json
**Domain**: `assets`
**输出**: `assets.ndjson`

资产元数据，记录每个 Unity 对象的基本信息：

```json
{
  "domain": "assets",
  "pk": {
    "collectionId": "sharedassets0.assets",
    "pathId": 1
  },
  "classKey": 1,
  "classId": 1,
  "className": "GameObject",
  "name": "Main Camera",
  "collectionName": "sharedassets0.assets",
  "bundleName": "GameBundle",
  "sceneName": "MainScene",
  "hierarchy": {
    "bundlePath": ["00000000", "A1B2C3D4"],
    "bundleNames": ["GameBundle", "level0"],
    "depth": 1
  }
}
```

**关键字段**：
- `pk`: 主键 (collectionId + pathId)
- `classKey`: TypeDictionary 分配的类型键
- `classId`: Unity ClassID
- `className`: 类型名称
- `hierarchy`: 完整层次路径

#### bundles.schema.json
**Domain**: `bundles`
**输出**: `bundles.ndjson`

Bundle 容器信息，支持嵌套层次结构：

```json
{
  "domain": "bundles",
  "pk": "A1B2C3D4",
  "name": "level0",
  "isRoot": false,
  "parentPk": "00000000",
  "bundleIndex": 0,
  "ancestorPath": ["00000000"],
  "childBundlePks": [],
  "collectionIds": ["CAB-1234", "CAB-5678"],
  "scenes": [
    {
      "sceneGuid": "a1b2c3d4...",
      "sceneName": "MainScene",
      "scenePath": "Assets/Scenes/MainScene.unity"
    }
  ],
  "failedFiles": []
}
```

**关键字段**：
- `pk`: Bundle 主键（FNV-1a 哈希）
- `isRoot`: 是否为根 GameBundle
- `parentPk`: 父 Bundle PK（非根必需）
- `ancestorPath`: 从根到父的祖先链
- `childBundlePks`: 直接子 Bundle 列表
- `scenes`: Bundle 中包含的场景

#### collections.schema.json
**Domain**: `collections`
**输出**: `collections.ndjson`

资产集合信息，连接 Bundle 和 Asset：

```json
{
  "domain": "collections",
  "collectionId": "CAB-1234",
  "name": "sharedassets0.assets",
  "flags": ["Processed", "Serialized"],
  "formatVersion": 2019,
  "bundle": {
    "bundlePk": "A1B2C3D4",
    "bundleName": "level0"
  },
  "scene": {
    "sceneGuid": "a1b2c3d4...",
    "sceneName": "MainScene"
  },
  "collectionIndex": 0,
  "dependencies": ["", "CAB-5678"],
  "dependencyIndices": {
    "0": "",
    "1": "CAB-5678"
  }
}
```

**关键字段**：
- `collectionId`: 集合主键
- `bundle`: 所属 Bundle 引用
- `scene`: 关联的场景（如果是场景集合）
- `dependencies`: 依赖的 CollectionID 列表
- `dependencyIndices`: FileID → CollectionID 映射

**注意**：
- 索引 0 始终是自引用（Unity 约定）
- 空字符串表示未解析的依赖

#### scenes.schema.json
**Domain**: `scenes`
**输出**: `scenes.ndjson`

场景层次结构和 GameObject 组成：

```json
{
  "domain": "scenes",
  "sceneGuid": "a1b2c3d4...",
  "sceneName": "MainScene",
  "scenePath": "Assets/Scenes/MainScene.unity",
  "primaryCollectionId": "CAB-1234",
  "bundle": {
    "bundlePk": "A1B2C3D4",
    "bundleName": "level0"
  },
  "collectionDetails": [
    {
      "collectionId": "CAB-1234",
      "collectionName": "mainscene",
      "collectionIndex": 0,
      "gameObjectCount": 15,
      "componentCount": 42
    }
  ],
  "gameObjectCount": 15,
  "rootGameObjectCount": 3,
  "componentCount": 42,
  "gameObjects": [...],
  "hierarchy": [...]
}
```

**关键字段**：
- `primaryCollectionId`: 主集合（`Collections[0]`）
- `collectionDetails`: 组成场景的所有集合详情
- `gameObjects`: GameObject 引用列表（可选，MinimalOutput 控制）
- `hierarchy`: 层次结构树（可选）

#### script_metadata.schema.json
**Domain**: `script_metadata`
**输出**: `script_metadata.ndjson`

MonoBehaviour 脚本元数据：

```json
{
  "domain": "script_metadata",
  "scriptPk": {
    "collectionId": "CAB-1234",
    "pathId": 100
  },
  "assemblyName": "Assembly-CSharp",
  "namespace": "Game.Controllers",
  "className": "PlayerController",
  "isGeneric": false,
  "genericParameterCount": 0,
  "scriptIdentifier": "Assembly-CSharp:Game.Controllers.PlayerController"
}
```

**关键字段**：
- `scriptPk`: 脚本资产主键
- `assemblyName`: 程序集名称
- `namespace`: 命名空间
- `className`: 类名
- `isGeneric`: 是否泛型类型

#### script_sources.schema.json
**Domain**: `script_sources`
**输出**: `script_sources.ndjson`

脚本源代码（反编译或原始）：

```json
{
  "domain": "script_sources",
  "scriptPk": {
    "collectionId": "CAB-1234",
    "pathId": 100
  },
  "sourceCode": "using UnityEngine;\n\npublic class PlayerController : MonoBehaviour\n{ ... }",
  "language": "CSharp",
  "isDecompiled": true
}
```

**关键字段**：
- `scriptPk`: 脚本资产主键
- `sourceCode`: 源代码文本
- `language`: 编程语言（CSharp, JavaScript 等）
- `isDecompiled`: 是否为反编译代码

#### types.schema.json
**Domain**: `types`
**输出**: `types.ndjson`

类型映射（TypeDictionary），支持 MonoBehaviour 脚本区分：

```json
{
  "domain": "types",
  "classKey": 114,
  "classId": 114,
  "className": "MonoBehaviour",
  "scriptPk": {
    "collectionId": "CAB-1234",
    "pathId": 100
  },
  "scriptIdentifier": "Assembly-CSharp:Game.Controllers.PlayerController"
}
```

**关键字段**：
- `classKey`: 唯一类型键（TypeDictionary 分配）
- `classId`: Unity ClassID
- `scriptPk`: 如果是 MonoBehaviour，关联的脚本 PK
- `scriptIdentifier`: 脚本唯一标识符

**设计要点**：
- 每个 MonoBehaviour 脚本有独立的 classKey
- 非脚本类型只有一个 classKey（如 GameObject classKey=1）

#### type_definitions.schema.json
**Domain**: `type_definitions`
**输出**: `type_definitions.ndjson`

完整的类型定义信息（从程序集提取）：

```json
{
  "domain": "type_definitions",
  "assemblyName": "Assembly-CSharp",
  "namespace": "Game.Controllers",
  "className": "PlayerController",
  "fullName": "Game.Controllers.PlayerController",
  "baseType": "UnityEngine.MonoBehaviour",
  "interfaces": ["IPoolable", "IEventHandler"],
  "isAbstract": false,
  "isSealed": false,
  "isGeneric": false,
  "genericParameters": []
}
```

**关键字段**：
- `fullName`: 完全限定名称
- `baseType`: 基类型
- `interfaces`: 实现的接口列表
- `genericParameters`: 泛型参数定义

#### type_members.schema.json
**Domain**: `type_members`
**输出**: `type_members.ndjson`

类型成员（字段、属性、方法）：

```json
{
  "domain": "type_members",
  "assemblyName": "Assembly-CSharp",
  "namespace": "Game.Controllers",
  "className": "PlayerController",
  "memberName": "moveSpeed",
  "memberType": "Field",
  "dataType": "System.Single",
  "isPublic": true,
  "isStatic": false
}
```

**关键字段**：
- `memberName`: 成员名称
- `memberType`: Field, Property, Method
- `dataType`: 数据类型
- `isPublic`: 访问级别

#### assemblies.schema.json
**Domain**: `assemblies`
**输出**: `assemblies.ndjson`

程序集信息：

```json
{
  "domain": "assemblies",
  "assemblyName": "Assembly-CSharp",
  "version": "0.0.0.0",
  "culture": "neutral",
  "publicKeyToken": null,
  "dependencies": [
    {
      "name": "UnityEngine",
      "version": "0.0.0.0"
    }
  ]
}
```

---

### Relations Layer (relations/)

记录实体间的关系边。

#### asset_dependencies.schema.json
**Domain**: `asset_dependencies`
**输出**: `asset_dependencies.ndjson`

资产级 PPtr 依赖：

```json
{
  "domain": "asset_dependencies",
  "from": {
    "collectionId": "CAB-1234",
    "pathId": 1
  },
  "to": {
    "collectionId": "CAB-5678",
    "pathId": 100
  },
  "edge": {
    "fieldPath": "m_Materials.Array.data[0]",
    "isNull": false,
    "isResolved": true
  }
}
```

**关键字段**：
- `from`: 源资产 PK
- `to`: 目标资产 PK
- `edge.fieldPath`: 引用字段路径
- `edge.isNull`: 是否为 null 引用
- `edge.isResolved`: 是否成功解析

#### collection_dependencies.schema.json
**Domain**: `collection_dependencies`
**输出**: `collection_dependencies.ndjson`

集合级依赖关系：

```json
{
  "domain": "collection_dependencies",
  "from": "CAB-1234",
  "to": "CAB-5678",
  "edge": {
    "fileId": 1,
    "fileIdentifier": {
      "guid": "a1b2c3d4...",
      "type": 3,
      "pathName": "Assets/Materials/Floor.mat"
    }
  }
}
```

**关键字段**：
- `from`: 源集合 ID
- `to`: 目标集合 ID
- `edge.fileId`: Unity FileID
- `edge.fileIdentifier`: Unity FileIdentifier 详情

#### bundle_hierarchy.schema.json
**Domain**: `bundle_hierarchy`
**输出**: `bundle_hierarchy.ndjson`

Bundle 父子关系：

```json
{
  "domain": "bundle_hierarchy",
  "from": "00000000",
  "to": "A1B2C3D4",
  "edge": {
    "childIndex": 0,
    "depth": 1
  }
}
```

**关键字段**：
- `from`: 父 Bundle PK
- `to`: 子 Bundle PK
- `edge.childIndex`: 在父的子列表中的索引
- `edge.depth`: 相对于根的深度

---

### Indexes Layer (indexes/)

预建索引，加速常见查询。

#### by_class.schema.json
**Domain**: `by_class`
**输出**: `by_class.ndjson`

按类型分组的资产索引：

```json
{
  "domain": "by_class",
  "classKey": 1,
  "classId": 1,
  "className": "GameObject",
  "count": 4523,
  "assets": [
    {
      "collectionId": "CAB-1234",
      "pathId": 1
    },
    {
      "collectionId": "CAB-1234",
      "pathId": 2
    }
  ]
}
```

**用途**：快速查找特定类型的所有资产

#### by_collection.schema.json
**Domain**: `by_collection`
**输出**: `by_collection.ndjson`

按集合分组的资产索引：

```json
{
  "domain": "by_collection",
  "collectionId": "CAB-1234",
  "bundleName": "level0",
  "classes": [
    {
      "classKey": 1,
      "className": "GameObject",
      "count": 150,
      "assets": [...]
    }
  ]
}
```

**用途**：查看集合内容和类型分布

---

### Metrics Layer (metrics/)

派生统计指标，支持分析和优化。

#### scene_stats.schema.json
**Domain**: `scene_stats`
**输出**: `scene_stats.json` (单记录)

场景统计指标：

```json
{
  "domain": "scene_stats",
  "totalScenes": 10,
  "totalGameObjects": 15423,
  "totalComponents": 45289,
  "rootGameObjects": 342,
  "activeGameObjects": 12000,
  "inactiveGameObjects": 3423,
  "averageGameObjectsPerScene": 1542,
  "sceneCollections": [
    {
      "collectionId": "CAB-1234",
      "sceneName": "MainScene",
      "gameObjectCount": 150
    }
  ]
}
```

#### asset_distribution.schema.json
**Domain**: `asset_distribution`
**输出**: `asset_distribution.json` (单记录)

资产类型分布和大小统计：

```json
{
  "domain": "asset_distribution",
  "summary": {
    "totalAssets": 45230,
    "totalCollections": 25,
    "totalBundles": 5,
    "assetsWithByteSize": 40000
  },
  "byClass": [
    {
      "classKey": 1,
      "classId": 1,
      "className": "GameObject",
      "count": 5000,
      "countWithByteSize": 4800,
      "totalBytes": 1280000,
      "averageBytes": 256,
      "minBytes": 128,
      "maxBytes": 512,
      "medianBytes": 240
    }
  ],
  "byBundle": [...]
}
```

**关键指标**：
- 类型分布（byClass）
- Bundle 分布（byBundle）
- 大小统计（min/max/median/average）

#### dependency_stats.schema.json
**Domain**: `dependency_stats`
**输出**: `dependency_stats.json` (单记录)

依赖图统计：

```json
{
  "domain": "dependency_stats",
  "edges": {
    "total": 250000,
    "averagePerAsset": 5.5,
    "internalReferences": 180000,
    "externalReferences": 50000,
    "crossBundleReferences": 20000,
    "nullReferences": 5000,
    "unresolvedReferences": 1200
  },
  "degree": {
    "outgoing": {
      "average": 5.5,
      "min": 0,
      "max": 450,
      "median": 2.0
    },
    "incoming": {...}
  },
  "health": {
    "totalAssets": 45230,
    "noOutgoingRefs": 15000,
    "noIncomingRefs": 3500,
    "completelyIsolated": 500
  },
  "byType": [...]
}
```

**关键指标**：
- 引用分类（internal/external/cross-bundle）
- 度数分布（in-degree/out-degree）
- 健康度（isolated assets）
- 按类型统计（byType）

---

## 🔧 Implementation Details

### Identifier Generation

#### FNV-1a Hash Algorithm
```csharp
public static string ComputeCollectionId(string collectionName)
{
    const uint FnvPrime = 16777619;
    const uint FnvOffsetBasis = 2166136261;

    uint hash = FnvOffsetBasis;
    foreach (char c in collectionName)
    {
        hash ^= c;
        hash *= FnvPrime;
    }

    return hash.ToString("X8"); // 8-char hex
}
```

**特点**：
- 快速计算（O(n) 时间复杂度）
- 稳定输出（相同输入总是相同输出）
- 低碰撞率（~1/4B）

### Dependency Mapping

#### Collections 双向索引
```csharp
public class CollectionRecord
{
    // 正向查询：迭代所有依赖
    public List<string> Dependencies { get; set; }

    // 反向查询：O(1) FileID → CollectionID
    public Dictionary<string, string> DependencyIndices { get; set; }
}
```

**用法**：
```csharp
// 查找 FileID=1 对应的 CollectionID
string targetCollection = collection.DependencyIndices["1"];

// 迭代所有依赖
foreach (string depId in collection.Dependencies)
{
    // Process dependency
}
```

### Optional Fields Strategy

#### 处理后字段（SceneHierarchyObject）
```csharp
public class SceneRecord
{
    // 可空类型表示可选字段
    public int? PathID { get; set; }
    public int? ClassID { get; set; }
    public string? ClassName { get; set; }

    // 非空但可为空列表
    public List<AssetRef> GameObjects { get; set; } = new();
}
```

#### MinimalOutput 模式
```csharp
if (!MinimalOutput)
{
    scene.GameObjects = CollectGameObjects(hierarchy);
    scene.Hierarchy = BuildHierarchyTree(hierarchy);
}
// 统计字段始终存在
scene.GameObjectCount = CountGameObjects(hierarchy);
```

---

## 📈 Design Decisions

### 1. Domain Field Strategy

**决策**: 所有 Schema 包含必需的 `domain` 常量字段

**理由**:
- 混合流支持（单个 NDJSON 多表）
- Schema 验证（类型安全）
- 查询路由（快速定位）
- 工具兼容性（通用解析器）

**权衡**:
- ✅ 优势：类型安全、易于验证
- ⚠️ 代价：每条记录 ~20 字节（压缩可缓解）

### 2. Identifier Design

**CollectionID 规范化**:
- ✅ 稳定性：FNV-1a 哈希确保跨导出一致
- ✅ 紧凑性：8 字符，节省 ~40 字节
- ✅ 字典序：支持高效排序和范围查询
- ⚠️ 哈希碰撞：理论概率 ~1/4B（实践可忽略）

**StableKey 格式**:
- ✅ 全局唯一：跨所有集合
- ✅ 可排序：字典序确保一致性
- ✅ 可解析：可拆分为组件
- ⚠️ 字符串长度：平均 ~20 字符

### 3. Hierarchy Expression

**四层模型**:
- ✅ 完整映射：忠实反映 AssetRipper 结构
- ✅ 查询支持：支持层次查询
- ✅ 灵活性：任意深度嵌套
- ⚠️ 冗余数据：bundleNames 数组（压缩可缓解）

**主集合概念**:
- ✅ 简单性：`Collections[0]` 为主集合
- ✅ 一致性：符合 Unity 约定
- ✅ 向后兼容：无需修改 AssetRipper
- ⚠️ 隐式依赖：依赖添加顺序（需文档化）

### 4. Dependency System

**双向映射**:
- ✅ 正向查询：dependencies 列表迭代
- ✅ 反向查询：dependencyIndices O(1) 查找
- ✅ PPtr 解析：快速 fileID 映射
- ⚠️ 索引 0 自引用：Unity 约定
- ⚠️ 空字符串：未解析依赖（需 collection_dependencies 表补充）

### 5. Optional Fields

**处理后字段**:
- ✅ 生命周期准确：反映真实数据可用性
- ✅ Schema 正确性：避免对缺失数据报错
- ✅ 可空类型：`int?`, `string?`

**MinimalOutput 模式**:
- ✅ 性能：大场景减少 ~80% 数据
- ✅ 灵活性：用户选择详细程度
- ✅ 核心保留：统计字段始终存在
- ⚠️ 分析限制：需完整导出才能做对象级分析

---

## 🚀 Usage Examples

### Query Examples

#### 查找特定类型的所有资产
```sql
-- 查找所有 Texture2D
SELECT * FROM by_class WHERE classId = 28;

-- 获取具体资产详情
SELECT a.* FROM assets a
JOIN by_class_assets bca ON a.pk.collectionId = bca.collectionId
                          AND a.pk.pathId = bca.pathId
WHERE bca.classKey = (SELECT classKey FROM by_class WHERE classId = 28);
```

#### 分析依赖健康度
```sql
-- 查找完全孤立的资产（清理候选）
SELECT health_completelyIsolated,
       ROUND(100.0 * health_completelyIsolated / health_totalAssets, 2) AS pct
FROM dependency_stats;

-- 检查跨 Bundle 依赖比例
SELECT ROUND(100.0 * edges_crossBundleReferences / edges_total, 2) AS crossBundle_pct
FROM dependency_stats;
-- Good: <10% | Warning: 10-20% | Issue: >20%
```

#### 查找大型资产
```sql
-- Top 10 largest asset types
SELECT className, totalBytes / 1024 / 1024 AS sizeMB
FROM asset_distribution_by_class
ORDER BY totalBytes DESC LIMIT 10;
```

#### 场景分析
```sql
-- 复杂场景识别
SELECT collectionId, gameObjectCount
FROM scene_stats_collections
ORDER BY gameObjectCount DESC
LIMIT 10;
```

### Export Pipeline

```csharp
// 1. 初始化导出器
var exporter = new AssetDumperPipeline();

// 2. 配置选项
exporter.Options = new ExportOptions
{
    OutputDirectory = "output/",
    MinimalOutput = false,  // 完整导出
    IncrementalMode = false // 全量导出
};

// 3. 执行导出
await exporter.ExportAsync(gameData);

// 4. 输出文件
// output/facts/assets.ndjson
// output/facts/bundles.ndjson
// output/relations/asset_dependencies.ndjson
// output/indexes/by_class.ndjson
// output/metrics/scene_stats.json
```

---

## 📝 Validation Notes

### Schema Validation

所有 Schema 遵循 JSON Schema Draft 2020-12 标准：

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://example.com/schemas/v2/facts/assets.schema.json",
  "title": "Asset Facts Schema",
  "description": "Records for individual Unity assets",
  ...
}
```

### Common Validation Rules

1. **domain 字段必需**: 所有记录第一个字段
2. **主键唯一性**: pk/collectionId/sceneGuid 等
3. **引用完整性**: 所有 AssetRef/BundleRef/SceneRef 引用有效
4. **Pattern 验证**: CollectionID、StableKey、UnityGuid 格式
5. **条件验证**: 如非根 Bundle 必须有 parentPk

### Validation Tools

```bash
# 使用 ajv-cli 验证 Schema
npm install -g ajv-cli
ajv validate -s assets.schema.json -d assets.ndjson

# 验证引用完整性
python scripts/validate_refs.py --facts output/facts/ --relations output/relations/
```

---

## 🔄 Migration from v1

### Breaking Changes

1. **domain 字段**: 所有记录必须包含 `domain` 字段
2. **CollectionID**: 从完整路径改为 FNV-1a 哈希
3. **层次结构**: 完整支持四层模型（v1 只有三层）
4. **依赖系统**: 新增 dependencyIndices 映射

### Migration Script

```python
import json

def migrate_asset_record(old_record):
    """迁移 v1 资产记录到 v2"""
    return {
        "domain": "assets",  # 新增
        "pk": {
            "collectionId": compute_collection_id(old_record["collectionName"]),  # 哈希化
            "pathId": old_record["pathId"]
        },
        "classKey": old_record.get("classKey", old_record["classId"]),
        "classId": old_record["classId"],
        "className": old_record["className"],
        "hierarchy": build_hierarchy_path(old_record),  # 新增
        # ... 其他字段
    }
```

---

## 📚 Additional Resources

### Documentation
- **SCHEMA_STRUCTURE.md**: 完整目录结构和字段说明（已整合到本文档）
- **DESIGN_DECISIONS.md**: 架构设计理由和权衡（已整合到本文档）
- **VALIDATION_NOTES.md**: Schema 验证规则（见本文档"Validation Notes"部分）

### Tools
- **Schema Validator**: JSON Schema 验证工具
- **Reference Checker**: 引用完整性检查
- **Migration Script**: v1 → v2 迁移脚本
- **Query Examples**: SQL 查询示例集

### Community
- **GitHub Issues**: 报告问题和功能请求
- **Discord**: 实时讨论和支持
- **Wiki**: 用户贡献的文档和教程

---

## 📄 License

AssetDump v2 Schemas are part of the AssetRipper project.
Licensed under the GNU General Public License v3.0.

---

**Last Updated**: 2025-11-11
**Schema Version**: v2
**Maintainers**: AssetRipper Team
````
