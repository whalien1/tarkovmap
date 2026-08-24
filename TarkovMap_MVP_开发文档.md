# TarkovMap v0.1 MVP 开发文档

> 工作名称：**TarkovMap**  
> 文档版本：v0.1 Draft  
> 平台：Windows 10 / Windows 11 x64  
> 技术栈：C# + .NET 10 + WinForms  
> 发布模式：Framework-dependent Portable ZIP  
> 核心目标：**低资源占用、纯地图、无 Chromium、无账号体系、无社区、无后台常驻、无运行时联网依赖**

---

# 1. 项目定位

TarkovMap 是一个面向《逃离塔科夫》的轻量本地互动地图工具。

项目不追求成为完整的“游戏助手平台”，也不承载账号、社区、聊天、市场、攻略聚合、云同步等功能。第一版只解决一个问题：

> **用尽可能低的系统资源，快速查看塔科夫地图、关键点位，并通过游戏截图文件名辅助定位玩家当前位置。**

现有同类工具大量采用 Electron / Chromium 技术栈，并在地图之外承载众多附属功能。TarkovMap 的方向相反：

- 单一职责
- Windows 原生桌面程序
- 本地数据
- 事件驱动
- 不持续轮询
- 不运行 Web Runtime
- 不访问游戏进程
- 不注入
- 不读取内存
- 不提供 Overlay
- 不做后台驻留

---

# 2. MVP 已确认范围

## 2.1 平台

仅支持：

- Windows 10 x64
- Windows 11 x64

不考虑 macOS、Linux、ARM、Web、移动端与跨平台 UI 框架。MVP 不为未来跨平台额外设计抽象层。

## 2.2 技术栈

客户端：

- C#
- .NET 10
- WinForms
- System.Drawing / GDI+
- System.Text.Json
- FileSystemWatcher
- 第三方 UI 框架：不使用

运行时发布采用 **Framework-dependent**，目标环境为 `.NET 10 Desktop Runtime x64`。若目标电脑未安装匹配 Runtime，由 .NET Host 提示用户下载安装。

---

# 3. MVP 核心功能

第一版必须包含：

1. 手动选择地图
2. 地图拖动
3. 鼠标滚轮缩放
4. 地图视口裁剪
5. Marker 分类显示 / 隐藏
6. Marker 点击后显示最关键的信息
7. 玩家截图定位
8. 玩家朝向显示
9. 定位成功后自动居中
10. 当前缩放比例保持不变
11. 窗口置顶开关
12. 截图目录持久化
13. 本地地图数据读取
14. 本地配置读取 / 保存
15. 传统 Windows 工具型 UI
16. 关闭窗口即完全退出

---

# 4. 明确不做的功能

## 4.1 地图能力

MVP 不做：

- 室内地图
- 多楼层地图
- 楼层自动切换
- Y 高度层判断
- 地图搜索
- 自定义 Marker
- 手动打点
- 画线 / 画笔 / 橡皮擦
- 路线规划
- 路径导航
- 地图编辑器
- 任务攻略
- Wiki 百科
- 复杂 Marker 详情页

## 4.2 游戏联动

不做：

- 自动识别当前战局地图
- 游戏日志解析
- 游戏目录自动检测
- 读取游戏内存
- DLL 注入
- Hook
- 进程扫描
- Overlay
- 游戏窗口绑定
- 实时 GPS 式定位

玩家位置只通过**新截图文件名**更新。

## 4.3 在线功能

MVP 运行时完全不依赖网络。不做地图在线更新、manifest、CDN、后台版本检查、程序自动更新、地图自动更新、API 请求、云同步、登录、用户账号、社区、聊天、在线组队定位或数据上传。

新地图或新数据直接通过发布新的完整 ZIP 更新。

---

# 5. 数据来源与复用原则

开发时优先复用现有成熟项目中已经验证过的：

- 地图数据结构
- 地图 Bounds
- 坐标映射逻辑
- Marker 分类
- 撤离点 / 出生点 / Boss / Loot / Hazard / 固定武器数据
- 截图文件名坐标解析规则
- Quaternion → 朝向计算思路

参考项目：

```text
https://github.com/tiltysola/tarkov-tilty-frontend-opensource
https://github.com/tiltysola/magic-mana-client-opensource
```

参考资料：

```text
https://realm.mahoutsukai.cn/article/a42b1fc9335440498c3e1e05acc10bfd
```

复用优先级：

```text
已有成熟数据
    >
已有成熟算法 / 规则
    >
.NET / Windows 内置能力
    >
自定义实现
```

建议重新以 C# 实现 Canvas 交互、WinForms 绘图、缩放平移、Marker 绘制、FileSystemWatcher、配置系统和窗口管理，从而真正摆脱 React、Konva、Electron、Chromium 与 Node Runtime。

即使 v0.1 仅个人使用，也建议记录每份地图、Marker、图标和算法的来源，避免未来公开发布时无法追溯。

---

# 6. 第一版地图范围

MVP 原则上支持：

> **现有数据中所有适合单层显示的可用地图。**

不逐张地图开发专用代码。统一流程：

```text
MapEngine
   ↓
读取 map.json
   ↓
加载 map.png
   ↓
加载 Marker 数据
   ↓
统一渲染
```

新地图原则上应做到“添加地图数据 + 添加地图 PNG = 程序自动支持”。

禁止大量出现：

```text
if map == Customs ...
else if map == Woods ...
```

只有确实不可避免的地图级差异才允许特殊处理。

---

# 7. 室内 / 多楼层地图

v0.1 不开发多楼层系统。

第一版不实现：

- Layer 切换 UI
- 高度判断
- 玩家 Y 自动切层
- 层间 Marker 管理
- 室内 / 室外坐标切换

如果某地图有独立主要室外层，可以只使用该层。室内与多楼层系统后续单独设计，避免污染 MVP 数据模型。

---

# 8. 地图资源策略

现有 SVG 地图不在客户端运行时直接渲染。

采用：

> **开发 / 构建阶段 SVG → 高分辨率 PNG，正式客户端仅读取 PNG。**

优点：

- WinForms 原生支持
- 不需要运行时 SVG 库
- 行为确定
- 易缓存
- 易调试
- 避免实时 SVG rasterize

v0.1 优先稳定与低依赖，而不是无限缩放清晰度。

---

# 9. MapPackBuilder

单独提供开发期资源构建工具：

```text
Tools/
└─ MapPackBuilder/
```

它不进入正式客户端。

流程：

```text
原始地图数据 / SVG
        ↓
MapPackBuilder
        ↓
统一字段
        ↓
SVG Rasterize
        ↓
高分辨率 PNG
        ↓
生成 map.json
        ↓
输出 Data/
```

客户端完全不知道原始 SVG 的存在。

MapPackBuilder 可以使用必要的成熟 SVG 转换依赖；不要为了资源构建需求给正式 TarkovMap.exe 增加依赖。

---

# 10. 运行目录

推荐：

```text
TarkovMap/
├─ TarkovMap.exe
│
├─ Data/
│  ├─ maps.json
│  ├─ icons/
│  │  ├─ extract.png
│  │  ├─ spawn_pmc.png
│  │  ├─ spawn_scav.png
│  │  ├─ boss.png
│  │  ├─ loot.png
│  │  ├─ lock.png
│  │  ├─ hazard.png
│  │  └─ stationary_weapon.png
│  │
│  └─ maps/
│     ├─ customs/
│     │  ├─ map.json
│     │  └─ map.png
│     ├─ woods/
│     │  ├─ map.json
│     │  └─ map.png
│     └─ ...
│
├─ Config/
│  └─ config.json
│
└─ Logs/
   └─ errors.log
```

发布 ZIP **不包含用户个人的 `config.json`**，首次运行自动生成。

---

# 11. maps.json

用于描述客户端可用地图：

```json
{
  "schemaVersion": 1,
  "maps": [
    {
      "id": "customs",
      "name": "海关",
      "directory": "maps/customs",
      "enabled": true
    },
    {
      "id": "woods",
      "name": "森林",
      "directory": "maps/woods",
      "enabled": true
    }
  ]
}
```

作用：

- 控制地图列表
- 确定显示名称
- 指向实际地图目录
- 支持未来 Schema 升级

---

# 12. map.json

客户端不要直接绑定第三方项目的数据结构。

数据链：

```text
第三方数据
      ↓
MapPackBuilder
      ↓
TarkovMap 自有 Schema
      ↓
客户端
```

建议：

```json
{
  "schemaVersion": 1,
  "id": "customs",
  "name": "海关",
  "image": {
    "file": "map.png",
    "width": 8192,
    "height": 8192
  },
  "worldBounds": {
    "minX": -500,
    "maxX": 500,
    "minZ": -500,
    "maxZ": 500
  },
  "markers": []
}
```

这样第三方数据变化时优先修改 MapPackBuilder，而不是客户端。

---

# 13. Marker 数据模型

统一 Marker：

```json
{
  "id": "zb1011",
  "type": "extract_pmc",
  "name": "ZB-1011",
  "x": 123.4,
  "z": -245.8
}
```

允许保留未来扩展数据：

```json
{
  "id": "boss_kaban",
  "type": "boss",
  "name": "Kaban",
  "x": 100.2,
  "z": 300.8,
  "metadata": {
    "source": "..."
  }
}
```

MVP UI 不必展示 metadata。

建议枚举：

```csharp
enum MarkerType
{
    ExtractPmc,
    ExtractScav,
    ExtractShared,
    SpawnPmc,
    SpawnScav,
    Boss,
    LootContainer,
    Lock,
    Hazard,
    StationaryWeapon,
    Label
}
```

如果源数据的 Loot 分类更细，可以在数据层保留 subtype，但 UI v0.1 只按大类控制。

---

# 14. Marker 默认显示策略

默认开启：

```text
☑ PMC 撤离点
☑ Scav 撤离点
☑ PMC / Scav 共用撤离点
☑ PMC 出生点
☑ Scav 出生点
☑ Boss / 特殊敌人
```

默认关闭：

```text
□ 物资容器
□ 门锁 / 钥匙点
□ 危险区域
□ 固定武器
□ 地图文字标注
```

目标是避免初次打开时形成“Marker 海洋”。

---

# 15. Marker 点击行为

不做复杂详情面板。

点击 Marker 只显示：

```text
名称
+
关键类别
```

例如：

```text
ZB-1011
PMC 撤离点
```

或：

```text
Kaban
Boss
```

不显示百科、图片、任务、物品价值、刷新机制详解、Wiki、攻略或联网内容。

---

# 16. 坐标系统

区分三套坐标。

## 16.1 World Coordinate

游戏世界：

```text
X / Y / Z
```

MVP 地图主要使用 X / Z。Y 仅随截图解析保留，不用于楼层。

## 16.2 Image Coordinate

PNG 像素：

```text
imageX / imageY
```

## 16.3 Screen Coordinate

MapCanvas 当前控件坐标：

```text
screenX / screenY
```

考虑 Zoom 与 PanOffset。

---

# 17. World → Image

矩形 Bounds 可采用线性映射，例如：

```text
imageX =
(worldX - minX)
/
(maxX - minX)
*
imageWidth
```

Z 轴根据地图方向处理，例如：

```text
imageY =
(maxZ - worldZ)
/
(maxZ - minZ)
*
imageHeight
```

但不能默认所有源地图坐标方向一致。

**MapPackBuilder 应负责把不同来源转换成统一坐标规范。**

---

# 18. Image ↔ Screen

MapCanvas 保持：

```csharp
double Zoom;
PointF PanOffset;
```

转换：

```text
screenX = imageX * Zoom + PanOffset.X
screenY = imageY * Zoom + PanOffset.Y
```

反向：

```text
imageX = (screenX - PanOffset.X) / Zoom
imageY = (screenY - PanOffset.Y) / Zoom
```

统一链路：

```text
World → Image → Screen
```

---

# 19. MapCanvas

创建：

```text
Controls/
└─ MapCanvas.cs
```

继承 WinForms `Control`。

职责：

- 绘制地图底图
- 维护 Zoom
- 维护 PanOffset
- 鼠标拖动
- 鼠标滚轮缩放
- Marker 绘制
- Marker Hit Test
- 玩家位置绘制
- 朝向绘制
- 当前视口裁剪
- 触发重绘

不要把上述逻辑塞进 MainForm。

开启：

```csharp
ControlStyles.UserPaint
ControlStyles.AllPaintingInWmPaint
ControlStyles.OptimizedDoubleBuffer
```

---

# 20. 事件驱动绘制

禁止：

- 60 FPS Timer
- 持续 Render Loop
- 地图静止时不断刷新

只在以下情况调用 `Invalidate()`：

```text
地图切换
缩放
拖动
Resize
Marker 显示状态变化
玩家位置更新
```

目标：

> **地图静止时几乎不产生持续绘制开销。**

---

# 21. 拖动与缩放

## 拖动

```text
左键按住地图
→ MouseMove
→ 更新 PanOffset
→ Invalidate
```

不做惯性动画。

## 缩放

鼠标滚轮：

```text
Wheel Up   → Zoom *= 1.15
Wheel Down → Zoom /= 1.15
```

缩放中心是鼠标指针所在位置，而不是固定窗口中心。

最小 Zoom 动态以“完整地图适配视口”为基础；最大 Zoom 根据最终 PNG 清晰度实测决定，不预先锁死。

---

# 22. Marker 视口裁剪

绘制前：

```text
World
→ Image
→ Screen
```

若 Marker 不在当前 ClientRectangle 附近：

```text
不 DrawImage
不 DrawString
```

v0.1 不提前引入：

- Quadtree
- R-Tree
- Spatial Hash

先使用简单 O(n) 可见性裁剪，只有 profiling 证明 Marker 数量确实成为瓶颈时再优化。

---

# 23. Marker 图标缓存

使用本地小 PNG 图标，例如 24×24 或 32×32。

禁止每帧：

```csharp
Image.FromFile(...)
```

采用：

```text
首次使用 / 启动加载
→ MarkerIconCache
→ 重复绘制
```

程序退出统一 Dispose。

玩家 Marker 建议采用固定屏幕像素大小，避免随地图 Zoom 无限放大或缩小。

---

# 24. 玩家截图定位

用户首次手动配置截图目录：

```text
玩家定位
截图目录：[ D:\... ] [浏览...]
```

保存到 `Config/config.json`，之后启动自动监听。

不自动猜测塔科夫安装目录，不扫描硬盘。

---

# 25. ScreenshotWatcher

使用：

```csharp
FileSystemWatcher
```

监听新文件相关事件，例如：

```text
Created
Renamed
```

只处理符合已知截图命名格式的文件。

不周期扫描整个目录。

FileSystemWatcher 可能重复触发，因此增加基于“路径 + 短时间窗口”的简单 debounce。

由于只解析**文件名**，无需等待图片文件完全写入，也无需读取截图图像内容。

---

# 26. ScreenshotLocationParser

单独类：

```text
Services/
└─ ScreenshotLocationParser.cs
```

接口建议：

```csharp
bool TryParse(
    string fileName,
    out PlayerLocation location
);
```

只负责：

```text
filename → PlayerLocation
```

没有 WinForms 依赖。

---

# 27. PlayerLocation

建议：

```csharp
sealed class PlayerLocation
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }

    public QuaternionData Rotation { get; init; }
}
```

MVP 不保存：

- 历史轨迹
- Raid ID
- 游戏账号
- 战局信息
- 截图内容

只保留当前内存状态。

程序关闭后当前位置消失。

---

# 28. 玩家朝向

截图中的 Quaternion：

```text
Quaternion
    ↓
Yaw
    ↓
Map Rotation
    ↓
Arrow Direction
```

需要专项验证原始数据的四元数元素顺序。

不能直接假设源数组与 `System.Numerics.Quaternion(x, y, z, w)` 完全一致。

使用多组已知朝向截图进行验证后再锁定转换公式。

建议独立：

```text
PlayerDirectionService
```

方便测试。

---

# 29. 定位成功行为

固定逻辑：

```text
发现新截图
        ↓
解析 X / Y / Z + Quaternion
        ↓
转换 World Coordinate
        ↓
判断是否属于当前地图 Bounds
        ↓
合法
        ↓
更新 Player Marker
        ↓
自动平移到玩家居中
        ↓
保持当前 Zoom
```

不：

- 自动缩放
- 自动切地图
- 自动切楼层

---

# 30. 地图不匹配

如果截图坐标明显超出当前地图 Bounds：

- 不绘制玩家 Marker
- 不自动猜测地图
- 不切图
- 不抛出阻断式错误

StatusStrip 显示：

```text
当前位置与当前地图不匹配
```

下一张合法截图继续正常处理。

---

# 31. UI 设计语言

采用：

> **Classic Windows Utility UI / 传统 Win32 工具软件式信息架构**

不是仿 Windows 98 皮肤，也不是复古主题。

核心：

- 高信息密度
- 清晰分类
- 原生控件
- 低装饰
- 强空间记忆
- 少点击
- 功能优先

参考 Windows 7 时代成熟工具软件的信息架构。

---

# 32. 主窗口布局

推荐：

```text
┌───────────────────────────────────────────────────────────┐
│ 文件   地图   视图   帮助                                │
├───────────────────────────────────────────────────────────┤
│ ┌───────────────┐ ┌─────────────────────────────────────┐ │
│ │ 地图           │ │                                     │ │
│ │ [海关        ▼]│ │                                     │ │
│ │               │ │                                     │ │
│ │ 地图标记       │ │                                     │ │
│ │ ☑ PMC撤离点    │ │             MapCanvas               │ │
│ │ ☑ Scav撤离点   │ │                                     │ │
│ │ ☑ PMC出生点    │ │                                     │ │
│ │ ☑ Scav出生点   │ │                                     │ │
│ │ ☑ Boss         │ │                                     │ │
│ │ □ 物资         │ │                                     │ │
│ │ □ 门锁         │ │                                     │ │
│ │ □ 危险区域     │ │                                     │ │
│ │               │ │                                     │ │
│ │ 玩家定位       │ │                                     │ │
│ │ 状态：等待截图 │ │                                     │ │
│ │ [截图目录...]  │ │                                     │ │
│ └───────────────┘ └─────────────────────────────────────┘ │
├───────────────────────────────────────────────────────────┤
│ 海关 │ Zoom 125% │ X:123.4 Z:-88.2 │ 等待截图           │
└───────────────────────────────────────────────────────────┘
```

打开程序直接进入地图，不增加启动器首页或 Dashboard。

---

# 33. WinForms 控件

优先：

- MenuStrip
- GroupBox
- Label
- ComboBox
- CheckBox
- Button
- SplitContainer
- StatusStrip
- ToolStripStatusLabel
- ContextMenuStrip
- FolderBrowserDialog
- 自定义 MapCanvas

不使用：

- WebView
- Chromium
- React
- CSS / HTML
- 卡片 UI 框架
- Fluent UI 第三方库
- 模糊玻璃
- 大面积圆角卡片

---

# 34. 左侧功能区

## 地图

```text
地图
[ 海关 ▼ ]
```

## 地图标记

```text
地图标记

☑ PMC 撤离点
☑ Scav 撤离点
☑ 共用撤离点
☑ PMC 出生点
☑ Scav 出生点
☑ Boss

□ 物资容器
□ 门锁 / 钥匙
□ 危险区域
□ 固定武器
□ 地图标注
```

## 玩家定位

```text
玩家定位

截图目录：
D:\...

[浏览...]

状态：
等待新截图
```

详细帮助放 ToolTip 或帮助菜单，不占主界面空间。

---

# 35. MenuStrip

推荐：

```text
文件
├─ 重新加载地图数据
└─ 退出

地图
├─ 海关
├─ 森林
└─ ...

视图
├─ □ 窗口置顶
└─ 重置地图视图

帮助
└─ 关于
```

不提供“检查更新”。

窗口置顶仅通过 WinForms `TopMost`，默认关闭，不属于 Overlay。

---

# 36. StatusStrip

长期显示：

```text
当前地图
Zoom
光标 World X/Z
截图定位状态
```

例如：

```text
海关 | Zoom 125% | X:123.4 Z:-88.2 | 等待截图
```

普通状态与可恢复错误优先放状态栏，不频繁弹 MessageBox。

---

# 37. 配置系统

`Config/config.json`：

```json
{
  "schemaVersion": 1,
  "lastMapId": "customs",
  "screenshotDirectory": "D:\\Tarkov\\Screenshots",
  "topMost": false,
  "markerVisibility": {
    "extractPmc": true,
    "extractScav": true,
    "extractShared": true,
    "spawnPmc": true,
    "spawnScav": true,
    "boss": true,
    "lootContainer": false,
    "lock": false,
    "hazard": false,
    "stationaryWeapon": false,
    "label": false
  }
}
```

不保存玩家坐标、截图历史、战局 ID、账号、文件名历史或 Marker 点击历史。

配置不存在：创建默认值。

配置损坏：回退默认值，不阻止程序启动。

---

# 38. 建议项目结构

```text
TarkovMap/
│
├─ Program.cs
├─ MainForm.cs
├─ MainForm.Designer.cs
│
├─ Controls/
│  └─ MapCanvas.cs
│
├─ Models/
│  ├─ MapDefinition.cs
│  ├─ MapImageInfo.cs
│  ├─ WorldBounds.cs
│  ├─ Marker.cs
│  ├─ MarkerType.cs
│  ├─ PlayerLocation.cs
│  └─ QuaternionData.cs
│
├─ Services/
│  ├─ MapRepository.cs
│  ├─ MapCoordinateService.cs
│  ├─ ScreenshotWatcher.cs
│  ├─ ScreenshotLocationParser.cs
│  ├─ PlayerDirectionService.cs
│  ├─ ConfigService.cs
│  └─ IconCache.cs
│
├─ Infrastructure/
│  ├─ AppPaths.cs
│  └─ ErrorLogger.cs
│
└─ Properties/
```

开发工具：

```text
Tools/
└─ MapPackBuilder/
```

---

# 39. 模块职责

## MapRepository

负责：

- 读取 maps.json
- 读取 map.json
- 加载当前地图
- 校验基本数据

不负责绘制。

## MapCoordinateService

负责：

```text
World ↔ Image
```

保持无 WinForms 依赖，方便单元测试。

## ScreenshotWatcher

负责：

- FileSystemWatcher 生命周期
- 目录变更
- Created / Renamed
- debounce
- 将文件名交给 Parser

不解析具体坐标。

## ScreenshotLocationParser

只负责：

```text
filename → TryParse → PlayerLocation
```

## PlayerDirectionService

负责：

```text
Quaternion → Yaw → Map angle
```

## ConfigService

负责 Load / Save / Default / 容错。

## IconCache

负责：

```text
MarkerType → Cached Image
```

---

# 40. MainForm 职责

MainForm 只做 UI 协调：

```text
用户选择地图
→ MapRepository.Load
→ MapCanvas.SetMap

用户改变 Checkbox
→ MapCanvas.SetMarkerVisibility

截图定位
→ MapCanvas.SetPlayerLocation

窗口置顶
→ MainForm.TopMost
```

MainForm 不负责坐标数学、Regex、JSON 解析、图标读取和 Marker 文件解析。

---

# 41. 地图加载生命周期

```text
用户选择地图
      ↓
MapRepository
      ↓
读取 map.json
      ↓
加载 map.png
      ↓
构建 Marker 列表
      ↓
MapCanvas.SetMap()
      ↓
计算 Fit Zoom
      ↓
地图居中
      ↓
Invalidate
```

切换地图时：

- Dispose 旧地图 Bitmap
- 清除旧 Player Marker
- 保留 Marker 可见性设置
- 截图目录监听保持不变

---

# 42. 内存控制

PNG 磁盘大小不等于解码后的内存大小。

例如：

```text
8192 × 8192 × 4 bytes
≈ 256 MB
```

因此：

> **一次只持有当前地图的大 Bitmap。**

禁止启动时 preload 所有地图。

切换：

```text
Dispose old Bitmap
→ Load new Bitmap
```

地图 JSON 可以缓存，大 Bitmap 默认不缓存。

如果后续实测需要，可以考虑 LRU 1～2 张图，但不进入 v0.1。

---

# 43. PNG 输出尺寸

MapPackBuilder 根据实际地图决定输出尺寸。

不要统一把所有地图强制转成 16384×16384。

考虑：

- 原始 SVG 细节
- Marker 密度
- 2K / 4K 屏幕清晰度
- 最大实用 Zoom
- 解码内存

MVP 目标：

> 在常见 2K / 4K 显示器的正常地图查看倍率下清晰。

不是无限放大。

---

# 44. Tile 作为后续优化

如果大 PNG 实测成为性能瓶颈：

```text
PNG Bitmap Renderer
        ↓
Tile Renderer
```

上层的：

- MapDefinition
- Marker
- World Coordinate
- Screenshot Location
- UI

应保持不变。

v0.1 不提前实现：

- Zoom Pyramid
- 512×512 Tile
- LRU Tile Cache
- Tile Prefetch

坚持“先 profiling，再优化”。

---

# 45. CPU / GPU 原则

空闲状态不应存在：

- 60 FPS Canvas loop
- 高频 Timer
- 定时扫描截图目录
- HTTP polling
- 游戏日志 polling
- 动画

主要事件源：

```text
Windows UI Message Loop
+
FileSystemWatcher
```

地图静止时 CPU 应接近空闲状态。

---

# 46. 磁盘与网络原则

正常浏览只在地图加载时读取文件。

截图定位：

```text
新文件事件
→ 解析文件名
```

不读取截图图片。

MVP：

```text
0 runtime network dependency
```

不存在 telemetry、analytics、update check、remote API 或 crash upload。

---

# 47. 错误处理

## 可恢复错误

例如：

- 截图目录不存在
- 某 Marker 非法
- 图标缺失
- 玩家位置与当前地图不匹配

优先：

```text
StatusStrip
+
必要时禁用对应功能
```

## 无法继续

例如：

- Data/maps.json 不存在
- 当前地图 PNG 无法读取
- map.json 严重损坏

可弹：

```text
地图数据无法加载。
请重新解压完整 TarkovMap 压缩包。
```

不自动联网修复。

---

# 48. ErrorLogger

本地：

```text
Logs/errors.log
```

仅发生错误时创建 / 写入。

可记录：

- Timestamp
- Module
- Exception
- StackTrace
- Map ID

不记录：

- 截图内容
- 游戏账号
- 玩家位置历史
- 文件名历史

日志失败不能导致程序二次崩溃。

---

# 49. Portable 发布

用户流程：

```text
下载 TarkovMap-v0.1.zip
→ 解压
→ 双击 TarkovMap.exe
```

完整 ZIP 同时包含程序和 Data。

不做程序包 / 地图包分离。

新地图或新数据出现时：

```text
下载新的完整 ZIP
```

不做在线增量更新。

建议放在普通可写目录，例如：

```text
D:\Tools\TarkovMap\
```

不建议放入 Program Files，也不通过管理员权限解决写入问题。

---

# 50. 发布命令

Framework-dependent x64：

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

MVP 不启用 Trim / NativeAOT，先确保 WinForms 与 System.Drawing 行为稳定。

正式发布前再根据实际输出验证是否有必须随 EXE 分发的文件。

---

# 51. 关闭与后台行为

点击窗口 X：

```text
停止 ScreenshotWatcher
→ Dispose 当前 Bitmap
→ Dispose Marker icons
→ 保存配置
→ 退出
```

没有：

- NotifyIcon
- 系统托盘
- 后台进程
- Windows 服务
- 开机启动

---

# 52. 单实例

建议 MVP 实现单实例，因为两个窗口同时监听同一个截图目录没有价值。

使用：

```csharp
Mutex
```

第一阶段至少做到：

```text
发现已有实例
→ 新实例退出
```

后续可增加“激活已有窗口”，但不是核心阻塞项。

---

# 53. DPI 与字体

WinForms 开启 PerMonitorV2，适配 1080p、2K、4K 与多显示器。

推荐系统字体：

```text
Segoe UI 9pt
```

不打包自定义字体。

---

# 54. 开发阶段

## P0：数据验证

先选择一张典型地图作为 fixture。

完成：

1. 导出现有 Bounds
2. 导出 Marker
3. SVG → PNG
4. World → Image
5. 在 PNG 上绘制多个已知点
6. 验证坐标准确

**验收：点位与地图实际位置一致。**

P0 不通过，不继续堆 UI。

## P1：最小 MapCanvas

实现：

- 加载 PNG
- Fit to Window
- 拖动
- 滚轮缩放
- 鼠标 Image Coordinate
- World Coordinate 状态栏

暂不做 Marker。

验收：

- 无明显闪烁
- 鼠标中心缩放正确
- 拖动稳定
- Resize 正常
- 空闲无持续重绘

## P2：Marker 系统

实现：

- Marker Model
- MarkerType
- IconCache
- World → Image
- Marker Draw
- 分类开关
- Hit Test
- 名称 + 类别

验收：

- 主要 Marker 类型正确显示
- 分类开关即时生效
- Marker 全开仍可流畅浏览

## P3：全部单层地图

完成 MapPackBuilder，实现：

```text
源数据
→ 一键生成全部 Data/
```

检查每张地图：

- 名称
- Bounds
- Marker 数量
- PNG
- 坐标方向
- 非法数据

客户端不得为了具体地图修改主逻辑。

## P4：截图定位

实现：

- 目录选择
- FileSystemWatcher
- Filename Parser
- Quaternion
- Player Marker
- Direction
- Auto Center
- Map Mismatch Warning

验收：

```text
游戏产生截图
→ 地图快速显示当前位置和朝向
```

不读取截图内容。

## P5：经典 Windows UI

完善：

- MenuStrip
- 左侧 GroupBox
- StatusStrip
- TopMost
- 配置持久化
- ToolTip
- SplitContainer
- 窗口尺寸

目标是信息组织，不是视觉“美化”。

## P6：性能与稳定性

测试：

- 所有地图逐张加载
- 连续地图切换
- 快速拖动 / 缩放
- Marker 全开
- 连续截图
- 截图目录失效
- config 损坏
- map.json 损坏
- 4K / 高 DPI
- 重复打开程序

使用 Windows Task Manager、Visual Studio Diagnostic Tools，必要时使用 dotnet-counters。

---

# 55. MVP 功能验收清单

- [ ] 可选择所有支持的单层地图
- [ ] 地图 PNG 正确加载
- [ ] 地图可拖动
- [ ] 地图可滚轮缩放
- [ ] 缩放以鼠标位置为中心
- [ ] Marker 正确显示
- [ ] Marker 分类开关正常
- [ ] Marker 点击显示名称 + 类别
- [ ] 截图目录可以手动选择
- [ ] 截图目录配置可以保存
- [ ] 新截图可以解析玩家位置
- [ ] 玩家位置映射正确
- [ ] 玩家方向正确
- [ ] 定位后地图自动居中
- [ ] 定位后 Zoom 不变
- [ ] 坐标与地图不匹配时不错误绘制
- [ ] 窗口置顶可选且默认关闭
- [ ] 关闭窗口后无后台进程
- [ ] 无运行时网络请求

---

# 56. 性能验收原则

发布前实测并记录：

```text
Cold Start
Working Set
Idle CPU
Map Switch Peak Memory
Marker All Enabled
Continuous Screenshot Events
```

不在开发前虚构一个固定 RAM 目标。

结构性要求：

- [ ] 空闲无持续重绘
- [ ] 空闲无高频 Timer
- [ ] 空闲无目录轮询
- [ ] 空闲无网络活动
- [ ] 同时只持有当前大地图 Bitmap
- [ ] Marker icon 不重复加载
- [ ] 拖动无明显卡顿
- [ ] 缩放无明显卡顿
- [ ] 地图切换后旧 Bitmap 可回收
- [ ] 连续运行无明显内存持续增长

---

# 57. UI 验收原则

优先级：

```text
功能找得到
>
分类明确
>
状态明确
>
操作少
>
信息密度合理
>
视觉装饰
```

禁止：

- 巨型 Dashboard
- 游戏启动器式首页
- 大面积无意义留白
- 圆角卡片堆叠
- 动画背景
- 模糊玻璃
- Fluent 化大按钮

本项目就是地图工具：

> **打开即地图。**

---

# 58. 首次启动

即使未配置截图目录，也直接进入地图。

StatusStrip：

```text
截图定位未配置
```

左侧：

```text
玩家定位
[选择截图目录...]
```

不做首次启动 Wizard，因为截图定位不是地图查看的必要条件。

初始地图优先恢复 `lastMapId`；无历史时加载 `maps.json` 中第一张启用地图。

---

# 59. 第三方依赖策略

客户端：

> **优先零第三方 NuGet。**

主要依赖：

- .NET 10
- WinForms
- System.Drawing
- System.Text.Json

只有 profiling 或明确技术需求证明必要时，才评估窄用途第三方库。

MapPackBuilder 不受这一限制，可以为 SVG 转 PNG 使用成熟依赖，因为它不是常驻客户端。

---

# 60. 后续版本候选

## v0.2

可评估：

- Marker 搜索
- 快捷过滤
- 更丰富的轻量 Tooltip
- 地图资源校验
- 截图目录自动建议

## v0.3

重点解决：

- 室内地图
- 多楼层
- Y 高度判断
- 楼层切换

## v0.4

仅当 PNG 实测成为瓶颈时：

- Tile Pyramid
- Tile Cache
- 超大地图局部加载

## 更远期

只有真实需求证明值得时才考虑：

- 个人 Marker
- 路线
- 简单绘图

仍不建议加入账号、社区、聊天、启动器、广告或内容推荐流，否则项目会重新变成大型“助手平台”。

---

# 61. 开发边界判定

开发过程中出现新想法时统一先问：

> **“这个功能是不是地图查看和截图定位必需的？”**

如果不是，放入 Backlog，不顺手实现。

MVP 成功标准：

```text
启动快
地图清楚
拖动顺
缩放顺
点位准
截图定位准
资源低
没有多余功能
```

---

# 62. 最终架构

```text
                      ┌────────────────────┐
                      │      MainForm      │
                      │ Classic WinForms UI│
                      └─────────┬──────────┘
                                │
             ┌──────────────────┼───────────────────┐
             │                  │                   │
             ▼                  ▼                   ▼
      ┌────────────┐     ┌─────────────┐     ┌─────────────┐
      │ MapCanvas  │     │MapRepository│     │ConfigService│
      └─────┬──────┘     └──────┬──────┘     └─────────────┘
            │                   │
            │                   ▼
            │               Local Data/
            │
      ┌─────┼──────────────┐
      │     │              │
      ▼     ▼              ▼
   BaseMap Marker       Player Marker
                          ▲
                          │
                 ┌────────┴─────────┐
                 │ ScreenshotWatcher│
                 └────────┬─────────┘
                          │
                 ┌────────▼────────────┐
                 │ LocationParser      │
                 │ Quaternion → Heading│
                 └─────────────────────┘
```

整个客户端没有：

```text
Chromium
Electron
Node
Web Server
Account
Cloud
API
Game Process Access
Memory Reading
Injection
Overlay
Background Service
```

---

# 63. 一句话产品定义

> **TarkovMap v0.1 是一个用 C# + WinForms 编写的 Windows 原生《逃离塔科夫》本地互动地图：解压即用，手动选择地图，查看关键 Marker，通过截图文件名辅助定位当前位置，不联网、不读取游戏进程、不驻留后台，只做地图。**

这句话作为后续判断功能是否越界的基准。
