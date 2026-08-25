using System.Text.Json.Serialization;
using MapPackBuilder.Calibration;
using MapPackBuilder.Sources;

namespace MapPackBuilder.Output;

internal sealed class RuntimeMarker
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("z")]
    public double Z { get; init; }

    [JsonPropertyName("outline")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double[]>? Outline { get; init; }

    [JsonPropertyName("metadata")]
    public object Metadata { get; init; } = new { source = RuntimeMapProjector.SourceTag };
}

internal static class RuntimeMapProjector
{
    internal const string SourceTag = "json.tarkov.dev/pve/maps";
    private static readonly HashSet<string> ExcludedBosses = new(StringComparer.OrdinalIgnoreCase)
    {
        "寻血猎犬", "Bloodhound", "Bloodhounds"
    };

    public static IReadOnlyList<RuntimeMarker> Project(
        TarkovDevMap map,
        MapCalibration calibration)
    {
        if (!string.Equals(map.MapId, calibration.MapId, StringComparison.Ordinal))
        {
            throw new ArgumentException("地图数据与校准配置不匹配。", nameof(calibration));
        }

        var markers = new List<RuntimeMarker>();
        foreach (var extract in map.Extracts)
        {
            var type = extract.Faction switch
            {
                "pmc" => "extract_pmc",
                "scav" => "extract_scav",
                _ => "extract_shared"
            };
            markers.Add(Create(map.MapId, type, extract.Name, extract.Position, extract.Id));
        }

        foreach (var transit in map.Transits)
        {
            markers.Add(Create(map.MapId, "extract_transit", transit.Name, transit.Position, transit.Id));
        }

        foreach (var spawn in map.Spawns)
        {
            if (!InMainHeightRange(spawn.Position, calibration))
            {
                continue;
            }

            var type = ClassifySpawn(spawn, map);
            if (type is not null)
            {
                markers.Add(Create(map.MapId, type, spawn.ZoneName, spawn.Position, ""));
            }
        }

        AddBosses(markers, map);

        foreach (var hazard in map.Hazards)
        {
            var outline = hazard.Outline.Count >= 3
                ? hazard.Outline.Select(position => new[]
                {
                    Math.Round(position.X, 2), Math.Round(position.Z, 2)
                }).ToList()
                : null;
            markers.Add(Create(map.MapId, "hazard", hazard.Name, hazard.Position, hazard.Id, outline));
        }

        return EnsureUniqueIds(markers, map.MapId);
    }

    private static void AddBosses(List<RuntimeMarker> markers, TarkovDevMap map)
    {
        var zones = new Dictionary<string, BossZone>(StringComparer.Ordinal);
        foreach (var boss in map.Bosses.Where(boss => !ExcludedBosses.Contains(boss.Name)))
        {
            foreach (var location in boss.SpawnLocations)
            {
                var spawn = map.Spawns.FirstOrDefault(candidate =>
                    string.Equals(candidate.ZoneName, location.SpawnKey, StringComparison.Ordinal));
                if (spawn is null)
                {
                    continue;
                }

                var key = $"{Math.Round(spawn.Position.X, 1)},{Math.Round(spawn.Position.Z, 1)}";
                if (!zones.TryGetValue(key, out var zone))
                {
                    zone = new BossZone(spawn.Position, location.Name);
                    zones.Add(key, zone);
                }

                if (!zone.Names.Contains(boss.Name, StringComparer.Ordinal))
                {
                    zone.Names.Add(boss.Name);
                }
            }
        }

        foreach (var zone in zones.Values)
        {
            var names = string.Join(" / ", zone.Names);
            var displayName = string.IsNullOrWhiteSpace(zone.LocationName)
                ? names
                : $"{names}（{zone.LocationName}）";
            markers.Add(Create(map.MapId, "boss", displayName, zone.Position, ""));
        }
    }

    /// <summary>
    /// 与 tarkov.dev 地图页保持相同的类别优先级。categories 是多用途标签，
    /// 因此含 player+sniper 的点仍是玩家出生点，不能因出现 sniper 就整条丢弃。
    /// 当前 Schema 没有独立 sniper_scav 类型，纯狙击 Scav 点继续不输出。
    /// </summary>
    private static string? ClassifySpawn(TarkovDevSpawn spawn, TarkovDevMap map)
    {
        if (spawn.Categories.Contains("boss", StringComparer.Ordinal))
        {
            var hasConfiguredBoss = map.Bosses
                .Where(boss => !ExcludedBosses.Contains(boss.Name))
                .Any(boss => boss.SpawnLocations.Any(location =>
                    string.Equals(location.SpawnKey, spawn.ZoneName, StringComparison.Ordinal)));
            return !hasConfiguredBoss &&
                   spawn.Categories.Contains("bot", StringComparer.Ordinal) &&
                   spawn.Sides.Contains("scav", StringComparer.Ordinal)
                ? "spawn_scav"
                : null;
        }

        if (spawn.Categories.Contains("player", StringComparer.Ordinal))
        {
            return spawn.Sides.Contains("pmc", StringComparer.Ordinal) ||
                   spawn.Sides.Contains("all", StringComparer.Ordinal)
                ? "spawn_pmc"
                : null;
        }

        if (spawn.Categories.Contains("sniper", StringComparer.Ordinal))
        {
            return null;
        }

        return spawn.Sides.Contains("scav", StringComparer.Ordinal) &&
               (spawn.Categories.Contains("bot", StringComparer.Ordinal) ||
                spawn.Categories.Contains("all", StringComparer.Ordinal))
            ? "spawn_scav"
            : null;
    }

    private static RuntimeMarker Create(
        string mapId,
        string type,
        string name,
        SourcePosition position,
        string upstreamId,
        List<double[]>? outline = null)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? "未命名点位" : name.Trim();
        return new RuntimeMarker
        {
            Id = string.IsNullOrWhiteSpace(upstreamId)
                ? StableMarkerId.Create(SourceTag, mapId, type, normalizedName, position.X, position.Z)
                : upstreamId,
            Type = type,
            Name = normalizedName,
            X = Math.Round(position.X, 2),
            Z = Math.Round(position.Z, 2),
            Outline = outline
        };
    }

    private static bool InMainHeightRange(SourcePosition position, MapCalibration calibration) =>
        calibration.MinY is null ||
        position.Y >= calibration.MinY.Value && position.Y <= calibration.MaxY!.Value;

    private static IReadOnlyList<RuntimeMarker> EnsureUniqueIds(
        List<RuntimeMarker> markers,
        string mapId)
    {
        var duplicatedSourceIds = markers.GroupBy(marker => marker.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var marker in markers.Where(marker => duplicatedSourceIds.Contains(marker.Id)))
        {
            marker.Id = StableMarkerId.Create(
                SourceTag, mapId, marker.Type, marker.Name, marker.X, marker.Z);
        }

        return markers
            .GroupBy(marker => marker.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private sealed class BossZone(SourcePosition position, string locationName)
    {
        public SourcePosition Position { get; } = position;
        public string LocationName { get; } = locationName;
        public List<string> Names { get; } = [];
    }
}
