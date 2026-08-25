# TarkovMap v1.1.1

《逃离塔科夫》本地互动地图工具（Windows 原生桌面程序）。

当前内置地图数据：`2026.08.25.5-pve`（PvE，11 张地图）。

**打开即地图。不联网、不碰游戏进程、不驻留后台，只做地图。**

## 系统要求

- Windows 10 / 11 x64
- 安装 [.NET 10 桌面运行时（Desktop Runtime x64）](https://dotnet.microsoft.com/download)（未安装时程序会提示下载）

## 安装与启动

1. 解压 `TarkovMap-v1.1.1.zip` 到普通可写目录（如 `D:\Tools\TarkovMap\`，**不要**放 Program Files）
2. 双击 `TarkovMap.exe`

## 功能

- **11 张地图**：街区 / 中心区 / 海关 / 工厂 / 立交桥 / 实验室 / 灯塔 / 储备站 / 海岸线 / 森林 / 迷宫
- 左键拖动、滚轮缩放（以鼠标为中心）、最大化自动适配
- Marker 分类开关：撤离点（大图标+名称）/ 出生点 / Boss（红圈红名）/ 危险区域（红色区域块，即死机制）/ 物资容器 / 门锁 / 固定武器 / 地图标注，勾选状态自动记忆
- 点击 Marker 显示名称 + 类别
- **截图定位**：游戏内用自带截图功能（PrintScreen）截图 → 程序通过文件名自动解析位置与朝向 → 地图画箭头并居中
- 窗口置顶开关、上次地图/窗口尺寸记忆、单实例
- **小地图**（v1.1 新增）：无边框置顶悬浮窗，跟随截图定位实时显示周边地图、玩家位置与朝向箭头
  - 固定显示撤离点 / Boss / 危险区 / 地区名（独立于主图开关）
  - 可拖动位置、滚轮缩放视野；形状（方/圆）、大小（大 300px / 小 280px）、透明度三档可调
  - 主窗口最小化不消失；主程序退出时自动跟随关闭，不留残留

## 截图定位设置

1. 左侧"玩家定位" → "选择截图目录"
2. 选择游戏截图目录，默认为：
   `C:\Users\<用户名>\Documents\Escape from Tarkov\Screenshots`
3. 之后游戏内每截一张图，地图自动定位。坐标与当前地图不符时只在状态栏提示，不打断。

## 安全说明

程序仅监听游戏生成的截图目录并解析截图**文件名**，不读图片内容、不读游戏内存、不注入、不修改游戏文件、不使用游戏内 Overlay，采用低侵入的只读实现。未对任何第三方反作弊策略作承诺。

## 点位数据维护

- 官方点位：来自 json.tarkov.dev PvE 数据；当前优先提供撤离点、Transit、PMC/Scav 出生点、Boss 和危险区
- 地图底图：10 张来自固定提交的 `the-hideout/tarkov-dev-svg-maps` SVG，迷宫暂时沿用已有 PNG
- 游戏大版本更新后使用 `Tools/MapPackBuilder` 的抓取、快照重放、Validation、打包、应用和恢复流程
- 手工补录：编辑 `Tools/manual_overrides.json`（重新生成不丢失），或直接改 `Data/maps/<地图>/map.json` 的 markers 数组
- 实测坐标方法：站在点位处游戏内截图，文件名自带精确 X/Z

## 版本更新维护流程

游戏大版本更新后（新地图 / 点位变动），按此流程出新版本：

1. **更新数据源**：更新 `ref/` 下的参考数据仓库（重新 clone 或 pull 最新社区数据）
2. **重新生成地图数据**：运行 `Tools/MapPackBuilder`（双击其 exe 或 `dotnet run`），一键重建全部 `Data/`，查看每张图的核查报告（点位数、非法点位数）
3. **抽查校准**：开本地模式在 2–3 个已知点位截图，对照地图上 Marker 位置确认坐标无偏移
4. **手工补录检查**：`Tools/manual_overrides.json` 里的补录点位不受影响；新地图（如破冰船）用社区高清 PNG + 两个已知点截图校准边界后新增
5. **Boss 名单检查**：新增/移除 Boss 改 `Program.cs` 里的 `ExcludedBosses`
6. **打包发布**：更新版本号 → 重新打 ZIP → `git commit` + 打新标签（如 v1.1.1）

## 数据来源与致谢

- **地图图片**：10 张地图基于 [the-hideout/tarkov-dev-svg-maps](https://github.com/the-hideout/tarkov-dev-svg-maps) 的固定提交生成，迷宫沿用已有 PNG；具体提交和修改方式见 `Data/THIRD_PARTY_NOTICES.md`。
- **点位数据**：来自 [json.tarkov.dev](https://json.tarkov.dev) PvE 地图接口及中文翻译接口，经 MapPackBuilder 转换和验证。
- **核心 Marker 图标**：由本项目 MapPackBuilder 的几何绘图代码生成，包括撤离点、Transit、出生点、Boss 和危险区，不使用外部图片素材或字体字形。
- **截图文件名解析**：参考 Re5pawnn/Tarkov_webmap（ScreenshotCoordinateParser）。

### 许可证与署名说明

- 本仓库**代码未附带开源许可证，默认保留所有权利**；仅作为个人项目发布/参考，作者未进行开源授权。
- **地图图片**（`Data/maps/*/map.png`）中由 [the-hideout/tarkov-dev-svg-maps](https://github.com/the-hideout/tarkov-dev-svg-maps) 生成的部分依其 **CC BY-NC-SA 4.0（署名-非商业性使用-相同方式共享）** 授权：允许非商业性使用与修改，但需署名原作者、衍生内容须以相同许可证共享，**禁止商用**；点位 JSON 的来源和快照哈希记录在 `manifest.json`。
- **核心 Marker 图标**（`Data/icons/*.png`）是本项目通过 `MarkerIconAssetGenerator.cs` 自行绘制的原创几何图形，不包含第三方图片素材或字体字形。
- 本项目仅解析游戏截图文件名以定位，不访问游戏进程、不读取游戏内存、不注入、不修改游戏文件、无联网、不使用游戏内 Overlay，为低侵入只读实现。
