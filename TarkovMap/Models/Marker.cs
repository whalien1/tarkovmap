using System.Text.Json.Serialization;

namespace TarkovMap.Models;

public enum MarkerType
{
    ExtractPmc,
    ExtractScav,
    ExtractShared,
    ExtractTransit,
    SpawnPmc,
    SpawnScav,
    Boss,
    LootContainer,
    Lock,
    Hazard,
    StationaryWeapon,
    Label
}

public static class MarkerTypeNames
{
    public static string Of(MarkerType type) => type switch
    {
        MarkerType.ExtractPmc => "PMC 撤离点",
        MarkerType.ExtractScav => "Scav 撤离点",
        MarkerType.ExtractShared => "共用撤离点",
        MarkerType.ExtractTransit => "转移点",
        MarkerType.SpawnPmc => "PMC 出生点",
        MarkerType.SpawnScav => "Scav 出生点",
        MarkerType.Boss => "Boss",
        MarkerType.LootContainer => "物资容器",
        MarkerType.Lock => "门锁 / 钥匙",
        MarkerType.Hazard => "危险区域",
        MarkerType.StationaryWeapon => "固定武器",
        MarkerType.Label => "地图标注",
        _ => "未知"
    };
}

public sealed class Marker
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>JSON 中的蛇形命名，如 extract_pmc。</summary>
    [JsonPropertyName("type")]
    public string TypeRaw { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("z")]
    public double Z { get; set; }

    /// <summary>危险区轮廓多边形（世界坐标 [x,z] 列表）。</summary>
    [JsonPropertyName("outline")]
    public List<double[]>? Outline { get; set; }

    [JsonIgnore]
    public MarkerType Type => TypeRaw switch
    {
        "extract_pmc" => MarkerType.ExtractPmc,
        "extract_scav" => MarkerType.ExtractScav,
        "extract_shared" => MarkerType.ExtractShared,
        "extract_transit" => MarkerType.ExtractTransit,
        "spawn_pmc" => MarkerType.SpawnPmc,
        "spawn_scav" => MarkerType.SpawnScav,
        "boss" => MarkerType.Boss,
        "loot_container" => MarkerType.LootContainer,
        "lock" => MarkerType.Lock,
        "hazard" => MarkerType.Hazard,
        "stationary_weapon" => MarkerType.StationaryWeapon,
        "label" => MarkerType.Label,
        _ => MarkerType.Label
    };
}
