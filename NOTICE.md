# 第三方资源与版权说明

本仓库是《逃离塔科夫》本地互动地图工具 TarkovMap 的个人、非商业项目。运行时程序保持离线；MapPackBuilder 仅在维护地图数据时联网获取来源材料。

## 1. SVG 地图底图

- 来源：[the-hideout/tarkov-dev-svg-maps](https://github.com/the-hideout/tarkov-dev-svg-maps)，当前正式数据固定提交 `5a8b6115d1c0cf56f2ebaac1a96fa5ae3074d178`。
- 范围：10 张 `Data/maps/*/map.png`；迷宫除外。
- 授权：Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International（CC BY-NC-SA 4.0）。
- 修改：选择配置的主楼层、栅格化、按 TarkovMap Bounds 调整尺寸，并在需要时旋转。
- 要求：署名、非商业、相同方式共享；同时遵守上游关于不得用于作弊或获取不公平优势的说明。

每个正式包的准确提交、文件哈希和许可证原文保存在来源快照中，运行时 `Data/THIRD_PARTY_NOTICES.md` 记录对应提交。

## 2. 迷宫底图

`Data/maps/the-labyrinth/map.png` 暂时沿用 TarkovMap v1.1.1 的既有 PNG，历史来源链为 Re5pawnn/Tarkov_webmap 与 the-hideout 社区地图材料。该图没有进入当前 SVG 快照生成链；公开再分发前应单独复核其原始授权，或替换为来源清晰的自制/授权底图。

## 3. 点位数据

- 来源：[json.tarkov.dev](https://json.tarkov.dev) 的 PvE 地图和中文数据接口。
- 范围：撤离点、Transit、PMC/Scav 出生点、Boss、危险区。
- 处理：MapPackBuilder 按固定分类语义转换、生成稳定 ID，并执行 Bounds、字段、数量 Diff 与来源哈希校验。

点位响应原文、获取时间与 SHA-256 保存在正式包的 `snapshots/` 中。项目不对游戏或社区整理数据主张所有权；公开使用时应同时检查 tarkov.dev 与游戏权利方的适用条款。

## 4. TarkovMap 自有 Marker 图标

`Data/icons/*.png` 的 8 个核心 Marker 图标由本仓库 `Tools/MapPackBuilder/Assets/MarkerIconAssetGenerator.cs` 使用几何绘图指令生成，不使用字体字形、第三方图片或生成式图片。旧版来源许可不明确的撤离图标已全部移除。

## 5. 截图文件名解析参考

截图文件名格式与四元数转朝向的实现曾参考 Re5pawnn/Tarkov_webmap 的 `ScreenshotCoordinateParser`。当前代码只解析游戏自行生成的截图文件名，不读取图片内容、游戏进程或游戏内存。

## 6. 本项目代码

除上述第三方材料外，本仓库代码未附带开源许可证，默认保留所有权利。若计划公开再分发或商用，应先完成独立法律与授权审查；本说明不构成法律意见。
