# 第三方资源与版权说明

本仓库为《逃离塔科夫》本地互动地图工具（TarkovMap）的个人项目发布/参考。以下列出所包含第三方资源的版权与授权状态。

## 1. 地图图片与点位数据

- **来源**：[the-hideout/tarkov-dev-svg-maps](https://github.com/the-hideout/tarkov-dev-svg-maps)（作者 **Shebuka**）；[tarkov.dev](https://tarkov.dev)
- **整理**：[Re5pawnn/Tarkov_webmap](https://github.com/Re5pawnn/Tarkov_webmap)
- **授权**：CC BY-NC-SA 4.0（署名-非商业性使用-相同方式共享）
- **范围**：`Data/maps/*/map.png`、`map.json`、`Data/maps.json`
- **要求**：署名原作者；仅限非商业性使用；衍生作品须同样以 CC BY-NC-SA 4.0 共享。

## 2. 撤离点图标

- **来源**：Re5pawnn/Tarkov_webmap（`assets/map-icons`）
- **授权**：该仓库未附带许可证，来源授权情况不明。
- **状态**：按“个人自用”保留；作者不对公开再分发此图标承担授权承诺；公开分发或商用前请替换或自行绘制。

## 3. 截图文件名解析算法参考

- **参考**：Re5pawnn/Tarkov_webmap（`TarkovMapLocator.Core/Maps/ScreenshotCoordinateParser.cs`）
- **说明**：本项目的小地图朝向（四元数→朝向角）与截图文件名解析逻辑参考该实现。所用为标准四元数→朝向公式与文件名正则，通常不属受著作权保护的表达；如需完全独立，可自行重写。

## 4. 本项目自身

- 本仓库代码**未附带开源许可证，默认保留所有权利**（作者保留权利，未进行开源授权）。

> **提示**：如需将此项目用于商业用途或公开再分发，请先替换第 2 项图标，并仔细审阅第 1、3 项的授权要求。
