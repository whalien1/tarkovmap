# TarkovMap 悬浮小地图模块开发文档

> 模块：MiniMap / 悬浮小地图  
> 版本：v0.1 MVP  
> 所属项目：TarkovMap  
> 主开发文档：`TarkovMap_MVP_开发文档.md`  
> 技术栈：C# + .NET 10 + WinForms

---

## 0. 需求确认记录（2026-08-24 Q&A，10/10 已锁定）

以下为开发前与用户对齐的最终决策，**优先级高于正文默认建议**：

1. **游戏显示模式**：无边框窗口 → 普通 WinForms TopMost 窗口方案成立，无全屏独占遮挡风险。
2. **全局快捷键**：不做。仅主界面勾选框控制显示/隐藏。
3. **Marker 显示规则**：小地图**固定只显示** 撤离点 / 转移点 + Boss + 危险区 + 地区名标注，**与主地图 MarkerVisibility 解耦**；第一版规则定死，后续迭代再开放。
4. **默认外观**：方形（Square）+ 透明度中（75%）+ 大（300px，小 280px @96DPI）+ 主屏右上角。
5. **M0 共享状态重构**：接受。完成后主程序行为必须与 v1.0 完全一致，按 v1.0 全清单回归验收。
6. **显示器适配**：只适配单显示器；保留基本屏幕边缘保护，不做多屏恢复逻辑。
7. **鼠标焦点**：不做鼠标穿透（维持正文 §29），小地图可点击可拖动，接受点击时游戏短暂失焦。
8. **开发阶段划分**（替代正文 §39 的 M0~M7 呈现给用户）：
   - M0 内部翻新（MapViewState 重构，无新功能，全清单回归）
   - M1 最小可用小地图（方形 300px 不透明右上角；玩家居中、箭头朝向、滚轮缩放）
   - M2 图标同步（按第 3 条规则固定显示四类内容）
   - M3 窗口交互+外观（拖动、位置记忆、透明度三档、大小两档、圆/方切换、主界面设置区、等待定位提示）
   - M4 生命周期+性能（最小化/失焦仍显示、随主程序退出、内存不翻倍、静止零持续重绘）
9. **v1.0 遗留项**：立交桥"河畔之路（信号弹）"撤离点坐标仍为估算值，本次迭代不处理，待办保留。
10. **收尾方式**：M4 验收通过后同步更新主文档+本文档、打 git 标签、打包 `TarkovMap-v1.1.zip`。

---

## 1. 模块目标

悬浮小地图是 TarkovMap v0.1 的核心辅助视图。它不是 DirectX Overlay，也不是一个独立地图程序，而是主程序地图状态的第二个 View。

目标：

> 在一个独立、无边框、始终置顶的 WinForms 小窗口中，根据主程序已经解析出的截图定位结果，显示玩家周围局部地图、附近 Marker 和玩家朝向。

继续遵循主项目边界：

- 不读取游戏进程或内存
- 不注入、不 Hook
- 不绑定游戏窗口
- 不使用 Chromium / Electron / WebView
- 不联网
- 不持续轮询
- 不重复解析截图
- 不维护第二份地图数据
- 不维护第二套 Marker 设置

---

## 2. 与主程序的联动关系

本模块直接依赖主开发文档中的：

- `MapRepository`
- `MapCoordinateService`
- `ScreenshotWatcher`
- `ScreenshotLocationParser`
- `PlayerDirectionService`
- `ConfigService`
- `IconCache`
- `MapDefinition`
- `Marker`
- `MarkerType`
- `PlayerLocation`
- `WorldBounds`

主程序是唯一状态来源。

```text
ScreenshotWatcher
       ↓
ScreenshotLocationParser
       ↓
PlayerDirectionService
       ↓
   MapViewState
    /       \
   /         \
  ▼           ▼
MainMap     MiniMap
```

严禁：

```text
MainMap 监听一次截图
MiniMap 再监听一次截图
```

也严禁 MiniMap 自己重新读取 `map.json`、重新加载大地图 Bitmap、重新维护 MarkerVisibility。

---

## 3. 已确认的 MVP 规则

| 项目 | 方案 |
|---|---|
| 窗口 | 独立无边框 WinForms Form |
| TopMost | 始终开启 |
| 鼠标穿透 | 不做 |
| 移动 | 左键拖动窗口 |
| 自由 Resize | 不允许 |
| 形状 | 圆形 / 正方形切换 |
| 尺寸 | 大 / 小两档 |
| 透明度 | 低 / 中 / 高三档 |
| 地图缩放 | 鼠标滚轮连续缩放 |
| 地图方向 | 固定北向 |
| 玩家位置 | 始终位于 MiniMap 中心 |
| 玩家方向 | 玩家箭头旋转 |
| Marker | 固定显示（撤离点/转移点、Boss、危险区、地区名），与主图 MarkerVisibility 解耦 |
| Marker 点击 | 不支持 |
| 主窗口最小化 | MiniMap 继续显示 |
| 主窗口失焦 | MiniMap 继续显示 |
| 显示开关 | 独立 `显示悬浮小地图` |
| 无定位时 | 显示“等待截图定位” |
| 定位后无新截图 | 保持最后一次有效位置 |
| 程序重启 | 不恢复旧玩家位置 |
| 主程序退出 | MiniMap 一同退出 |

---

## 4. 模块职责

### 负责

- MiniMapForm 生命周期
- MiniMapCanvas 绘制
- 圆 / 方切换
- 大 / 小尺寸
- 三档透明度
- 窗口拖动
- MiniMap 独立 Zoom
- 玩家中心局部地图
- 玩家方向箭头
- 附近 Marker 绘制
- 等待定位状态
- MiniMap 配置持久化
- 位置恢复与多显示器边界检查

### 不负责

- 截图目录监听
- 截图文件名解析
- Quaternion 解析
- 自动识别地图
- 游戏日志
- 地图 JSON 解析
- Marker 数据解析
- Marker 点击
- Marker Tooltip / 详情
- 搜索
- 自定义 Marker
- 多楼层 / 室内
- 网络
- 托盘

---
## 5. 推荐主程序架构调整

为了让主地图和 MiniMap 真正同步，建议在主程序中增加共享状态层：

```text
                     AppState
                       │
          ┌────────────┼────────────┐
          │            │            │
       MapState    PlayerState   Settings
          │            │
          └──────┬─────┘
                 │
        ┌────────┴────────┐
        ▼                 ▼
 MainMapCanvas       MiniMapCanvas
```

推荐新增：

```text
Forms/
└─ MiniMapForm.cs

Controls/
├─ MapCanvas.cs
└─ MiniMapCanvas.cs

Models/
├─ MiniMapSettings.cs
├─ MiniMapShape.cs
├─ MiniMapSize.cs
└─ MiniMapOpacity.cs

Services/
├─ MapViewState.cs
└─ MiniMapWindowService.cs

Rendering/
├─ MarkerRenderer.cs
└─ PlayerMarkerRenderer.cs
```

其中：

- `MapViewState`：共享当前地图、玩家位置、玩家朝向、MarkerVisibility。
- `MiniMapWindowService`：统一创建、显示、隐藏和销毁 MiniMapForm。
- `MarkerRenderer`：主地图和 MiniMap 共用。
- `PlayerMarkerRenderer`：主地图和 MiniMap 共用。

---

## 6. MapViewState

建议：

```csharp
sealed class MapViewState
{
    public MapDefinition? CurrentMap { get; private set; }
    public PlayerLocation? PlayerLocation { get; private set; }
    public double? PlayerHeadingDegrees { get; private set; }

    public IReadOnlyDictionary<MarkerType, bool> MarkerVisibility { get; }

    public event EventHandler? MapChanged;
    public event EventHandler? PlayerLocationChanged;
    public event EventHandler? MarkerVisibilityChanged;
}
```

MainForm 与截图处理服务只更新 `MapViewState`。

两个 Canvas 只订阅状态变化，不互相调用。

正确：

```text
截图事件
→ Parse Once
→ Heading Once
→ Update MapViewState
→ MainMap Invalidate
→ MiniMap Invalidate
```

---

## 7. MiniMapForm

建议：

```csharp
sealed class MiniMapForm : Form
```

基础属性：

```csharp
FormBorderStyle = FormBorderStyle.None;
ShowInTaskbar = false;
TopMost = true;
StartPosition = FormStartPosition.Manual;
```

不提供：

- 标题栏
- 最大化
- 最小化
- Resize Border
- SizeGrip

MiniMap 的显示与 MainForm 的 `WindowState` 无关。

---

## 8. MiniMapCanvas

建议：

```csharp
sealed class MiniMapCanvas : Control
```

职责：

- 计算 MiniMap SourceRectangle
- 绘制当前地图局部区域
- 绘制附近 Marker
- 绘制玩家箭头
- 绘制等待定位状态
- 处理鼠标滚轮 Zoom
- 发起窗口拖动

不处理：

- JSON
- Regex
- Config IO
- Quaternion
- ScreenshotWatcher

---

## 9. 共享渲染代码

不要复制主地图的 Marker 绘制逻辑。

推荐抽离：

```text
MarkerRenderer
PlayerMarkerRenderer
```

主地图：

```text
World → Image → Main View Transform → Renderer
```

MiniMap：

```text
World → Image → MiniMap View Transform → Renderer
```

二者只有 View Transform 不同。

---
## 10. MiniMap 视口模型

主地图：

```text
Zoom + PanOffset
```

MiniMap：

```text
PlayerImagePoint + MiniMapZoom + CanvasSize
```

MiniMap 不存在自由 Pan。

玩家固定在：

```csharp
centerX = Width / 2f;
centerY = Height / 2f;
```

---

## 11. 底图绘制算法

截图定位成功：

```text
Player World X/Z
        ↓
MapCoordinateService.WorldToImage()
        ↓
playerImagePoint
        ↓
MiniMapZoom + CanvasSize
        ↓
sourceRectangle
        ↓
Graphics.DrawImage()
```

推荐直接绘制原 Bitmap 的一部分：

```csharp
g.DrawImage(
    mapBitmap,
    destinationRectangle,
    sourceRectangle,
    GraphicsUnit.Pixel);
```

不要每次截图：

```text
Clone Bitmap
→ Crop
→ New Bitmap
→ Draw
```

这样可以避免频繁内存分配。

---

## 12. SourceRectangle

概念公式：

```text
sourceWidth  = canvasWidth  / miniMapZoom
sourceHeight = canvasHeight / miniMapZoom

sourceX = playerImageX - sourceWidth / 2
sourceY = playerImageY - sourceHeight / 2
```

玩家靠近地图边缘时，仍保持玩家在窗口中心；超出底图的区域绘制统一背景，不通过移动玩家点来“补满”画面。

---

## 13. MiniMap Zoom

滚轮连续缩放：

```text
Wheel Up   → zoom *= 1.10
Wheel Down → zoom /= 1.10
```

必须 Clamp 到 `MinMiniMapZoom` / `MaxMiniMapZoom`。

MiniMap Zoom：

- 与主地图 Zoom 独立
- 切换地图后保留
- 切换圆 / 方后保留
- 切换大小档位后保留
- 保存到配置

---

## 14. 地图方向与玩家朝向

已确认：

> 地图固定北向，只旋转玩家箭头。

因此：

- BaseMap 不旋转
- Marker 不旋转
- 文字不旋转
- 只更新 `PlayerHeadingDegrees`

推荐接口：

```csharp
void DrawPlayer(
    Graphics g,
    PointF center,
    float headingDegrees,
    float size);
```

玩家箭头使用固定屏幕像素大小，不随 MiniMap Zoom 改变。

---
## 15. Marker 联动

MiniMap 直接使用主程序的：

```text
MarkerVisibility
```

不增加 `MiniMapMarkerVisibility`。

主界面：

```text
☑ Boss
```

修改共享状态后：

```text
MainMapCanvas.Invalidate()
MiniMapCanvas.Invalidate()
```

MiniMap Marker 只显示，不做：

- Hit Test
- Click
- Tooltip
- Selection
- 详情

---

## 16. Marker 绘制

```text
Marker World
    ↓
WorldToImage
    ↓
相对于 PlayerImagePoint 计算偏移
    ↓
MiniMapZoom
    ↓
MiniMap Screen Point
    ↓
Viewport / Shape Culling
    ↓
MarkerRenderer
```

只绘制当前 MiniMap 范围内的 Marker。

圆形模式下，方形 Bounds 过滤后还可以做一次“是否在圆内”的简单距离判断，减少圆角外无效绘制。

---

## 17. Bitmap 必须共享

这是本模块最重要的性能约束之一：

> **MiniMap 不允许重新加载一份完整地图 Bitmap。**

正确：

```text
MapImageProvider owns Current Bitmap
             │
       ┌─────┴─────┐
       ▼           ▼
 MainMapCanvas  MiniMapCanvas
```

错误：

```text
MainMap: 250 MB Bitmap
MiniMap: 250 MB Bitmap
```

否则开启 MiniMap 后内存可能接近翻倍。

Bitmap 生命周期只能由共享地图资源拥有者管理；两个 Canvas 不单独 Dispose。

---

## 18. Marker 与图标资源也共享

同样共用：

```text
IReadOnlyList<Marker>
IconCache
```

MiniMap 不 Clone Marker List，也不重新加载 Marker PNG。

---

## 19. 绘制顺序

建议：

```text
1. Clear Background
2. Apply Shape Clip
3. Draw BaseMap
4. Draw visible Markers
5. Draw Border
6. Draw Player Marker
7. Draw Waiting / status if needed
```

玩家 Marker 最后绘制，始终位于其他 Marker 上方。

---
## 20. 窗口拖动

MiniMap 任意区域左键拖动窗口。

推荐利用 Windows 原生窗口拖动：

```text
WM_NCLBUTTONDOWN
HTCAPTION
```

而不是自己长期计算 MouseMove 坐标。

概念：

```csharp
ReleaseCapture();
SendMessage(
    Handle,
    WM_NCLBUTTONDOWN,
    HTCAPTION,
    0);
```

MiniMap 仅保留：

```text
Left Drag   → Move Window
Mouse Wheel → MiniMap Zoom
```

不做单击、双击和 Marker 点击。

---

## 21. 窗口形状

枚举：

```csharp
enum MiniMapShape
{
    Circle,
    Square
}
```

### Square

```text
Region = null
Width == Height
```

### Circle

用 `GraphicsPath.AddEllipse()` 创建窗口 Region。

Region 只在：

- Shape 改变
- Size 改变
- DPI 导致实际尺寸改变

时更新。

截图更新时绝不重新生成 Region。

---

## 22. 圆形 Clip

除了 Form.Region，MiniMapCanvas 绘制时建议同时设置圆形 Clip：

```text
GraphicsPath
→ AddEllipse
→ SetClip
→ Draw BaseMap / Markers
```

保证绘制边界一致。

---

## 23. 固定尺寸

只提供：

```csharp
enum MiniMapSize
{
    Small,
    Large
}
```

不允许用户自由 Resize。

开发阶段候选值，已按实测锁定（以当前已验收代码 `MiniMapSettings.PixelSize` 为准）：

```text
Small = 280 × 280
Large = 300 × 300
```

像素尺寸已在主流分辨率 / DPI 下实测后锁定；不要自行改动（除非用户明确要求）。

配置只保存 `small / large`，不保存任意宽高。

---

## 24. 固定透明度

只提供：

```csharp
enum MiniMapOpacity
{
    Low,
    Medium,
    High
}
```

候选映射：

```text
Low    = 50%
Medium = 75%
High   = 100%
```

最终数值通过游戏画面测试微调。

实现直接使用：

```csharp
MiniMapForm.Opacity
```

不分别控制地图、Marker 与玩家箭头透明度。

---
## 25. 显示开关与生命周期

主界面提供：

```text
☑ 显示悬浮小地图
```

规则：

```text
Visible = true
→ MiniMap 始终存在
```

MainForm 以下状态都不影响 MiniMap：

- Normal
- Minimized
- Restored
- Lost Focus
- 被游戏覆盖
- 移到其他屏幕

只有：

1. 用户取消 `显示悬浮小地图`
2. TarkovMap 真正退出

才关闭 MiniMap。

主程序关闭仍遵循主开发文档：

```text
X = 完全退出，不驻留托盘
```

---

## 26. 无定位状态

本次运行尚无合法截图定位时：

```text
等待截图定位
```

不要展示默认地图区域，也不要恢复上次运行玩家坐标。

第一次合法定位后：

```text
WaitingForLocation
→ Located
```

之后没有新截图时继续保持最后一次有效位置。

程序重启后重新从 Waiting 开始。

---

## 27. 手动切换地图

主程序切换地图后，MiniMap 同步切换。

MiniMap 不允许拥有自己的地图选择。

建议切图后：

```text
清除当前 MiniMap Player View
→ WaitingForLocation
→ 等待新截图
```

避免把上一张地图的 PlayerLocation 映射到新地图。

Bounds 判断规则必须与主地图共享。

---

## 28. TopMost

MainForm 的可选 TopMost 与 MiniMap 独立。

允许：

```text
MainForm.TopMost = false
MiniMapForm.TopMost = true
```

MiniMap 始终置顶。

---

## 29. 鼠标穿透与焦点

已确认不做鼠标穿透：

- 不使用 `WS_EX_TRANSPARENT`
- 不设计锁定模式
- 不设计调整模式

用户点击 MiniMap 时它可能获得焦点，v0.1 接受该行为。

暂不做 `WS_EX_NOACTIVATE` 等复杂扩展样式。若实际测试明显影响游戏，再单独评估。

---
## 30. 配置联动

MiniMap 不使用独立配置文件。

在主程序 `config.json` 中增加：

```json
{
  "schemaVersion": 1,
  "lastMapId": "customs",
  "screenshotDirectory": "D:\\Tarkov\\Screenshots",
  "topMost": false,

  "miniMap": {
    "visible": true,
    "shape": "circle",
    "size": "large",
    "opacity": "medium",
    "zoom": 2.0,
    "x": 1520,
    "y": 80
  },

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

建议模型：

```csharp
sealed class MiniMapSettings
{
    public bool Visible { get; set; }
    public MiniMapShape Shape { get; set; }
    public MiniMapSize Size { get; set; }
    public MiniMapOpacity Opacity { get; set; }

    public double Zoom { get; set; }

    public int X { get; set; }
    public int Y { get; set; }
}
```

推荐默认：

```text
Visible = false
Shape = Circle
Opacity = Medium
Size = 待显示器实测
Position = 主屏右上角安全位置
```

---

## 31. 位置保存

用户拖动过程中只更新内存位置，不要每个 MouseMove 都写配置文件。

推荐：

```text
拖动结束
→ 更新 MiniMapSettings.X/Y

程序退出
→ ConfigService.Save()
```

如果主程序已有即时保存机制，也至少要做 debounce。

---

## 32. 多显示器恢复

启动恢复 X/Y 前，检查 MiniMap Rectangle 是否与任一：

```csharp
Screen.WorkingArea
```

有有效交集。

如果完全在屏幕外：

```text
Fallback → Primary Screen 右上角
```

防止：

- 上次双屏、本次单屏
- 显示器排列变化
- 分辨率变化

导致悬浮窗永久不可见。

---

## 33. DPI

沿用主程序的 `PerMonitorV2`。

MiniMap 跨 DPI 显示器移动后，应重新应用：

- Size 档位
- Circle Region
- Border
- Marker icon 屏幕尺寸
- Player arrow 屏幕尺寸

不要假定某一像素尺寸在所有 DPI 下具有相同视觉尺寸。

---
## 34. 主界面设置建议

主程序增加传统 WinForms GroupBox：

```text
悬浮小地图

☑ 显示悬浮小地图

形状：
(●) 圆形
( ) 正方形

大小：
( ) 小
(●) 大

透明度：
( ) 低
(●) 中
( ) 高

提示：鼠标滚轮调整悬浮地图范围
```

位置不提供 X/Y 输入框，由用户直接拖动。

所有设置立即生效，不需要重启。

---

## 35. MiniMapWindowService

集中管理窗口：

```csharp
sealed class MiniMapWindowService
{
    public void Show();
    public void Hide();
    public void ApplySettings();
    public void Close();
}
```

MainForm 不要在多个事件中散落 `new MiniMapForm()` / `Dispose()`。

---

## 36. UI 线程联动

`FileSystemWatcher` 回调可能不在 UI 线程。

正确流程：

```text
Background File Event
→ Parse
→ Calculate Heading
→ Marshal to UI thread
→ Update MapViewState
→ Notify both Views
```

不要让 MiniMap 自己再处理一套线程切换。

---

## 37. 重绘触发

MiniMap 只在以下事件重绘：

- PlayerLocationChanged
- MarkerVisibilityChanged
- MapChanged
- MiniMapZoom Changed
- Shape Changed
- Size Changed
- 窗口首次显示 / Resize

不允许 Timer 持续刷新。

---

## 38. 性能红线

MiniMap 开启后：

- 不加载第二份 BaseMap Bitmap
- 不加载第二套 Marker icons
- 不建立第二个 ScreenshotWatcher
- 不重复 Regex 解析截图名
- 不重复 Quaternion → Heading
- 不持续 Timer 重绘
- 不每次截图创建裁剪 Bitmap
- 不周期扫描目录
- 不产生网络请求

空闲时：

```text
Windows Message Loop + FileSystemWatcher
```

地图静止应接近零持续绘制成本。

---
## 39. 开发顺序

### M0：共享状态重构

先完成 `MapViewState`。

目标：主程序现有功能在重构后行为完全不变。

### M1：最小方形 MiniMap

只实现：

```text
Square
固定一个尺寸
100% Opacity
Player Center
No Marker
```

验证共享 Bitmap、局部裁剪和 Zoom。

### M2：玩家朝向

加入中心 Player Marker 与 Heading。

使用多组已知朝向截图验证 Quaternion 映射。

### M3：Marker 同步

抽取 `MarkerRenderer`，实现主地图 / MiniMap 共用，并同步 MarkerVisibility。

### M4：窗口交互

实现：

- Drag
- MouseWheel Zoom
- 位置保存
- 多显示器恢复

### M5：外观

实现：

- Circle / Square
- Small / Large
- Low / Medium / High
- Border
- Waiting State

### M6：生命周期

完整测试：

```text
MainForm Normal
Minimized
Restored
Lost Focus
MiniMap Show / Hide
Program Exit
```

### M7：性能验证

比较加入 MiniMap 前后：

```text
Working Set
Idle CPU
Screenshot Update
Map Interaction
```

如果 MiniMap 开启后内存显著接近翻倍，第一检查项就是 Bitmap 是否被重复加载。

---
## 40. 外观测试矩阵

至少验证 12 种组合：

```text
Circle × Small × Low
Circle × Small × Medium
Circle × Small × High
Circle × Large × Low
Circle × Large × Medium
Circle × Large × High

Square × Small × Low
Square × Small × Medium
Square × Small × High
Square × Large × Low
Square × Large × Medium
Square × Large × High
```

再分别验证：

- 1080p
- 2K
- 4K
- 单屏
- 双屏
- 不同 DPI

---

## 41. 定位测试

必须覆盖：

- 启动后尚无截图
- 第一张合法截图
- 连续快速截图
- 同位置多次截图
- 玩家靠地图边缘
- 玩家坐标超 Bounds
- 当前地图选错
- 切回正确地图后重新截图
- 多种玩家朝向
- 截图目录中的无关文件

---

## 42. 模块验收清单

### 生命周期

- [ ] 主界面可勾选显示 MiniMap
- [ ] 取消勾选立即隐藏
- [ ] 主窗口最小化时继续显示
- [ ] 主窗口失焦时继续显示
- [ ] 主程序退出时 MiniMap 同时退出
- [ ] MiniMap 不单独出现在任务栏

### 定位

- [ ] 无定位时显示“等待截图定位”
- [ ] 第一张合法截图后显示局部地图
- [ ] 玩家始终居中
- [ ] 玩家方向正确
- [ ] 新截图及时更新
- [ ] 无新截图保持最后位置
- [ ] 程序重启不恢复旧玩家位置
- [ ] 选错地图时不错误绘制

### 地图与 Marker

- [ ] 地图固定北向
- [ ] 滚轮连续缩放
- [ ] Zoom 有合理上下限
- [ ] MiniMap Zoom 与主地图独立
- [ ] 固定显示撤离点/转移点、Boss、危险区、地区名，与主图 MarkerVisibility 解耦
- [ ] Marker 不响应点击

### 窗口

- [ ] 可左键拖动
- [ ] 不允许自由 Resize
- [ ] Circle / Square 可切换
- [ ] Small / Large 可切换
- [ ] 透明度三档可切换
- [ ] 窗口位置可保存
- [ ] 显示器变化后不会跑到屏幕外
- [ ] 始终 TopMost

### 性能

- [ ] 不创建第二份 BaseMap Bitmap
- [ ] 不创建第二个 ScreenshotWatcher
- [ ] 不重复解析截图名
- [ ] 不重复加载 Marker icons
- [ ] 不使用持续刷新 Timer
- [ ] 静止时无持续重绘
- [ ] MiniMap 开启后的内存增量不来自地图副本

---
## 43. 主开发文档必须同步修改的部分

实现本模块时，`TarkovMap_MVP_开发文档.md` 必须同步更新。

### MVP 核心功能

增加：

- 悬浮小地图
- 圆 / 方切换
- 大 / 小两档
- 三档透明度
- MiniMap 独立 Zoom
- 位置拖动与保存
- Marker 固定显示组（撤离点/转移点、Boss、危险区、地区名），与主图 MarkerVisibility 解耦

### “明确不做”

继续保留：

```text
不做游戏 Overlay
```

并补充说明：

> MiniMap 是普通 Windows TopMost Form，不是 DirectX / 游戏内 Overlay。

### 项目结构

加入：

```text
Forms/MiniMapForm.cs
Controls/MiniMapCanvas.cs
Models/MiniMapSettings.cs
Services/MapViewState.cs
Services/MiniMapWindowService.cs
Rendering/MarkerRenderer.cs
Rendering/PlayerMarkerRenderer.cs
```

### 配置章节

增加 `miniMap` 配置段。

### 地图资源章节

明确：

> 当前 BaseMap Bitmap 由共享 Provider 持有，MainMapCanvas 与 MiniMapCanvas 只引用，不各自加载。

### 截图定位章节

更新为：

```text
ScreenshotWatcher
→ Parse Once
→ Update Shared Player State
→ Notify MainMap + MiniMap
```

### 性能章节

增加：

- 禁止 MiniMap 重复加载地图
- 禁止第二个截图监听器
- 禁止独立 Marker / Icon 副本
- 禁止持续 MiniMap Render Loop

### 开发阶段

在截图定位完成后增加 MiniMap 阶段，或直接引用本文 M0～M7。

### MVP 验收

增加本文第 42 节的关键联动项。

---
## 44. 最终架构

```text
                         ConfigService
                              │
                         MapViewState
                     ┌────────┼────────┐
                     │        │        │
                 CurrentMap Player   MarkerVisibility
                     │        │        │
                     └────┬───┴────────┘
                          │
             ┌────────────┴────────────┐
             │                         │
             ▼                         ▼
       MainMapCanvas              MiniMapCanvas
             │                         │
             └──────────┬──────────────┘
                        │
                Shared Renderers
                ├─ MarkerRenderer
                └─ PlayerMarkerRenderer

ScreenshotWatcher
      │
      ▼
ScreenshotLocationParser
      │
      ▼
PlayerDirectionService
      │
      └────────────────────────► MapViewState

MapImageProvider
      │
      └──────────── Current Bitmap ─────► 两个 Canvas 共用
```

---

## 45. 模块一句话定义

> **MiniMap 是 TarkovMap 共享地图状态的局部悬浮视图：使用普通 WinForms TopMost 窗口，以玩家为中心显示周围地图和同步 Marker，固定北向、旋转玩家箭头；它不独立监听游戏、不重复解析截图、不加载第二份大地图，也不承担主程序以外的业务逻辑。**

---

## 46. 开发红线

如果开发过程中出现以下实现，应立即回头检查架构：

```text
MiniMap 创建自己的 ScreenshotWatcher
MiniMap 自己读取 map.json
MiniMap 自己 Image.FromFile(map.png)
MiniMap 保存独立 MarkerVisibility
MiniMap 自己解析 Quaternion
MiniMap 从 MainForm Label/TextBox 读取业务状态
MiniMap 用 Timer 持续刷新
MiniMap 每次截图创建新裁剪 Bitmap
```

正确方向始终是：

> **共享状态、共享数据、共享资源、共享 Renderer；MiniMap 只拥有自己的窗口与 View Transform。**

---

## 47. 完成记录（v1.1，2026-08-25 全部验收通过）

- **M0–M4 全部完成并通过用户实测验收**：共享状态重构、最小可用小地图、图标同步、窗口交互+外观、生命周期+性能。
- **实测性能**：开小地图内存增量约 0.2MB（67.0 vs 66.8MB），共享 Bitmap 设计达标。
- **实测确认的默认设置**：方形 / 大（300px，小 280px）/ 透明度中 / 右上角；设置项用下拉框（高 DPI 下单选按钮会重叠）。
- **朝向定稿**：箭头角度 = Yaw + coordinateRotation + 90°；海关 coordinateRotation 180→90 修正（RotationOverrides）。
- **生命周期实现**：`MiniMapForm.OnFormClosing` 拦截 UserClosing → Cancel+Hide+`UserClosed` 事件 → 主界面取消勾选；主程序退出时 `AllowClose=true` 放行。WFO1000 报错需加 `[DesignerSerializationVisibility(Hidden)]`。
- **悬挂待办**：多显示器适配、Marker 组可配置、河畔之路坐标校准——见《后续迭代借鉴记录.md》项 5。
