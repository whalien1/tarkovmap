# MapPackBuilder

MapPackBuilder 是 TarkovMap 的独立地图数据构建工具。新版流程只生成 PvE MapData，并且默认输出到调用者指定的测试目录，不会覆盖正式 `TarkovMap/Data`。

## 生成整批 PvE 测试包

```powershell
dotnet run --project Tools/MapPackBuilder -- pve-build <新的测试包目录> <YYYY.MM.DD.N-pve> <现有Data目录>
```

示例：

```powershell
dotnet run --project Tools/MapPackBuilder -- pve-build .phase4-test-pack 2026.08.25.1-pve TarkovMap/Data
```

安全规则：

- 测试包目录必须不存在；Builder 不会清空或覆盖已有目录。
- 现有 Data 只作为迷宫 PNG 和临时运行时图标的兼容来源。
- 10 张有 SVG 的地图使用上游当前提交；迷宫继续使用现有 PNG。
- SVG 只渲染校准配置指定的主楼层，并按 Bounds 生成兼容宽高比。
- 生成完成后使用 TarkovMap 自身的运行时读取器加载 manifest、地图 JSON 和全部图片。

## 只保存 PvE API 快照

```powershell
dotnet run --project Tools/MapPackBuilder -- pve-fetch <快照目录> <YYYY.MM.DD.N-pve>
```

## 测试包结构

```text
测试包目录/
├─ Data/                  # 可供客户端读取的 MapData
├─ snapshots/             # API、SVG、许可证和校准配置原始快照
└─ build-report.json      # 地图、点位、图片来源和新地图发现报告
```

SVG 地图来自 `the-hideout/tarkov-dev-svg-maps`，采用 CC BY-NC-SA 4.0，并额外禁止用于作弊或获取不公平优势。生成的 Data 内含 `THIRD_PARTY_NOTICES.md`，原始许可证保存在来源快照中。
