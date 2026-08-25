using System.Security.Cryptography;
using System.Text.Json;
using TarkovMap.Models;

namespace MapPackBuilder.Sources;

internal static class SvgSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static IReadOnlyList<MapDataSourceSnapshot> Save(
        string rootDirectory,
        string dataVersion,
        SvgRepositorySnapshot snapshot)
    {
        if (!MapDataVersion.TryParse(dataVersion, out var version) || version.GameMode != "pve")
        {
            throw new ArgumentException($"无效的 PvE MapData 版本：{dataVersion}", nameof(dataVersion));
        }

        var relativeDirectory = Path.Combine("snapshots", dataVersion, "the-hideout",
            "tarkov-dev-svg-maps");
        var outputDirectory = Path.Combine(rootDirectory, relativeDirectory);
        Directory.CreateDirectory(outputDirectory);

        var records = new List<MapDataSourceSnapshot>();
        foreach (var (name, content) in snapshot.Assets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            records.Add(SaveFile(outputDirectory, relativeDirectory, name, content,
                GitHubSvgSource.RawUri(snapshot.CommitSha, name), snapshot));
        }

        records.Add(SaveFile(outputDirectory, relativeDirectory, "LICENSE.md", snapshot.License,
            GitHubSvgSource.RawUri(snapshot.CommitSha, "LICENSE.md"), snapshot));
        WriteAtomic(Path.Combine(outputDirectory, "snapshot.json"),
            JsonSerializer.SerializeToUtf8Bytes(records, JsonOptions));
        return records;
    }

    public static SvgRepositorySnapshot Load(string packRoot, string dataVersion)
    {
        var manifestFile = Path.Combine(packRoot, "snapshots", dataVersion, "the-hideout",
            "tarkov-dev-svg-maps", "snapshot.json");
        var files = SnapshotRecordReader.Read(packRoot, manifestFile);
        var license = files.SingleOrDefault(file => string.Equals(Path.GetFileName(file.FullPath),
                          "LICENSE.md", StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidDataException("SVG 快照缺少 LICENSE.md。");
        var assets = files
            .Where(file => file.FullPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(file => Path.GetFileName(file.FullPath), file => file.Content,
                StringComparer.Ordinal);
        if (assets.Count == 0)
        {
            throw new InvalidDataException("SVG 快照没有地图资源。");
        }

        var revisions = files.Select(file => file.Record.Revision)
            .Distinct(StringComparer.Ordinal).ToList();
        if (revisions.Count != 1 || revisions[0].Length != 40)
        {
            throw new InvalidDataException("SVG 快照提交版本不一致或无效。");
        }

        return new SvgRepositorySnapshot(revisions[0], assets, license.Content,
            files.Max(file => file.Record.RetrievedAt));
    }

    private static MapDataSourceSnapshot SaveFile(
        string outputDirectory,
        string relativeDirectory,
        string fileName,
        byte[] content,
        Uri source,
        SvgRepositorySnapshot repository)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        WriteAtomic(Path.Combine(outputDirectory, fileName), content);
        return new MapDataSourceSnapshot
        {
            Name = source.AbsoluteUri,
            Location = Path.Combine(relativeDirectory, fileName).Replace('\\', '/'),
            Revision = repository.CommitSha,
            RetrievedAt = repository.RetrievedAt,
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
