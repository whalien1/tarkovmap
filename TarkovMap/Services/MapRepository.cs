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

    /// <summary>
    /// 读取并校验可选的 manifest.json。旧版 Data 没有 manifest 时保持兼容并返回 null。
    /// </summary>
    public MapDataManifest? LoadManifest()
    {
        var file = Path.Combine(_dataDirectory, "manifest.json");
        if (!File.Exists(file))
        {
            return null;
        }

        var json = File.ReadAllText(file);
        var manifest = JsonSerializer.Deserialize<MapDataManifest>(json, JsonOptions)
                       ?? throw new InvalidDataException($"MapData manifest 为空：{file}");
        MapDataManifestValidator.Validate(manifest);
        return manifest;
    }

    /// <summary>读取 Data/maps.json 清单。</summary>
    public IReadOnlyList<MapListEntry> LoadMapList()
    {
        _ = LoadManifest();
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
        // 返回独立副本，避免 Bitmap 在后续绘制时依赖已释放的源文件流。
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var source = new Bitmap(stream);
        return new Bitmap(source);
    }
}
