# PvE Spawn 分类语义审计

## 结论

中心区 `spawn_scav 114 → 24` 不是 API 字段丢失，而是旧适配规则错误理解了上游 `categories`：该字段是多用途标签集合，不能在出现 `boss` 或 `sniper` 时无条件丢弃整条 Spawn。

已将全部地图统一改为与同一数据源的 tarkov.dev 地图页一致的分类优先级：

1. `boss` 优先；存在实际 Boss 配置时由 Boss Marker 表示，没有 Boss 配置的 `bot + scav` 区域回退为 Scav 出生点。
2. 其次是 `player`；`sides=pmc/all` 归为 PMC 出生点，即使同时含 `sniper`。
3. 纯 `sniper` 属于独立狙击 Scav 类别；当前 Schema 没有该类型，暂不输出。
4. 普通 `bot/all + sides=scav` 归为 Scav 出生点。
5. `player + sides=scav` 等不符合上游显示规则的组合不再误标为 Scav。

依据是上游 tarkov.dev 在提交 `d3dc9b8401c9a4312dc5cd6b4e52e0a4e398a5cb` 的地图渲染实现：`src/pages/map/index.jsx` 第 1236–1305 行。该实现明确先判断 `boss`，再判断 `player`，最后判断 `sniper` 和普通 Scav。

上游链接：https://github.com/the-hideout/tarkov-dev/blob/d3dc9b8401c9a4312dc5cd6b4e52e0a4e398a5cb/src/pages/map/index.jsx#L1236-L1305

## 中心区原始证据

`2026.08.25.2-pve` 的普通中心区原始快照包含 192 条 Spawn：

| categories | sides | 数量 | 新规则 |
|---|---|---:|---|
| `player, sniper` | `all` | 100 | PMC；主楼层内 90 |
| `boss, bot, player` | `scav` | 64 | 无实际 Boss 位置配置，回退 Scav；主楼层内 64 |
| `botpmc, player` | `scav` | 27 | 不符合玩家 sides，跳过 |
| `bot` | `scav` | 1 | 普通 Scav，但位于当前主楼层高度外 |

因此修正后中心区核心出生点是 `spawn_pmc 90`、`spawn_scav 64`，不再是旧适配器生成的 `spawn_pmc 0`、`spawn_scav 24`。

## 全地图影响

使用已验证的 `.2` 原始快照离线重放为 `2026.08.25.3-pve` 后，Validation 得到 Error 10 / Warning 11 / Info 7。10 个 Error 全是 Scav Spawn 相对旧 v1.1.1 基线超过 30%；新增 PMC Spawn 出现在工厂 50、中心区 90、灯塔 71、储备站 67、街区 196、实验室 16、迷宫 5。

这说明旧基线曾把大量 `player` 或 `botpmc` 多用途区域归为 Scav，而不是本次只对中心区发生异常。按此前决定“检查后发现现有数据源更新则全部更新为现有数据源”，修正采用全地图统一规则，没有给中心区设置特例。

## 当前状态

- 适配器语义已修正并通过自动测试。
- `pve-replay` 已能校验并重放保存的 API、SVG、许可证和校准快照，不依赖实时网络。
- 当前项目共 55 个自动测试，全部通过。
- 新测试包仍被 Validation 阻止，未应用到正式 `TarkovMap/Data`。
- 下一步需要对代表地图实测 PMC/Scav 图层，并据此建立新版基线；不能对 10 个阻断项批量盲目审批。
