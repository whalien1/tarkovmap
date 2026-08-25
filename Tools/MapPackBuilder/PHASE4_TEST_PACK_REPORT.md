# Phase 4 整批测试 MapData 报告

**生成日期：** 2026-08-25  
**MapData 版本：** `2026.08.25.1-pve`  
**SVG 提交：** `5a8b6115d1c0cf56f2ebaac1a96fa5ae3074d178`  
**内容哈希：** `17bbaf95f59b38cceade3a51e4ec2cf7749ad6f251d1b13f584991c04d9cb821`  
**结论：** Phase 4 技术目标通过；测试包可进入 Phase 5 Validation + Diff，但不得应用到正式 Data。

## 1. 测试包

本地输出目录：`.phase4-test-pack/`

- 11 张现有地图一次性生成。
- Customs、Factory、Ground Zero、Interchange、Labs、Lighthouse、Reserve、Shoreline、Streets、Woods 使用 SVG。
- Labyrinth 因上游仍无 SVG，使用 TarkovMap v1.1.1 兼容 PNG。
- 每次构建保存 2 个 PvE API 文件、10 个 SVG、SVG 许可证和 1 个校准配置，共 14 项 manifest 来源快照。
- Icebreaker、Terminal 只写入构建报告的 `discoveredNewMaps`，没有进入 `Data/maps.json`。

## 2. 地图输出

| 地图 | 图片尺寸 | 核心点位数 | 图片来源 |
|---|---:|---:|---|
| Customs | 3000×1525 | 272 | SVG / Ground_Level |
| Factory | 3000×2777 | 80 | SVG / Ground_Floor |
| Ground Zero | 2139×3000 | 40 | SVG / Ground_Level |
| Interchange | 3000×2506 | 176 | SVG / Ground_Level |
| Lighthouse | 1846×3000 | 532 | SVG / Ground_Level |
| Reserve | 3000×2759 | 141 | SVG / Ground_Level |
| Shoreline | 3000×1987 | 280 | SVG / Ground_Level |
| Streets | 2185×3000 | 444 | SVG / Ground_Level |
| Labs | 3000×2153 | 33 | SVG / First_Level |
| Labyrinth | 1794×1662 | 26 | v1.1.1 PNG fallback |
| Woods | 3000×2895 | 350 | SVG / Ground_Level |

实验室应用 `-0.9..3` 高度过滤后，Scav 出生点为 20，与 v1.1.1 基线一致；未过滤时的 82 个多楼层出生点没有进入测试包。

## 3. 自动检查结果

- 自动测试：44/44 通过。
- 连续两次真实在线构建的 `contentHash` 完全一致。
- TarkovMap 运行时读取器成功加载 manifest、11 个 map.json 和 11 张 PNG，声明尺寸与实际尺寸一致。
- 生成后重复 Marker ID：0。
- 发现 19 个 Bounds 外点位：Customs 2、Factory 7、Interchange 1、Lighthouse 4、Reserve 1、Shoreline 3、Woods 1。当前保留原始点位，交由 Phase 5 按 Error/Warning 规则处理。

## 4. 30% 阻断项

唯一超过既定 30% 阈值的核心类别：

| 地图 | 类别 | v1.1.1 | PvE 测试包 | 变化 |
|---|---|---:|---:|---:|
| Ground Zero | Scav Spawn | 114 | 24 | -78.9% |

该变化可能与上游已将 Ground Zero 普通版、21+ 版和教程版拆分有关。Phase 5 必须把它标为阻断项；未经人工确认，不得正式打包或一键应用。

## 5. 当前边界

- 测试包仍使用现有 4 个撤离点图标，新的自有 Marker 图标尚未进入本阶段。
- 本阶段完成底图、核心点位和整批测试输出，不代替 Customs、Labs、Streets/Ground Zero 的游戏内人工定位验收。
- 正式 `TarkovMap/Data` 未修改。
