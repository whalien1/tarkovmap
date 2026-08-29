using System.Text.Json;
using System.Text.Json.Serialization;
using MapPackBuilder.Assets;
using MapPackBuilder.Calibration;
using MapPackBuilder.Output;
using MapPackBuilder.Packaging;
using MapPackBuilder.Sources;
using MapPackBuilder.Validation;

namespace MapPackBuilder;

/// <summary>
/// TarkovMap 地图数据构建工具（开发期使用，不进入正式客户端）。
/// 新流程读取 PvE API 与 SVG 上游，生成带来源快照的独立测试包；
/// 同时保留旧版 ref → Data 构建入口用于基线回归。
/// </summary>
internal static class Program
{
    private const string CurrentBaselineFileName = "baseline-2026.08.29.1-pve.json";
    private const string SourceTag = "Re5pawnn/Tarkov_webmap maps_detail.json (author: the-hideout/tarkov-dev-svg-maps)";

    // 跳过变体条目（夜间工厂 / 中心区21+ 与主条目同 key）
    private static readonly HashSet<string> SkipNames = new(StringComparer.Ordinal) { "夜间工厂", "中心区 21+" };

    // 当前版本已移除的 Boss（数据生成时剔除，回归版本时从名单删除即可）
    private static readonly HashSet<string> ExcludedBosses = new(StringComparer.Ordinal) { "寻血猎犬" };

    // 地图朝向旋转角人工修正（覆盖源数据的 coordinateRotation；实测校准后登记在此）
    private static readonly Dictionary<string, double> RotationOverrides = new(StringComparer.Ordinal)
    {
        ["ground-zero"] = 90.0, // 中心区：源数据 180 有误，2026-08-24 实测 90（正对 Emercom 检查点校准）
        ["customs"] = 90.0,     // 海关：源数据 180 有误，2026-08-24 实测 90（正对 Scav 检查站校准）
        // 其余普通坐标地图按 90° 校正；reverseCoordinate 地图必须单独实测。
        ["factory"] = 90.0,
        ["interchange"] = 90.0,
        ["lighthouse"] = 90.0,
        ["reserve"] = 90.0,
        ["shoreline"] = 90.0,
        ["streets-of-tarkov"] = 90.0,
        ["the-lab"] = 0.0, // 实验室 reverseCoordinate=true：2026-08-29 实测，90° 会使箭头顺时针偏 90°
        ["the-labyrinth"] = 90.0,
        ["woods"] = 90.0,
    };

    private static int Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "pve-build", StringComparison.OrdinalIgnoreCase))
        {
            return RunPveBuildAsync(args[1..]).GetAwaiter().GetResult();
        }

        if (args.Length > 0 && string.Equals(args[0], "pve-fetch", StringComparison.OrdinalIgnoreCase))
        {
            return RunPveFetchAsync(args[1..]).GetAwaiter().GetResult();
        }

        if (args.Length > 0 && string.Equals(args[0], "pve-validate", StringComparison.OrdinalIgnoreCase))
        {
            return RunPveValidate(args[1..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "pve-replay", StringComparison.OrdinalIgnoreCase))
        {
            return RunPveReplay(args[1..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "pve-package", StringComparison.OrdinalIgnoreCase))
        {
            return RunPvePackage(args[1..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "pve-apply", StringComparison.OrdinalIgnoreCase))
        {
            return RunPveApply(args[1..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "pve-restore", StringComparison.OrdinalIgnoreCase))
        {
            return RunPveRestore(args[1..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "pve-baseline", StringComparison.OrdinalIgnoreCase))
        {
            return RunPveBaseline(args[1..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "pve-icons", StringComparison.OrdinalIgnoreCase))
        {
            return RunPveIcons(args[1..]);
        }

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var refDir = args.Length > 0 ? args[0] : Path.Combine(root, "ref", "Tarkov_webmap");
        var outDir = args.Length > 1 ? args[1] : Path.Combine(root, "TarkovMap", "Data");

        var detailPath = Path.Combine(refDir, "data", "maps_detail.json");
        var cacheDir = Path.Combine(refDir, "assets", "maps", "native-cache");
        if (!File.Exists(detailPath) || !Directory.Exists(cacheDir))
        {
            Console.WriteLine($"[错误] 找不到源数据: {detailPath}");
            return 1;
        }

        Console.WriteLine($"源数据: {detailPath}");
        Console.WriteLine($"输出到: {outDir}");
        Console.WriteLine();

        Directory.CreateDirectory(outDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(detailPath));

        var mapList = new List<MapListEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var totalInvalid = 0;

        foreach (var node in doc.RootElement.EnumerateObject())
        {
            var name = node.Value.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (!node.Value.TryGetProperty("raw", out var raw) ||
                !raw.TryGetProperty("data", out var data))
            {
                continue;
            }
            var key = data.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
            if (key.Length == 0 || !seen.Add(key) || SkipNames.Contains(name))
            {
                continue;
            }

            try
            {
                var invalid = BuildMap(key, name, data, cacheDir, outDir);
                totalInvalid += invalid;
                mapList.Add(new MapListEntry(key, name, $"maps/{key}", true));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[跳过] {name}({key}): {ex.Message}");
            }
        }

        var listJson = JsonSerializer.Serialize(
            new { schemaVersion = 1, maps = mapList },
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(Path.Combine(outDir, "maps.json"), listJson);

        Console.WriteLine();
        Console.WriteLine($"完成: {mapList.Count} 张地图, 非法点位 {totalInvalid} 个");
        Console.WriteLine($"maps.json 已写入 {outDir}");
        return 0;
    }

    private static async Task<int> RunPveBuildAsync(string[] args)
    {
        if (args.Length is < 3 or > 4)
        {
            Console.WriteLine(
                "用法: MapPackBuilder.exe pve-build <独立测试包目录> <YYYY.MM.DD.N-pve> <现有Data兼容目录> [审批文件]");
            return 1;
        }

        var outputDirectory = Path.GetFullPath(args[0]);
        var dataVersion = args[1];
        var fallbackDataDirectory = Path.GetFullPath(args[2]);
        var approvalsFile = args.Length == 4 ? Path.GetFullPath(args[3]) : null;
        if (File.Exists(outputDirectory) || Directory.Exists(outputDirectory))
        {
            Console.WriteLine($"[错误] 测试包目录已经存在，请使用新的空路径：{outputDirectory}");
            return 1;
        }

        if (!File.Exists(Path.Combine(fallbackDataDirectory, "maps.json")))
        {
            Console.WriteLine($"[错误] 现有 Data 兼容目录无效：{fallbackDataDirectory}");
            return 1;
        }

        var stagingDirectory = $"{outputDirectory}.building-{Guid.NewGuid():N}";
        try
        {
            var calibrationFile = Path.Combine(AppContext.BaseDirectory, "calibration-v1.1.1.json");
            var calibration = MapCalibrationCatalog.Load(calibrationFile);
            using var httpClient = new HttpClient();

            Console.WriteLine("[1/5] 正在读取 PvE 地图与中文点位……");
            var apiSnapshot = await new TarkovDevSource(httpClient).FetchAsync();
            var maps = TarkovDevMapParser.Parse(apiSnapshot);

            Console.WriteLine("[2/5] 正在读取指定提交的 SVG 地图资源……");
            var svgAssets = calibration.Maps
                .Select(map => map.SvgAsset)
                .Where(name => name is not null)
                .Select(name => name!);
            var svgSnapshot = await new GitHubSvgSource(httpClient).FetchAsync(svgAssets);

            Console.WriteLine("[3/5] 正在保存来源快照并生成 11 张测试地图……");
            var apiRecords = SourceSnapshotStore.Save(stagingDirectory, dataVersion, apiSnapshot);
            var svgRecords = SvgSnapshotStore.Save(stagingDirectory, dataVersion, svgSnapshot);
            var result = PveTestPackBuilder.Build(
                stagingDirectory,
                dataVersion,
                maps,
                svgSnapshot,
                calibration,
                calibrationFile,
                fallbackDataDirectory,
                apiRecords.Concat(svgRecords).ToList(),
                DateTimeOffset.UtcNow);

            Console.WriteLine("[4/5] 正在执行 Validation + Diff……");
            var validation = MapDataValidator.Validate(
                stagingDirectory,
                DefaultBaselineFile(),
                approvalsFile);
            ValidationReportWriter.Write(stagingDirectory, validation);

            Directory.Move(stagingDirectory, outputDirectory);
            Console.WriteLine("[5/5] 整批测试 MapData 已完成。");
            PrintPveSummary(outputDirectory, svgSnapshot, result, validation);
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[错误] PvE 测试包生成失败: {exception.Message}");
            if (Directory.Exists(stagingDirectory))
            {
                Console.WriteLine($"失败现场保留在: {stagingDirectory}");
            }
            return 1;
        }
    }

    private static int RunPveReplay(string[] args)
    {
        if (args.Length is < 5 or > 6)
        {
            Console.WriteLine(
                "用法: MapPackBuilder.exe pve-replay <来源测试包> <来源版本> <新测试包目录> <新版本> <现有Data兼容目录> [审批文件]");
            return 1;
        }

        var sourcePack = Path.GetFullPath(args[0]);
        var sourceVersion = args[1];
        var outputDirectory = Path.GetFullPath(args[2]);
        var dataVersion = args[3];
        var fallbackDataDirectory = Path.GetFullPath(args[4]);
        var approvalsFile = args.Length == 6 ? Path.GetFullPath(args[5]) : null;
        if (File.Exists(outputDirectory) || Directory.Exists(outputDirectory))
        {
            Console.WriteLine($"[错误] 测试包目录已经存在，请使用新的空路径：{outputDirectory}");
            return 1;
        }

        if (!File.Exists(Path.Combine(fallbackDataDirectory, "maps.json")))
        {
            Console.WriteLine($"[错误] 现有 Data 兼容目录无效：{fallbackDataDirectory}");
            return 1;
        }

        var stagingDirectory = $"{outputDirectory}.building-{Guid.NewGuid():N}";
        try
        {
            Console.WriteLine("[1/4] 正在读取并校验已保存的 PvE/API 与 SVG 快照……");
            var apiSnapshot = SourceSnapshotStore.Load(sourcePack, sourceVersion);
            var svgSnapshot = SvgSnapshotStore.Load(sourcePack, sourceVersion);
            var calibrationFile = SnapshotRecordReader.ReadManifestSnapshot(sourcePack,
                sourceVersion, "TarkovMap calibration metadata").FullPath;
            var calibration = MapCalibrationCatalog.Load(calibrationFile);
            var maps = TarkovDevMapParser.Parse(apiSnapshot);

            Console.WriteLine("[2/4] 正在复制已验证快照并重放 11 张测试地图……");
            var apiRecords = SourceSnapshotStore.Save(stagingDirectory, dataVersion, apiSnapshot);
            var svgRecords = SvgSnapshotStore.Save(stagingDirectory, dataVersion, svgSnapshot);
            var result = PveTestPackBuilder.Build(
                stagingDirectory,
                dataVersion,
                maps,
                svgSnapshot,
                calibration,
                calibrationFile,
                fallbackDataDirectory,
                apiRecords.Concat(svgRecords).ToList(),
                DateTimeOffset.UtcNow);

            Console.WriteLine("[3/4] 正在执行 Validation + Diff……");
            var validation = MapDataValidator.Validate(stagingDirectory,
                DefaultBaselineFile(), approvalsFile);
            ValidationReportWriter.Write(stagingDirectory, validation);
            Directory.Move(stagingDirectory, outputDirectory);

            Console.WriteLine("[4/4] 快照重放测试包已完成。");
            PrintPveSummary(outputDirectory, svgSnapshot, result, validation);
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[错误] PvE 快照重放失败: {exception.Message}");
            if (Directory.Exists(stagingDirectory))
            {
                Console.WriteLine($"失败现场保留在: {stagingDirectory}");
            }

            return 1;
        }
    }

    private static void PrintPveSummary(
        string outputDirectory,
        SvgRepositorySnapshot svgSnapshot,
        TestPackBuildResult result,
        MapDataValidationReport validation)
    {
        foreach (var map in result.Maps)
        {
            var coreCounts = string.Join(" ", map.MarkerTypes.Select(pair => $"{pair.Key}×{pair.Value}"));
            Console.WriteLine(
                $"  {map.Name}({map.MapId}) {map.ImageWidth}×{map.ImageHeight} 点位 {map.MarkerCount}  {coreCounts}");
        }

        Console.WriteLine($"SVG 版本: {svgSnapshot.CommitSha}");
        Console.WriteLine($"内容哈希: {result.ContentHash}");
        Console.WriteLine(
            $"Validation: Error {validation.ErrorCount} / Warning {validation.WarningCount} / Info {validation.InfoCount}");
        Console.WriteLine(validation.CanPackage
            ? "Validation 已通过：允许进入正式打包。"
            : "Validation 未通过：已阻止正式打包；测试包仍保留供检查。");
        Console.WriteLine($"测试包目录: {outputDirectory}");
        Console.WriteLine("正式 TarkovMap/Data 未修改。");
    }

    private static int RunPveValidate(string[] args)
    {
        if (args.Length is < 1 or > 3)
        {
            Console.WriteLine(
                "用法: MapPackBuilder.exe pve-validate <测试包目录> [基线文件] [审批文件]");
            return 1;
        }

        try
        {
            var packRoot = Path.GetFullPath(args[0]);
            var baselineFile = args.Length >= 2
                ? Path.GetFullPath(args[1])
                : DefaultBaselineFile();
            var approvalsFile = args.Length == 3 ? Path.GetFullPath(args[2]) : null;
            var report = MapDataValidator.Validate(packRoot, baselineFile, approvalsFile);
            ValidationReportWriter.Write(packRoot, report);
            Console.WriteLine(
                $"Validation: Error {report.ErrorCount} / Warning {report.WarningCount} / Info {report.InfoCount}");
            Console.WriteLine(report.CanPackage
                ? "校验通过：允许进入正式打包。"
                : "校验未通过：已阻止正式打包。");
            Console.WriteLine($"报告: {Path.Combine(packRoot, "validation-report.md")}");
            return report.CanPackage ? 0 : 2;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[错误] Validation 执行失败: {exception.Message}");
            return 1;
        }
    }

    private static int RunPvePackage(string[] args)
    {
        if (args.Length is < 3 or > 4)
        {
            Console.WriteLine(
                "用法: MapPackBuilder.exe pve-package <测试包目录> <审批文件> <ZIP输出目录> [基线文件]");
            return 1;
        }

        try
        {
            var testPackRoot = Path.GetFullPath(args[0]);
            var approvalsFile = Path.GetFullPath(args[1]);
            var outputDirectory = Path.GetFullPath(args[2]);
            var baselineFile = args.Length == 4
                ? Path.GetFullPath(args[3])
                : DefaultBaselineFile();
            var manifest = new TarkovMap.Services.MapRepository(Path.Combine(testPackRoot, "Data"))
                               .LoadManifest()
                           ?? throw new InvalidDataException("测试包缺少 manifest.json。");
            var outputFile = Path.Combine(outputDirectory, $"MapData-{manifest.DataVersion}.zip");
            Console.WriteLine("正在重新校验、打包并执行解包复验……");
            var result = MapDataPackageService.Create(testPackRoot, baselineFile,
                approvalsFile, outputFile);
            Console.WriteLine($"正式包已生成：{result.PackageFile}");
            Console.WriteLine($"版本：{result.DataVersion}，地图：{result.MapCount}，大小：{result.Size} 字节");
            Console.WriteLine($"ZIP SHA-256：{result.Sha256}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[错误] 正式打包失败: {exception.Message}");
            return 1;
        }
    }

    private static int RunPveApply(string[] args)
    {
        if (args.Length is < 2 or > 4)
        {
            Console.WriteLine(
                "用法: MapPackBuilder.exe pve-apply <ZIP正式包> <正式Data目录> [备份目录] [基线文件]");
            return 1;
        }

        try
        {
            var packageFile = Path.GetFullPath(args[0]);
            var dataDirectory = Path.GetFullPath(args[1]);
            var backupDirectory = args.Length >= 3
                ? Path.GetFullPath(args[2])
                : Path.Combine(Path.GetDirectoryName(dataDirectory)!, "Data.backup");
            var baselineFile = args.Length == 4
                ? Path.GetFullPath(args[3])
                : DefaultBaselineFile();
            Console.WriteLine("正在校验正式包并备份当前 Data……");
            var result = MapDataInstaller.Apply(packageFile, dataDirectory,
                backupDirectory, baselineFile);
            Console.WriteLine($"已应用 MapData {result.DataVersion}，共 {result.MapCount} 张地图。");
            Console.WriteLine($"正式目录：{result.DataDirectory}");
            Console.WriteLine($"可恢复备份：{result.BackupDirectory}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[错误] MapData 应用失败: {exception.Message}");
            return 1;
        }
    }

    private static int RunPveRestore(string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            Console.WriteLine("用法: MapPackBuilder.exe pve-restore <正式Data目录> [备份目录]");
            return 1;
        }

        try
        {
            var dataDirectory = Path.GetFullPath(args[0]);
            var backupDirectory = args.Length == 2
                ? Path.GetFullPath(args[1])
                : Path.Combine(Path.GetDirectoryName(dataDirectory)!, "Data.backup");
            var mapCount = MapDataInstaller.Restore(dataDirectory, backupDirectory);
            Console.WriteLine($"已恢复上一个 MapData，共 {mapCount} 张地图。");
            Console.WriteLine("本次被替换的数据已删除；备份槽已清空。");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[错误] MapData 恢复失败: {exception.Message}");
            return 1;
        }
    }

    private static int RunPveBaseline(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("用法: MapPackBuilder.exe pve-baseline <Data目录> <基线输出文件>");
            return 1;
        }

        try
        {
            var dataDirectory = Path.GetFullPath(args[0]);
            var outputFile = Path.GetFullPath(args[1]);
            MapDataBaselineWriter.Write(dataDirectory, outputFile);
            _ = MapDataBaseline.Load(outputFile);
            Console.WriteLine($"当前 MapData 基线已生成：{outputFile}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[错误] MapData 基线生成失败: {exception.Message}");
            return 1;
        }
    }

    private static string DefaultBaselineFile() =>
        Path.Combine(AppContext.BaseDirectory, CurrentBaselineFileName);

    private static int RunPveIcons(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("用法: MapPackBuilder.exe pve-icons <图标输出目录>");
            return 1;
        }

        try
        {
            var outputDirectory = Path.GetFullPath(args[0]);
            var files = MarkerIconAssetGenerator.Generate(outputDirectory);
            Console.WriteLine($"已生成 {files.Count} 个 TarkovMap 自有 Marker 图标：{outputDirectory}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[错误] Marker 图标生成失败: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunPveFetchAsync(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("用法: MapPackBuilder.exe pve-fetch <快照输出目录> <YYYY.MM.DD.N-pve>");
            return 1;
        }

        var outputDirectory = Path.GetFullPath(args[0]);
        var dataVersion = args[1];
        try
        {
            using var httpClient = new HttpClient();
            var source = new TarkovDevSource(httpClient);
            Console.WriteLine("正在读取 json.tarkov.dev PvE 地图和中文数据……");
            var snapshot = await source.FetchAsync();
            var maps = TarkovDevMapParser.Parse(snapshot);
            var calibration = MapCalibrationCatalog.Load(
                Path.Combine(AppContext.BaseDirectory, "calibration-v1.1.1.json"));
            var missingCalibration = maps
                .Where(map => map.Disposition == SourceMapDisposition.Existing &&
                              !calibration.TryGet(map.MapId, out _))
                .Select(map => map.MapId)
                .ToList();
            if (missingCalibration.Count > 0)
            {
                throw new InvalidDataException(
                    $"现有地图缺少校准配置：{string.Join("、", missingCalibration)}。");
            }

            var files = SourceSnapshotStore.Save(outputDirectory, dataVersion, snapshot);

            Console.WriteLine($"已保存原始快照: {outputDirectory}");
            foreach (var file in files)
            {
                Console.WriteLine($"  {file.Location}  {file.Revision}");
            }

            foreach (var group in maps.GroupBy(map => map.Disposition).OrderBy(group => group.Key))
            {
                Console.WriteLine($"{DispositionName(group.Key)}: {group.Count()} 张 — {string.Join("、", group.Select(map => map.Name))}");
            }

            Console.WriteLine($"校准配置: {calibration.Maps.Count} 张现有地图，全部匹配。");
            Console.WriteLine($"接口解析完成: {maps.Count} 张地图/变体。未生成或覆盖正式 Data。");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[错误] PvE 数据抓取失败: {exception.Message}");
            return 1;
        }
    }

    private static string DispositionName(SourceMapDisposition disposition) => disposition switch
    {
        SourceMapDisposition.Existing => "现有地图",
        SourceMapDisposition.Variant => "默认跳过的变体",
        SourceMapDisposition.New => "待校准的新地图",
        _ => disposition.ToString()
    };

    private static int BuildMap(string key, string name, JsonElement data, string cacheDir, string outDir)
    {
        var bounds = data.GetProperty("bounds");
        var x0 = bounds[0][0].GetDouble();
        var z0 = bounds[0][1].GetDouble();
        var x1 = bounds[1][0].GetDouble();
        var z1 = bounds[1][1].GetDouble();
        var reverse = data.TryGetProperty("reverseCoordinate", out var rev) &&
                      rev.ValueKind is JsonValueKind.True or JsonValueKind.False && rev.GetBoolean();
        var coordRotation = data.TryGetProperty("coordinateRotation", out var cr) &&
                            cr.ValueKind == JsonValueKind.Number ? cr.GetDouble() : 0.0;

        // 朝向旋转角人工修正（源数据有误时用，重新生成不丢失）：
        // ground-zero 中心区源数据为 180，2026-08-24 实测应为 90（正对 Emercom 检查点校准）
        if (RotationOverrides.TryGetValue(key, out var fixedRotation))
        {
            coordRotation = fixedRotation;
        }

        // 图层过滤：仅保留主层高度范围内的点位（多层图的楼上/地下不画在 v0.1）
        double? minY = null, maxY = null;
        if (data.TryGetProperty("heightRange", out var hr) && hr.GetArrayLength() >= 2)
        {
            minY = Math.Min(hr[0].GetDouble(), hr[1].GetDouble());
            maxY = Math.Max(hr[0].GetDouble(), hr[1].GetDouble());
        }

        bool InRange(JsonElement item)
        {
            if (minY is null || !item.TryGetProperty("position", out var p) ||
                p.ValueKind != JsonValueKind.Object || !p.TryGetProperty("y", out var y) ||
                y.ValueKind != JsonValueKind.Number)
            {
                return true;
            }
            var v = y.GetDouble();
            return v >= minY && v <= maxY;
        }

        var markers = new List<MarkerOut>();

        MarkerOut Marker(string type, string markerName, double markerX, double markerZ,
            string idRaw, List<double[]>? outline = null) =>
            new(key, type, markerName, markerX, markerZ, idRaw, outline);

        foreach (var e in ArrayOf(data, "extracts"))
        {
            if (!TryPos(e, out var x, out var z)) continue;
            var faction = (ReadStr(e, "faction") ?? "shared").ToLowerInvariant();
            var type = faction switch { "pmc" => "extract_pmc", "scav" => "extract_scav", _ => "extract_shared" };
            markers.Add(Marker(type, ReadStr(e, "name") ?? "撤离点", x, z, ReadStr(e, "id") ?? ""));
        }

        foreach (var t in ArrayOf(data, "transits"))
        {
            if (!TryPos(t, out var x, out var z)) continue;
            markers.Add(Marker("extract_transit",
                ReadStr(t, "description") ?? ReadStr(t, "name") ?? "转移点", x, z, ReadStr(t, "id") ?? ""));
        }

        var spawns = ArrayOf(data, "spawns").ToList();
        foreach (var s in spawns)
        {
            if (!InRange(s) || !TryPos(s, out var x, out var z)) continue;
            var cats = StrArray(s, "categories");
            if (cats.Contains("boss") || cats.Contains("sniper")) continue;
            var sides = StrArray(s, "sides");
            var type = sides.Contains("pmc") ? "spawn_pmc" : "spawn_scav";
            markers.Add(Marker(type, ReadStr(s, "zoneName") ?? "出生点", x, z, ""));
        }

        // Boss：跳过已移除名单；同一刷新点（同坐标）的多个 Boss 合并为一个 Marker，名字用 / 连接
        var bossByZone = new Dictionary<string, (double X, double Z, string LocName, List<string> Names)>();
        foreach (var b in ArrayOf(data, "bosses"))
        {
            var bossName = b.TryGetProperty("boss", out var bo) ? ReadStr(bo, "name") ?? "Boss" : "Boss";
            if (ExcludedBosses.Contains(bossName)) continue;
            foreach (var loc in ArrayOf(b, "spawnLocations"))
            {
                var spawnKey = ReadStr(loc, "spawnKey");
                var hit = spawns.FirstOrDefault(s => ReadStr(s, "zoneName") == spawnKey);
                if (hit.ValueKind != JsonValueKind.Object || !TryPos(hit, out var x, out var z)) continue;
                var locName = ReadStr(loc, "name") ?? "";
                var zoneKey = $"{Math.Round(x, 1)},{Math.Round(z, 1)}";
                if (!bossByZone.TryGetValue(zoneKey, out var entry))
                {
                    entry = (x, z, locName, new List<string>());
                    bossByZone[zoneKey] = entry;
                }
                if (!entry.Names.Contains(bossName))
                {
                    entry.Names.Add(bossName);
                }
            }
        }
        foreach (var (_, entry) in bossByZone)
        {
            var names = string.Join(" / ", entry.Names);
            var display = string.IsNullOrEmpty(entry.LocName) ? names : $"{names}（{entry.LocName}）";
            markers.Add(Marker("boss", display, entry.X, entry.Z, ""));
        }

        foreach (var l in ArrayOf(data, "locks"))
        {
            if (!TryPos(l, out var x, out var z)) continue;
            var keyName = l.TryGetProperty("key", out var kk) ? ReadStr(kk, "name") : null;
            markers.Add(Marker("lock", keyName ?? ReadStr(l, "lockType") ?? "门锁", x, z, ""));
        }

        foreach (var h in ArrayOf(data, "hazards"))
        {
            if (!TryPos(h, out var x, out var z)) continue;
            var outline = ArrayOf(h, "outline")
                .Select(p => TryDirectPos(p, out var ox, out var oz) ? new[] { ox, oz } : null)
                .Where(p => p is not null)
                .Select(p => p!)
                .ToList();
            markers.Add(Marker("hazard", ReadStr(h, "name") ?? ReadStr(h, "hazardType") ?? "危险区",
                x, z, "", outline.Count >= 3 ? outline : null));
        }

        foreach (var w in ArrayOf(data, "stationaryWeapons"))
        {
            if (!TryPos(w, out var x, out var z)) continue;
            var wname = w.TryGetProperty("stationaryWeapon", out var sw) ? ReadStr(sw, "name") : null;
            markers.Add(Marker("stationary_weapon", wname ?? "固定武器", x, z, ""));
        }

        foreach (var lb in ArrayOf(data, "labels"))
        {
            if (!lb.TryGetProperty("position", out var p) || p.ValueKind != JsonValueKind.Array ||
                p.GetArrayLength() < 2) continue;
            markers.Add(Marker("label", ReadStr(lb, "text") ?? ReadStr(lb, "name") ?? "",
                p[0].GetDouble(), p[1].GetDouble(), ""));
        }

        foreach (var c in ArrayOf(data, "lootContainers"))
        {
            if (!InRange(c) || !TryPos(c, out var x, out var z)) continue;
            var cname = c.TryGetProperty("lootContainer", out var lc) ? ReadStr(lc, "name") : null;
            markers.Add(Marker("loot_container", cname ?? "物资容器", x, z, ""));
        }

        // 地图图片：优先 native-cache/<key>.png；jpg 转 png；超过最大边长则等比缩小
        const int MaxImageSide = 3000;
        var mapDir = Path.Combine(outDir, "maps", key);
        Directory.CreateDirectory(mapDir);
        var pngSrc = Path.Combine(cacheDir, $"{key}.png");
        var jpgSrc = Path.Combine(cacheDir, $"{key}.jpg");
        var pngOut = Path.Combine(mapDir, "map.png");
        int imgW, imgH;

        using (var srcImg = File.Exists(pngSrc) ? Image.FromFile(pngSrc)
             : File.Exists(jpgSrc) ? Image.FromFile(jpgSrc)
             : throw new FileNotFoundException("缺少地图图片"))
        {
            var scale = Math.Min(1.0, (double)MaxImageSide / Math.Max(srcImg.Width, srcImg.Height));
            imgW = (int)Math.Round(srcImg.Width * scale);
            imgH = (int)Math.Round(srcImg.Height * scale);

            if (scale >= 1.0 && File.Exists(pngSrc))
            {
                File.Copy(pngSrc, pngOut, overwrite: true);
            }
            else
            {
                using var bmp = new Bitmap(imgW, imgH);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(srcImg, 0, 0, imgW, imgH);
                }
                bmp.Save(pngOut, System.Drawing.Imaging.ImageFormat.Png);
                if (scale < 1.0)
                {
                    Console.WriteLine($"        图片过大已缩小: {srcImg.Width}×{srcImg.Height} → {imgW}×{imgH}");
                }
            }
        }

        // 非法点位检查：超出 bounds 的标记
        var minX = Math.Min(x0, x1); var maxX = Math.Max(x0, x1);
        var minZ = Math.Min(z0, z1); var maxZ = Math.Max(z0, z1);

        // 手工补录点位（manual_overrides.json 中按地图 id 登记，每次生成自动带上）
        foreach (var ov in LoadManualOverrides(key))
        {
            markers.Add(ov);
        }
        var invalid = markers.Count(m => m.x < minX || m.x > maxX || m.z < minZ || m.z > maxZ);

        var mapJson = new
        {
            schemaVersion = 1,
            id = key,
            name,
            image = new { file = "map.png", width = imgW, height = imgH },
            worldBounds = new { x0, z0, x1, z1, reverseCoordinate = reverse, coordinateRotation = coordRotation },
            markers
        };
        var json = JsonSerializer.Serialize(mapJson,
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(Path.Combine(mapDir, "map.json"), json);

        var counts = markers.GroupBy(m => m.type).OrderBy(g => g.Key)
            .Select(g => $"{g.Key}×{g.Count()}");
        Console.WriteLine($"[完成] {name}({key})  {imgW}×{imgH}  点位 {markers.Count}  非法 {invalid}");
        Console.WriteLine($"        {string.Join(" ", counts)}");
        return invalid;
    }

    private static IEnumerable<JsonElement> ArrayOf(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    private static bool TryPos(JsonElement e, out double x, out double z)
    {
        x = z = 0;
        if (!e.TryGetProperty("position", out var p) || p.ValueKind != JsonValueKind.Object ||
            !p.TryGetProperty("x", out var px) || !p.TryGetProperty("z", out var pz) ||
            px.ValueKind != JsonValueKind.Number || pz.ValueKind != JsonValueKind.Number)
        {
            return false;
        }
        x = px.GetDouble();
        z = pz.GetDouble();
        return double.IsFinite(x) && double.IsFinite(z);
    }

    /// <summary>读取直接写在对象上的 x/z（用于 outline 轮廓点）。</summary>
    private static bool TryDirectPos(JsonElement p, out double x, out double z)
    {
        x = z = 0;
        if (p.ValueKind != JsonValueKind.Object ||
            !p.TryGetProperty("x", out var px) || !p.TryGetProperty("z", out var pz) ||
            px.ValueKind != JsonValueKind.Number || pz.ValueKind != JsonValueKind.Number)
        {
            return false;
        }
        x = px.GetDouble();
        z = pz.GetDouble();
        return double.IsFinite(x) && double.IsFinite(z);
    }

    private static string? ReadStr(JsonElement e, string prop) =>
        e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()?.Trim()
            : null;

    private static HashSet<string> StrArray(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray()
                .Where(i => i.ValueKind == JsonValueKind.String)
                .Select(i => (i.GetString() ?? "").ToLowerInvariant())
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    /// <summary>读取 Tools/manual_overrides.json 中该地图的手工补录点位。</summary>
    private static List<MarkerOut> LoadManualOverrides(string mapKey)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "manual_overrides.json");
        file = Path.GetFullPath(file);
        var result = new List<MarkerOut>();
        if (!File.Exists(file))
        {
            return result;
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (!doc.RootElement.TryGetProperty(mapKey, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var m in arr.EnumerateArray())
            {
                var type = ReadStr(m, "type") ?? "label";
                var name = ReadStr(m, "name") ?? "";
                var x = m.GetProperty("x").GetDouble();
                var z = m.GetProperty("z").GetDouble();
                result.Add(new MarkerOut(mapKey, type, name, x, z, ReadStr(m, "id") ?? ""));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[警告] 手工补录文件读取失败: {ex.Message}");
        }
        return result;
    }

    private sealed class MarkerOut
    {
        public MarkerOut(string mapId, string type, string name, double x, double z,
            string idRaw, List<double[]>? outline = null)
        {
            this.type = type;
            this.name = name;
            this.x = Math.Round(x, 2);
            this.z = Math.Round(z, 2);
            this.outline = outline;
            id = idRaw.Length > 0
                ? idRaw
                : StableMarkerId.Create(SourceTag, mapId, type, name, x, z);
        }

        public string id { get; }
        public string type { get; }
        public string name { get; }
        public double x { get; }
        public double z { get; }

        /// <summary>危险区轮廓多边形（世界坐标 [x,z] 列表），null 不序列化。</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<double[]>? outline { get; }

        public object metadata { get; } = new { source = SourceTag };
    }

    private sealed record MapListEntry(string id, string name, string directory, bool enabled);
}
