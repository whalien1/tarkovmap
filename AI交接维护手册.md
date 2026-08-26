# TarkovMap AI 交接维护手册

> 面向后续接手的 AI 智能体：记录本项目的构建方法、已踩过的坑、常见 Bug 与修复定式。
> 阅读顺序：先读本手册，再读《TarkovMap_MapData_下一阶段开发计划书.md》（当前数据供应链决策）、《Tools/MapPackBuilder/README.md》（操作方法），最后按需查阅客户端历史文档。
> 当前基线：客户端 **v1.1.1**；正式 MapData **2026.08.25.5-pve**（PvE，11 张地图）。

## 1. 环境与构建（最容易踩的坑）

> **路径说明**：本文出现的 `D:\tarkov map\` 等均为作者本机**示例路径**，**不是工程构建前提**。

- **客户端标准构建（推荐）**：在任意克隆目录执行 `dotnet restore` + `dotnet build TarkovMap/TarkovMap.csproj -c Release`
  （或直接运行仓库根目录的 `build.cmd`）即可完成。客户端依赖 `net10.0-windows` + WinForms，无第三方 NuGet；MapPackBuilder 另使用 SVG 渲染包。
  `dotnet restore` **可以**正常执行（本项目**不是**靠手工构造 `obj/project.assets.json` 才能构建）。
- **`build.cmd` 已是可移植脚本**：使用脚本自身相对路径（`%~dp0`），直接调用 PATH 中的 `dotnet`，
  不依赖作者机器的绝对路径；构建成功后会把 `Data\` 拷进运行目录。
- **历史教训（已废弃）**：早期为规避环境问题，本项目曾以"`--no-restore` + 保留手工 obj"的方式构建。
  该认知有误——**删除 `bin/obj` 后 `dotnet restore` + `dotnet build -c Release` 可正常通过**。
  此后请按标准流程构建，不要沿用"避免 restore"。
- **重要教训**：不要把任何生成物放进**不被默认 glob 排除**的目录（如 `obj.bak/`）。只排除 `obj/`，
  若新建 `obj.bak/`，其中的 `*.cs` 会被当源码二次编译，触发 CS0579（程序集特性重复）。
- 本机示例：作者机器 .NET SDK 10.0.400，dotnet 在 `C:\Program Files\dotnet\dotnet.exe`；
  可执行文件在 `D:\tarkov map\TarkovMap\bin\Release\net10.0-windows\TarkovMap.exe`。
- 终端中文输出乱码：管道 `iconv -f GBK -t UTF-8//IGNORE`。
- **自动测试**：位于 `TarkovMap.Tests/`，覆盖客户端算法以及 MapData Schema、来源适配、Validation、打包、图标、GUI 工作区和审批生成；不以自动化代替地图人工目测。
  运行：`dotnet test TarkovMap.Tests/TarkovMap.Tests.csproj -c Release`。改动截图解析、朝向、坐标换算、边界判定后务必跑一遍。
- **逐图校准工具**：`Tools/RotationCalibrator`（开发期 console，复用客户端解析/坐标逻辑，不进入客户端）。
  运行：`dotnet run --project Tools/RotationCalibrator -- <map.json> <截图A> <截图B> [<A2> <B2> ...]`。
  方法见《TarkovMap_coordinateRotation_实测校准方法.md》：直线移动双截图反推 coordinateRotation；本工具只测算与提示，**绝不自动写入 RotationOverrides**。
- WinForms 分析器把 **WFO1000 当 error**（项目开了 warnings-as-errors）：Form 上新增公共属性必须加
  `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]`，否则编译炸。
- **Builder GUI 高 DPI 教训**：不能在 WinForms 进程内直接运行 SVG 栅格化，同一固定 SVG 会因 DPI 上下文产生不同 PNG。`MapPackBuilder.Gui` 必须继续调用独立 `MapPackBuilder.exe` 子进程；不要改回进程内调用。

## 2. 自测截图工具（Tools/，全部 PowerShell 全路径调用）

PS 路径：`C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`。

- `screenshot-one.ps1`：PrintWindow 截主窗口；参数 `-ExePath -OutPath -InjectFile -DestName`，注入定位截图用 Copy-Item **必须用 -LiteralPath**（截图文件名含 `[]`，通配符会炸）。
- `screenshot-all-windows.ps1`：枚举进程所有顶层窗口分别截图（测小地图用）。
- **PS 脚本里不能写中文注释**：无 BOM 的 UTF-8 被 PS5.1 当 GBK 读，会吞行导致脚本损坏。
- 用户真实截图目录：`C:\Users\whalien\Documents\Escape from Tarkov\Screenshots`。

## 3. 已修复的典型 Bug（修复定式，复发时照查）

### 3.1 朝向/箭头方向（历史上反复修，已锁定公式）

- **定稿公式**：箭头角度 = **Yaw + coordinateRotation + 90°**（工厂/中心区/海关三张图实测通过）。
- Yaw 由截图文件名四元数算出，公式在 `Services/PlayerDirectionService.cs`（源自 ref/Tarkov_webmap 的 ScreenshotCoordinateParser）。
- **每张地图的 coordinateRotation 可能错**：修正表在 `Tools/MapPackBuilder/Program.cs` 的 `RotationOverrides`（重生成数据不丢）。已修正：ground-zero 180→90、customs 180→90。
- **两张 map.json 要同步改**：源 `TarkovMap/Data/maps/<id>/map.json` 和 bin 输出目录那份。
- 用户报方向偏时的标准流程：让用户在已知地标**正对/背对**各截一张图 → 算 Yaw 差 → 调 `RotationOverrides`。
- 调试现象参考：箭头刚好反 180° = rotation 差 180；偏 90° = 缺 +90° 常量。

### 3.2 性能 / 内存

- 实验室地图曾 30MB→200MB 卡顿。定式：**PNG 最大边长压到 3000px** + 画布只画可见区域（视口裁剪）+ 事件驱动绘制（空闲不 Invalidate）。修后约 60MB。
- 小地图与主界面**共享同一份 Bitmap**（MapViewState），实测开小地图内存增量仅约 0.2MB。新增窗口/画布时禁止二次加载底图。

### 3.3 WinForms 高 DPI / 布局

- 项目 PerMonitorV2。**高 DPI 下单选按钮（RadioButton）会重叠** → 设置项一律用下拉框 ComboBox。
- 下拉框/按钮文字被遮挡：容器宽度不够或 AutoSize 顺序问题，加宽并复查。
- 窗口最大化地图未同步放大：Resize 事件里要做 Fit 适配。

### 3.4 截图文件名解析

- 格式：`YYYY-MM-DD[HH-mm]_X, Y, Z_q0, q1, q2, q3_FOV (序号).png`。
- 约 5% 文件无坐标（结算/仓库画面）→ TryParse=false 直接跳过，不提示不报错。
- 坐标与当前地图 bounds 不符 → 只在状态栏提示，绝不弹窗打断。

### 3.5 C# 编辑引发的编译事故（AI 自身操作教训）

- 大块插入新方法时曾把方法插进另一方法的注释/签名中间，产生约 50 个连环语法错误。
  定式：插入点选**完整方法之间的空行**；Edit 后立刻 build 验证，不要连改多处再编译。
- 注释与代码被并到同一行（缺换行）也会连锁报错，报错起点行号即断行处。

## 4. 架构要点（改代码前必读）

- `MapViewState`（Services/）：主图与小地图的共享状态（当前地图、Bitmap、玩家位置/朝向）。**单一数据源**，两边画布都只读它。
- `MapCanvas`（主画布）/ `MiniMapCanvas` + `Forms/MiniMapForm`（小地图）。
- 小地图生命周期：用户关小地图 → `OnFormClosing` 拦截 → Cancel+Hide+触发 `UserClosed` → 主界面取消勾选；主程序退出时 `AllowClose=true` 放行。
- 小地图 Marker 规则（用户定的）：固定显示 撤离点+Boss+危险区+地区名，**与主图勾选解耦**。
- Marker 数据流：`json.tarkov.dev/pve` + 固定 SVG 提交 + `calibration-v1.1.1.json` → `Tools/MapPackBuilder` → 独立测试包 → Validation/Diff → 人工验收 → ZIP → 原子应用到 `Data`。`ref/` 入口只为旧基线回归保留，不是当前更新主链。
- 客户端红线：不碰游戏进程、不读内存、不注入、运行时不联网、无 Overlay，只读截图文件名。Builder 仅在人工维护数据时联网，不能把网络依赖带进客户端。

## 5. MapData 日常更新（优先使用 GUI）

1. 运行 `dotnet run --project Tools/MapPackBuilder.Gui`；发布版可直接双击 `TarkovMap.MapDataBuilder.exe`。
2. 确认 GUI 自动识别的正式 Data 和建议版本号。
3. 点“获取数据”与“构建 MapData”；输出只进入“文档/TarkovMap MapData Builds”，不得直接覆盖正式 Data。
4. 打开 Validation + Diff 报告。任何 Error 都要先查原因；超过 30% 的变化必须结合来源和独立客户端目测。
5. 实际检查海关、中心区、街区、实验室后，项目所有者才可点“确认验收”。AI 不得代替所有者给出人工结论。
6. 点“导出 ZIP”；CLI 会再次校验、打包并解包复验。
7. 点“应用到正式程序”；当前 Data 会成为唯一 `Data.backup`。应用后运行客户端冒烟和全部测试。
8. 若异常，点“恢复上一版本”。恢复会消耗备份槽；不要手工移动或删除这两个目录。

CLI、审批格式、快照重放与故障返回码详见 `Tools/MapPackBuilder/README.md`。新地图只允许报告提示，完成底图、Bounds、方向和截图定位验收前不得启用。

## 6. 客户端发布打包流程（v1.1.1 实测）

1. 运行根目录 `package-client.cmd`；脚本使用相对路径发布 Release，并生成 `dist/TarkovMap-v1.1.1.zip`。
2. 打包内容 = `TarkovMap.exe/dll/deps.json/runtimeconfig.json` + 正式 `Data/` + `README.md` + `NOTICE.md`；**不带 `Config/`、`Logs/`、`.pdb` 或 `Data.backup/`**（Config 首次运行自动生成，带旧配置会污染用户设置）。
3. 先用 `git status --short` 审计工作区，只显式暂存本次文件；禁止在存在用户目录或无关改动时直接 `git add -A`。只有明确发布客户端时才创建 `vX.Y` 标签。
4. ZIP 命名 `TarkovMap-vX.Y.zip`，统一放在被 Git 忽略的 `dist/`；正式对外上传前再次从 ZIP 解压启动，而不是直接测试 bin 目录。

## 7. 悬挂待办

- 立交桥"河畔之路（信号弹）"撤离点坐标是**估算值**，待用户本地模式实测截图校准。
- 其余地图（街区/海岸线/森林/灯塔/储备站/实验室/迷宫）的 coordinateRotation 未逐一实测，方向偏了按 §3.1 流程处理。
- 楼层/高度机制、游戏内时钟等迭代方向见《后续迭代借鉴记录.md》。

## 8. 用户协作规则（必须遵守）

- 用户 **0 编程基础**：每阶段给「实现功能 / 大概原理 / 傻瓜验收清单」，验收清单只需双击 exe 操作，不要让用户碰命令行。
- 人工验收结论必须来自用户；AI 可以做自动验证、准备独立客户端和总结差异，但不得自行点击 GUI 的“确认验收”。
- 所有交付物放工作区 `D:\tarkov map\`。
- 永远保留用户原有的未跟踪目录和无关改动；当前已知 `BOSS/`、`塔科夫地图/` 不属于 MapData 提交范围。
