using System.Security.Cryptography;
using System.Text.Json;
using TarkovMap.Models;

namespace MapPackBuilder.Sources;

internal static class SourceSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static IReadOnlyList<MapDataSourceSnapshot> Save(
        string rootDirectory,
        string dataVersion,
        TarkovDevRawSnapshot snapshot)
    {
        if (!MapDataVersion.TryParse(dataVersion, out var version) || version.GameMode != "pve")
        {
            throw new ArgumentException($"无效的 PvE MapData 版本：{dataVersion}", nameof(dataVersion));
        }

        var relativeDirectory = Path.Combine("snapshots", dataVersion, "json.tarkov.dev");
        var outputDirectory = Path.Combine(rootDirectory, relativeDirectory);
        Directory.CreateDirectory(outputDirectory);

        var maps = SaveFile(outputDirectory, relativeDirectory, "maps.json", snapshot.MapsJson,
            TarkovDevSource.MapsUri, snapshot.RetrievedAt);
        var translations = SaveFile(outputDirectory, relativeDirectory, "maps_zh.json",
            snapshot.ChineseTranslationsJson, TarkovDevSource.ChineseTranslationsUri, snapshot.RetrievedAt);

        var records = new[] { maps, translations };
        WriteAtomic(Path.Combine(outputDirectory, "snapshot.json"),
            JsonSerializer.SerializeToUtf8Bytes(records, JsonOptions));
        return records;
    }

    private static MapDataSourceSnapshot SaveFile(
        string outputDirectory,
        string relativeDirectory,
        string fileName,
        byte[] content,
        Uri source,
        DateTimeOffset retrievedAt)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        WriteAtomic(Path.Combine(outputDirectory, fileName), content);
        return new MapDataSourceSnapshot
        {
            Name = source.AbsoluteUri,
            Location = Path.Combine(relativeDirectory, fileName).Replace('\\', '/'),
            Revision = $"sha256:{hash[..16]}",
            RetrievedAt = retrievedAt,
            Sha256 = hash
        };
    }

    private static void WriteAtomic(string path, byte[] content)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
