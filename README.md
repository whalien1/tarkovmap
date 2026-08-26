# TarkovMap v1.1.2

《逃离塔科夫》本地互动地图工具（Windows 原生桌面程序）。

当前内置地图数据：`2026.08.26.1-pve`（PvE，11 张地图）。

MapData Schema v1 第一轮供应链已经完成并冻结；维护入口和故障处理见 [MapPackBuilder 说明](Tools/MapPackBuilder/README.md) 与 [AI 交接维护手册](AI交接维护手册.md)。

需要双击版维护工具时，在仓库根目录运行 `Tools\build-builder.cmd`，输出位于 `dist\TarkovMap-MapDataBuilder-v1.0.0.zip`。

**打开即地图。不联网、不碰游戏进程、不驻留后台，只做地图。**

## 系统要求

- Windows 10 / 11 x64
- 安装 [.NET 10 桌面运行时（Desktop Runtime x64）](https://dotnet.microsoft.com/download)（未安装时程序会提示下载）

## 安装与启动

1. 解压 `TarkovMap-v1.1.2.zip` 到普通可写目录（如 `D:\Tools\TarkovMap\`，**不要**放 Program Files）
2. 双击 `TarkovMap.exe`

仓库维护者可运行根目录的 `package-client.cmd` 生成经过清理的 `dist\TarkovMap-v1.1.2.zip`；发布包不包含个人 Config、日志、PDB 或 MapData 备份。

## 功能

- **11 张地图**：街区 / 中心区 / 海关 / 工厂 / 立交桥 / 实验室 / 灯塔 / 储备站 / 海岸线 / 森林 / 迷宫
- 左键拖动、滚轮缩放（以鼠标为中心）、最大化自动适配
- Marker 分类开关：撤离点（大图标+名称）/ 出生点 / Boss（红圈红名）/ 危险区域（红色区域块，即死机制）/ 物资容器 / 门锁 / 固定武器 / 地图标注，勾选状态自动记忆
- 点击 Marker 显示名称 + 类别
- **截图定位**：游戏内用自带截图功能（PrintScreen）截图 → 程序通过文件名自动解析位置与朝向 → 地图画箭头并居中
- 窗口置顶开关、上次地图记忆、单实例；普通窗口固定为 1800 × 1000，支持最大化，禁止自由拖拽缩放；较小屏幕自动适配可用区域
- **小地图**（v1.1 新增）：无边框置顶悬浮窗，跟随截图定位实时显示周边地图、玩家位置与朝向箭头
  - 固定显示撤离点 / Boss / 危险区 / 地区名（独立于主图开关）
  - 可拖动位置、滚轮缩放视野；形状（方/圆）、大小（小 260px / 中 300px / 大 340px）、透明度三档可调
  - 主窗口最小化不消失；主程序退出时自动跟随关闭，不留残留

## 截图定位设置

1. 左侧“玩家定位” → “设置截图目录…”
2. 选择游戏截图目录，默认为：
   `C:\Users\<用户名>\Documents\Escape from Tarkov\Screenshots`
3. 之后游戏内每截一张图，地图自动定位。坐标与当前地图不符时只在状态栏提示，不打断。

## 安全说明

程序仅监听游戏生成的截图目录并解析截图**文件名**，不读图片内容、不读游戏内存、不注入、不修改游戏文件、不使用游戏内 Overlay，采用低侵入的只读实现。未对任何第三方反作弊策略作承诺。

## 点位数据维护

- 官方点位：来自 json.tarkov.dev PvE 数据；当前优先提供撤离点、Transit、PMC/Scav 出生点、Boss 和危险区
- 地图底图：10 张来自固定提交的 `the-hideout/tarkov-dev-svg-maps` SVG，迷宫暂时沿用已有 PNG
- 游戏大版本更新后使用 `Tools/MapPackBuilder` 的抓取、快照重放、Validation、打包、应用和恢复流程
- 日常更新可直接运行 `Tools/MapPackBuilder.Gui` 图形界面，无需记忆命令；人工验收后才能导出和应用正式包
- 校准参数：编辑 `Tools/MapPackBuilder/calibration-v1.1.1.json` 后重新构建并完整验收；`Tools/manual_overrides.json` 只供旧 `ref/` 构建入口回归，不会自动进入当前 PvE 流程
- 不建议直接修改正式 `Data/maps/<地图>/map.json`：下一次整包应用会覆盖手改内容；确需补点时应先为 PvE Builder 增加可追踪的覆盖层并配套测试
- 实测坐标方法：站在点位处游戏内截图，文件名自带精确 X/Z

## 版本更新维护流程

游戏大版本更新后（新地图 / 点位变动），按此流程出新版本：

1. **打开维护界面**：运行 `dotnet run --project Tools/MapPackBuilder.Gui`，确认正式 Data 路径和自动建议的新版本号。
2. **获取并构建**：点击“获取数据”和“构建 MapData”；来源响应、SVG、许可证与校准配置会保存为带 SHA-256 的快照，测试包不会覆盖正式 Data。
3. **查看变化**：打开 Validation + Diff 报告。任何 Error 必须先修复；新地图只提示，不自动启用。
4. **人工验收**：用独立客户端检查海关、中心区、街区、实验室及所有超过 30% 的类别变化，再由项目所有者点击“确认验收”。
5. **导出并应用**：点击“导出 ZIP”，通过再次校验和解包复验后再点击“应用到正式程序”；当前版本会保留为唯一可恢复备份。
6. **回归与提交**：运行全部自动测试和客户端冒烟，核对正式版本/内容哈希，再显式暂存本次文件并提交；只有发布客户端版本时才创建 Git 标签。

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
