# TarkovMap MapData Schema v1

**状态：** 已冻结用于 PvE MapData 第一轮开发  
**日期：** 2026-08-25  
**兼容基线：** TarkovMap v1.1.1

## 1. 兼容原则

Schema v1 在现有 `Data/` 结构上增量扩展：

- 保留 `maps.json`。
- 保留 `maps/<id>/map.json` 与 `map.png`。
- 保留现有 `image`、`worldBounds` 和 Marker Type。
- 新增根目录 `manifest.json`。
- 旧版 Data 没有 `manifest.json` 时，客户端继续按 v1.1.1 方式加载。
- 存在 `manifest.json` 时，客户端必须先验证 Schema、数据版本、游戏模式、来源快照和内容 Hash。

## 2. 目录结构

```text
MapData/
├─ manifest.json
├─ maps.json
├─ icons/
└─ maps/
   └─ customs/
      ├─ map.json
      └─ map.png
```

来源原始快照属于 Builder 工作目录和复现材料，不要求复制进运行时 MapData ZIP；`manifest.json` 通过相对位置、版本和 SHA-256 引用它们。

## 3. manifest.json

```json
{
  "schemaVersion": 1,
  "dataVersion": "2026.08.25.1-pve",
  "gameMode": "pve",
  "generatedAt": "2026-08-25T12:00:00+08:00",
  "sources": [
    "json.tarkov.dev",
    "the-hideout/tarkov-dev-svg-maps",
    "TarkovMap calibration metadata"
  ],
  "sourceSnapshots": [
    {
      "name": "json.tarkov.dev/pve/maps",
      "location": "snapshots/2026.08.25.1-pve/maps.json",
      "revision": "sha256:...",
      "retrievedAt": "2026-08-25T11:30:00+08:00",
      "sha256": "64位十六进制SHA-256"
    }
  ],
  "contentHash": "64位十六进制SHA-256"
}
```

### 3.1 schemaVersion

当前固定为 `1`。客户端遇到其他值必须停止加载并提示更新主程序。

### 3.2 dataVersion

格式固定为：

```text
YYYY.MM.DD.N-pve
```

- 日期必须有效。
- `N` 从 1 开始，代表当天第几个数据版本。
- `-pve` 必须与 `gameMode` 一致。

### 3.3 gameMode

Schema v1 当前只接受 `pve`。不生成 PvP/PvE 双数据包，也不在客户端提供模式切换。

### 3.4 sourceSnapshots

每个正式 MapData 至少记录一个来源快照。每项必须包含名称、位置、修订标识、获取时间和 SHA-256。

### 3.5 contentHash

代表除 `manifest.json` 自身外，运行时 MapData 文件集合的确定性 SHA-256。具体文件排序与组合算法由 Package 阶段固定，避免 manifest 自引用。

## 4. maps.json

保持现有结构：

```json
{
  "schemaVersion": 1,
  "maps": [
    {
      "id": "customs",
      "name": "海关",
      "directory": "maps/customs",
      "enabled": true
    }
  ]
}
```

上游新地图只能以 `enabled: false` 或构建报告提示的形式出现；完成底图、Bounds 和截图定位验证前不得自动启用。

## 5. map.json

运行时核心字段保持兼容：

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

`defaultFloor` 与 `floors` 为预留字段，客户端 v1.1.1 可以忽略；第一轮不实现楼层 UI。

## 6. Marker

```json
{
  "id": "extract_pmc_0123456789abcdefabcd",
  "type": "extract_pmc",
  "name": "ZB-1011",
  "x": 621.5,
  "z": -128.6,
  "floor": null,
  "metadata": {
    "source": "json.tarkov.dev/pve/maps"
  }
}
```

核心类型：

- `extract_pmc`
- `extract_scav`
- `extract_shared`
- `extract_transit`
- `spawn_pmc`
- `spawn_scav`
- `boss`
- `hazard`

继续兼容的现有类型包括 `lock`、`stationary_weapon`、`label`、`loot_container`。

上游有稳定 ID 时优先复用；没有 ID 时使用来源、地图 ID、类型、名称和两位小数坐标生成确定性 SHA-256 ID。禁止使用进程相关 Hash。

## 7. 客户端加载规则

```text
manifest.json 不存在
  → 按 v1.1.1 旧数据兼容加载

manifest.json 存在
  → 解析
  → 验证 schemaVersion = 1
  → 验证 dataVersion 格式
  → 验证 gameMode = pve
  → 验证来源快照与 SHA-256 字段
  → 加载 maps.json
```

任一 manifest 硬校验失败时，客户端不得继续加载看似完整但协议不兼容的数据包。

## 8. Schema 升级

以下变化需要提高 `schemaVersion`：

- 删除或重命名现有必需字段。
- 改变 Bounds 或坐标含义。
- 改变 Marker 核心字段含义。
- 引入旧客户端不能安全忽略的结构。

只增加旧客户端可忽略的可选字段，不需要升级 Schema。
