using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MapPackBuilder;

/// <summary>
/// 为没有上游 ID 的 Marker 生成跨进程、跨机器可重复的持久 ID。
/// </summary>
internal static class StableMarkerId
{
    public static string Create(
        string source,
        string mapId,
        string type,
        string name,
        double x,
        double z)
    {
        var canonical = string.Join('\n',
            NormalizeKey(source),
            NormalizeKey(mapId),
            NormalizeKey(type),
            NormalizeName(name),
            Math.Round(x, 2).ToString("0.00", CultureInfo.InvariantCulture),
            Math.Round(z, 2).ToString("0.00", CultureInfo.InvariantCulture));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return $"{NormalizeKey(type)}_{Convert.ToHexString(hash.AsSpan(0, 10)).ToLowerInvariant()}";
    }

    private static string NormalizeKey(string value) => value.Trim().ToLowerInvariant();

    private static string NormalizeName(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
