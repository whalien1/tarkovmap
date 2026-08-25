using System.Security.Cryptography;
using System.Text;

namespace MapPackBuilder.Output;

internal static class MapDataContentHasher
{
    public static string Compute(string dataDirectory)
    {
        var files = Directory.EnumerateFiles(dataDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), "manifest.json",
                StringComparison.OrdinalIgnoreCase))
            .Select(path => new
            {
                FullPath = path,
                RelativePath = Path.GetRelativePath(dataDirectory, path).Replace('\\', '/')
            })
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            throw new InvalidDataException("MapData 没有可计算哈希的运行时文件。");
        }

        using var combined = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            combined.AppendData(Encoding.UTF8.GetBytes(file.RelativePath));
            combined.AppendData([0]);
            combined.AppendData(SHA256.HashData(File.ReadAllBytes(file.FullPath)));
        }

        return Convert.ToHexString(combined.GetHashAndReset()).ToLowerInvariant();
    }
}
