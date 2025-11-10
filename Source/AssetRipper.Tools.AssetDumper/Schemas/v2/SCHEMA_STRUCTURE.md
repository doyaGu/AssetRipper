# AssetDump v2 Schema Layout

## 目录结构
```
Schemas/
└── v2/
    ├── core.schema.json
    ├── facts/
    │   ├── assets.schema.json
    │   ├── bundles.schema.json
    │   ├── collections.schema.json
    │   ├── scenes.schema.json
    │   ├── scripts.schema.json
    │   ├── types.schema.json
    │   └── README.md
    ├── relations/
    │   ├── asset_dependencies.schema.json
    │   ├── collection_dependencies.schema.json
    │   ├── bundle_hierarchy.schema.json
    │   └── README.md
    ├── indexes/
    │   ├── by_class.schema.json
    │   └── README.md
    ├── metrics/
    │   └── scene_stats.schema.json
    └── README.md
```

- **core.schema.json**：声明公共 `$defs` 与 `$anchor`，供各业务 schema 复用，例如 `CollectionID`、`AssetPK`、`UnityGuid`、`BundleRef`、`SceneRef`、`HierarchyPath` 等。
- **facts/**：存放事实层对象 schema，每张事实表对应一个文件（集合、资产、脚本、场景、引用边等）；可在 README 中说明字段含义与版本差异。
- **relations/**：存放关系边的 schema，包括资产依赖、集合依赖、Bundle 层次结构等。
- **indexes/**：定义可再生索引文件结构。
- **metrics/**：定义派生统计的数据结构。

## 核心定义 (core.schema.json)

### 新增类型定义

#### BundleRef
引用 Bundle 节点的结构，使用稳定的 PK：
```json
{
  "bundlePk": "A1B2C3D4",
  "bundleName": "level0"
}
```

#### SceneRef
引用 Scene 的结构，使用 Unity GUID：
```json
{
  "sceneGuid": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6",
  "sceneName": "MainScene",
  "scenePath": "Assets/Scenes/MainScene.unity"
}
```

#### HierarchyPath
完整的层次结构路径，从根到目标实体：
```json
{
  "bundlePath": ["00000000", "A1B2C3D4"],
  "bundleNames": ["GameBundle", "level0"],
  "depth": 1
}
```

## 四层层次结构

AssetDumper v2 Schema 现在完整支持 AssetRipper 的四层层次结构：

```
GameBundle (根容器)
  └─ Bundle (子容器, 可嵌套)
      └─ AssetCollection (资产集合)
          └─ IUnityObjectBase (资产)
```

### 层次关系表达

1. **Bundle 层次** (bundles.schema.json):
   - `childBundlePks`: 直接子 Bundle 的稳定 PK 列表
   - `ancestorPath`: 从根到父级的祖先 Bundle PK 列表
   - `bundleIndex`: 在父 Bundle 的子列表中的索引

2. **Collection 归属** (collections.schema.json):
   - `bundle`: 所属的父 Bundle 引用
   - `scene`: 可选的 Scene 引用（如果是场景集合）
   - `dependencies`: 依赖的 Collection ID 列表
   - `collectionIndex`: 在父 Bundle 的集合列表中的索引

3. **Asset 路径** (assets.schema.json):
   - `hierarchy`: 从根 Bundle 到该资产的完整路径
   - `collectionName`, `bundleName`, `sceneName`: 冗余字段便于可读性

4. **Scene 组成** (scenes.schema.json):
   - `primaryCollectionId`: 主要（第一个）Collection
   - `bundle`: 包含主 Collection 的 Bundle
   - `collectionDetails`: 组成该 Scene 的所有 Collection 详情

## 关系层 (relations/)

### collection_dependencies.schema.json
记录集合级别的依赖关系（对应 `AssetCollection.Dependencies`）：
```json
{
  "sourceCollection": "SHAREDASSETS0",
  "dependencyIndex": 1,
  "targetCollection": "BUILTIN-EXTRA",
  "fileIdentifier": {
    "guid": "0000000000000000f000000000000000",
    "type": 0,
    "pathName": "Resources/unity_builtin_extra"
  }
}
```

### bundle_hierarchy.schema.json
记录 Bundle 父子关系：
```json
{
  "parentPk": "A1B2C3D4",
  "childPk": "E5F6G7H8",
  "childIndex": 0,
  "childName": "level0_textures"
}
```

## 查询模式支持

更新后的 schema 支持以下核心访问模式：

1. **Asset → Collection → Bundle → Scene**
   - 通过 Asset 的 `hierarchy` 和 `sceneName` 字段
   - 或通过 Collection 的 `bundle` 和 `scene` 引用

2. **Bundle → 所有子 Bundle（递归）**
   - 通过 Bundle 的 `childBundlePks` 字段递归遍历

3. **Bundle → 所有 Collection（直接）**
   - 通过 Bundle 的 `collectionIds` 字段

4. **Scene → 所有 Collection（组成）**
   - 通过 Scene 的 `collectionIds` 和 `collectionDetails` 字段

5. **Collection → 依赖 Collection 列表**
   - 通过 Collection 的 `dependencies` 字段
   - 或查询 `collection_dependencies` 关系表

- **core.schema.json**：声明公共 `$defs` 与 `$anchor`，供各业务 schema 复用，例如 `CollectionID`、`AssetPK`、`UnityGuid` 等。
- **facts/**：存放事实层对象 schema，每张事实表对应一个文件（集合、资产、脚本、场景、引用边等）；可在 README 中说明字段含义与版本差异。
- **indexes/**：定义可再生索引文件结构。
- **metrics/**：定义派生统计的数据结构。

## `$id` 约定
- 统一前缀：`https://schemas.assetripper.dev/assetdump/v2/`
- 子目录命名：
  - `core.schema.json` → `https://schemas.assetripper.dev/assetdump/v2/core.schema.json`
  - `facts/assets.schema.json` → `https://schemas.assetripper.dev/assetdump/v2/facts/assets.schema.json`
  - `relations/asset_dependencies.schema.json` → `https://schemas.assetripper.dev/assetdump/v2/relations/asset_dependencies.schema.json`
- `$ref` 必须使用完整 URI + 片段，例如：
  ```json
  {
    "pk": { "$ref": "https://schemas.assetripper.dev/assetdump/v2/core.schema.json#AssetPK" }
  }
  ```

## 版本与兼容策略
- 默认方言：`https://json-schema.org/draft/2020-12/schema`
- **重要提示**：v2 架构改进包含破坏性变更，不保证向后兼容。现有基于旧版 v2 schema 的工具和数据需要相应更新。
- 新增字段：`BundleRef`、`SceneRef`、`HierarchyPath` 以及 Bundle、Collection、Asset、Scene 中的层次关系字段
- 新增关系表：`collection_dependencies` 和 `bundle_hierarchy`
- 若需引入下一版 schema，应在 `Schemas/v3/` 内新建一套目录，以避免破坏现有消费端。

## 破坏性变更清单

### bundles.schema.json
- **新增必需字段**: `childBundlePks`
- **新增可选字段**: `childBundleNames`, `bundleIndex`, `ancestorPath`

### collections.schema.json
- **新增必需字段**: `bundle`, `dependencies`, `assetCount`
- **新增可选字段**: `scene`, `collectionIndex`, `dependencyIndices`

### assets.schema.json
- **新增可选字段**: `hierarchy`, `collectionName`, `bundleName`, `sceneName`

### scenes.schema.json
- **新增可选字段**: `primaryCollectionId`, `bundle`, `collectionDetails`
- **SceneCollectionDescriptor 更新**: 新增 `bundlePk`, `assetCount`

### 新增 schema 文件
- `relations/collection_dependencies.schema.json`
- `relations/bundle_hierarchy.schema.json`

## 下一步
1. ✅ 完成 `core.schema.json` 更新，新增 `BundleRef`、`SceneRef`、`HierarchyPath` 定义
2. ✅ 更新 `facts/bundles.schema.json`，增强 Bundle 层次结构表达
3. ✅ 更新 `facts/collections.schema.json`，添加 Bundle/Scene 引用和依赖列表
4. ✅ 更新 `facts/assets.schema.json`，添加层次路径和冗余名称字段
5. ✅ 更新 `facts/scenes.schema.json`，增强 Scene 组成表达
6. ✅ 创建 `relations/collection_dependencies.schema.json`
7. ✅ 创建 `relations/bundle_hierarchy.schema.json`
8. ⏳ 更新 C# Model 类以匹配新 schema
9. ⏳ 实现 Exporter 代码生成新字段
10. ⏳ 集成到导出管线并测试
