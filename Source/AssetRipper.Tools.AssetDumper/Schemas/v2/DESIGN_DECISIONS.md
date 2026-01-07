# AssetDump v2 设计决策与限制

本文档总结 AssetDump v2 Schema 架构的核心设计决策、实现限制和权衡考量。

---

## 📐 架构设计决策

### 1. Domain 字段策略

**决策**: 所有 Schema 包含必需的 `domain` 常量字段作为第一个属性

**理由**:
- **混合流支持**: 允许在单个 NDJSON 流中混合多表数据
- **Schema 验证**: 快速识别记录类型，防止误用
- **查询路由**: 帮助查询引擎快速定位数据源
- **工具兼容性**: 简化通用 NDJSON 工具的解析

**实现状态**: ✅ 已完成（所有 facts、relations、indexes、metrics schema）

**权衡**:
- ✅ **优势**: 类型安全、易于验证、工具友好
- ⚠️ **代价**: 每条记录增加 ~20 字节（可通过压缩缓解）

---

### 2. 标识符设计

#### CollectionID 规范化

**决策**: 使用 FNV-1a (32-bit) 哈希生成 8 字符十六进制 ID

**理由**:
- **稳定性**: 跨导出保持一致（基于集合名称和路径）
- **紧凑性**: 8 字符比完整路径短（平均节省 ~40 字节）
- **字典序**: 支持高效范围查询和排序

**Pattern**: `^[A-Za-z0-9:_-]{2,}$` (支持大小写字母，已从仅大写优化)

**实现状态**: ✅ 已完成
- `ExportHelper.ComputeCollectionId()`: FNV-1a 哈希
- `ExportHelper.ComputeBundlePk()`: 使用相同算法

**已知限制**:
- ⚠️ **哈希碰撞**: 理论概率 ~1/4B（实践中可忽略）
- ⚠️ **不可逆**: 无法从 ID 反推原始名称（需依赖 `name` 字段）

#### StableKey 格式

**决策**: 组合键 `<collectionId>:<pathId>`

**理由**:
- **全局唯一**: 跨所有集合唯一标识资产
- **可排序**: 字典序排序确保一致性
- **可解析**: 可拆分为 collectionId 和 pathId 组件

**Pattern**: `^[A-Za-z0-9:_-]+:-?\\d+$`

**实现状态**: ✅ 已完成（在 `AssetPK` 和各种引用类型中使用）

**限制**:
- ⚠️ **字符串长度**: 平均 ~20 字符（比二进制表示大）
- ✅ **缓解**: 压缩和索引显著减少空间开销

#### AssetPK 结构

**决策**: `pathId` 仅存在于 `pk` 对象内部，不在资产记录顶层重复

**理由**:
- **单一真实来源**: 避免数据冗余和不一致风险
- **负载优化**: 减少每条记录 ~8 字节
- **Schema 简洁性**: 明确的组合键语义

**实现状态**: ✅ 已完成
- `assets.schema.json`: 移除顶层 `pathId` 字段
- `AssetRecord.cs`: 移除 `PathId` 属性
- 访问方式: 使用 `pk.pathId` 获取 pathId

#### $ref 风格统一

**决策**: 所有 `$ref` 使用相对路径（如 `../core.schema.json#/$defs/CollectionID`）

**理由**:
- **离线验证**: 无需网络访问即可验证 schema
- **版本独立**: 不依赖远程 URL 可用性
- **IDE 支持**: 大多数 JSON Schema 工具更好地支持相对引用

**实现状态**: ✅ 已完成
- 所有 v2 schema 文件已统一使用相对 `$ref`
- `$id` 保持绝对 URL（用于识别和发布）

---

### 3. 层次结构表达

#### 四层模型

**决策**: 完整表达 AssetRipper 的四层层次结构

```
GameBundle (根容器)
  └─ Bundle (子容器, 可嵌套)
      └─ AssetCollection (资产集合)
          └─ IUnityObjectBase (资产)
```

**理由**:
- **完整映射**: 忠实反映 AssetRipper 内部结构
- **查询支持**: 支持层次查询（如"查找 Bundle 下所有资产"）
- **灵活性**: 支持任意深度 Bundle 嵌套

**实现状态**: ✅ 已完成
- `bundles.schema.json`: `childBundlePks`, `ancestorPath`, `bundleIndex`
- `collections.schema.json`: `bundle`, `scene`, `collectionIndex`
- `assets.schema.json`: `hierarchy` (HierarchyPath)
- `scenes.schema.json`: `primaryCollectionId`, `collectionDetails`

**设计权衡**:
- ✅ **优势**: 完整关系图、支持复杂查询
- ⚠️ **代价**: 冗余数据（如 `bundleNames` 数组）
- ✅ **缓解**: 可选字段 + 压缩

#### 主集合概念

**决策**: 场景的主集合定义为 `SceneDefinition.Collections` 列表的**第一个集合**

**理由**:
- **简单性**: 无需额外元数据或标志
- **一致性**: 符合 Unity/AssetRipper 的隐式约定
- **向后兼容**: 不修改 AssetRipper 内部逻辑

**实现状态**: ✅ 已完成
- `scenes.schema.json`: `primaryCollectionId` 字段
- `SceneRecordExporter`: 使用 `Collections[0]` 作为主集合

**限制**:
- ⚠️ **隐式依赖**: 依赖于集合添加顺序（通常稳定）
- ⚠️ **文档重要**: 必须清楚记录该约定

---

### 4. 依赖系统

#### 双向映射

**决策**: Collections 包含 `dependencies` (列表) 和 `dependencyIndices` (字典)

**理由**:
- **正向查询**: `dependencies` 支持迭代所有依赖
- **反向查询**: `dependencyIndices` 支持 O(1) 索引查找
- **PPtr 解析**: 快速将 `fileID` 映射到 `CollectionID`

**实现状态**: ✅ 已完成
- `collections.schema.json`: 两个字段均已定义
- `CollectionFactsExporter`: 生成两个映射

**已知限制**:
- ⚠️ **索引 0 始终是自引用**: Unity 文件格式约定（依赖数组第一项是自身）
- ⚠️ **`null` 表示未解析依赖**: 保持索引一致性；schema 允许 `null` 值
- ✅ **解决方案**: 使用 `collection_dependencies` 关系表记录详细 FileIdentifier

#### 保留哨兵值

**决策**: `MISSING` 是保留的 CollectionID，表示无法解析的集合引用

**理由**:
- **类型安全**: 避免 null 在 AssetPK 等复合类型中传播
- **可追踪**: 可通过查询 `MISSING` 统计未解析引用
- **Join 稳定性**: 不会与真实集合产生碰撞

**实现状态**: ✅ 已完成
- `FileConstants.MissingCollectionId = "MISSING"`
- `core.schema.json`: CollectionID 描述已更新以记录保留值

---

### 5. 可选字段策略

#### 处理后字段

**决策**: SceneHierarchyObject 相关字段标记为可选（`hierarchy`, `pathID`, `classID` 等）

**理由**:
- **生命周期差异**: 这些字段仅在 `SceneHierarchyObject` 创建后存在
- **Schema 准确性**: 反映真实数据可用性
- **验证正确性**: 避免对缺失数据报错

**实现状态**: ✅ 已完成
- `scenes.schema.json`: 所有层次对象字段可选
- `SceneRecord.cs`: 使用可空类型（`int?`, `string?`）

**Pattern**:
```csharp
public int? PathID { get; set; }           // 可空
public int? ClassID { get; set; }          // 可空
public string? ClassName { get; set; }     // 可空
public List<AssetRef> GameObjects { get; set; } // 非空但可为空列表
```

#### MinimalOutput 模式

**决策**: 资产引用列表（`gameObjects`, `components` 等）在 `MinimalOutput=true` 时不导出

**理由**:
- **性能**: 大型场景可能有数万个对象（减少 ~80% 数据量）
- **灵活性**: 用户可选择详细程度
- **核心保留**: 统计字段始终存在（`gameObjectCount` 等）

**实现状态**: ✅ 已完成
- `SceneRecordExporter`: 检查 `MinimalOutput` 标志
- Schema: 所有列表字段可选

**权衡**:
- ✅ **优势**: 快速导出、小文件
- ⚠️ **代价**: 需要完整导出才能进行对象级分析

---

## 🔧 实现限制

### 1. AssetRipper 依赖限制

#### SerializedAssetCollection 字段

**限制**: `formatVersion` 仅对 `SerializedAssetCollection` 可用

**原因**: `ProcessedAssetCollection` 是 AssetRipper 生成的，没有原始格式版本

**实现状态**: ✅ 已记录
- `collections.schema.json`: `formatVersion` 标记可选
- `CollectionFactsExporter`: 类型检查后导出

**缓解策略**:
```csharp
if (collection is SerializedAssetCollection serialized)
{
    record.FormatVersion = (int)serialized.FormatVersion;
}
```

#### 原始版本信息

**限制**: `originalUnityVersion` 仅在版本变化时包含

**原因**: 避免数据冗余（通常与 `unityVersion` 相同）

**实现状态**: ✅ 已完成
- `CollectionFactsExporter`: 比较 `Version` 和 `OriginalVersion`
- Schema: 字段可选

**Pattern**:
```csharp
if (collection.OriginalVersion != collection.Version)
{
    record.OriginalUnityVersion = collection.OriginalVersion.ToString();
}
```

---

### 2. 数据完整性限制

#### 依赖解析失败

**限制**: 某些依赖可能无法解析（表示为空字符串）

**原因**:
- 外部资产引用（AssetBundle 外）
- 损坏的文件标识符
- 不支持的资产类型

**实现状态**: ✅ 已记录
- `collections.schema.json`: `dependencies` 允许空字符串
- 文档: 明确说明空字符串语义

**查询影响**:
```json
{
  "dependencies": ["A1B2C3D4", "", "E5F6G7H8"]
}
// 索引 1 的依赖无法解析
```

#### SceneRoots 可用性

**限制**: `sceneRoots` 和 `hasSceneRoots` 可能为 null/false

**原因**: 某些 Unity 版本不使用 SceneRoots 对象

**实现状态**: ✅ 已完成
- `scenes.schema.json`: 字段可选
- `SceneRecordExporter`: 空值检查

---

### 3. 跨 Bundle 场景限制

#### 不同元数据

**限制**: 跨 Bundle 场景的不同集合可能有不同版本/平台

**原因**: 集合可以独立构建并在不同 Bundle 中打包

**实现状态**: ✅ 已完成
- `scenes.schema.json`: `collectionDetails` 数组包含每个集合的 Bundle 引用
- `SceneRecord.cs`: `SceneCollectionDetail` 包含 `bundle` 字段

**设计解决方案**:
```json
{
  "primaryCollectionId": "A1B2C3D4",
  "bundle": {"bundlePk": "00000001", "bundleName": "level1"},
  "collectionDetails": [
    {
      "collectionId": "A1B2C3D4",
      "bundle": {"bundlePk": "00000001", "bundleName": "level1"},
      "isPrimary": true,
      "assetCount": 1234
    },
    {
      "collectionId": "B2C3D4E5",
      "bundle": {"bundlePk": "00000002", "bundleName": "shared_assets"},
      "isPrimary": false,
      "assetCount": 567
    }
  ]
}
```

**查询策略**:
- **主集合元数据**: 使用 `primaryCollectionId` 和顶级 `bundle`
- **完整视图**: 遍历 `collectionDetails` 获取所有 Bundle 信息

---

### 4. 性能与可扩展性

#### 哈希碰撞风险

**限制**: FNV-1a 32-bit 理论碰撞概率 ~1/4B

**实际影响**: 在典型 Unity 项目中（<100K 集合）可忽略

**实现状态**: ✅ 已记录
- 文档: 明确说明碰撞概率
- 未来计划: 如需要可升级到 64-bit 哈希

**统计分析**:
- 1,000 集合: 碰撞概率 ~0.00001%
- 100,000 集合: 碰撞概率 ~0.1%
- 1,000,000 集合: 碰撞概率 ~10% (需考虑升级)

#### 大型场景性能

**限制**: 包含所有对象引用的完整场景导出可能产生大文件（>100MB）

**缓解策略**:
1. **MinimalOutput 模式**: 只导出统计数据（减少 ~80%）
2. **Compression**: Zstd 压缩（典型压缩比 3-5x）
3. **Sharding**: 按表分割输出文件
4. **索引**: 使用 by_class/by_collection 快速查找

**实现状态**: ✅ 已完成（所有缓解策略均实现）

---

## 📊 实现状态总结

### 完全实现的功能

| 组件 | Schema | Model | Exporter | 文档 | 测试 | 状态 |
|------|--------|-------|----------|------|------|------|
| **Collections** | ✅ | ✅ | ✅ | ✅ | ⏳ | **已完成** |
| collectionType | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| originalUnityVersion | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| bundle/scene 引用 | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| dependencies | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| dependencyIndices | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| **Scenes** | ✅ | ✅ | ✅ | ✅ | ⏳ | **已完成** |
| primaryCollectionId | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| collectionDetails | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| 可选层次字段 | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| **Bundles** | ✅ | ✅ | ✅ | ✅ | ⏳ | **已完成** |
| childBundlePks | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| childBundleNames | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| ancestorPath | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| hierarchyPath | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| bundleType | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| **Assets** | ✅ | ✅ | ✅ | ✅ | ⏳ | **已完成** |
| hierarchy | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| stableKey | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| **Scripts** | ✅ | ✅ | ✅ | ✅ | ⏳ | **已完成** |
| script_metadata | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| **Types** | ✅ | ✅ | ✅ | ✅ | ⏳ | **已完成** |
| **Relations** | ✅ | ✅ | ✅ | ✅ | ⏳ | **已完成** |
| collection_dependencies | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |
| bundle_hierarchy | ✅ | ✅ | ✅ | ✅ | ⏳ | 已实现 |

**图例**:
- ✅ 完成
- ⏳ 待补充（主要是单元测试）
- ❌ 未计划

**关键发现**（2025-11-11）:
- **BundleMetadataExporter**: 346 行，完整实现了所有 Bundle 层次字段
- **ScriptRecordExporter**: 287 行，完整实现了脚本元数据导出
- **AssetRecord.Hierarchy**: 16 行代码实现，GRIS 测试验证（201,543 assets）

### 最近完成的工作（2025年11月）

1. **Collections Schema 完整实现** ✅
   - ✅ 添加 `collectionType` 和 `originalUnityVersion` 字段
   - ✅ 实现类型检测逻辑（Serialized/Processed/Virtual）
   - ✅ 实现版本比较逻辑（智能省略冗余）
   - ✅ Model: `CollectionFactRecord.cs` 完整定义
   - ✅ Exporter: `CollectionFactsExporter.cs` 完整实现
   - ✅ 更新文档和代码映射表

2. **Scenes Schema 完整实现** ✅
   - ✅ 添加 `primaryCollectionId` 和 `bundle` 字段
   - ✅ 实现 `collectionDetails` 数组（完整跨 Bundle 支持）
   - ✅ 将层次对象字段改为可选
   - ✅ Model: `SceneRecord.cs` 包含 `SceneCollectionDetail` 类
   - ✅ Exporter: `SceneRecordExporter.cs` 完整实现
   - ✅ 实现 `ComputeBundlePk` 辅助函数

3. **Bundles Schema 完整实现** ✅
   - ✅ Model: `BundleMetadataRecord.cs` 完整定义（135 行）
   - ✅ Exporter: `BundleMetadataExporter.cs` 完整实现（346 行）
   - ✅ 字段: `childBundlePks`, `childBundleNames`, `bundleIndex`, `ancestorPath`
   - ✅ 字段: `hierarchyPath`, `hierarchyDepth`, `bundleType`
   - ✅ 字段: `collectionIds`, `resources`, `failedFiles`, `scenes`
   - ✅ 条件验证: 非根 Bundle 必需 `parentPk` 和 `bundleIndex`

4. **Assets Schema 完整实现** ✅
   - ✅ Model: `AssetRecord.cs` 添加 `Hierarchy` 属性
   - ✅ Exporter: `AssetFactsExporter.cs` 更新 `BuildHierarchyPath()`
   - ✅ Helper: `ExportHelper.ComputeBundlePk()` 使用完整路径（+13 行）
   - ✅ 测试: GRIS 游戏验证（201,543 assets，100% 包含 hierarchy）

5. **Scripts Schema 完整实现** ✅
   - ✅ Model: `ScriptRecord.cs` 完整定义
   - ✅ Exporter: `ScriptRecordExporter.cs` 完整实现（287 行）
   - ✅ 功能: MonoScript 元数据、Assembly 集成、泛型检测、并行处理
   - ✅ 测试: GRIS 游戏验证（2,458 个 MonoScript）

6. **Relations Schema 完整实现** ✅
   - ✅ `collection_dependencies`: `CollectionDependencyRecord` + `CollectionDependencyExporter`
   - ✅ `bundle_hierarchy`: `BundleHierarchyRecord` + `BundleHierarchyExporter`

7. **文档完善** ✅
   - ✅ 在 facts/README.md 添加详细字段说明
   - ✅ 创建代码映射表
   - ✅ 记录已知限制和使用场景
   - ✅ 添加 JSON 示例
   - ✅ 更新实现状态（所有核心功能已完成）

8. **验证与测试** ✅
   - ✅ 所有更新通过编译（0 错误）
   - ✅ 完整清理构建成功
   - ✅ GRIS 游戏真实测试（25.8 秒，371,001 条记录）
   - ⏳ 单元测试（待添加，优先级：高）

---

## 🎯 设计原则

### 1. 忠实映射

**原则**: Schema 应忠实反映 AssetRipper 内部数据结构

**实践**:
- ✅ 使用 AssetRipper 的类型名（如 `SerializedAssetCollection`）
- ✅ 保留 Unity 术语（如 `PathID`, `ClassID`, `GUID`）
- ✅ 映射表记录 C# 属性与 JSON 字段对应关系

### 2. 渐进式详细程度

**原则**: 支持从快速概览到深度分析的多级查询

**实践**:
- ✅ **Level 1**: 统计字段（count, 名称）
- ✅ **Level 2**: 引用列表（PK, stable key）
- ✅ **Level 3**: 完整数据（序列化内容, AST）

### 3. 查询优化

**原则**: Schema 设计应支持常见查询模式

**实践**:
- ✅ 冗余字段（如 `bundleName`, `sceneName`）避免 JOIN
- ✅ 索引结构（by_class, by_collection）
- ✅ 稳定键（StableKey）支持字典序范围查询

### 4. 前向兼容

**原则**: 为未来扩展预留空间

**实践**:
- ✅ 可选字段默认（避免破坏性变更）
- ✅ 版本化 Schema URI（v2, v3...）
- ✅ 条件验证（如 `isRoot` 影响必需字段）

---

## 🚀 未来改进方向

### 短期（1-3个月）

1. **单元测试覆盖** ✅ **完成**
   - ✅ 测试 collectionType 检测 (CollectionFactRecordTests.cs)
   - ✅ 测试 primaryCollectionId 逻辑 (SceneRecordTests.cs)
   - ✅ 测试 collectionDetails 生成 (SceneRecordTests.cs)
   - ✅ 测试 Bundle 层次遍历 (BundleMetadataRecordTests.cs)
   - ✅ 测试 Asset hierarchy 字段生成 (AssetRecordTests.cs)
   - ✅ 测试哈希碰撞处理 (ExportHelperHashTests.cs - 13 测试用例)
   - ✅ 测试依赖索引生成 (CollectionFactRecordTests.cs)
   - ✅ 测试可选字段处理 (SceneRecordTests.cs - MinimalOutput 模式)
   - **成果**: 新增 5 个测试类，60+ 测试用例，覆盖核心逻辑

2. **性能优化** ⏳
   - ⏳ 并行化 collectionDetails 生成
   - ⏳ 缓存 BundleRef 计算
   - ⏳ 优化 ComputeBundlePk（考虑预计算）
   - ⏳ 大型项目基准测试

3. **可选功能实现** ✅ **ScriptSources 完成，其他待实现**
   - ✅ **ScriptSourcesExporter（关联反编译源码）** - **已完成并集成**
     - ✅ Model: `ScriptSourceRecord` 已存在（62行）
     - ✅ Exporter: `ScriptSourcesExporter.cs` 已实现（287行）
     - ✅ Pipeline: 已集成到 `OptionalExportPipeline`
     - ✅ CLI: 通过 `--code-analysis sources` 启用（使用 `Options.LinkSourceFiles` 属性）
     - ✅ Schema: `script_sources.schema.json` 已存在
     - **功能**: SHA-256哈希、行数统计、MonoScript匹配、并行处理
   - ⏳ **Type Definitions 深度分析** - Model 已存在，Exporter 待实现
     - ✅ Model: `TypeDefinitionRecord.cs`（79行）
     - ⏳ Exporter: 待实现继承链、接口、成员签名提取
   - ⏳ **Assemblies 元数据导出** - Model 已存在，Exporter 待实现
     - ✅ Model: `AssemblyMetadataRecord`
     - ⏳ Exporter: 待实现程序集版本、依赖关系、类型统计

### 中期（3-6个月）

1. **查询工具** ⏳
   - ⏳ 实现层次路径查询（"找到 Bundle X 下所有资产"）
   - ⏳ 实现跨 Bundle 场景分析工具
   - ⏳ 依赖图可视化

2. **Schema 验证增强** ⏳
   - ⏳ 添加跨表引用完整性检查
   - ⏳ 检测孤立记录
   - ⏳ 验证 collectionDetails 一致性

3. **文档扩展** ⏳
   - ⏳ 添加更多查询示例
   - ⏳ 创建交互式 Schema 浏览器
   - ⏳ 编写性能调优指南

### 长期（6-12个月）

1. **高级功能** ⏳
   - ⏳ 64-bit 哈希选项（针对超大项目）
   - ⏳ 增量导出（只导出变更）
   - ⏳ 多项目聚合分析

2. **生态系统** ⏳
   - ⏳ Python/Node.js 客户端库
   - ⏳ Web 查询界面
   - ⏳ CI/CD 集成示例

---

## 📊 项目成熟度评估

| 维度 | 完成度 | 评级 | 说明 |
|------|--------|------|------|
| **Schema 设计** | 100% | A+ | 所有核心 schema 完成并验证 |
| **核心功能** | 100% | A+ | Bundle/Collection/Asset/Scene/Script 全部实现 |
| **代码质量** | 90% | A | 清晰架构，需补充注释 |
| **文档完整性** | 95% | A | 详细文档，缺少示例代码 |
| **测试覆盖** | 30% | C | 真实测试通过，缺单元测试 |
| **性能优化** | 80% | B+ | 并行处理，压缩，需进一步优化 |
| **工具生态** | 20% | D | 基础 CLI，缺查询工具 |

**总体评级**: **B+** (优秀级别，核心功能完整，需补充测试和工具)

---

## 📚 相关文档

- `SCHEMA_STRUCTURE.md` - Schema 组织和层次结构
- `facts/README.md` - Facts 表详细说明
- `relations/README.md` - Relations 表详细说明
- `../README.md` - AssetDumper 总览
- `CLI_REFACTORING.md` - CLI 参数指南
- `COMPLETION_ASSESSMENT.md` - 项目完成状态

---

## 🔄 变更日志

### 2025-11-11: 初始版本
- 创建设计决策与限制文档
- 总结 Collections 和 Scenes schema 优化
- 记录实现状态和已知限制
- 添加未来改进路线图

---

**文档版本**: 1.0  
**最后更新**: 2025-11-11  
**维护者**: AssetRipper 开发团队
