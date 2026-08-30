# TarkovMap MapData 下一阶段开发计划书

**文档版本：** v1.1（15 项决策收口版）  
**制定日期：** 2026-08-25  
**适用项目：** TarkovMap  
**计划制定时主程序版本基线：** v1.1.1
**当前冻结基线（2026-08-30）：** 客户端 v1.1.2；正式 MapData `2026.08.29.1-pve`；Schema v1；Git 标签 `v1.1.2-final`
**开发方式：** AI 辅助开发 / 个人自用 / MVP 优先

---

## 0. 已确认决策（2026-08-25）

本计划已通过 15 个问题完成范围收口。后续开发以本节为最高优先级；其他章节若与本节冲突，以本节为准。

1. 数据源采用三层分工：`json.tarkov.dev` 提供结构化点位，`the-hideout/tarkov-dev-svg-maps` 提供 SVG 底图，现有项目数据/实测结果提供坐标映射参数、缺失地图与必要校准。
2. MapData v1 **只生成 PvE 数据**；主程序直接使用 PvE 数据，不增加 PvP/PvE 切换界面。
3. 第一阶段优先跑通撤离点、Transit、PMC/Scav 出生点、Boss、危险区。若新数据源已稳定提供其他现有类别，可一并导入，但不将它们设为 MVP 验收前提。
4. 所有新数据先生成到独立测试目录，不覆盖正式 `Data/`。
5. 测试包一次性采用新底图和新点位，不长期维护逐图混用的新旧组合。
6. 正式应用前，全部地图必须通过自动校验，并人工实测 Customs、Labs、Streets/Ground Zero 三类代表地图。
7. 单张地图或单个核心类别的点位数变化超过 30% 时，必须阻止正式打包，经人工确认后才能继续。
8. Builder 最终提供“一键应用”：先备份当前正式 MapData，再替换新版；只保留最近一个已验证可用版本。
9. 核心 CLI/Core 稳定后再开发简单 GUI，但 GUI 属于本轮最终交付物。
10. 每次获取上游数据都保存 API JSON、SVG 版本和来源信息快照，保证可离线重复构建。
11. MapData 版本格式固定为 `YYYY.MM.DD.N-pve`。
12. 发现上游新地图时只提示，不自动进入正式数据包；必须完成底图、Bounds 和定位验证后才能启用。
13. 本轮开发包含一套全新自有 Marker 图标，在正式应用新 MapData 前替换授权不明的现有图标。

---

## 0.1 当前执行状态

- **Phase 0 已完成：** 建立 TarkovMap v1.1.1 的 11 图数据基线；无上游 ID 的 Marker 已改用确定性 SHA-256 ID；连续两次完整构建的 23 个输出文件完全一致。
- **Phase 1 已完成：** PvE maps 与中文翻译接口可用；三张代表地图的数据比较与字段边界见 `Tools/MapPackBuilder/PVE_DATA_FEASIBILITY.md`。
- **Phase 2 已完成：** MapData Schema v1、`manifest.json` 模型、版本格式、客户端兼容读取与硬校验已经落地；正式规范见 `TarkovMap_MapData_Schema_v1.md`。
- **Phase 3 已完成：** `TarkovDevSource` 可同时获取 PvE 地图与中文数据，原始响应按版本保存并记录 SHA-256；17 个上游地图/变体已转换为内部模型并分为 11 个现有地图、4 个默认跳过变体和 2 个待校准新地图；11 个现有地图的 Bounds、方向和高度范围已迁入独立校准配置。
- **Phase 4 已完成：** Builder 可获取并固定 SVG 上游提交，筛选主楼层、按 Bounds 兼容比例渲染 PNG，并将 PvE 核心点位生成到独立整批测试包；10 张地图使用 SVG，迷宫回退现有 PNG。连续两次构建内容哈希一致，运行时读取冒烟检查通过；详见 `Tools/MapPackBuilder/PHASE4_TEST_PACK_REPORT.md`。
- **Phase 5 已完成：** Validation + Diff 已覆盖 manifest/快照哈希、文件与图片、Bounds、Marker 字段与重复 ID、越界点位和核心类别数量差异；支持与数据版本和新旧数量精确绑定的人工审批；详见 `Tools/MapPackBuilder/PHASE5_VALIDATION_REPORT.md`。
- **Spawn 语义审计已完成：** 旧适配器错误地把 `categories` 当作互斥标签，导致 `player+sniper` 被整体丢弃、部分 `player/botpmc` 被误标为 Scav。现已按 tarkov.dev 同源地图实现对全部地图统一修正，并增加经 SHA-256 校验的 `pve-replay` 快照重放入口；详见 `Tools/MapPackBuilder/SPAWN_CLASSIFICATION_AUDIT.md`。
- **Phase 6 已完成：** 项目所有者已通过独立客户端完成人工目测验收；精确审批后正式包 Validation 为 0 Error，确定性 ZIP 两次构建 SHA 一致，隔离环境应用/恢复后 27 个旧文件 SHA 差异为 0。`2026.08.25.4-pve` 已原子应用到正式 `TarkovMap/Data`，旧数据保留在本地唯一备份槽，新基线已建立；当前 56 个自动测试全部通过。详见 `Tools/MapPackBuilder/PHASE6_PACKAGE_REPORT.md`。
- **Phase 7 已完成：** 8 个核心类别的自有 Marker 图标已完成设计和目测验收；`2026.08.25.5-pve` 已通过确定性打包、隔离应用/恢复演练并写入正式 MapData，`.4` 保留在唯一备份槽；详见 `Tools/MapPackBuilder/PHASE7_ICON_REPORT.md`。
- **Phase 8 已完成：** WinForms GUI 已覆盖获取、构建、Diff、校验、人工验收、导出、一键应用、恢复和报告入口；所有实际操作由独立 CLI 子进程执行，避免 GUI DPI 环境影响 SVG 确定性输出；详见 `Tools/MapPackBuilder/PHASE8_GUI_REPORT.md`。
- **Phase 9 已完成：** README、Schema、Builder、许可和 AI 交接文档已按当前实现统一；固定快照完成 `.4 → .5 → .4` 隔离应用/恢复模拟，29 个恢复文件 SHA 差异为 0；详见 `Tools/MapPackBuilder/PHASE9_CLOSEOUT_REPORT.md`。
- **当前状态：** MapData Schema v1 第一轮开发已冻结。上方各 Phase 的版本号、测试数量和报告结论均为阶段历史记录；当前正式实例以 `TarkovMap/Data/manifest.json` 为准，即 `2026.08.29.1-pve`。后续仅在真实数据变化、缺陷或明确新需求出现时重新开启。

---

## 1. 项目背景

TarkovMap 当前已经完成本地互动地图、截图文件名定位、玩家朝向、小地图、Marker 分类、配置记忆等主要功能。现阶段主程序已经可以长期使用，项目下一阶段的主要短板不再是客户端功能，而是：

> **地图底图与点位数据如何在《逃离塔科夫》版本更新后，以较低维护成本持续更新。**

现有仓库已经具备 `Tools/MapPackBuilder`、`manual_overrides.json`、地图坐标转换和校准工具，因此下一阶段不应推倒重来，而应将现有 MapPackBuilder 升级为一套独立的 **MapData 数据供应链**。

---

# 2. 核心产品策略

## 2.1 双版本体系

TarkovMap 从下一阶段开始明确拆成两个生命周期：

### 主程序版本

示例：

```text
TarkovMap v1.2.0
```

主程序只在以下情况升级：

- UI / 交互发生明显变化
- 截图解析逻辑变化
- 坐标算法变化
- MiniMap 或地图渲染器变化
- MapData Schema 出现不兼容升级
- 新地图出现客户端无法通过现有数据结构支持的特殊机制

原则：

> **主程序稳定优先，没有实际需求不轻易更新。**

### MapData 数据版本

示例：

```text
MapData 2026.08.25.1-pve
MapData 2026.08.25.2-pve
```

采用：

```text
YYYY.MM.DD.N-pve
```

MapData 可以独立更新：

- 地图底图
- 地图 Bounds
- coordinateRotation
- Marker
- 撤离点
- Transit
- 出生点
- Boss
- 危险区域
- 楼层元数据
- 后续可选任务点
- 后续可选活动数据

原则：

> **游戏更新优先更新 MapData，而不是升级 TarkovMap.exe。**

---

# 3. 总体开发原则

后续所有 AI 开发必须遵守以下原则。

## 3.1 最低维护成本优先

项目为个人自用，开发者同时也是实际玩家。

因此不采用需要长期逐点人工维护的数据模式。

默认策略：

```text
上游数据正确
↓
自动获取
↓
自动转换
↓
自动校验
↓
生成摘要
↓
人工只确认是否存在明显异常
```

人工校对只用于：

- 上游出现严重错误
- 截图定位明显错位
- 新地图暂时缺少必要参数
- 极少数无法从公开数据获得的关键信息

不得把“每张地图逐点人工维护”设计成正式流程。

---

## 3.2 多源分工，按字段选择权威源

MapData v1 不强求单一数据源同时提供点位、底图和坐标校准。实际分工为：

```text
json.tarkov.dev                    → PvE 结构化点位
the-hideout/tarkov-dev-svg-maps   → SVG 地图底图
现有数据 / 实测校准              → Bounds、方向、缺失地图
```

`json.tarkov.dev` 是结构化点位的优先源，但不能因此默认其与任意 SVG 已完成世界坐标映射。底图、Bounds、`reverseCoordinate` 和朝向参数必须作为一个整体验证。

TarkovMap 不建立自己的大型 EFT 数据库。社区 Overlay 和 `manual_overrides.json` 只用于已确认的紧急修正，不作为常规维护方式。

---

## 3.3 构建阶段联网，游戏阶段离线

TarkovMap 客户端保持当前定位：

- 不访问游戏进程
- 不读取游戏内存
- 不修改游戏文件
- 不要求联网
- 不依赖实时 API
- 不在后台同步地图数据

联网行为只发生在：

```text
MapDataBuilder
```

客户端运行时只读取本地 MapData。

---

## 3.4 优先复用，不重复造轮子

对地图、SVG、坐标、Marker、翻译等数据：

> 上游已经提供可用数据时，不重新制作第二套。

例如：

- 上游直接有 PNG → 直接使用
- 上游只有 SVG → Builder 转换为运行时 PNG
- 上游已有中文 → 使用中文
- 无可靠中文 → 英文兜底
- 上游已有 Bounds → 直接适配
- 上游已有楼层结构 → 直接读取
- 上游没有数据 → 第一版宁可缺失，也不建立高维护成本的人工数据库

---

# 4. 第一阶段目标与非目标

## 4.1 MapData v1 核心目标

第一版只证明以下链路可靠：

```text
Tarkov.dev / 地图资源
        ↓
MapDataBuilder
        ↓
标准化
        ↓
校验
        ↓
生成 MapData
        ↓
TarkovMap 加载
        ↓
截图定位 / Marker 正常工作
```

第一版固定使用 `json.tarkov.dev` 的 **PvE** 数据，不同时生成 PvP 数据包，也不为主程序增加模式切换。

第一版优先包含：

- 地图基本信息
- 地图底图
- Bounds
- coordinateRotation
- 撤离点
- Transit
- PMC / Scav 出生相关点
- Boss
- 危险区域

门锁、固定武器、地图标注、物资容器等非核心类别不作为 MVP 验收前提；若 PvE 数据源已可靠提供，允许在不增加人工维护成本的前提下一并导入。

---

## 4.2 第一版明确不做

以下功能全部不得进入 P0：

- 为了追求数量而单独建立或人工维护 Loot 全量点位
- 物资价格
- 完整任务系统
- 任务进度
- 账号系统
- 云同步
- 实时联网地图
- 自动检测 Tarkov.dev 更新
- 自动 Release
- 客户端自动下载安装
- 多版本复杂回滚
- 完整活动系统
- 所有建筑的复杂楼层
- 客户端直接 SVG 渲染

这些功能未来只有出现真实需求时再评估。

---

# 5. 上游资源与可复用内容

## 5.1 json.tarkov.dev

定位：

> **MapData v1 的 PvE 结构化点位优先数据源。**

优先复用：

- Maps
- Map metadata
- Map spawns
- Extract / Transit 相关数据（以实际 endpoint 内容为准）
- Boss / spawn 相关数据
- 地图名称
- 多语言数据
- 后续任务相关数据

实现要求：

- Builder 必须通过独立 `TarkovDevSource` 访问
- 第一版只请求 PvE endpoint，不构建 PvP/PvE 双包
- 客户端不能直接依赖其 JSON Schema
- 所有上游 JSON 必须先转换成 TarkovMap 内部模型
- 支持中文数据时优先使用中文
- 中文不存在时回退英文
- 必须设置网络超时
- 网络失败时明确报错，不生成“看似成功”的空数据包
- 保存本次 API 原始 JSON 快照、来源 URL、获取时间和哈希，支持离线重复构建
- 不将 API 中的 `coordinateToCardinalRotation` 直接视为客户端 `coordinateRotation`；两者必须经现有算法与实测验证
- API 未提供与底图配套的 Bounds 时，必须从已验证的元数据或实测校准层取得，不得猜测

参考：

- https://json.tarkov.dev/endpoints
- https://github.com/tarkovtracker-org/TarkovTracker

---

## 5.2 the-hideout/tarkov-dev-svg-maps

定位：

> **地图底图的优先来源。**

可复用：

- SVG 地图底图
- 多楼层 SVG `<g>` 结构
- 部分道路、地形等 feature group
- 地图视觉资源

地址：

https://github.com/the-hideout/tarkov-dev-svg-maps

第一阶段策略：

```text
SVG
 ↓
MapDataBuilder
 ↓
PNG
 ↓
TarkovMap
```

附加要求：

- 记录 SVG 仓库提交号/版本、文件哈希和获取时间
- 上游缺失的地图允许继续使用项目已有 PNG，并在构建报告中明确标记
- SVG 的 `viewBox` 只代表图像画布，不等于游戏世界 Bounds
- 新 SVG 底图和新点位应整批输出到独立测试目录；正式目录不维护长期的新旧混用状态

客户端继续使用 PNG。

SVG 是否保存在最终 Runtime MapData 中不做强制要求，应以：

> **减少加工和减少维护工作量**

为判断标准。

注意：

该地图项目当前采用 CC BY-NC-SA 4.0，并带有针对作弊/不公平优势软件的额外限制。TarkovMap 继续保持外部只读截图定位、个人非商业辅助地图定位；公开分发衍生地图时必须继续保留对应署名和许可证要求。

---

## 5.3 sayser/TarkovTracker

定位：

> **架构和数据刷新流程参考项目。**

地址：

https://github.com/sayser/TarkovTracker

主要参考：

- `Tools/` 数据刷新脚本设计
- API → 本地 Config 的流程
- SVG map 使用方式
- Extract / Transit / Boss / Spawn / Hazard 数据适配
- Map metadata / coordinates / floors 的处理方式
- 开发阶段联网、运行阶段使用本地数据的思路

不得：

- 整体复制其客户端
- 为了复制其功能而扩大 TarkovMap 范围
- 未确认许可时直接复制代码

TarkovMap 只学习其数据供应链思想。

---

## 5.4 TarkovTracker/tarkovdata

定位：

> **结构设计参考 + 兼容数据参考。**

地址：

https://github.com/TarkovTracker/tarkovdata

可参考：

- `maps.json`
- SVG metadata
- objective GPS
- Quest 数据结构
- map floor
- coordinate rotation
- map bounds
- 数据完整性检查思路

其定位不是 MapData v1 的第一权威源。

优先级：

```text
json.tarkov.dev
  >
tarkovdata 参考
```

---

## 5.5 tarkov-data-overlay

定位：

> **未来备用修正层。**

地址：

https://github.com/tarkovtracker-org/tarkov-data-overlay

第一版默认不启用复杂 Overlay 流程。

仅保留未来扩展位置：

```text
Tarkov.dev
   ↓
[Optional Overlay]
   ↓
TarkovMap
```

当出现：

- 游戏更新速度明显快于上游
- Tarkov.dev 出现已确认错误
- 社区已经维护可靠修正

才考虑启用。

不能让 Overlay 变成需要本人长期维护的第二数据库。

---

## 5.6 当前 TarkovMap 自有资产

现有项目已经具备：

```text
Tools/MapPackBuilder
Tools/RotationCalibrator
Tools/manual_overrides.json
```

这些资产必须优先复用。

下一阶段不是新建完全独立的工具，而是逐步重构现有 MapPackBuilder。

当前 `manual_overrides.json` 保留，但角色调整为：

> **极少数紧急人工修正层**

而不是日常点位维护工具。

---

# 6. MapData v1 结构设计

建议 Runtime 数据包：

```text
MapData/
│
├─ manifest.json
├─ maps.json
│
├─ maps/
│   ├─ customs/
│   │   ├─ map.json
│   │   └─ map.png
│   │
│   ├─ labs/
│   │   ├─ map.json
│   │   └─ map.png
│   │
│   └─ ...
│
└─ icons/
```

第一版保持接近现有 TarkovMap Data 结构，避免主程序大规模修改。

---

# 7. manifest.json

新增：

```json
{
  "schemaVersion": 1,
  "dataVersion": "2026.08.25.1-pve",
  "gameMode": "pve",
  "generatedAt": "2026-08-25T12:00:00+08:00",
  "sources": [
    "json.tarkov.dev",
    "the-hideout/tarkov-dev-svg-maps"
  ],
  "sourceSnapshots": [],
  "contentHash": "..."
}
```

## 字段说明

### schemaVersion

代表：

> TarkovMap 与 MapData 之间的数据协议版本。

客户端必须优先检查该值。

例如：

```text
schemaVersion = 1
客户端支持
→ 正常加载

schemaVersion = 2
当前客户端不支持
→ 明确提示更新 TarkovMap
```

### dataVersion

格式：

```text
YYYY.MM.DD.N-pve
```

只代表数据版本。

不能与主程序版本混淆。

---

# 8. Map Schema

建议单张地图至少包含：

```json
{
  "schemaVersion": 1,
  "id": "customs",
  "name": "海关",
  "image": {
    "file": "map.png",
    "width": 2000,
    "height": 1017
  },
  "worldBounds": {
    "x0": 698,
    "z0": -307,
    "x1": -372,
    "z1": 237,
    "reverseCoordinate": false,
    "coordinateRotation": 90
  },
  "defaultFloor": null,
  "floors": [],
  "markers": []
}
```

第一版必须兼容现有 `map.json`。`image` 必须继续为包含 `file/width/height` 的对象，Bounds 必须继续使用 `worldBounds.x0/z0/x1/z1/reverseCoordinate/coordinateRotation`。新增字段应允许 v1.1.1 客户端忽略。

不得为了“设计漂亮”一次性破坏现有客户端结构。

---

# 9. Marker Schema

MapDataBuilder 内部统一 Marker Model。

建议字段：

```json
{
  "id": "unique-marker-id",
  "type": "extract_pmc",
  "name": "ZB-1011",
  "x": 0,
  "z": 0,
  "floor": null,
  "metadata": {
    "source": "json.tarkov.dev/pve/maps"
  }
}
```

第一版核心 Marker Type 必须继续输出客户端已识别的类型：

```text
extract_pmc
extract_scav
extract_shared
extract_transit
spawn_pmc
spawn_scav
boss
hazard
```

若新源可靠提供，允许继续输出现有客户端已支持的 `lock`、`stationary_weapon`、`label`、`loot_container` 等类型。Schema 可预留但不启用：

```text
quest
objective
loot
switch
event
```

Marker ID 必须跨次构建稳定。禁止使用 `.NET HashCode.Combine` 或进程内随机 Hash 作为持久 ID。上游有 ID 时优先复用；无 ID 时使用 `source + mapId + type + 规范化坐标 + 名称` 生成确定性 Hash。

---

# 10. 楼层设计

楼层不是 P0。

Schema 预留：

```json
{
  "defaultFloor": "ground",
  "floors": [
    {
      "id": "ground",
      "name": "Ground"
    }
  ]
}
```

第一阶段：

- 客户端可以完全忽略 `floors`
- 不开发完整楼层 UI

后续 P2 优先考虑：

- 实验室
- 破冰船
- 少数确实影响实战的建筑

目标不是：

> 给所有 Tarkov 地图建立完整建筑楼层系统。

---

# 11. MapDataBuilder 技术架构

现有：

```text
Tools/MapPackBuilder
```

逐步演变为：

```text
Tools/
└─ MapDataBuilder/
   │
   ├─ MapDataBuilder.Core/
   │   ├─ Sources/
   │   ├─ Models/
   │   ├─ Transformers/
   │   ├─ Validation/
   │   ├─ Diff/
   │   └─ Output/
   │
   ├─ MapDataBuilder.Cli/
   │
   └─ MapDataBuilder.Gui/
```

如果第一阶段拆成三个项目会增加过多工作量，可以先保持一个项目，通过文件夹隔离逻辑。

禁止第一阶段为了“架构漂亮”进行过度拆分。

---

# 12. Source Adapter

必须建立数据源适配层。

例如：

```text
Sources/
├─ TarkovDevSource.cs
├─ SvgMapSource.cs
└─ LegacyMapSource.cs
```

其职责：

```text
上游格式
 ↓
Source Models
 ↓
TarkovMap 内部格式
```

关键原则：

> **客户端永远不直接认识 Tarkov.dev Schema。**

未来上游字段改变：

只修改：

```text
TarkovDevSource
```

而不是修改：

```text
TarkovMap.exe
```

---

# 13. 数据构建完整流水线

MapDataBuilder 最终流程：

```text
1. Fetch
   ↓
2. Parse
   ↓
3. Normalize
   ↓
4. Transform
   ↓
5. Validate
   ↓
6. Diff
   ↓
7. Build
   ↓
8. Package
```

---

## 13.1 Fetch

获取：

- json.tarkov.dev
- 地图 SVG / PNG
- 必要 metadata

要求：

- 超时控制
- 网络错误明确
- 不静默跳过关键数据
- 支持本地缓存，减少重复下载

---

## 13.2 Normalize

统一：

- Map ID
- Marker Type
- Name
- World coordinates
- Floor ID
- 数据源名称

不得把上游奇怪字段直接带入客户端。

---

## 13.3 Transform

转换：

```text
World X / Z
   ↓
TarkovMap Map Coordinate
   ↓
现有地图渲染器可读取的数据
```

必须尽可能复用现有：

- MapCoordinateService
- MapPackBuilder 坐标逻辑
- coordinateRotation 校准结果

---

## 13.4 Validate

校验分三级：

### Error

出现后：

> **禁止生成正式 MapData ZIP**

包括：

- 底图缺失
- 核心 JSON 无法解析
- Bounds 不存在 / 非法
- 必需字段缺失
- 重复 ID 导致冲突
- 大量 Marker 越界
- 输出目录不完整
- 未经确认的单图或单个核心 Marker 类别数量变化超过 30%
- 未校准的新地图被标记为正式启用

### Warning

允许生成，但 GUI 必须明显提示：

- 少量 Marker 越界
- 未超过阻断阈值的 Marker 数量变化
- coordinateRotation 缺失
- 未知 Marker Type
- 中文翻译缺失
- 某张新地图上游数据明显不完整

数量变化超过 30% 不代表上游一定错误，但必须完成一次显式人工确认，并将确认结果写入本次构建报告，才能解除打包阻断。

### Info

正常变更：

- 新增 Marker
- 删除 Marker
- 名称变化
- Boss 列表更新
- 底图 Hash 改变
- 普通数量变化

---

# 14. Diff 系统

默认只显示摘要：

```text
Customs
+ 2 Extract
+ 1 Transit
- 1 Boss

Labs
Map image changed

Woods
No change
```

如果出现异常：

```text
WARNING:
Streets marker count -48%
```

允许展开详细 Diff。

目标：

> **本人不需要读 JSON，也能判断这次更新是否看起来正常。**

---

# 15. GUI 设计

采用：

> **CLI / Core + 简单 GUI 外壳**

GUI 不做复杂地图编辑器。

第一版只需要：

```text
┌────────────────────────────┐
│ TarkovMap MapData Builder │
├────────────────────────────┤
│ 当前数据：2026.08.25.1     │
│                            │
│ [ 获取数据 ]               │
│ [ 构建 MapData ]           │
│ [ 查看变化 ]               │
│ [ 导出 ZIP ]               │
│ [ 应用到正式程序 ]         │
│ [ 恢复上一个可用版本 ]     │
│                            │
│ 状态：构建成功             │
└────────────────────────────┘
```

GUI 的意义只是：

> 本人不需要记命令。

真正逻辑必须在 Core 中。

---

# 16. CLI 设计

建议：

```text
MapDataBuilder fetch
MapDataBuilder build
MapDataBuilder validate
MapDataBuilder diff
MapDataBuilder package
```

以及最终：

```text
MapDataBuilder all
```

执行完整流程。

未来迁移 GitHub Actions 时：

直接调用 CLI。

---

# 17. 中文策略

优先：

```text
上游 zh
```

如果对应名称没有可靠中文：

```text
英文 fallback
```

禁止：

- 第一版建立完整本地翻译表
- 每次更新要求人工翻译
- Builder 调用 AI 实时翻译

极少数长期影响体验的名称，未来可以增加轻量 Alias。

---

# 18. 地图底图策略

总体原则：

> **根据上游实际格式选择最少加工路线。**

运行时继续优先：

```text
PNG
```

理由：

- 当前客户端已经成熟
- WinForms / GDI+ 负担低
- 无需引入 SVG 渲染库
- 不增加运行时 CPU 复杂度

如果源为 SVG：

```text
SVG
 ↓ Builder
PNG
```

SVG 是数据源，不是客户端必须依赖。

未来如果多楼层功能证明有价值，再评估是否增加 SVG 处理能力。

---

# 19. MapData 发布方式

第一阶段：

```text
同一个 GitHub 仓库
```

不拆：

```text
tarkovmap-data
```

Release 中允许同时存在：

```text
TarkovMap-v1.2.0.zip
MapData-2026.08.25.1-pve.zip
```

本人无需学习复杂 GitHub 流程。

由 AI 智能体负责：

```text
build
↓
test
↓
commit
↓
tag / release
```

第一阶段不做自动 Release。

本地应用流程：

```text
构建到独立测试目录
  ↓
全地图自动校验
  ↓
三类代表地图人工实测
  ↓
备份当前正式 Data
  ↓
原子替换新 MapData
```

Builder 必须避免将半成品直接写入正式目录。“一键应用”只能在构建、校验和人工确认都已完成后启用。

---

# 20. 更新频率

采用：

> **游戏版本驱动。**

触发更新的主要情况：

- 新赛季
- 游戏大版本
- 地图扩建
- 新地图
- 撤离机制明显变化
- Boss / 出生机制明显变化

不追求：

- 每日更新
- 每周固定同步
- 上游一变化立即发布

目标：

> **降低本人维护负担。**

---

# 21. 活动数据

活动数据：

> **不是必须功能。**

只有当未来数据供应链已经非常简单，活动数据可以快速生成时才考虑更新。

第一版：

- Schema 可预留 Event
- 不开发独立 Event Package
- 不要求每次活动更新

优先级：

```text
永久地图数据
  >
版本更新数据
  >
临时活动数据
```

---

# 22. 回滚策略

MapData v1 提供轻量回滚：

- 每次正式应用新 MapData 前，自动备份当前已验证版本
- 只保留最近一个已验证可用备份，不建立多版本历史管理
- GUI 提供“恢复上一个可用版本”操作
- 恢复时同样先在临时目录校验，再替换正式目录

---

# 23. 开发阶段（修订后执行顺序）

## Phase 0：现有 Builder 基线与稳定 ID

目标：在不改变主程序行为的情况下，建立可比较的旧版输出基线。

工作：

1. 为现有 MapPackBuilder 增加基础测试。
2. 保存当前 Data 的结构、数量和文件哈希基线。
3. 将无上游 ID 的 Marker 改为确定性稳定 ID。
4. 保证旧数据仍能生成，且连续两次构建不产生虚假 Diff。

---

## Phase 1：PvE 数据可行性验证

P0，未通过前不开始大规模 Schema 改造。

工作：

1. 获取并保存 `json.tarkov.dev` PvE 原始快照。
2. 对 Customs、Labs、Streets/Ground Zero 解析 Maps、Spawns、Extract/Transit、Boss、Hazard。
3. 记录 API 字段与现有内部模型的明确映射表。
4. 确认 SVG、Bounds、`reverseCoordinate`、`coordinateRotation` 的可用来源和组合方式。

验收：三张代表地图的点位数量合理，坐标能投影到底图，且已识别所有必须由校准层补充的字段。

---

## Phase 2：兼容的 MapData Schema v1

P0。

完成 `manifest.json`、`schemaVersion`、`dataVersion`、`gameMode=pve`、来源快照、文件哈希和预留楼层字段。运行时 `map.json` 继续使用现有 `image`、`worldBounds` 和 Marker Type。

验收：v1.1.1 客户端可读取，现有地图不受影响，Schema 有正式 MD 文档。

---

## Phase 3：PvE Source Adapter

P0。

```text
json.tarkov.dev/pve
    ↓
TarkovDevSource
    ↓
Internal Model
    ↓
现有 TarkovMap Runtime Schema
```

核心类别为撤离点、Transit、PMC/Scav 出生点、Boss、危险区。其他现有类别在上游数据可靠时允许一并导入。

---

## Phase 4：SVG 地图资源与整批测试包

P0。Builder 自动获取 SVG，记录版本与哈希，必要时转换为 PNG，并与已验证的 Bounds/校准参数组合。上游缺失的底图允许沿用现有 PNG。

输出必须位于独立测试目录，一次性包含全部新底图与点位，不覆盖正式 `Data/`。

---

## Phase 5：Validation + Diff

P0。完成 Error/Warning/Info、Bounds、Marker 越界、重复 ID、必填字段、文件哈希、数量变化与 Diff Summary。

必须专门测试底图缺失、非法 Bounds、越界 Marker、重复 ID、空数据和核心类别变化超过 30% 等情况。

**状态：已完成。** Builder 自动输出 `validation-report.json` 和 `validation-report.md`；旧基线的大幅变化已完成语义审计、人工验收和精确审批，新正式基线已建立。

---

## Phase 6：Package + 一键应用/恢复

P0。自动生成 `MapData-YYYY.MM.DD.N-pve.zip`，内含 manifest、maps、map.json、map.png 和 runtime icons。

一键应用必须：

1. 确认全部自动校验通过。
2. 确认三类代表地图的人工实测结果。
3. 备份当前已验证 Data。
4. 以临时目录 + 原子替换的方式安装新数据。
5. 只保留最近一个可用备份，并支持一键恢复。

到这里，MapData 核心 MVP 成立。

**状态：已完成。** `2026.08.25.4-pve` 已完成确定性打包、解包复验、应用/恢复演练和正式原子应用；本地保留唯一可恢复备份。

---

## Phase 7：自有 Marker 图标

P1，但属于本轮正式交付前必做项。制作并替换撤离点、Transit、出生点、Boss、危险区等现用图标，保持一致的视觉语言，同步更新 NOTICE 与资源来源说明。

采用 96×96 透明 PNG：撤离/转移使用方向符号，PMC/Scav 出生点使用定位菱形，Boss 使用皇冠，危险区使用警示三角。全部资产由仓库内几何绘图代码确定性生成，不使用字体或外部图片素材。

**状态：已完成。** 8 个图标已接入 Builder 和客户端缓存，项目所有者目测确认无明显问题；`2026.08.25.5-pve` 已正式应用并建立新基线。

---

## Phase 8：简单 GUI

P1，在 CLI/Core 稳定后开发，但必须在本轮收口前交付。只提供获取、构建、Diff、校验、导出、一键应用、恢复上一个可用版本和构建报告，不得变成地图编辑器。

**状态：已完成。** GUI 只组织参数、显示日志和执行显式确认，不包含地图编辑能力；正式应用和恢复继续复用 CLI/Core 的验证、备份与原子切换逻辑。

---

## Phase 9：长期可用收口

P1。完成 README、AI 维护手册、MapData Schema、Builder 操作说明、数据源/许可说明，并使用已保存的原始快照进行一次完整的“大版本模拟更新 + 应用 + 恢复”。

完成后，MapData 第一轮开发冻结。

**状态：已完成。** 固定快照离线重放、确定性打包、隔离应用与恢复全部通过，长期维护文档已统一，第一轮开发冻结。

---

# 24. P2 / Future

只有真实需求出现时再做。

## 可选 P2

- 简单楼层切换
- Labs / Icebreaker 优先
- 社区 Overlay
- Quest Marker
- Event Marker
- GitHub Actions

## 暂不规划

- 完整任务系统
- 实时在线地图
- 云端账号
- 多人同步
- 复杂地图编辑器
- 自动每日数据发布

---

# 25. 第一阶段验收地图

所有已启用地图都必须通过自动校验；不要求所有地图都进行完整人工实测。

推荐抽样：

## Customs

验证：

- 常规 Bounds
- Extract
- Boss
- Spawn
- 截图坐标

## Labs

验证：

- 小地图
- 特殊地图尺寸
- 未来 Floor Schema 兼容

## Streets / Ground Zero

验证：

- Marker 数量较多
- Bounds 较复杂
- 数据量压力

三类代表地图的人工实测与全地图自动校验都通过后，才能将整批测试数据应用为正式 MapData。

---

# 26. 自动测试要求

新增测试重点：

```text
TarkovDevSourceTests
MapDataSchemaTests
MarkerTransformTests
StableMarkerIdTests
BoundsValidatorTests
DuplicateValidatorTests
DiffServiceTests
ChangeThresholdTests
ManifestTests
SnapshotRebuildTests
ApplyRollbackTests
```

所有 AI 修改 Builder 后至少执行：

```text
dotnet build
dotnet test
```

MapData 发布前额外执行：

```text
MapDataBuilder validate
```

---

# 27. AI 开发约束

每次智能体开发必须按以下格式汇报：

## 1. 本次目的

为什么要做。

## 2. 本次完成内容

实际完成什么。

## 3. 修改文件

列出所有文件。

## 4. 数据结构变化

如果 Schema 有变化必须单独说明。

## 5. 测试结果

必须说明：

```text
dotnet build
dotnet test
MapData validate
```

是否通过。

## 6. 人工测试方法

告诉本人：

> 只需要点哪里 / 做什么。

不能要求本人自行分析代码。

## 7. 下一步

只推荐当前阶段下一项。

---

# 28. 禁止 AI 擅自扩大范围

AI 不得因为“顺便可以做”添加：

- 自动更新客户端
- 任务系统
- Loot 系统
- 新 UI 框架
- 在线服务
- 数据库
- 登录系统
- 新部署服务
- 大规模重构主程序

出现新的技术选择时：

> 优先选择改动最少的方案。

---

# 29. 许可证与数据来源约束

当前地图资源涉及第三方许可。

必须继续维护：

```text
NOTICE.md
```

MapData 中建议增加：

```text
sources
license
attribution
```

公开发布时重点注意：

- SVG map 的 CC BY-NC-SA 4.0 要求
- 衍生地图署名
- 非商业限制
- ShareAlike 要求
- 地图仓库对作弊工具的额外禁止条款
- 核心 Marker 图标已经替换为项目自有几何图形；发布包不得重新引入来源不明的旧图标

自有图标已经通过目测验收、正式应用和自动回归，该风险项已关闭。

---

# 30. 最终完成标准

本轮开发分两个“停止点”。

## Stop 1：MVP 成立

满足：

```text
上游数据
↓
Builder
↓
MapData
↓
TarkovMap
```

能在独立测试目录完整跑通。

所有已启用地图通过自动校验，并在 3 张代表地图人工测试成功。

达到后：

> 暂停增加新数据类型。

---

## Stop 2：可长期使用

进一步满足：

- Schema v1
- manifest
- Source Adapter
- 自动转换
- 自动校验
- Diff
- ZIP
- CLI
- 一键应用 + 最近一版恢复
- 原始数据快照可离线重建
- 自有 Marker 图标
- 简单 GUI
- 文档
- 测试
- 大版本更新流程验证

达到后：

> **本轮开发冻结。**

之后只做真实使用中出现的问题。

---

# 31. 最终架构

```text
 json.tarkov.dev/pve    SVG / Map Assets    现有校准参数
         │                 │                 │
         ▼                 ▼                 ▼
   Game/Marker Data       Map Images       Bounds/Rotation
         │                 │                 │
         └─────────────────┬─────────────────┘
                           ▼
                    Source Snapshots
                           │
                           ▼
                    MapDataBuilder
                     │
           ┌─────────┼─────────┐
           │         │         │
        Normalize Validate    Diff
           │         │         │
           └─────────┼─────────┘
                     ▼
                  Package
                     │
                     ▼
       MapData-2026.08.25.1-pve.zip
                     │
                     ▼
                 TarkovMap
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
      主地图       MiniMap      截图定位

游戏运行阶段：
            完全本地
```

---

# 32. 最终一句话定义

TarkovMap 下一阶段不是继续增加客户端功能，而是建立：

> **一条低维护、可重复、可验证的地图数据更新流水线。**

产品长期策略：

> **客户端稳定，数据独立更新；上游优先，人工维护最少；构建阶段联网，游戏阶段本地；先验证数据链，再做到长期可用，然后停止扩张。**

---

# 33. 参考资源

- TarkovMap  
  https://github.com/whalien1/tarkovmap

- Tarkov.dev / The Hideout  
  https://tarkov.dev  
  https://github.com/the-hideout

- json.tarkov.dev  
  https://json.tarkov.dev/endpoints

- Tarkov SVG Maps  
  https://github.com/the-hideout/tarkov-dev-svg-maps

- sayser/TarkovTracker  
  https://github.com/sayser/TarkovTracker

- TarkovTracker/tarkovdata  
  https://github.com/TarkovTracker/tarkovdata

- tarkov-data-overlay  
  https://github.com/tarkovtracker-org/tarkov-data-overlay
