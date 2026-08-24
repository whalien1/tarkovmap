using System.Text.Json.Serialization;

namespace TarkovMap.Models;

public sealed class MapImageInfo
{
    [JsonPropertyName("file")]
    public string File { get; set; } = "map.png";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public sealed class MapDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("image")]
    public MapImageInfo Image { get; set; } = new();

    [JsonPropertyName("worldBounds")]
    public WorldBounds Bounds { get; set; } = new();

    [JsonPropertyName("markers")]
    public List<Marker> Markers { get; set; } = [];

    /// <summary>地图目录（相对 Data/），运行时赋值，不来自 JSON。</summary>
    [JsonIgnore]
    public string Directory { get; set; } = "";
}

public sealed class MapListEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("directory")]
    public string Directory { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public sealed class MapList
{
    [JsonPropertyName("maps")]
    public List<MapListEntry> Maps { get; set; } = [];
}
