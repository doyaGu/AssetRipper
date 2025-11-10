# AssetDump v2 Schemas

此目录包含 AssetDumper v2 导出的 JSON Schema（Draft 2020-12）：

- `core.schema.json`：通用 `$defs` 与锚点（`CollectionID`、`AssetPK`、`AssetRef` 等）。
- `facts/`：事实层对象（集合、资产、类型、脚本、场景等）。
- `relations/`：跨资产的引用关系（如 PPtr 依赖边）。
- `indexes/`、`metrics/`：可再生索引与派生统计样例。

验证注意事项请参见 `VALIDATION_NOTES.md`。
