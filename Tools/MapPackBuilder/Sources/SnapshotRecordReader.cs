using System.Security.Cryptography;
using System.Text.Json;
using TarkovMap.Models;
using TarkovMap.Services;

namespace MapPackBuilder.Sources;

internal sealed record VerifiedSnapshotFile(MapDataSourceSnapshot Record, string FullPath, byte[] Content);

internal static class SnapshotRecordReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<VerifiedSnapshotFile> Read(string packRoot, string manifestFile)
    {
        packRoot = Path.GetFullPath(packRoot);
        if (!File.Exists(manifestFile))
        {
            throw new FileNotFoundException("找不到来源快照清单。", manifestFile);
        }

        var records = JsonSerializer.Deserialize<List<MapDataSourceSnapshot>>(
                          File.ReadAllText(manifestFile), JsonOptions)
                      ?? throw new InvalidDataException("来源快照清单内容为空。");
        if (records.Count == 0)
        {
            throw new InvalidDataException("来源快照清单没有文件记录。");
        }

        var normalizedRoot = packRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var result = new List<VerifiedSnapshotFile>();
        foreach (var record in records)
        {
            result.Add(ReadFile(normalizedRoot, record));
        }

        return result;
    }

    public static VerifiedSnapshotFile ReadManifestSnapshot(
        string packRoot, string dataVersion, string sourceName)
    {
        packRoot = Path.GetFullPath(packRoot);
        var manifestFile = Path.Combine(packRoot, "Data", "manifest.json");
        if (!File.Exists(manifestFile))
        {
            throw new FileNotFoundException("来源测试包缺少 Data/manifest.json。", manifestFile);
        }

        var manifest = JsonSerializer.Deserialize<MapDataManifest>(File.ReadAllText(manifestFile), JsonOptions)
                       ?? throw new InvalidDataException("来源测试包 manifest.json 内容为空。");
        MapDataManifestValidator.Validate(manifest);
        if (!string.Equals(manifest.DataVersion, dataVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"来源测试包版本不一致：要求 {dataVersion}，实际 {manifest.DataVersion}。");
        }

        var record = manifest.SourceSnapshots.SingleOrDefault(snapshot =>
                         string.Equals(snapshot.Name, sourceName, StringComparison.Ordinal))
                     ?? throw new InvalidDataException($"来源测试包缺少快照记录：{sourceName}。");
        var normalizedRoot = packRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return ReadFile(normalizedRoot, record);
    }

    private static VerifiedSnapshotFile ReadFile(
        string normalizedRoot, MapDataSourceSnapshot record)
    {
        if (string.IsNullOrWhiteSpace(record.Location) || Path.IsPathRooted(record.Location))
        {
            throw new InvalidDataException($"来源快照路径无效：{record.Location}。");
        }

        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot,
            record.Location.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"来源快照路径越出测试包：{record.Location}。");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"来源快照不存在：{record.Location}。", fullPath);
        }

        var content = File.ReadAllBytes(fullPath);
        var actualHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!string.Equals(actualHash, record.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"来源快照哈希不一致：{record.Location}。");
        }

        return new VerifiedSnapshotFile(record, fullPath, content);
    }
}
