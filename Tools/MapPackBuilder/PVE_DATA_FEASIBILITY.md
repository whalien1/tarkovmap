# PvE MapData 数据可行性验证报告

**验证日期：** 2026-08-25  
**对应阶段：** MapData 开发计划 Phase 1  
**结论：** 有条件通过，可以进入 PvE Source Adapter 开发。

## 1. 已验证接口

- `https://json.tarkov.dev/pve/maps`
- `https://json.tarkov.dev/pve/maps_zh`

PvE maps 接口当前返回：

- maps
- goonReports
- mobs
- lootContainers
- stationaryWeapons

地图对象包含 Extract、Transit、Spawn、Boss、Hazard、Lock、LootContainer、StationaryWeapon 等 TarkovMap 所需数据。中文接口采用“翻译键 → 中文文本”结构，能够覆盖地图名、撤离点、Transit、容器与大量 Boss/区域名称。

## 2. 地图识别结果

接口当前包含 17 个地图/变体条目，其中包括项目现有地图、夜间工厂、中心区 21+、实验室 Dark、Ground Zero 教程，以及新地图 Icebreaker（破冰船）和 Terminal（码头）。

按照已确认规则：

- 现有地图继续按内部 Map ID 映射。
- 变体地图默认跳过。
- 破冰船、码头等新地图只生成“发现新地图”提示，不自动启用。

## 3. 三张代表地图数据比较

以下“新数据”数量按现有 MapPackBuilder 的基础分类规则计算：出生点排除 `boss` 和 `sniper` 分类，Boss 按同坐标合并。

| 地图 | 类别 | v1.1.1 基线 | PvE API | 变化 |
|---|---:|---:|---:|---:|
| Customs | Extract | 27 | 27 | 0% |
| Customs | Transit | 4 | 4 | 0% |
| Customs | PMC Spawn | 120 | 120 | 0% |
| Customs | Scav Spawn | 117 | 113 | -3.4% |
| Customs | Boss Zone | 3 | 3 | 0% |
| Customs | Hazard | 5 | 5 | 0% |
| Streets | Extract | 17 | 17 | 0% |
| Streets | Transit | 3 | 3 | 0% |
| Streets | PMC Spawn | 0 | 0 | 0% |
| Streets | Scav Spawn | 218 | 228 | +4.6% |
| Streets | Boss Zone | 3 | 3 | 0% |
| Streets | Hazard | 197 | 197 | 0% |
| Labs | Extract | 7 | 7 | 0% |
| Labs | Transit | 1 | 1 | 0% |
| Labs | PMC Spawn | 0 | 0 | 0% |
| Labs | Scav Spawn | 20 | 82 | +310%（阻断） |
| Labs | Boss Zone | 5 | 5 | 0% |
| Labs | Hazard | 0 | 0 | 0% |

实验室出生点变化超过已确认的 30% 阻断阈值。初步原因是 PvE maps 对象不提供现有 Builder 使用的主层 `heightRange`，因此不能直接复刻现有楼层过滤。Source Adapter 必须继续从校准/兼容元数据层读取高度范围。

## 4. 坐标一致性

以可按上游 ID 对应的 Extract、Transit、Hazard 做比较：

| 地图 | API 点位 | 按 ID 匹配 | 最大 X/Z 差值 |
|---|---:|---:|---:|
| Customs | 36 | 31 | 0.004919 |
| Streets | 217 | 20 | 0.005000 |
| Labs | 8 | 8 | 0.004500 |

最大差值来自当前 Data 将坐标保留两位小数，说明匹配点使用同一游戏世界坐标体系。Streets 大量 Hazard 在旧数据中没有沿用上游 ID，因此不能按 ID 直接匹配；后续应通过稳定 ID/轮廓 Hash 比较。

## 5. 不能直接采用的字段

### Bounds

PvE maps 对象没有与地图底图配套的世界坐标 Bounds。Source Adapter 不能从 API 猜测 Bounds，必须继续使用已经验证的兼容元数据或实测校准结果。

### coordinateRotation

API 字段名为 `coordinateToCardinalRotation`，不能直接替代客户端 `coordinateRotation`：

| 地图 | 当前客户端值 | PvE API 值 |
|---|---:|---:|
| Customs | 90 | 180 |
| Streets | 180 | 180 |
| Labs | 180 | 270 |

Customs 已有实测结果证明客户端应使用 90。因此 Builder 必须保留独立的 Rotation 校准层，不能直接映射 API 数值。

## 6. Source Adapter 实现边界

进入下一阶段时应遵循：

1. API 负责 PvE Marker 原始数据与翻译键。
2. 中文数据通过 `maps_zh` 覆盖，单项缺失时回退英文。
3. Bounds、`reverseCoordinate`、`coordinateRotation`、`heightRange` 来自独立兼容/校准元数据。
4. 新地图和地图变体必须经过明确的 ID 分类，不自动启用。
5. 所有原始 API 响应保存 URL、获取时间、SHA-256 与本地快照。
6. 实验室高度过滤在解决前必须保持打包阻断状态。

## 7. Phase 1 结论

PvE API 与现有地图点位的数据血缘高度一致，Customs、Streets 的核心数据变化处于合理范围，中文覆盖可用，因此采用 PvE API 作为结构化 Marker 优先源是可行的。

但它不是完整的地图包来源。正式数据链必须保持：

```text
PvE API Marker
  + SVG/现有 PNG
  + Bounds/Rotation/HeightRange 校准层
  = TarkovMap Runtime MapData
```

下一步可以开发 `TarkovDevSource` 与独立校准元数据模型，不应修改主程序渲染和截图定位算法。
