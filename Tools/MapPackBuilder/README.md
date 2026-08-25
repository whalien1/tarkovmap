# MapPackBuilder

MapPackBuilder 是 TarkovMap 的独立地图数据构建工具。新版流程只生成 PvE MapData，并且默认输出到调用者指定的测试目录，不会覆盖正式 `TarkovMap/Data`。

## 生成整批 PvE 测试包

```powershell
dotnet run --project Tools/MapPackBuilder -- pve-build <新的测试包目录> <YYYY.MM.DD.N-pve> <现有Data目录> [审批文件]
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
- 生成结束自动执行 Validation + Diff；即使存在阻断项也会保留测试包和报告，但不允许进入正式打包。

## 验证已有测试包

```powershell
dotnet run --project Tools/MapPackBuilder -- pve-validate <测试包目录> [基线文件] [审批文件]
```

返回码 `0` 表示允许正式打包，`2` 表示存在 Error、已阻止正式打包，`1` 表示命令自身执行失败。验证范围包括 manifest 和来源快照哈希、地图 JSON、底图文件及尺寸、Bounds、Marker 必填字段与重复 ID、越界比例，以及 8 个核心类别相对基线的数量变化。

单张地图的单个核心类别变化绝对值超过 30% 时，必须使用精确审批才能放行。审批只匹配指定数据版本和指定的新旧数量；上游再次变化后旧审批自动失效：

```json
{
  "schemaVersion": 1,
  "dataVersion": "2026.08.25.1-pve",
  "approvals": [
    {
      "mapId": "ground-zero",
      "markerType": "spawn_scav",
      "baselineCount": 114,
      "currentCount": 24,
      "reason": "人工复核 PvE 数据源和地图后确认",
      "confirmedAt": "2026-08-25T12:00:00+08:00"
    }
  ]
}
```

审批文件必须由人工复核后创建；Builder 不会自动批准数量骤变。

## 只保存 PvE API 快照

```powershell
dotnet run --project Tools/MapPackBuilder -- pve-fetch <快照目录> <YYYY.MM.DD.N-pve>
```

## 测试包结构

```text
测试包目录/
├─ Data/                  # 可供客户端读取的 MapData
├─ snapshots/             # API、SVG、许可证和校准配置原始快照
├─ build-report.json      # 地图、点位、图片来源和新地图发现报告
├─ validation-report.json # 供自动流程读取的完整验证结果
└─ validation-report.md   # 供人工验收阅读的中文差异报告
```

SVG 地图来自 `the-hideout/tarkov-dev-svg-maps`，采用 CC BY-NC-SA 4.0，并额外禁止用于作弊或获取不公平优势。生成的 Data 内含 `THIRD_PARTY_NOTICES.md`，原始许可证保存在来源快照中。
