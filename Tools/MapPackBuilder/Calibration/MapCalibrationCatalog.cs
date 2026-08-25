using System.Text.Json;

namespace MapPackBuilder.Calibration;

internal sealed record MapCalibration(
    string MapId,
    string? SvgAsset,
    string? SvgLayer,
    int SvgRotationDegrees,
    double X0,
    double Z0,
    double X1,
    double Z1,
    bool ReverseCoordinate,
    double CoordinateRotation,
    double? MinY,
    double? MaxY);

internal sealed class MapCalibrationCatalog
{
    private sealed class CatalogDocument
    {
        public int SchemaVersion { get; set; }
        public string SourceBaseline { get; set; } = "";
        public List<MapCalibration> Maps { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyDictionary<string, MapCalibration> _maps;

    private MapCalibrationCatalog(string sourceBaseline, IReadOnlyDictionary<string, MapCalibration> maps)
    {
        SourceBaseline = sourceBaseline;
        _maps = maps;
    }

    public string SourceBaseline { get; }
    public IReadOnlyCollection<MapCalibration> Maps => _maps.Values.ToArray();

    public static MapCalibrationCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("找不到地图校准配置。", path);
        }

        var document = JsonSerializer.Deserialize<CatalogDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("地图校准配置为空。");
        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException($"不支持的地图校准 schemaVersion：{document.SchemaVersion}。");
        }

        var result = new Dictionary<string, MapCalibration>(StringComparer.Ordinal);
        foreach (var map in document.Maps)
        {
            Validate(map);
            if (!result.TryAdd(map.MapId, map))
            {
                throw new InvalidDataException($"地图校准配置包含重复 Map ID：{map.MapId}。");
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException("地图校准配置没有任何地图。");
        }

        return new MapCalibrationCatalog(document.SourceBaseline, result);
    }

    public bool TryGet(string mapId, out MapCalibration calibration) =>
        _maps.TryGetValue(mapId, out calibration!);

    private static void Validate(MapCalibration map)
    {
        if (string.IsNullOrWhiteSpace(map.MapId))
        {
            throw new InvalidDataException("地图校准配置包含空 Map ID。");
        }

        var values = new[] { map.X0, map.Z0, map.X1, map.Z1, map.CoordinateRotation };
        if (values.Any(value => !double.IsFinite(value)) || map.X0 == map.X1 || map.Z0 == map.Z1)
        {
            throw new InvalidDataException($"地图 {map.MapId} 的 Bounds 或 Rotation 无效。");
        }

        if (map.SvgRotationDegrees is not (0 or 90 or 180 or 270))
        {
            throw new InvalidDataException($"地图 {map.MapId} 的 SVG 图像旋转角无效。");
        }

        if (map.SvgAsset is not null && string.IsNullOrWhiteSpace(map.SvgLayer))
        {
            throw new InvalidDataException($"地图 {map.MapId} 使用 SVG，但没有指定主楼层。");
        }

        if ((map.MinY is null) != (map.MaxY is null) ||
            map.MinY is double minY && map.MaxY is double maxY &&
            (!double.IsFinite(minY) || !double.IsFinite(maxY) || minY > maxY))
        {
            throw new InvalidDataException($"地图 {map.MapId} 的高度范围无效。");
        }
    }
}
