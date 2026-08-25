using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TarkovMap.Models;

public sealed class MapDataManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("dataVersion")]
    public string DataVersion { get; set; } = "";

    [JsonPropertyName("gameMode")]
    public string GameMode { get; set; } = "pve";

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; set; }

    [JsonPropertyName("sources")]
    public List<string> Sources { get; set; } = [];

    [JsonPropertyName("sourceSnapshots")]
    public List<MapDataSourceSnapshot> SourceSnapshots { get; set; } = [];

    /// <summary>除 manifest.json 自身外，运行时 MapData 内容的 SHA-256。</summary>
    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = "";
}

public sealed class MapDataSourceSnapshot
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("revision")]
    public string Revision { get; set; } = "";

    [JsonPropertyName("retrievedAt")]
    public DateTimeOffset RetrievedAt { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";
}

public readonly record struct MapDataVersion(DateOnly Date, int Revision, string GameMode)
{
    private static readonly Regex Pattern = new(
        @"^(?<date>\d{4}\.\d{2}\.\d{2})\.(?<revision>[1-9]\d*)-(?<mode>[a-z0-9-]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryParse(string? text, out MapDataVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Pattern.Match(text);
        if (!match.Success ||
            !DateOnly.TryParseExact(match.Groups["date"].Value, "yyyy.MM.dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
            !int.TryParse(match.Groups["revision"].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var revision))
        {
            return false;
        }

        version = new MapDataVersion(date, revision, match.Groups["mode"].Value);
        return true;
    }

    public override string ToString() =>
        $"{Date:yyyy.MM.dd}.{Revision.ToString(CultureInfo.InvariantCulture)}-{GameMode}";
}
