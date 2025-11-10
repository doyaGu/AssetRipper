# Facts Schemas

- `collections.schema.json`：导出集合（SerializedFile）级元数据。
- `assets.schema.json`：导出资产基本事实（主键、类型、偏移信息等）。
- `types.schema.json`：维护 `classKey` 与 Unity ClassID/名称映射表。
- `bundles.schema.json`：描述 Bundle 层级节点及其聚合计数信息。
- `scripts.schema.json`：描述脚本域 NDJSON 记录的结构（集合引用 + GUID、执行顺序、场景信息等元数据）。
- `scenes.schema.json`：描述场景域 NDJSON 记录的结构（聚合指标 + 引用列表）。

所有 Schema 均依赖 `../core.schema.json` 提供的通用 `$defs`（如 `AssetPK`、`CollectionID`）。
