# Phase 5：Validation + Diff 实施报告

## 结论

Phase 5 的自动验证、基线差异和正式打包阻断规则已经落地。全新生成的 `2026.08.25.2-pve` 测试包验证结果为：

- Error：1
- Warning：16
- Info：0
- 结论：未通过，已阻止正式打包

唯一阻断项是 `ground-zero/spawn_scav` 从基线 114 个降至 24 个，变化 -78.9%，超过 30% 且尚未人工确认。Builder 没有静默放行该变化。

本次完整在线构建在 GitHub API 达到未认证频率限制后，自动改从公开 commit patch 的首行取得并校验 40 位提交 SHA，随后按该 SHA 下载资源。最终 SVG 提交仍为 `5a8b6115d1c0cf56f2ebaac1a96fa5ae3074d178`，运行时内容哈希仍为 `17bbaf95f59b38cceade3a51e4ec2cf7749ad6f251d1b13f584991c04d9cb821`，与 Phase 4 两次构建完全一致。

## 已实现

- 校验 `manifest.json`、运行时内容哈希和全部来源快照 SHA-256。
- 校验 `maps.json`、`map.json`、底图存在性与实际尺寸。
- 校验 Bounds、方向字段、Marker 必填字段、有限坐标、轮廓和重复 ID。
- 按地图汇总 Bounds 外 Marker；当前 19 个均属于少量越界，记录为 Warning。
- 仅对撤离点、Transit、PMC/Scav 出生点、Boss、危险区 8 个核心类别执行基线数量 Diff。
- 单地图、单核心类别变化绝对值超过 30% 时产生 Error 并阻止正式打包。
- 支持精确审批文件；审批必须匹配数据版本、地图、类别、基线数量和当前数量。
- `pve-build` 自动生成 JSON/Markdown 验证报告；`pve-validate` 可重复验证已有测试包。

## 当前差异

除中心区阻断外，共有 9 项未超过阈值的核心类别数量变化。其中较大的包括：

- `interchange/extract_pmc`：4 → 3（-25.0%）
- `shoreline/spawn_scav`：85 → 106（+24.7%）
- `lighthouse/spawn_scav`：132 → 162（+22.7%）
- `interchange/spawn_scav`：30 → 36（+20.0%）

Bounds 外 Marker 分布为：海关 2、工厂 7、立交桥 1、灯塔 4、储备站 1、海岸线 3、森林 1。每张地图均未达到“大量越界”的 Error 标准。

## 自动测试

新增 9 个验证测试，覆盖正常通过、30% 未确认阻断、精确审批放行、重复 Marker ID、少量越界 Warning、来源快照哈希篡改、底图缺失、非法 Bounds 和空 Marker 数据；另补充 GitHub API 限流降级测试。项目测试总数为 54，全部通过。

## 下一步

先调查中心区 114 → 24 是否来自 PvE API 对普通中心区、21+ 和教学变体的拆分；确认 24 个点是否完整。没有证据前不创建审批文件，也不进入 Phase 6 正式打包与一键应用。
