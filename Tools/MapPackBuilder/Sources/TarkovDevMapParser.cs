using System.Text.Json;

namespace MapPackBuilder.Sources;

internal static class TarkovDevMapParser
{
    private static readonly HashSet<string> ExistingMapIds = new(StringComparer.Ordinal)
    {
        "streets-of-tarkov", "ground-zero", "customs", "factory", "interchange", "the-lab",
        "lighthouse", "reserve", "shoreline", "woods", "the-labyrinth"
    };

    private static readonly HashSet<string> VariantMapIds = new(StringComparer.Ordinal)
    {
        "night-factory", "ground-zero-21", "ground-zero-tutorial", "the-lab-dark"
    };

    public static IReadOnlyList<TarkovDevMap> Parse(TarkovDevRawSnapshot snapshot)
    {
        using var mapsDocument = JsonDocument.Parse(snapshot.MapsJson);
        using var translationsDocument = JsonDocument.Parse(snapshot.ChineseTranslationsJson);
        var translations = ReadTranslations(translationsDocument.RootElement);

        var data = mapsDocument.RootElement.GetProperty("data");
        if (!data.TryGetProperty("maps", out var maps) || maps.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("PvE maps JSON 缺少 data.maps。");
        }

        var result = new List<TarkovDevMap>();
        foreach (var property in maps.EnumerateObject())
        {
            var map = property.Value;
            var mapId = ReadString(map, "normalizedName");
            if (mapId.Length == 0)
            {
                continue;
            }

            result.Add(new TarkovDevMap(
                ReadString(map, "id", property.Name),
                mapId,
                Translate(ReadString(map, "name", mapId), translations),
                Classify(mapId),
                ReadNullableDouble(map, "coordinateToCardinalRotation"),
                ParseExtracts(map, translations),
                ParseTransits(map, translations),
                ParseSpawns(map),
                ParseBosses(map, translations),
                ParseHazards(map, translations)));
        }

        return result.OrderBy(map => map.MapId, StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyList<TarkovDevExtract> ParseExtracts(
        JsonElement map, IReadOnlyDictionary<string, string> translations) =>
        ArrayOf(map, "extracts")
            .Select(item => new TarkovDevExtract(
                ReadString(item, "id"),
                Translate(ReadString(item, "name", "撤离点"), translations),
                ReadString(item, "faction", "shared").ToLowerInvariant(),
                ReadPosition(item)))
            .ToList();

    private static IReadOnlyList<TarkovDevTransit> ParseTransits(
        JsonElement map, IReadOnlyDictionary<string, string> translations) =>
        ArrayOf(map, "transits")
            .Select(item => new TarkovDevTransit(
                ReadString(item, "id"),
                Translate(ReadString(item, "description", ReadString(item, "name", "转移点")), translations),
                ReadPosition(item)))
            .ToList();

    private static IReadOnlyList<TarkovDevSpawn> ParseSpawns(JsonElement map) =>
        ArrayOf(map, "spawns")
            .Select(item => new TarkovDevSpawn(
                ReadString(item, "zoneName", "出生点"),
                ReadPosition(item),
                ReadStringArray(item, "sides"),
                ReadStringArray(item, "categories")))
            .ToList();

    private static IReadOnlyList<TarkovDevBoss> ParseBosses(
        JsonElement map, IReadOnlyDictionary<string, string> translations) =>
        ArrayOf(map, "bosses")
            .Select(item => new TarkovDevBoss(
                Translate(ReadString(item, "mob", "Boss"), translations),
                ArrayOf(item, "spawnLocations")
                    .Select(location => new TarkovDevBossLocation(
                        Translate(ReadString(location, "name"), translations),
                        ReadString(location, "spawnKey")))
                    .ToList()))
            .ToList();

    private static IReadOnlyList<TarkovDevHazard> ParseHazards(
        JsonElement map, IReadOnlyDictionary<string, string> translations) =>
        ArrayOf(map, "hazards")
            .Select(item => new TarkovDevHazard(
                ReadString(item, "id"),
                Translate(ReadString(item, "name", ReadString(item, "hazardType", "危险区")), translations),
                ReadPosition(item),
                ArrayOf(item, "outline").Select(ReadDirectPosition).ToList()))
            .ToList();

    private static SourceMapDisposition Classify(string mapId) =>
        ExistingMapIds.Contains(mapId) ? SourceMapDisposition.Existing :
        VariantMapIds.Contains(mapId) ? SourceMapDisposition.Variant :
        SourceMapDisposition.New;

    private static Dictionary<string, string> ReadTranslations(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("中文翻译 JSON 缺少 data。");
        }

        return data.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(property => property.Name, property => property.Value.GetString() ?? "",
                StringComparer.Ordinal);
    }

    private static string Translate(string key, IReadOnlyDictionary<string, string> translations) =>
        translations.TryGetValue(key, out var translated) && !string.IsNullOrWhiteSpace(translated)
            ? translated.Trim()
            : key;

    private static SourcePosition ReadPosition(JsonElement item)
    {
        if (!item.TryGetProperty("position", out var position) || position.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("上游点位缺少 position。");
        }

        return ReadDirectPosition(position);
    }

    private static SourcePosition ReadDirectPosition(JsonElement position) => new(
        ReadDouble(position, "x"),
        ReadNullableDouble(position, "y") ?? 0,
        ReadDouble(position, "z"));

    private static IEnumerable<JsonElement> ArrayOf(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : [];

    private static IReadOnlyList<string> ReadStringArray(JsonElement item, string propertyName) =>
        ArrayOf(item, propertyName)
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => (value.GetString() ?? "").ToLowerInvariant())
            .ToList();

    private static string ReadString(JsonElement item, string propertyName, string fallback = "") =>
        item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? fallback
            : fallback;

    private static double ReadDouble(JsonElement item, string propertyName) =>
        ReadNullableDouble(item, propertyName)
        ?? throw new InvalidDataException($"上游点位缺少数值字段 {propertyName}。");

    private static double? ReadNullableDouble(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
}
