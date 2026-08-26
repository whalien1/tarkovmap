# Phase 9 长期可用收口报告

日期：2026-08-25

## 文档收口

- 根 README：记录正式 `.5` 数据、自有图标、GUI 维护入口和来源。
- `AI交接维护手册.md`：更新为当前三层数据源、GUI/CLI 流程、人工验收边界、备份规则与高 DPI 确定性教训。
- `TarkovMap_MapData_Schema_v1.md`：明确正式 ZIP 与安装后 Data 的快照边界及 8 个自有图标。
- `Tools/MapPackBuilder/README.md`：覆盖 GUI、在线构建、离线重放、验证、审批、打包、应用和恢复。
- `NOTICE.md` 与运行时 Notice：移除旧图标许可风险，分别说明 SVG、迷宫、点位、自有图标和解析参考。
- 历史开发/借鉴文档：标记旧 `ref/` 主链和“不接入 tarkov.dev”结论已被当前方案替代。

## 固定快照全流程模拟

使用已保存且 SHA-256 校验通过的 `2026.08.25.4-pve` API、SVG、许可证和校准快照，按当前代码离线重放为已验收的 `.5`：

1. 重放 11 张地图并生成测试包。
2. Validation：0 Error / 7 Warning / 0 Info。
3. 内容哈希：`eebbbb02d456b211be0ee8b8f5c1d19c19684a5bf30eede45e5e019315b05bed`。
4. 使用已有 `.5` 人工验收记录生成正式 ZIP，两次输出均为 8,453,197 字节，SHA-256 均为 `9501d9cfb3e4f6de1dba3c472e060241a57e20a2212df723f597501ec1b4ac7b`。
5. 在隔离目录复制正式备份槽中的 `.4`，应用模拟 `.5`，随后恢复 `.4`。
6. 路径结果：`.4 → .5 → .4`；恢复后的 29 个文件逐一比较 SHA-256，差异为 0。

正式 `TarkovMap/Data` 和正式 `.4` 备份槽未被本次模拟修改。

## 本地可交付包

- `Tools/build-builder.cmd` 生成 `dist/TarkovMap-MapDataBuilder-v1.0.0.zip`，包含 GUI、独立 CLI、基线、校准、README 与 NOTICE，不包含 Data、快照或调试符号。
- `package-client.cmd` 生成 `dist/TarkovMap-v1.1.1.zip`，包含正式 `.5` Data、客户端、README 与 NOTICE，不包含个人 Config、Logs、PDB 或 `Data.backup`。
- 两个 ZIP 均已从全新目录解压启动验证；客户端包中的 33 个 Data 文件与正式 Data 逐文件 SHA-256 差异为 0。

## 冻结结论

MapData Schema v1 第一轮开发已达到停止条件：数据源、快照、转换、底图、Validation/Diff、人工审批、确定性打包、原子应用/恢复、自有图标、GUI 和维护文档均已形成闭环。后续只在游戏数据真实变化、发现缺陷或用户明确提出新需求时重新开启功能开发。
