using System.Text.Json;

namespace MapPackBuilder.Validation;

internal static class MapDataBaselineWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void Write(string dataDirectory, string outputFile)
    {
        dataDirectory = Path.GetFullPath(dataDirectory);
        var manifest = new TarkovMap.Services.MapRepository(dataDirectory).LoadManifest()
                       ?? throw new InvalidDataException("新基线要求 Data 包含 manifest.json。");
        using var listDocument = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(dataDirectory, "maps.json")));
        var maps = new List<object>();
        foreach (var entry in listDocument.RootElement.GetProperty("maps").EnumerateArray())
        {
            var id = entry.GetProperty("id").GetString()
                     ?? throw new InvalidDataException("maps.json 存在空地图 ID。");
            var directory = entry.GetProperty("directory").GetString()
                            ?? throw new InvalidDataException($"地图 {id} 缺少目录。");
            using var mapDocument = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(dataDirectory, directory, "map.json")));
            var map = mapDocument.RootElement;
            var image = map.GetProperty("image");
            var bounds = map.GetProperty("worldBounds");
            var markers = map.GetProperty("markers");
            var markerTypes = markers.EnumerateArray()
                .GroupBy(marker => marker.GetProperty("type").GetString()!, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            maps.Add(new
            {
                id,
                imageWidth = image.GetProperty("width").GetInt32(),
                imageHeight = image.GetProperty("height").GetInt32(),
                x0 = bounds.GetProperty("x0").GetDouble(),
                z0 = bounds.GetProperty("z0").GetDouble(),
                x1 = bounds.GetProperty("x1").GetDouble(),
                z1 = bounds.GetProperty("z1").GetDouble(),
                reverseCoordinate = bounds.GetProperty("reverseCoordinate").GetBoolean(),
                coordinateRotation = bounds.GetProperty("coordinateRotation").GetDouble(),
                markerCount = markers.GetArrayLength(),
                markerTypes
            });
        }

        var content = JsonSerializer.SerializeToUtf8Bytes(new
        {
            baselineVersion = manifest.DataVersion,
            mapCount = maps.Count,
            maps
        }, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFile))!);
        File.WriteAllBytes(outputFile, content);
    }
}
