# Draft 2020-12 Validation Notes

## 背景
- 项目目标是采用 JSON Schema Draft 2020-12 组织 AssetDump v2 的 Facts / Relations / Manifest 规范。
- 现有仓库附带的 schema 校验脚本使用的验证器（Newtonsoft.Json.Schema v3）仅支持 Draft-07，运行 `core.schema.json` 时提示 `The schema uses meta-schema features ($dynamicRef) that are not yet supported`。

## 建议方案
1. **开发阶段**：
   - 对内部调试保持 Draft 2020-12 schema；
   - 使用 [json-schema.org](https://www.json-schema.org/) 推荐的外部工具链（例如 Ajv v8、Spectral、djv）进行校验；
   - 在 README 中标注当前 `Schemas/v2` 依赖 Draft 2020-12，并提供验证命令示例。
2. **仓库内兼容层**：
   - 若必须使用现有 Draft-07 验证器，可在 `Schemas/v2/compat/` 下维护一次性转换脚本（例如自动降级 `$defs`/`$anchor` 用法），暂不纳入主流程。
         - `AssetDumper --validate-schema` 集成了基于 JsonSchema.Net 的行级校验，可在导出流程中即时暴露 Draft 2020-12 违规，并支持对 Zstandard 压缩分片的流式解压校验。
3. **后续工作项**：
   - 追踪 Newtonsoft.Json.Schema 或其他 .NET 验证器对 2020-12 的支持计划；
   - 如短期内无法升级，可评估在导出流程中运行外部命令行验证器。

## 现状结论
- Draft 2020-12 仍是首选规范；
- 仓库自带验证器暂不支持，需要在文档与工具链中明确说明并提供外部校验途径。
