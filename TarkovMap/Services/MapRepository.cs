using System.Text.Json;
using TarkovMap.Models;

namespace TarkovMap.Services;

/// <summary>
/// 读取 Data/ 下的地图清单与单张地图数据，负责加载地图 PNG。
/// </summary>
public sealed class MapRepository
{
    private readonly string _dataDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public MapRepository(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
    }

    /// <summary>读取 Data/maps.json 清单。</summary>
    public IReadOnlyList<MapListEntry> LoadMapList()
    {
        var file = Path.Combine(_dataDirectory, "maps.json");
        var json = File.ReadAllText(file);
        var list = JsonSerializer.Deserialize<MapList>(json, JsonOptions);
        return list?.Maps ?? [];
    }

    /// <summary>读取单张地图的 map.json。</summary>
    public MapDefinition LoadMapDefinition(string directory)
    {
        var mapDir = Path.Combine(_dataDirectory, directory);
        var file = Path.Combine(mapDir, "map.json");
        var json = File.ReadAllText(file);
        var def = JsonSerializer.Deserialize<MapDefinition>(json, JsonOptions)
                  ?? throw new InvalidDataException($"地图数据为空：{file}");
        def.Directory = mapDir;
        return def;
    }

    /// <summary>加载地图 PNG（调用方负责 Dispose）。</summary>
    public Bitmap LoadMapImage(MapDefinition map)
    {
        var file = Path.Combine(map.Directory, map.Image.File);
        // 读进内存流再构造 Bitmap，避免文件被句柄锁定
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new Bitmap(stream);
    }
}
