using System.Text.RegularExpressions;
using TarkovMap.Models;

namespace TarkovMap.Services;

public static class MapDataManifestValidator
{
    public const int SupportedSchemaVersion = 1;
    public const string SupportedGameMode = "pve";

    private static readonly Regex Sha256Pattern = new(
        "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void Validate(MapDataManifest manifest)
    {
        if (manifest.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidDataException(
                $"不支持的 MapData Schema：{manifest.SchemaVersion}，当前支持 {SupportedSchemaVersion}。");
        }

        if (!MapDataVersion.TryParse(manifest.DataVersion, out var version))
        {
            throw new InvalidDataException($"MapData 版本格式无效：{manifest.DataVersion}");
        }

        if (!string.Equals(manifest.GameMode, SupportedGameMode, StringComparison.Ordinal) ||
            !string.Equals(version.GameMode, manifest.GameMode, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"MapData 游戏模式必须为 {SupportedGameMode}，当前为 {manifest.GameMode}。");
        }

        if (manifest.GeneratedAt == default)
        {
            throw new InvalidDataException("MapData 缺少有效的 generatedAt。");
        }

        if (manifest.Sources.Count == 0 || manifest.Sources.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("MapData 至少需要一个有效数据源。");
        }

        if (manifest.SourceSnapshots.Count == 0)
        {
            throw new InvalidDataException("MapData 缺少可复现构建所需的来源快照。");
        }

        foreach (var snapshot in manifest.SourceSnapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.Name) ||
                string.IsNullOrWhiteSpace(snapshot.Location) ||
                string.IsNullOrWhiteSpace(snapshot.Revision) ||
                snapshot.RetrievedAt == default ||
                !Sha256Pattern.IsMatch(snapshot.Sha256))
            {
                throw new InvalidDataException($"MapData 来源快照无效：{snapshot.Name}");
            }
        }

        if (!Sha256Pattern.IsMatch(manifest.ContentHash))
        {
            throw new InvalidDataException("MapData contentHash 必须为 64 位 SHA-256。");
        }
    }
}
