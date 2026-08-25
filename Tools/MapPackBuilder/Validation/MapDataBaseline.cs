using System.Text.Json;

namespace MapPackBuilder.Validation;

internal sealed record BaselineMap(
    string Id,
    IReadOnlyDictionary<string, int> MarkerTypes);

internal sealed class MapDataBaseline
{
    public required string Version { get; init; }
    public required IReadOnlyDictionary<string, BaselineMap> Maps { get; init; }

    public static MapDataBaseline Load(string file)
    {
        if (!File.Exists(file))
        {
            throw new FileNotFoundException("找不到 MapData 基线文件。", file);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var root = document.RootElement;
        var version = RequiredString(root, "baselineVersion");
        if (!root.TryGetProperty("maps", out var mapsNode) || mapsNode.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("MapData 基线缺少 maps 数组。");
        }

        var maps = new Dictionary<string, BaselineMap>(StringComparer.Ordinal);
        foreach (var node in mapsNode.EnumerateArray())
        {
            var id = RequiredString(node, "id");
            if (!node.TryGetProperty("markerTypes", out var typesNode) ||
                typesNode.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"MapData 基线地图 {id} 缺少 markerTypes。");
            }

            var types = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var property in typesNode.EnumerateObject())
            {
                if (!property.Value.TryGetInt32(out var count) || count < 0)
                {
                    throw new InvalidDataException($"MapData 基线 {id}/{property.Name} 数量无效。");
                }

                if (!types.TryAdd(property.Name, count))
                {
                    throw new InvalidDataException($"MapData 基线 {id} 存在重复 Marker 类别。");
                }
            }

            if (!maps.TryAdd(id, new BaselineMap(id, types)))
            {
                throw new InvalidDataException($"MapData 基线存在重复地图：{id}。");
            }
        }

        if (root.TryGetProperty("mapCount", out var countNode) &&
            countNode.TryGetInt32(out var expectedCount) && expectedCount != maps.Count)
        {
            throw new InvalidDataException(
                $"MapData 基线地图数量不一致：声明 {expectedCount}，实际 {maps.Count}。");
        }

        return new MapDataBaseline { Version = version, Maps = maps };
    }

    private static string RequiredString(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"MapData 基线缺少有效的 {propertyName}。");
        }

        return value.GetString()!.Trim();
    }
}
