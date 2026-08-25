using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MapPackBuilder.Validation;
using TarkovMap.Models;

namespace MapPackBuilder.Packaging;

internal sealed record MapDataPackageResult(
    string PackageFile,
    string Sha256,
    long Size,
    string DataVersion,
    int MapCount);

internal static class MapDataPackageService
{
    private static readonly string[] RequiredAcceptanceMaps =
    [
        "customs", "ground-zero", "streets-of-tarkov", "the-lab"
    ];

    public static MapDataPackageResult Create(
        string testPackRoot,
        string baselineFile,
        string approvalsFile,
        string outputFile)
    {
        testPackRoot = Path.GetFullPath(testPackRoot);
        outputFile = Path.GetFullPath(outputFile);
        if (File.Exists(outputFile) || Directory.Exists(outputFile))
        {
            throw new IOException($"正式包输出路径已经存在：{outputFile}");
        }

        var manifest = LoadManifest(Path.Combine(testPackRoot, "Data", "manifest.json"));
        var report = MapDataValidator.Validate(testPackRoot, baselineFile, approvalsFile,
            manifest.GeneratedAt);
        ValidationReportWriter.Write(testPackRoot, report);
        if (!report.CanPackage)
        {
            throw new InvalidDataException(
                $"Validation 未通过：Error {report.ErrorCount}，禁止正式打包。");
        }

        ValidationApprovalCatalog.Load(approvalsFile)
            .RequireManualAcceptance(manifest.DataVersion, RequiredAcceptanceMaps);
        var mapCount = RuntimeDataSmokeValidator.Validate(Path.Combine(testPackRoot, "Data"));

        var parent = Path.GetDirectoryName(outputFile)
                     ?? throw new InvalidDataException("正式包输出路径没有父目录。");
        Directory.CreateDirectory(parent);
        var temporaryFile = Path.Combine(parent, $".{Path.GetFileName(outputFile)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                var files = EnumeratePackageFiles(testPackRoot, approvalsFile);
                foreach (var file in files.OrderBy(file => file.ArchivePath, StringComparer.Ordinal))
                {
                    var entry = archive.CreateEntry(file.ArchivePath, CompressionLevel.Optimal);
                    entry.LastWriteTime = manifest.GeneratedAt;
                    using var input = File.OpenRead(file.SourcePath);
                    using var output = entry.Open();
                    input.CopyTo(output);
                }
            }

            VerifyPackage(temporaryFile, baselineFile, manifest.DataVersion);
            File.Move(temporaryFile, outputFile);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }

        var info = new FileInfo(outputFile);
        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(outputFile))).ToLowerInvariant();
        return new MapDataPackageResult(outputFile, sha, info.Length,
            manifest.DataVersion, mapCount);
    }

    internal static string ExtractAndValidate(
        string packageFile,
        string extractionRoot,
        string baselineFile)
    {
        packageFile = Path.GetFullPath(packageFile);
        extractionRoot = Path.GetFullPath(extractionRoot);
        if (!File.Exists(packageFile))
        {
            throw new FileNotFoundException("找不到 MapData 正式包。", packageFile);
        }

        if (File.Exists(extractionRoot) || Directory.Exists(extractionRoot))
        {
            throw new IOException($"解包目录必须不存在：{extractionRoot}");
        }

        Directory.CreateDirectory(extractionRoot);
        try
        {
            using var archive = ZipFile.OpenRead(packageFile);
            var normalizedRoot = extractionRoot.TrimEnd(Path.DirectorySeparatorChar) +
                                 Path.DirectorySeparatorChar;
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                var target = Path.GetFullPath(Path.Combine(normalizedRoot,
                    entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"ZIP 条目路径越界：{entry.FullName}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = entry.Open();
                using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
            }

            var approvalsFile = Path.Combine(extractionRoot, "validation-approvals.json");
            var manifest = LoadManifest(Path.Combine(extractionRoot, "Data", "manifest.json"));
            var report = MapDataValidator.Validate(extractionRoot, baselineFile, approvalsFile,
                manifest.GeneratedAt);
            if (!report.CanPackage)
            {
                throw new InvalidDataException(
                    $"解包后 Validation 未通过：Error {report.ErrorCount}。");
            }

            ValidationApprovalCatalog.Load(approvalsFile)
                .RequireManualAcceptance(manifest.DataVersion, RequiredAcceptanceMaps);
            _ = RuntimeDataSmokeValidator.Validate(Path.Combine(extractionRoot, "Data"));
            return manifest.DataVersion;
        }
        catch
        {
            if (Directory.Exists(extractionRoot))
            {
                Directory.Delete(extractionRoot, recursive: true);
            }

            throw;
        }
    }

    private static void VerifyPackage(string packageFile, string baselineFile, string expectedVersion)
    {
        var directory = Path.Combine(Path.GetDirectoryName(packageFile)!,
            $".mapdata-package-verify-{Guid.NewGuid():N}");
        try
        {
            var actualVersion = ExtractAndValidate(packageFile, directory, baselineFile);
            if (!string.Equals(expectedVersion, actualVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"正式包版本不一致：预期 {expectedVersion}，实际 {actualVersion}。");
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static IReadOnlyList<PackageFile> EnumeratePackageFiles(
        string testPackRoot, string approvalsFile)
    {
        var result = new List<PackageFile>();
        AddDirectory("Data");
        AddDirectory("snapshots");
        AddRootFile("validation-report.json");
        AddRootFile("validation-report.md");
        result.Add(new PackageFile(Path.GetFullPath(approvalsFile), "validation-approvals.json"));
        return result;

        void AddDirectory(string name)
        {
            var directory = Path.Combine(testPackRoot, name);
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"测试包缺少 {name} 目录。");
            }

            result.AddRange(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(file => new PackageFile(file,
                    Path.GetRelativePath(testPackRoot, file).Replace('\\', '/'))));
        }

        void AddRootFile(string name)
        {
            var file = Path.Combine(testPackRoot, name);
            if (!File.Exists(file))
            {
                throw new FileNotFoundException($"测试包缺少 {name}。", file);
            }

            result.Add(new PackageFile(file, name));
        }
    }

    private static MapDataManifest LoadManifest(string file)
    {
        if (!File.Exists(file))
        {
            throw new FileNotFoundException("测试包缺少 manifest.json。", file);
        }

        return JsonSerializer.Deserialize<MapDataManifest>(File.ReadAllText(file),
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidDataException("manifest.json 内容为空。");
    }

    private sealed record PackageFile(string SourcePath, string ArchivePath);
}
