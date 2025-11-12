# Facts Schemas

本文档描述 AssetDump v2 Facts 层的所有 Schema，这些 Schema 定义了从 Unity 资产中提取的基本事实数据。

## 📋 Schema 概览

| Schema | Domain | 描述 | 实现状态 |
|--------|--------|------|----------|
| `collections.schema.json` | `collections` | 集合元数据（版本、依赖、层次归属） | ✅ 完成 |
| `assets.schema.json` | `assets` | 资产基本信息（PK、类型、数据） | ✅ 完成 |
| `types.schema.json` | `types` | ClassID/名称映射表 | ✅ 完成 |
| `bundles.schema.json` | `bundles` | Bundle 层次节点和统计 | ⏳ Schema完成 |
| `scenes.schema.json` | `scenes` | 场景聚合数据和对象列表 | ✅ 完成 |
| `script_metadata.schema.json` | `script_metadata` | MonoScript 详细元数据（✨ 已优化实现） | ✅ 完成 |
| `script_sources.schema.json` | `script_sources` | 源码文件信息（路径、哈希）（✨ 已优化） | ⏳ Schema完成 |
| `type_definitions.schema.json` | `type_definitions` | 类型定义详细信息 | ⏳ Schema完成 |
| `type_members.schema.json` | `type_members` | 类型成员（字段/方法） | ⏳ Schema完成 |
| `assemblies.schema.json` | `assemblies` | 程序集元数据 | ⏳ Schema完成 |

所有 Schema 均依赖 `../core.schema.json` 提供的通用 `$defs`（如 `AssetPK`、`CollectionID`、`BundleRef`、`SceneRef`）。

## 🎯 实现优先级

**高优先级（已完成）**:
- ✅ `collections.schema.json` - 核心依赖图基础
- ✅ `assets.schema.json` - 所有资产的基础数据
- ✅ `types.schema.json` - 类型映射（必需）
- ✅ `scenes.schema.json` - 场景分析核心
- ✅ `script_metadata.schema.json` - MonoScript 元数据（已优化实现）

**中优先级（Schema 完成，待实现 Exporter）**:
- ⏳ `bundles.schema.json` - 层次导航增强

**低优先级（计划中）**:
- ⏳ `script_sources.schema.json` - 源码分析增强（已优化 Schema）
- ⏳ `type_definitions.schema.json` - 深度类型分析
- ⏳ `assemblies.schema.json` - 程序集级元数据

---

## collections.schema.json 详细说明

### 核心字段

**基本标识**:
- `collectionId`: 集合的全局唯一标识符
- `name`: 集合主文件名（如 sharedassets1.assets）
- `filePath`: 磁盘上的相对路径

**集合分类**:
- `collectionType`: 集合类型枚举
  - `Serialized`: 从 SerializedFile 反序列化（对应 `SerializedAssetCollection`）
  - `Processed`: AssetRipper 处理生成（对应 `ProcessedAssetCollection`）
  - `Virtual`: 基类实例（特殊情况）
- `isSceneCollection`: 标识该集合是否来自 Unity 场景文件 (.unity)

**Unity 元数据**:
- `platform`: Unity 构建目标平台字符串
- `unityVersion`: 当前 Unity 版本（处理后）
- `originalUnityVersion`: 原始 Unity 版本（仅当与 unityVersion 不同时包含）
- `formatVersion`: SerializedFile 格式版本（仅 Serialized 集合）
- `endian`: 字节序（LittleEndian/BigEndian）
- `flagsRaw`: 原始标志位字符串
- `flags`: 解析后的标志数组

**层次关系**:
- `bundle`: 父 Bundle 引用（必需，每个集合必须属于一个 Bundle）
- `scene`: 场景引用（可选，仅当 `isSceneCollection=true` 时）
- `collectionIndex`: 在父 Bundle 的集合列表中的索引位置

**依赖系统**:
- `dependencies`: 依赖的 CollectionID 有序列表
  - **Index 0 始终是自引用**（与 Unity 文件索引对应）
  - 后续条目可能为空字符串（无法解析的依赖）
- `dependencyIndices`: CollectionID → 索引的反向映射
  - 用于快速解析 PPtr 引用
  - 只包含非空依赖

**统计信息**:
- `assetCount`: 集合中的资产总数

**物理来源** (`source` 对象):
- `uri`: 物理来源的 URI（文件路径或资源标识符）
- `offset`: 数据在源文件中的字节偏移
- `size`: 数据在源文件中的字节大小
- 用途：追踪大型 bundle 文件中的集合位置

**Unity 特殊分类** (`unity` 对象):
- `builtInClassification`: Unity 内置资源分类
  - `BUILTIN-EXTRA`: Unity 额外内置资源
  - `BUILTIN-DEFAULT`: Unity 默认内置资源
  - `BUILTIN-EDITOR`: Unity 编辑器内置资源

### 代码映射

字段与 AssetRipper 代码的对应关系:

```csharp
// AssetCollection 核心属性
Name                → name
FilePath            → filePath
Platform            → platform
Version             → unityVersion
OriginalVersion     → originalUnityVersion (仅当不同时)
EndianType          → endian
Flags               → flags/flagsRaw
IsScene             → isSceneCollection
Count               → assetCount

// 类型判断
SerializedAssetCollection   → collectionType = "Serialized"
ProcessedAssetCollection    → collectionType = "Processed"

// 层次关系
Bundle              → bundle (BundleRef)
Scene               → scene (SceneRef, 可选)
Dependencies        → dependencies (CollectionID 列表)

// SerializedAssetCollection 特有
FormatVersion       → formatVersion (仅 Serialized)
```

### 使用场景

1. **依赖解析**: 使用 `dependencies` 和 `dependencyIndices` 快速解析 PPtr 引用
2. **版本追踪**: 对比 `unityVersion` 和 `originalUnityVersion` 识别版本升级
3. **类型过滤**: 通过 `collectionType` 区分原始文件和处理生成的集合
4. **物理定位**: 使用 `source` 定位大型 bundle 中的特定集合
5. **内置资源**: 通过 `unity.builtInClassification` 识别 Unity 内置资源

### 已知限制

- `formatVersion` 只对 `SerializedAssetCollection` 可用，`ProcessedAssetCollection` 不适用
- `originalUnityVersion` 只在版本发生变化时包含（避免冗余）
- 依赖列表中的空字符串表示无法解析的依赖（保持索引一致性）

## scenes.schema.json 详细说明

### 核心字段

**场景基本信息**:
- `name`: 场景名称
- `sceneGuid`: Unity 场景 GUID（来自 SceneHierarchyObject.Scene.GUID）
- `scenePath`: 场景在项目中的路径（如 Assets/Scenes/Level1.unity）
- `exportedAt`: 导出时间戳

**集合信息**:
- `sceneCollectionCount`: 组成该场景的集合数量（最少为1）
- `collectionIds`: 所有集合的 CollectionID 列表
- `primaryCollectionId`: 主集合ID（第一个添加到场景的集合）
- `bundle`: 主集合所属的 Bundle 引用
- `collectionDetails`: 每个集合的详细元数据数组

**层次结构对象**（可选）:
- `hierarchy`: SceneHierarchyObject 资产引用（仅在处理后存在）
- `hierarchyAssetId`: 层次对象的稳定键
- `pathID`, `classID`, `className`: 层次对象的标识信息

**统计信息**:
- `assetCount`: 场景中的总资产数
- `gameObjectCount`: GameObject 数量
- `componentCount`: 组件数量
- `managerCount`: 场景管理器数量
- `prefabInstanceCount`: Prefab 实例数量
- `dependencyCount`: 依赖数量
- `rootGameObjectCount`: 根 GameObject 数量
- `strippedAssetCount`: 被剥离的资产数量
- `hiddenAssetCount`: 隐藏资产数量
- `hasSceneRoots`: 是否有 SceneRoots 对象

**资产引用列表**（可选，取决于 MinimalOutput 设置）:
- `sceneRootsAsset`: SceneRoots 资产引用
- `sceneRoots`: 场景根列表
- `rootGameObjects`: 根 GameObject 列表
- `gameObjects`: 所有 GameObject 列表
- `components`: 所有组件列表
- `managers`: 场景管理器列表
- `prefabInstances`: Prefab 实例列表
- `strippedAssets`: 被剥离的资产列表
- `hiddenAssets`: 隐藏资产列表

### collectionDetails 详解

每个 `collectionDetails` 条目包含:

```json
{
  "collectionId": "A1B2C3D4",
  "bundle": {
    "bundlePk": "E5F6G7H8",
    "bundleName": "level1"
  },
  "isPrimary": true,
  "assetCount": 1234
}
```

- `collectionId`: 集合的唯一标识符
- `bundle`: 该集合所属的 Bundle（注意：不同集合可能属于不同 Bundle）
- `isPrimary`: 是否为主集合（第一个集合）
- `assetCount`: 该集合中的资产数量

### 代码映射

```csharp
// SceneDefinition 属性
Name                → name
Scene.GUID          → sceneGuid
Scene.Path          → scenePath
Collections.Count   → sceneCollectionCount
Collections         → collectionIds (通过 ComputeCollectionId)

// SceneHierarchyObject 属性
PathID              → pathID
ClassID             → classID
ClassName           → className
Assets.Count()      → assetCount
GameObjects.Count   → gameObjectCount
Components.Count    → componentCount
Managers.Count      → managerCount
PrefabInstances.Count → prefabInstanceCount
GetRoots().Count()  → rootGameObjectCount
StrippedAssets.Count → strippedAssetCount
HiddenAssets.Count  → hiddenAssetCount
SceneRoots != null  → hasSceneRoots
```

### 主集合概念

**主集合** (`primaryCollectionId`) 定义为场景集合列表中的**第一个集合**：
- 用于确定场景的版本、平台、标志等主要元数据
- 场景可以包含多个集合（跨 bundle 场景）
- `bundle` 字段只引用主集合的 Bundle
- 使用 `collectionDetails` 查看每个集合的完整 Bundle 信息

### 使用场景

1. **场景组成分析**: 使用 `collectionDetails` 了解场景跨哪些集合/Bundle
2. **资产统计**: 通过各种 count 字段快速了解场景规模
3. **依赖分析**: 检查 `dependencyCount` 和 `prefabInstanceCount` 了解场景复杂度
4. **层次遍历**: 使用 `rootGameObjects` 或 `gameObjects` 列表遍历场景对象
5. **完整性检查**: 通过 `strippedAssetCount` 和 `hiddenAssetCount` 识别数据丢失

### 已知限制

- `hierarchy`, `hierarchyAssetId`, `pathID`, `classID`, `className` 仅在场景处理后可用
- 资产引用列表（gameObjects, components等）在 `MinimalOutput=true` 时不导出
- `sceneRoots` 可能为 null（某些 Unity 版本不使用 SceneRoots）
- 跨 Bundle 场景的不同集合可能有不同的版本/平台（使用 `collectionDetails` 区分）

---

## 📊 实现状态详表

### Collections Schema（✅ 完全实现）

| 功能 | Schema | Model | Exporter | 文档 | 测试 |
|------|--------|-------|----------|------|------|
| 基本字段 | ✅ | ✅ | ✅ | ✅ | ✅ |
| `collectionType` | ✅ | ✅ | ✅ | ✅ | ⏳ |
| `originalUnityVersion` | ✅ | ✅ | ✅ | ✅ | ⏳ |
| `bundle` 引用 | ✅ | ✅ | ✅ | ✅ | ⏳ |
| `scene` 引用 | ✅ | ✅ | ✅ | ✅ | ⏳ |
| `dependencies` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `dependencyIndices` | ✅ | ✅ | ✅ | ✅ | ⏳ |
| `source` 物理来源 | ✅ | ❌ | ❌ | ✅ | ⏳ |
| `unity.builtInClassification` | ✅ | ❌ | ❌ | ✅ | ⏳ |

**完成度**: 核心功能 100%，增强功能 70%

**代码文件**:
- Model: `CollectionFactRecord.cs`
- Exporter: `CollectionFactsExporter.cs`
- Helper: `ExportHelper.cs` (ComputeCollectionId)

**最近更新** (2025-11-11):
- ✅ 添加 `collectionType` 字段和类型检测逻辑
- ✅ 添加 `originalUnityVersion` 字段和版本比较
- ✅ 完善文档和代码映射表

---

### Types（✅ Schema 完成，🏗️ 代码 50%）

**Schema**: `types.schema.json`
**Model**: `TypeRecord.cs` ✅
**Exporter**: `TypesExporter.cs` ⏳

**目的**: 建立 `classKey` 与 Unity 类型信息之间的映射，避免在每个资产记录中重复类型元数据。

**主键 (classKey)**: 导出器分配的稳定整数标识符，在 `assets.classKey` 中引用。

**数据来源**:
- `IUnityObjectBase.ClassID/ClassName`: 核心类型信息
- `ObjectInfo`: 来自 SerializedFile 的类型元数据
- `SerializedType`: 类型定义（Unity 5+）
- `UniversalClass`: AssetRipper 的类型层次信息
- `MonoScript`: MonoBehaviour 的脚本信息

**核心字段**:

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `domain` | `"types"` | ✅ | 固定为 "types" |
| `classKey` | integer | ✅ | 导出器分配的稳定标识符 |
| `classId` | integer | ✅ | Unity ClassID (114 = MonoBehaviour) |
| `className` | string | ✅ | Unity 类型名称 |
| `typeId` | integer | - | **新增** 类型 ID（对于 MonoBehaviour 是脚本的唯一标识符） |
| `serializedTypeIndex` | integer | - | **新增** SerializedFile.Types 数组索引（Unity 5+） |
| `scriptTypeIndex` | integer | - | MonoBehaviour 的脚本类型索引 |
| `isStripped` | boolean | - | 类型定义是否被剥离 |
| `originalClassName` | string | - | **新增** 原始类型名称（处理前） |
| `baseClassName` | string | - | **新增** 基类名称 |
| `isAbstract` | boolean | - | **新增** 是否为抽象类 |
| `isEditorOnly` | boolean | - | **新增** 是否仅编辑器类 |
| `isReleaseOnly` | boolean | - | **新增** 是否仅游戏构建类 |
| `monoScript` | object | - | **新增** MonoBehaviour 的脚本信息 |
| `notes` | string | - | 额外说明 |

**monoScript 对象字段**（仅 MonoBehaviour，ClassID 114）:
- `assemblyName`: 程序集名称（来自 `MonoScript.AssemblyName`）
- `namespace`: 命名空间（来自 `MonoScript.Namespace`）
- `className`: 脚本类名（来自 `MonoScript.ClassName`）
- `scriptGuid`: 脚本 GUID（来自 MonoScript GUID）

**优化点**（2025-01-20）:
1. ✅ 添加 `typeId` - 完整支持 MonoBehaviour 类型系统（`ObjectInfo.TypeID`）
2. ✅ 添加 `serializedTypeIndex` - 支持 Unity 5+ 类型引用机制（`ObjectInfo.SerializedTypeIndex`）
3. ✅ 添加类型层次信息 - `baseClassName`, `isAbstract`, `isEditorOnly`, `isReleaseOnly`（来自 `UniversalClass`）
4. ✅ 添加 `monoScript` 对象 - 完整描述 MonoBehaviour 脚本类型
5. ✅ 添加 `originalClassName` - 保留原始类型名称（`UniversalClass.OriginalName`）

**代码映射**:

```csharp
// IUnityObjectBase 核心属性
ClassID            → classId
ClassName          → className

// ObjectInfo 元数据
TypeID             → typeId
SerializedTypeIndex → serializedTypeIndex
ScriptTypeIndex    → scriptTypeIndex
Stripped           → isStripped

// UniversalClass 类型层次
OriginalName       → originalClassName
Base?.Name         → baseClassName
IsAbstract         → isAbstract
EditorRootNode     → isEditorOnly (EditorRootNode != null && ReleaseRootNode == null)
ReleaseRootNode    → isReleaseOnly (ReleaseRootNode != null && EditorRootNode == null)

// MonoScript (对于 MonoBehaviour, ClassID 114)
AssemblyName       → monoScript.assemblyName
Namespace          → monoScript.namespace
ClassName          → monoScript.className
GUID               → monoScript.scriptGuid
```

**关系**:
- **1:N** 与 `assets`: `types.classKey` → `assets.classKey`
- **关联** `script_metadata`: 通过 `monoScript.scriptGuid` 关联脚本元数据

**用例**:
- **类型字典**: 避免在每个资产记录中重复类型信息（节省 50%+ 空间）
- **MonoBehaviour 分析**: 通过 `monoScript` 对象识别自定义脚本及其来源程序集
- **类型层次查询**: 使用 `baseClassName` 构建继承关系图谱
- **构建类型检测**: 通过 `isEditorOnly`/`isReleaseOnly` 区分编辑器/游戏专用类型
- **剥离类型追踪**: 使用 `isStripped` 识别被构建优化剥离的类型定义

**示例输出**:

*普通类型*:
```json
{
  "domain": "types",
  "classKey": 1,
  "classId": 1,
  "className": "GameObject",
  "typeId": 1,
  "serializedTypeIndex": 0,
  "isStripped": false,
  "originalClassName": "GameObject",
  "baseClassName": "EditorExtension",
  "isAbstract": false,
  "isEditorOnly": false,
  "isReleaseOnly": false
}
```

*MonoBehaviour 类型*:
```json
{
  "domain": "types",
  "classKey": 42,
  "classId": 114,
  "className": "MonoBehaviour",
  "typeId": 123456,
  "serializedTypeIndex": 15,
  "scriptTypeIndex": 3,
  "isStripped": false,
  "monoScript": {
    "assemblyName": "Assembly-CSharp",
    "namespace": "Game.Controllers",
    "className": "PlayerController",
    "scriptGuid": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"
  }
}
```

*编辑器专用类型*:
```json
{
  "domain": "types",
  "classKey": 99,
  "classId": 129,
  "className": "EditorSettings",
  "typeId": 129,
  "isStripped": false,
  "isEditorOnly": true,
  "isReleaseOnly": false
}
```

**待实现**: TypesExporter（类型字典生成、classKey 分配、MonoScript 信息提取、UniversalClass 集成）

**待实现**:
- ⏳ `source` 字段（物理来源追踪）
- ⏳ `unity.builtInClassification` 字段
- ⏳ 单元测试覆盖

---

### Scenes Schema（✅ 完全实现）

| 功能 | Schema | Model | Exporter | 文档 | 测试 |
|------|--------|-------|----------|------|------|
| 基本字段 | ✅ | ✅ | ✅ | ✅ | ✅ |
| `primaryCollectionId` | ✅ | ✅ | ✅ | ✅ | ⏳ |
| `bundle` 引用 | ✅ | ✅ | ✅ | ✅ | ⏳ |
| `collectionDetails` | ✅ | ✅ | ✅ | ✅ | ⏳ |
| 可选层次字段 | ✅ | ✅ | ✅ | ✅ | ⏳ |
| 统计字段 | ✅ | ✅ | ✅ | ✅ | ✅ |
| 资产引用列表 | ✅ | ✅ | ✅ | ✅ | ✅ |
| `MinimalOutput` 支持 | ✅ | ✅ | ✅ | ✅ | ⏳ |

**完成度**: 核心功能 100%，增强功能 95%

**代码文件**:
- Model: `SceneRecord.cs`, `SceneCollectionDetail.cs`
- Exporter: `SceneRecordExporter.cs`
- Helper: `ExportHelper.cs` (ComputeBundlePk)

**最近更新** (2025-11-11):
- ✅ 添加 `primaryCollectionId` 和 `bundle` 字段
- ✅ 实现 `collectionDetails` 数组（完整跨 Bundle 支持）
- ✅ 将层次对象字段改为可选（`int?`, `string?`）
- ✅ 添加 `SceneCollectionDetail` 类
- ✅ 更新 `SceneCollectionDescriptor`（添加 `bundlePk`, `assetCount`）
- ✅ 实现 `CreateSceneCollectionDetail` 和 `BuildBundleRef` 方法
- ✅ 完善文档和 JSON 示例

**待实现**:
- ⏳ 单元测试覆盖

---

## script_metadata.schema.json 详细说明（✨ 2025-11-11 优化）

### 核心字段

**MonoScript 基本标识**:
- `collectionId` 和 `pathId`: 资产的唯一标识（MonoScript 是 IUnityObjectBase 的子类型）
- `classId`: ClassID（MonoScript 通常为 115）
- `className`: 短类名（来自 `IMonoScript.ClassName_R`）
- `fullName`: 完全限定类型名（来自 `MonoScriptExtensions.GetFullName()`）
- `namespace`: 命名空间
- `assemblyName`: 程序集名称（经过 `GetAssemblyNameFixed()` 处理）

**关键新增字段** (2025-11-11):
- `isPresent`: **必需字段**，标识脚本是否在程序集中存在（来自 `MonoScriptExtensions.IsScriptPresents()`）
- `assemblyNameRaw`: 原始程序集名称（未经 `FixAssemblyName()` 处理）
- `isGeneric`: 是否为泛型类型定义
- `genericParameterCount`: 泛型参数数量

**Unity 元数据**:
- `executionOrder`: 脚本执行顺序
- `scriptGuid`: 脚本 GUID（从内容哈希派生）
- `assemblyGuid`: 程序集 GUID
- `scriptFileId`: Unity 计算的文件标识符
- `propertiesHash`: 属性哈希（支持 8 或 32 字符，对应不同 Unity 版本）

**场景来源信息**:
- `scene.name`: 场景名称（当脚本来自场景集合时）
- `scene.path`: 场景路径
- `scene.guid`: 场景 GUID

### 代码映射

```csharp
// MonoScript 核心属性
PathID              → pathId
ClassID             → classId
ClassName_R         → className
Namespace           → namespace
AssemblyName        → assemblyNameRaw (原始)

// 扩展方法
GetFullName()       → fullName
GetAssemblyNameFixed() → assemblyName (处理后)
IsScriptPresents()  → isPresent
GetPropertiesHash() → propertiesHash

// 泛型检测 (需要实现)
IsGeneric()         → isGeneric
GetGenericParameterCount() → genericParameterCount
```

### 使用场景

1. **Missing Script 检测**
   ```json
   {
     "className": "PlayerBehavior",
     "isPresent": false  // ⚠️ 脚本丢失
   }
   ```

2. **泛型脚本处理**
   ```json
   {
     "className": "Singleton<T>",
     "isGeneric": true,
     "genericParameterCount": 1
   }
   ```

3. **程序集名称调试**
   ```json
   {
     "assemblyName": "Assembly-CSharp",
     "assemblyNameRaw": "assembly-csharp"  // 大小写不匹配
   }
   ```

4. **Unity 版本差异**
   ```json
   {"propertiesHash": "a1b2c3d4"}  // 8 字符 (旧版 UInt32)
   {"propertiesHash": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"}  // 32 字符 (新版 Hash128)
   ```

### 已知限制

- `isPresent` 依赖于程序集加载状态（需要先加载所有程序集）
- 泛型检测需要额外的解析逻辑（从类名或反射）
- `scene` 对象仅在脚本来自场景集合时存在

### 详细文档

完整的优化说明、代码示例和实现指南请参阅：
- `SCRIPT_METADATA_OPTIMIZATION.md` - 详细的优化说明和代码映射

---

### Bundles Schema（⏳ Schema 完成，Exporter 待实现）

| 功能 | Schema | Model | Exporter | 文档 | 测试 |
|------|--------|-------|----------|------|------|
| 基本字段 | ✅ | ⏳ | ⏳ | ✅ | ⏳ |
| `childBundlePks` | ✅ | ⏳ | ⏳ | ✅ | ⏳ |
| `childBundleNames` | ✅ | ⏳ | ⏳ | ✅ | ⏳ |
| `ancestorPath` | ✅ | ⏳ | ⏳ | ✅ | ⏳ |
| `bundleIndex` | ✅ | ⏳ | ⏳ | ✅ | ⏳ |
| `failedFiles` | ✅ | ⏳ | ⏳ | ✅ | ⏳ |
| `scenes` 列表 | ✅ | ⏳ | ⏳ | ✅ | ⏳ |
| 条件验证 | ✅ | ⏳ | ⏳ | ✅ | ⏳ |

**完成度**: Schema 100%，代码 0%

**Schema 设计亮点**:
- ✅ 完整的层次结构表达（`childBundlePks`, `ancestorPath`）
- ✅ 失败文件详细记录（`BundleFailedFileRecord`）
- ✅ 场景列表引用（`SceneRef` 数组）
- ✅ 条件验证（非根 Bundle 需要 `parentPk` 和 `bundleIndex`）

**待实现**:
- ⏳ `BundleRecord.cs` Model 类
- ⏳ `BundleExporter.cs` Exporter 实现
- ⏳ `ExportHelper` 中的 Bundle 遍历逻辑

---

### Assets Schema（⏳ Schema 完成，增强功能待实现）

| 功能 | Schema | Model | Exporter | 文档 | 测试 |
|------|--------|-------|----------|------|------|
| 基本字段 | ✅ | ✅ | ✅ | ✅ | ✅ |
| `pathId` | ✅ | ❌ | ❌ | ⏳ | ⏳ |
| `className` | ✅ | ❌ | ❌ | ⏳ | ⏳ |
| `hierarchy` | ✅ | ❌ | ❌ | ⏳ | ⏳ |
| `collectionName` | ✅ | ❌ | ❌ | ⏳ | ⏳ |
| `bundleName` | ✅ | ❌ | ❌ | ⏳ | ⏳ |
| `sceneName` | ✅ | ❌ | ❌ | ⏳ | ⏳ |
| 原始路径属性 | ✅ | ❌ | ❌ | ⏳ | ⏳ |

**完成度**: 核心功能 100%，增强功能 30%

**待实现**:
- ⏳ 冗余字段（`pathId`, `className`, `*Name`）
- ⏳ `HierarchyPath` 生成（需要 Bundle 层次信息）
- ⏳ 原始路径属性提取

---

### Types Schema（✅ 完全实现）

| 功能 | Schema | Model | Exporter | 文档 | 测试 |
|------|--------|-------|----------|------|------|
| ClassID 映射 | ✅ | ✅ | ✅ | ✅ | ✅ |
| 类名映射 | ✅ | ✅ | ✅ | ✅ | ✅ |
| `classKey` 系统 | ✅ | ✅ | ✅ | ✅ | ✅ |

**完成度**: 100%

---

## script_sources.schema.json 详细说明（✨ 2025-11-11 优化）

### 核心字段

**脚本关联**:
- `pk`: 脚本 GUID（来自 `ScriptHashing.CalculateScriptGuid()`）
- `scriptPk`: MonoScript 的 StableKey 引用（`collectionId:pathId`）
- `assemblyGuid`: 程序集 GUID（来自 `ScriptHashing.CalculateAssemblyGuid()`）

**源文件元数据**:
- `sourcePath`: 反编译后的源文件相对路径
- `sourceSize`: 文件大小（字节）
- `lineCount`: 行数统计
- `characterCount`: 字符数统计（可选）
- `sha256`: 文件内容的 SHA256 哈希（用于完整性验证）

**反编译信息**:
- `language`: 编程语言（`CSharp`, `UnityShader`, `HLSL`, `UnityScript`）
- `decompiler`: 反编译器名称（通常为 "ILSpy"）
- `decompilerVersion`: 反编译器版本

**关键新增字段** (2025-11-11):
- `decompilationStatus`: **必需字段**，反编译状态（`success`, `failed`, `empty`, `skipped`）
- `isEmpty`: 是否为空占位脚本（EmptyScript）
- `errorMessage`: 反编译失败时的错误信息
- `isPresent`: 脚本类型是否在程序集中存在
- `isGeneric`: 是否为泛型类型

**未来功能**:
- `hasAst`: 是否存在 AST 文件（目前未实现）
- `astPath`: AST JSON 文件路径（目前未实现）

### 代码映射

```csharp
// 脚本导出流程 (ScriptExportCollection.cs)
ScriptHashing.CalculateScriptGuid()      → pk
ScriptHashing.CalculateAssemblyGuid()    → assemblyGuid
GetExportSubPath()                       → sourcePath
MonoScriptExtensions.IsScriptPresents()  → isPresent

// 反编译 (ScriptDecompiler.cs)
DecompileWholeProject()                  → 生成源文件
ILSpy                                    → decompiler

// 空脚本处理 (EmptyScript.cs)
EmptyScript.Generate()                   → isEmpty = true, decompilationStatus = "empty"
```

### 使用场景

1. **成功反编译**
   ```json
   {
     "pk": "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6",
     "scriptPk": "A1B2C3D4:123456",
     "sourcePath": "Assembly-CSharp/PlayerController.cs",
     "sourceSize": 5432,
     "lineCount": 178,
     "sha256": "a1b2...",
     "language": "CSharp",
     "decompiler": "ILSpy",
     "decompilationStatus": "success",
     "isPresent": true
   }
   ```

2. **空占位脚本**（Missing Script）
   ```json
   {
     "pk": "...",
     "scriptPk": "...",
     "sourcePath": "Assembly-CSharp/MissingBehavior.cs",
     "sourceSize": 156,
     "lineCount": 8,
     "decompilationStatus": "empty",
     "isEmpty": true,
     "isPresent": false
   }
   ```

3. **反编译失败**
   ```json
   {
     "pk": "...",
     "scriptPk": "...",
     "decompilationStatus": "failed",
     "errorMessage": "Unable to resolve dependencies"
   }
   ```

4. **泛型类型**
   ```json
   {
     "pk": "...",
     "scriptPk": "...",
     "sourcePath": "Assembly-CSharp/Singleton.cs",
     "isPresent": true,
     "isGeneric": true
   }
   ```

### 重要说明

**一对多关系**:
- 在 AssetRipper 中，一个程序集会被反编译成多个源文件
- `DecompileWholeProject()` 将整个程序集反编译到一个目录
- 每个类型生成一个单独的 `.cs` 文件
- 因此，多个 `ScriptSourceRecord` 可能共享同一个 `assemblyGuid`

**语言枚举更新**:
- 将 `JavaScript` 改为 `UnityScript` 以反映历史准确性
- Unity 在早期版本中支持 UnityScript（类 JavaScript 语法）
- 现代版本已移除该支持

**AST 支持**:
- `hasAst` 和 `astPath` 字段标记为未来功能
- AssetRipper 当前不生成 AST 文件
- ILSpy 的 `DecompileWholeProject()` 只生成 C# 源代码

### 已知限制

- AST 功能未实现（需要额外的 Roslyn 集成）
- 反编译器版本追踪可能不精确（需要从 ILSpy 包元数据中提取）
- 字符数统计为可选（需要额外的文件读取）

---

### Script Metadata & Sources（✅ Schema 完成，代码部分实现）

| Schema | 完成度 | 优先级 | 说明 |
|--------|--------|--------|------|
| `script_metadata.schema.json` | Schema 100%, Code 85% | 高 | ✅ MonoScript 元数据已实现 |
| `script_sources.schema.json` | Schema 100%, Model 100% | 低 | ✨ 已优化，Exporter 待实现 |

**script_metadata 最新优化** (2025-11-11):
- ✅ 添加 `isPresent` 必需字段（检测 Missing Script）
- ✅ 添加 `assemblyNameRaw` 字段（保留原始程序集名）
- ✅ 添加 `isGeneric` 和 `genericParameterCount` 字段（泛型支持）
- ✅ 更新 `propertiesHash` 正则表达式（支持 8 或 32 字符）
- ✅ 更新 Model 和 Exporter 实现
- ✅ 通过完整构建验证

**script_sources 最新优化** (2025-11-11):
- ✅ 添加 `decompilationStatus` 必需字段（`success`/`failed`/`empty`/`skipped`）
- ✅ 添加 `isEmpty` 字段（标识 EmptyScript 占位符）
- ✅ 添加 `errorMessage` 字段（记录反编译失败原因）
- ✅ 添加 `isPresent` 和 `isGeneric` 字段（与 script_metadata 保持一致）
- ✅ 更新语言枚举：`JavaScript` → `UnityScript`（反映历史准确性）
- ✅ 标记 AST 功能为未来特性（当前未实现）
- ✅ 更新 Model 类添加新字段

**注意**: 原 `scripts.schema.json` 已移除，与 `script_metadata.schema.json` 合并以避免重复。

**待实现**: ScriptSourceExporter（反编译流程集成）

---

### Assemblies（✅ Schema 完成，🏗️ 代码 50%）

**Schema**: `assemblies.schema.json`
**Model**: `AssemblyRecord.cs` ✅
**Exporter**: `AssemblyFactsExporter.cs` ⏳

**目的**: 导出托管程序集的元数据，支持 Mono/IL2CPP 两种脚本后端。

**主键 (pk)**: 使用 `ScriptHashing.CalculateAssemblyGuid(assemblyName)` 生成的 SHA256 哈希（16字符）。

**数据来源**:
- `IAssemblyManager.GetAssemblies()`: 枚举所有程序集
- `AssemblyDefinition`: 程序集定义信息（版本、框架、类型）
- `MonoManager.AssemblyFolder`: 程序集 DLL 路径
- `ReferenceAssemblies.Predefined`: 预定义程序集列表
- `ScriptExportCollection.Types`: 程序集导出状态

**核心字段**:

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `domain` | `"assemblies"` | ✅ | 固定为 "assemblies" |
| `pk` | string | ✅ | 程序集 GUID（16字符 SHA256） |
| `name` | string | ✅ | 简单名称（如 "UnityEngine.CoreModule"） |
| `fullName` | string | ✅ | 完整程序集名称（含版本和公钥） |
| `version` | string | ✅ | 版本号（如 "1.0.0.0"） |
| `targetFramework` | string | - | 目标框架（如 ".NETStandard,Version=v2.1"） |
| `scriptingBackend` | enum | ✅ | 脚本后端: "Unknown"/"Mono"/"IL2CPP" |
| `assemblyType` | enum | ✅ | **新增** 程序集类型: "Predefined"/"UnityEngine"/"UnityExtension"/"User"/"System" |
| `dllPath` | string | - | DLL 文件相对路径（相对于 `AssemblyFolder`） |
| `dllSize` | integer | - | DLL 文件大小（字节） |
| `dllSha256` | string | - | DLL 文件的 SHA256 哈希（64字符） |
| `typeCount` | integer | ✅ | 包含的类型数量（来自 `AssemblyDefinition.MainModule.Types.Count`） |
| `scriptCount` | integer | ✅ | 导出的 MonoScript 数量 |
| `isDynamic` | boolean | ✅ | 是否为动态生成程序集（默认 false） |
| `isEditor` | boolean | ✅ | 是否为编辑器专用程序集（默认 false） |
| `platform` | string | - | 目标平台（如 "Android"/"iOS"） |
| `mscorlibVersion` | integer | - | **新增** mscorlib 引用的版本号（来自 `ReferenceAssemblies.GetMscorlibVersion()`） |
| `references` | array | - | **新增** 引用的程序集名称列表（来自 `AssemblyDefinition.MainModule.AssemblyReferences`） |
| `exportType` | string | - | **新增** 导出类型（来自 `ScriptExportCollection.ExportType.ToExportString()`） |
| `isModified` | boolean | - | **新增** 是否在导出过程中被修改（来自 `ScriptExportCollection.IsModifiedAssembly()`） |

**assemblyType 分类规则** (AssetRipper 代码映射):
- `"Predefined"`: 在 `ReferenceAssemblies.Predefined` 列表中
- `"UnityEngine"`: 名称以 "UnityEngine" 开头
- `"UnityExtension"`: 名称以 "Unity" 开头（但不是 UnityEngine）
- `"User"`: 用户脚本程序集（不符合上述条件）
- `"System"`: .NET 系统程序集（如 mscorlib, System.*）

**关系**:
- **1:N** 与 `script_metadata`: `assemblies.pk` → `script_metadata.assemblyPk`
- **引用关系**: `assemblies.references[]` 包含其他程序集的 `name`

**优化点**（2025-01-20）:
1. ✅ 添加 `assemblyType` 必需字段 - 支持程序集分类（预定义/Unity/用户/系统）
2. ✅ 添加 `mscorlibVersion` - 追踪 .NET 版本兼容性
3. ✅ 添加 `references` - 记录程序集依赖关系
4. ✅ 添加 `exportType` - 记录导出处理类型
5. ✅ 添加 `isModified` - 标记导出过程中的修改

**用例**:
- **Missing Script 修复**: 通过 `assemblyType` 优先加载 Unity 官方程序集
- **依赖分析**: 通过 `references` 构建程序集依赖图谱
- **.NET 兼容性**: 通过 `mscorlibVersion` 判断 Unity 版本的 .NET 目标框架
- **导出审计**: 通过 `exportType` 和 `isModified` 追踪反编译处理流程

**待实现**: AssemblyFactsExporter（IAssemblyManager 集成，类型统计，依赖解析）

---

### Type Definitions（✅ Schema 完成，🏗️ 代码 50%）

**Schema**: `type_definitions.schema.json`
**Model**: `TypeDefinitionRecord.cs` ✅, `ScriptReference.cs` ✅
**Exporter**: `TypeDefinitionsExporter.cs` ⏳

**目的**: 导出程序集中的 .NET 类型定义，包含完整的类型元数据和 Unity 特定标记。

**主键 (pk)**: 复合键格式 `ASSEMBLY::NAMESPACE::TYPENAME`（使用 `::` 避免与类型名中的 `:` 冲突）。

**数据来源**:
- `TypeDefinition`: AsmResolver 的类型定义（核心元数据）
- `TypeDefinitionExtensions`: 类型层次查询（`IsSubclassOf`）
- `BaseManager.IsValid()`: Unity 序列化检查
- `MonoScriptExtensions.GetTypeDefinition()`: MonoScript 与 TypeDefinition 关联

**核心字段**:

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `domain` | `"type_definitions"` | ✅ | 固定为 "type_definitions" |
| `pk` | string | ✅ | 复合键（`ASSEMBLY::NAMESPACE::TYPENAME`） |
| `assemblyGuid` | string | ✅ | 程序集 GUID（16字符，链接到 `assemblies.pk`） |
| `assemblyName` | string | ✅ | 程序集名称 |
| `namespace` | string | - | 命名空间（空字符串表示全局命名空间） |
| `typeName` | string | ✅ | 简单类型名 |
| `fullName` | string | ✅ | 完全限定类型名 |
| `isClass` | boolean | ✅ | 是否为类 |
| `isStruct` | boolean | ✅ | 是否为结构体 |
| `isInterface` | boolean | ✅ | 是否为接口 |
| `isEnum` | boolean | ✅ | 是否为枚举 |
| `isAbstract` | boolean | ✅ | 是否为抽象类 |
| `isSealed` | boolean | ✅ | 是否为密封类 |
| `isGeneric` | boolean | ✅ | 是否为泛型类型 |
| `genericParameterCount` | integer | - | 泛型参数数量 |
| `visibility` | string | ✅ | 可见性（`public`/`internal`/`private`/`protected`/`protected internal`/`private protected`） |
| `baseType` | string | - | **新增** 基类完全限定名 |
| `isNested` | boolean | - | **新增** 是否为嵌套类型 |
| `declaringType` | string | - | **新增** 声明类型（对于嵌套类型） |
| `interfaces` | array | - | **新增** 实现的接口列表 |
| `fieldCount` | integer | - | **新增** 字段数量 |
| `methodCount` | integer | - | **新增** 方法数量 |
| `propertyCount` | integer | - | **新增** 属性数量 |
| `isMonoBehaviour` | boolean | - | **新增** 是否继承自 MonoBehaviour |
| `isScriptableObject` | boolean | - | **新增** 是否继承自 ScriptableObject |
| `isSerializable` | boolean | - | **新增** Unity 是否可序列化 |
| `scriptRef` | object | - | MonoScript 资产引用 |

**scriptRef 对象字段**:
- `collectionId`: 集合 ID（来自 MonoScript 所在集合）
- `pathId`: PathID（来自 MonoScript 的 PathID）
- `scriptGuid`: 脚本 GUID（来自 MonoScript GUID）

**优化点**（2025-01-20）:
1. ✅ 修改 `pk` 格式 - 使用 `::` 分隔符避免与类型名冲突
2. ✅ 添加嵌套类型支持 - `isNested`, `declaringType`（来自 `TypeDefinition.IsNested`, `DeclaringType`）
3. ✅ 添加成员统计 - `fieldCount`, `methodCount`, `propertyCount`（来自 `TypeDefinition` 集合）
4. ✅ 添加 Unity 特定标记 - `isMonoBehaviour`, `isScriptableObject`, `isSerializable`
5. ✅ 完善可见性枚举 - 支持 6 种 .NET 可见性级别（包括 `protected internal`, `private protected`）
6. ✅ 添加接口实现列表 - `interfaces`（来自 `TypeDefinition.Interfaces`）
7. ✅ 修改 `assemblyGuid` 格式 - 16 字符（与 `assemblies.pk` 一致）

**代码映射**:

```csharp
// TypeDefinition 核心属性
Module.Assembly.Name   → assemblyName
Namespace              → namespace
Name                   → typeName
FullName               → fullName

// 类型分类
IsClass                → isClass
IsValueType && !IsEnum → isStruct
IsInterface            → isInterface
IsEnum                 → isEnum

// 类型修饰符
IsAbstract             → isAbstract
IsSealed               → isSealed
GenericParameters.Count > 0 → isGeneric
GenericParameters.Count     → genericParameterCount
Attributes             → visibility

// 类型层次
BaseType?.FullName     → baseType
IsNested               → isNested
DeclaringType?.FullName → declaringType
Interfaces             → interfaces

// 成员统计
Fields.Count           → fieldCount
Methods.Count          → methodCount
Properties.Count       → propertyCount

// Unity 特定（需要实现）
IsSubclassOf("MonoBehaviour") → isMonoBehaviour
IsSubclassOf("ScriptableObject") → isScriptableObject
BaseManager.IsValid()  → isSerializable

// MonoScript 关联
MonoScriptExtensions.GetTypeDefinition() → scriptRef
```

**关系**:
- **N:1** 与 `assemblies`: `type_definitions.assemblyGuid` → `assemblies.pk`
- **1:1** 与 `script_metadata`: `type_definitions.scriptRef.scriptGuid` → `script_metadata.scriptGuid`（双向关联）
- **继承关系**: `type_definitions.baseType` → 另一个 `type_definitions.fullName`

**用例**:
- **类型层次分析**: 通过 `baseType` 构建完整的继承树
- **MonoBehaviour 检测**: 使用 `isMonoBehaviour` 识别自定义脚本类型
- **接口实现查询**: 通过 `interfaces` 查找实现特定接口的所有类型
- **嵌套类型导航**: 使用 `isNested` 和 `declaringType` 理解类型组织结构
- **Unity 序列化分析**: 使用 `isSerializable` 识别可被 Unity 序列化的类型
- **成员统计**: 通过 `fieldCount`/`methodCount`/`propertyCount` 评估类型复杂度

**示例输出**:

*MonoBehaviour 类型*:
```json
{
  "domain": "type_definitions",
  "pk": "Assembly-CSharp::Game.Controllers::PlayerController",
  "assemblyGuid": "A1B2C3D4E5F6A7B8",
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
  "interfaces": ["ISerializationCallbackReceiver"],
  "fieldCount": 12,
  "methodCount": 8,
  "propertyCount": 3,
  "isMonoBehaviour": true,
  "isScriptableObject": false,
  "isSerializable": true,
  "scriptRef": {
    "collectionId": "A1B2C3D4",
    "pathId": 123456,
    "scriptGuid": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"
  }
}
```

*嵌套枚举类型*:
```json
{
  "domain": "type_definitions",
  "pk": "Assembly-CSharp::Game.Controllers::PlayerController+State",
  "assemblyGuid": "A1B2C3D4E5F6A7B8",
  "assemblyName": "Assembly-CSharp",
  "namespace": "Game.Controllers",
  "typeName": "State",
  "fullName": "Game.Controllers.PlayerController+State",
  "isClass": false,
  "isStruct": false,
  "isInterface": false,
  "isEnum": true,
  "isAbstract": false,
  "isSealed": true,
  "isGeneric": false,
  "visibility": "public",
  "baseType": "System.Enum",
  "isNested": true,
  "declaringType": "Game.Controllers.PlayerController",
  "fieldCount": 5
}
```

*泛型接口*:
```json
{
  "domain": "type_definitions",
  "pk": "Assembly-CSharp::Game.Utils::IPool`1",
  "assemblyGuid": "A1B2C3D4E5F6A7B8",
  "assemblyName": "Assembly-CSharp",
  "namespace": "Game.Utils",
  "typeName": "IPool`1",
  "fullName": "Game.Utils.IPool`1",
  "isClass": false,
  "isStruct": false,
  "isInterface": true,
  "isEnum": false,
  "isAbstract": true,
  "isSealed": false,
  "isGeneric": true,
  "genericParameterCount": 1,
  "visibility": "public",
  "methodCount": 4
}
```

**待实现**: TypeDefinitionsExporter（TypeDefinition 枚举、Unity 检查集成、MonoScript 关联）

---

### Type Members（✅ Schema 完成，🏗️ 代码 50%）

**Schema**: `type_members.schema.json`
**Model**: `TypeMemberRecord.cs` ✅, `ParameterInfo.cs` ✅
**Exporter**: `TypeMembersExporter.cs` ⏳

**目的**: 导出类型成员（字段、属性、方法）的详细元数据，支持文档生成、代码分析和 Unity 序列化检查。

**主键 (pk)**: 复合键格式 `ASSEMBLY::NAMESPACE::TYPENAME::MEMBERNAME`（使用 `::` 避免与成员名中的 `:` 冲突）。

**数据来源**:
- `FieldDefinition`: 字段元数据（AsmResolver.DotNet）
- `PropertyDefinition`: 属性元数据（AsmResolver.DotNet）
- `MethodDefinition`: 方法元数据（AsmResolver.DotNet）
- `AssemblyParser`: 文档提取（DocumentationString, ObsoleteMessage, NativeName）
- `DocumentationHandler`: XML 文档管理
- Unity 序列化规则: 判断成员是否被序列化

**核心字段**:

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `domain` | `"type_members"` | ✅ | 固定为 "type_members" |
| `pk` | string | ✅ | 复合键（`ASSEMBLY::NAMESPACE::TYPENAME::MEMBERNAME`） |
| `assemblyGuid` | string | ✅ | 程序集 GUID（16字符，链接到 `assemblies.pk`） |
| `typeFullName` | string | ✅ | 所属类型完全限定名 |
| `memberName` | string | ✅ | 成员名称 |
| `memberKind` | string | ✅ | 成员类型（`field`/`property`/`method`/`event`/`constructor`/`nestedType`） |
| `memberType` | string | ✅ | 成员类型（字段类型、属性类型或方法返回类型） |
| `visibility` | string | ✅ | 可见性（6 种 .NET 级别） |
| `isStatic` | boolean | ✅ | 是否为静态成员 |
| `serialized` | boolean | ✅ | Unity 是否序列化此成员 |
| `isVirtual` | boolean | - | 是否为虚成员（方法/属性） |
| `isOverride` | boolean | - | 是否重写基类成员 |
| `isSealed` | boolean | - | 是否为密封成员 |
| `attributes` | array | - | 应用的 C# 特性 |
| **documentationString** | string | - | **新增** XML 文档摘要 |
| **obsoleteMessage** | string | - | **新增** Obsolete 特性消息 |
| **nativeName** | string | - | **新增** Unity 原生名称 |
| **isCompilerGenerated** | boolean | - | **新增** 是否由编译器生成 |
| **hasGetter** | boolean | - | **新增** 属性是否有 getter（属性） |
| **hasSetter** | boolean | - | **新增** 属性是否有 setter（属性） |
| **hasParameters** | boolean | - | **新增** 属性是否有参数（索引器） |
| **isConst** | boolean | - | **新增** 字段是否为常量（字段） |
| **isReadOnly** | boolean | - | **新增** 字段是否为只读（字段） |
| **constantValue** | mixed | - | **新增** 常量值（const 字段） |
| **parameterCount** | integer | - | **新增** 方法参数数量（方法/构造函数） |
| **parameters** | array | - | **新增** 方法参数详情（方法/构造函数） |
| **serializeField** | boolean | - | **新增** 是否有 [SerializeField] 特性 |
| **hideInInspector** | boolean | - | **新增** 是否有 [HideInInspector] 特性 |
| **isAbstract** | boolean | - | **新增** 成员是否为抽象（方法/属性） |
| **isGeneric** | boolean | - | **新增** 方法/类型是否为泛型 |
| **genericParameterCount** | integer | - | **新增** 泛型参数数量（泛型方法） |

**parameters 数组字段**:
- `name`: 参数名称
- `type`: 参数类型（完全限定名）
- `isOptional`: 参数是否可选
- `defaultValue`: 可选参数的默认值

**优化点**（2025-01-20）:
1. ✅ 修改 `pk` 格式 - 使用 `::` 分隔符避免冲突
2. ✅ 添加 `memberKind` 枚举值 - 支持 `nestedType`（嵌套类型）
3. ✅ 完善 `visibility` 枚举 - 支持 6 种 .NET 可见性级别
4. ✅ 添加文档字段 - `documentationString`, `obsoleteMessage`, `nativeName`
5. ✅ 添加编译器生成标记 - `isCompilerGenerated`（过滤自动生成成员）
6. ✅ 添加属性特定字段 - `hasGetter`, `hasSetter`, `hasParameters`
7. ✅ 添加字段特定字段 - `isConst`, `isReadOnly`, `constantValue`
8. ✅ 添加方法特定字段 - `parameterCount`, `parameters` 数组
9. ✅ 添加 Unity 特定字段 - `serializeField`, `hideInInspector`
10. ✅ 添加泛型支持 - `isGeneric`, `genericParameterCount`
11. ✅ 添加抽象标记 - `isAbstract`

**代码映射**:

```csharp
// 成员基本信息
FieldDefinition.Name / PropertyDefinition.Name / MethodDefinition.Name → memberName
FieldDefinition.FieldType.FullName / PropertyDefinition.PropertyType.FullName → memberType
MethodDefinition.ReturnType.FullName → memberType (方法)

// 可见性和修饰符
FieldDefinition.IsStatic / MethodDefinition.IsStatic → isStatic
MethodDefinition.IsVirtual → isVirtual
MethodDefinition.IsReuseSlot && IsVirtual → isOverride
MethodDefinition.IsFinal → isSealed
MethodDefinition.IsAbstract → isAbstract

// 文档信息（新增）
AssemblyParser.DocumentationString → documentationString
AssemblyParser.ObsoleteMessage → obsoleteMessage
AssemblyParser.NativeName → nativeName
MemberDefinition.IsCompilerGenerated() → isCompilerGenerated

// 属性特定（新增）
PropertyDefinition.GetMethod != null → hasGetter
PropertyDefinition.SetMethod != null → hasSetter
PropertyDefinition.HasParameters() → hasParameters

// 字段特定（新增）
FieldDefinition.IsLiteral → isConst
FieldDefinition.IsInitOnly → isReadOnly
FieldDefinition.Constant → constantValue

// 方法特定（新增）
MethodDefinition.Parameters.Count → parameterCount
MethodDefinition.Parameters → parameters
MethodDefinition.HasGenericParameters → isGeneric
MethodDefinition.GenericParameters.Count → genericParameterCount

// Unity 特性（新增）
CustomAttributes["SerializeField"] → serializeField
CustomAttributes["HideInInspector"] → hideInInspector

// Unity 序列化规则
BaseManager.IsValid() + visibility checks → serialized
```

**关系**:
- **N:1** 与 `type_definitions`: `type_members.typeFullName` → `type_definitions.fullName`
- **N:1** 与 `assemblies`: `type_members.assemblyGuid` → `assemblies.pk`

**用例**:
- **文档生成**: 使用 `documentationString` 生成 API 文档
- **过时 API 检测**: 通过 `obsoleteMessage` 识别已废弃的成员
- **Unity 序列化分析**: 使用 `serialized`/`serializeField` 分析序列化数据
- **编译器生成过滤**: 使用 `isCompilerGenerated` 排除自动生成的成员
- **属性访问模式**: 通过 `hasGetter`/`hasSetter` 分析属性设计
- **常量提取**: 使用 `isConst`/`constantValue` 提取枚举值和常量
- **方法签名分析**: 通过 `parameters` 分析方法调用模式
- **Inspector 可见性**: 使用 `hideInInspector` 识别 Unity Inspector 行为

**示例输出**:

*序列化字段*:
```json
{
  "domain": "type_members",
  "pk": "Assembly-CSharp::Game.Controllers::PlayerController::currentHealth",
  "assemblyGuid": "A1B2C3D4E5F6A7B8",
  "typeFullName": "Game.Controllers.PlayerController",
  "memberName": "currentHealth",
  "memberKind": "field",
  "memberType": "System.Single",
  "visibility": "private",
  "isStatic": false,
  "serialized": true,
  "serializeField": true,
  "documentationString": "Player's current health points"
}
```

*公共属性*:
```json
{
  "domain": "type_members",
  "pk": "Assembly-CSharp::Game.Controllers::PlayerController::Health",
  "assemblyGuid": "A1B2C3D4E5F6A7B8",
  "typeFullName": "Game.Controllers.PlayerController",
  "memberName": "Health",
  "memberKind": "property",
  "memberType": "System.Single",
  "visibility": "public",
  "isStatic": false,
  "serialized": false,
  "hasGetter": true,
  "hasSetter": true,
  "documentationString": "Gets or sets player health (0-100)"
}
```

*虚方法*:
```json
{
  "domain": "type_members",
  "pk": "Assembly-CSharp::Game.Controllers::PlayerController::TakeDamage",
  "assemblyGuid": "A1B2C3D4E5F6A7B8",
  "typeFullName": "Game.Controllers.PlayerController",
  "memberName": "TakeDamage",
  "memberKind": "method",
  "memberType": "System.Void",
  "visibility": "public",
  "isStatic": false,
  "serialized": false,
  "isVirtual": true,
  "isOverride": false,
  "parameterCount": 2,
  "parameters": [
    {
      "name": "amount",
      "type": "System.Single"
    },
    {
      "name": "source",
      "type": "UnityEngine.GameObject",
      "isOptional": true,
      "defaultValue": null
    }
  ],
  "documentationString": "Applies damage to the player"
}
```

*常量字段*:
```json
{
  "domain": "type_members",
  "pk": "Assembly-CSharp::Game.Constants::GameConfig::MaxPlayers",
  "assemblyGuid": "A1B2C3D4E5F6A7B8",
  "typeFullName": "Game.Constants.GameConfig",
  "memberName": "MaxPlayers",
  "memberKind": "field",
  "memberType": "System.Int32",
  "visibility": "public",
  "isStatic": true,
  "serialized": false,
  "isConst": true,
  "constantValue": 4,
  "documentationString": "Maximum number of players in a game"
}
```

*过时方法*:
```json
{
  "domain": "type_members",
  "pk": "Assembly-CSharp::Game.Controllers::PlayerController::OldMove",
  "assemblyGuid": "A1B2C3D4E5F6A7B8",
  "typeFullName": "Game.Controllers.PlayerController",
  "memberName": "OldMove",
  "memberKind": "method",
  "memberType": "System.Void",
  "visibility": "public",
  "isStatic": false,
  "serialized": false,
  "obsoleteMessage": "Use Move() instead. This method will be removed in version 2.0",
  "parameterCount": 1,
  "parameters": [
    {
      "name": "direction",
      "type": "UnityEngine.Vector3"
    }
  ]
}
```

*编译器生成属性*:
```json
{
  "domain": "type_members",
  "pk": "Assembly-CSharp::Game.Data::PlayerData::get_Name",
  "assemblyGuid": "A1B2C3D4E5F6A7B8",
  "typeFullName": "Game.Data.PlayerData",
  "memberName": "get_Name",
  "memberKind": "method",
  "memberType": "System.String",
  "visibility": "public",
  "isStatic": false,
  "serialized": false,
  "isCompilerGenerated": true
}
```

**待实现**: TypeMembersExporter（成员枚举、文档提取、Unity 序列化检查）

---

## 🚀 实现状态

### ✅ 阶段 1: Bundle 层次支持（已完成）

**目标**: 完整的 Bundle 层次导航

**任务**:
1. ✅ **使用现有的 BundleMetadataExporter**
   - 发现并使用 `Exporters/Metadata/BundleMetadataExporter.cs`（346 lines，已存在）
   - 包含完整功能：层次遍历、祖先路径、失败文件收集、场景信息
   - 字段: `childBundlePks`, `ancestorPath`, `failedFiles`, `scenes` 全部实现
   
2. ✅ **Asset 的 `hierarchy` 字段已实现**
   - 修改 `Models/Records/AssetRecord.cs` 添加 `Hierarchy` 属性
   - 更新 `Helpers/ExportHelper.ComputeBundlePk()` 使用完整路径
   - 在 `AssetFactsExporter.BuildHierarchyPath()` 中填充数据
   - **已测试**: GRIS 游戏（201,543 assets）验证通过 ✅
   
3. ⏳ **单元测试待补充**
   - 测试 Bundle 层次遍历
   - 测试祖先路径计算
   - 测试失败文件记录

**完成日期**: 2025-11-11  
**实际修改**: 仅 16 行代码（重用现有组件）

---

### ✅ 阶段 2: 脚本分析支持（已完成）

**目标**: 基础脚本元数据导出

**任务**:
1. ✅ **使用现有的 ScriptRecordExporter**
   - 发现并使用 `Exporters/Records/ScriptRecordExporter.cs`（287 lines，已存在）
   - 包含完整功能：MonoScript 元数据、Assembly 集成、泛型检测、并行处理
   - **已测试**: GRIS 游戏（2,458 个 MonoScript）验证通过 ✅
   
2. ⏳ **（可选）ScriptSourcesExporter**
   - 关联反编译源码文件
   - 计算源码哈希和行数
   - **状态**: 待实现（低优先级）

**完成日期**: 2025-11-11  
**注意**: ScriptRecord.cs 和 ScriptRecordExporter 已存在并完整实现

---

### 阶段 3: 测试覆盖增强（进行中）

**目标**: 80%+ 代码覆盖率

**任务**:
1. ⏳ **Collections 测试**
   - `collectionType` 检测
   - `originalUnityVersion` 比较
   - 依赖解析
   
2. ⏳ **Scenes 测试**
   - `primaryCollectionId` 逻辑
   - `collectionDetails` 生成
   - 可选字段处理
   
3. ⏳ **Bundle 和 Script 测试**
   - Bundle 层次遍历测试
   - Script 元数据提取测试
   - 集成测试
   
4. ⏳ **集成测试**
   - 跨 Bundle 场景
   - 复杂依赖图
   - 大型项目性能

**预计时间**: 持续进行

---

### 📊 总体进度

| 阶段 | 核心功能 | 测试 | 文档 | 状态 |
|------|----------|------|------|------|
| Bundle 层次 | ✅ 100% | ⏳ 0% | ✅ 100% | ✅ 完成 |
| Script 元数据 | ✅ 100% | ⏳ 0% | ✅ 100% | ✅ 完成 |
| 测试覆盖 | - | ⏳ 30% | ✅ 100% | 🔄 进行中 |

**重要发现**: 原代码库已包含完善的 BundleMetadataExporter 和 ScriptRecordExporter，避免了重复开发。实际工作量远小于预期（16 行 vs 预期数百行）。

**相关文档**:
- `IMPLEMENTATION_COMPLETE_2025-11-11.md` - 详细实现报告
- `TEST_RESULTS_GRIS.md` - GRIS 测试结果
- `FINAL_SUMMARY.md` - 项目总结
- `HIERARCHY_FIELD_REFERENCE.md` - Hierarchy 字段使用指南

---

## 📚 相关文档

- `../DESIGN_DECISIONS.md` - 设计决策与限制完整文档
- `../SCHEMA_STRUCTURE.md` - Schema 架构总览
- `../core.schema.json` - 核心类型定义
- `../../README.md` - AssetDumper 项目总览

---

## 🔄 变更日志

### 2025-11-11: 移除 scripts.schema.json（重复清理）
- ✅ 移除 `scripts.schema.json`（与 `script_metadata.schema.json` 重复）
- ✅ 将代码中的 domain 从 `"scripts"` 改为 `"script_metadata"`
- ✅ 更新 `ScriptRecord` Model 的 domain 默认值
- ✅ 更新 `ScriptRecordExporter` 中的 domain、tableId 和 schemaPath
- ✅ 更新所有相关文档，移除对 scripts.schema.json 的引用
- 🎯 **理由**: `script_metadata.schema.json` 是更完整和优化的版本，包含所有必要字段

### 2025-11-11: script_sources.schema.json 优化
- ✅ 添加 `decompilationStatus` 必需字段（反编译状态追踪）
- ✅ 添加 `isEmpty` 字段（标识 EmptyScript 占位符）
- ✅ 添加 `errorMessage` 字段（记录反编译失败原因）
- ✅ 添加 `isPresent` 和 `isGeneric` 字段（类型信息）
- ✅ 更新语言枚举：`JavaScript` → `UnityScript`
- ✅ 标记 AST 字段为未来功能
- ✅ 更新 `ScriptSourceRecord.cs` 模型添加新字段
- ✅ 增强字段描述（引用 AssetRipper 代码方法）
- ✅ 添加详细使用场景和代码映射文档

### 2025-11-11: script_metadata.schema.json 优化
- ✅ 添加 `isPresent` 必需字段（检测 Missing Script）
- ✅ 添加 `assemblyNameRaw` 字段（保留原始程序集名）
- ✅ 添加 `isGeneric` 和 `genericParameterCount` 字段（泛型支持）
- ✅ 更新 `propertiesHash` 正则表达式（支持 8 或 32 字符）
- ✅ 增强所有字段描述（引用具体代码方法）
- ✅ 创建 `SCRIPT_METADATA_OPTIMIZATION.md` 详细文档
- ✅ **实现代码**:
  - 更新 `ScriptRecord.cs` 模型添加新字段
  - 更新 `ScriptRecordExporter.cs` 导出逻辑
  - 添加 `TryAssignTypeInfo()` 方法处理 `isPresent`、泛型检测
  - 添加 `assemblyNameRaw` 比对逻辑
  - 通过完整构建验证（0 错误）

### 2025-11-11: Collections 和 Scenes 优化
- ✅ Collections: 添加 `collectionType`, `originalUnityVersion`, 更新文档
- ✅ Scenes: 添加 `primaryCollectionId`, `collectionDetails`, 可选层次字段
- ✅ 实现所有相关代码和文档
- ✅ 通过完整构建验证（0 错误）

### 2025-11-10: 初始 Facts Schema
- ✅ 创建所有 Facts Schema 定义
- ✅ 实现 Collections, Assets, Types, Scenes 基础导出
- ✅ 添加 domain 字段到所有 Schema

---

**文档版本**: 2.1  
**最后更新**: 2025-11-11  
**维护者**: AssetRipper 开发团队
