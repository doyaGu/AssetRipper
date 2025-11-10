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
    │   └── README.md
    ├── indexes/
    │   ├── by_class.schema.json
    │   └── README.md
    ├── metrics/
    │   └── scene_stats.schema.json
    └── README.md
```

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
- 一旦发布稳定 URI，原则上不做破坏性修改；新增字段应在 `$defs` 中扩展或通过 `oneOf` 升级。
- 若需引入下一版 schema，应在 `Schemas/v3/` 内新建一套目录，以避免破坏现有消费端。

## 下一步
1. 草拟 `core.schema.json`，覆盖常用 `$defs` (`CollectionID`, `AssetPK`, `Timestamp`, `StableKey` 等)。
2. 依次为 `facts/collections.schema.json`、`facts/assets.schema.json` 等填充草稿。
3. 在 README 中同步维护字段解释与版本历史。
