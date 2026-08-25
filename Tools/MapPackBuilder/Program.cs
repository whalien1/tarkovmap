using System.Text.Json;
using System.Text.Json.Serialization;
using MapPackBuilder.Calibration;
using MapPackBuilder.Sources;

namespace MapPackBuilder;

/// <summary>
/// TarkovMap 地图数据构建工具（开发期使用，不进入正式客户端）。
/// 读取 ref/Tarkov_webmap/data/maps_detail.json + assets/maps/native-cache/*.png，
/// 生成 TarkovMap 自有 schema 的 Data/（maps.json + 每图 map.json + map.png）。
/// 用法: MapPackBuilder.exe &lt;数据仓库ref目录&gt; &lt;输出Data目录&gt;
/// </summary>
internal static class Program
{
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
    };

    private static int Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "pve-fetch", StringComparison.OrdinalIgnoreCase))
        {
            return RunPveFetchAsync(args[1..]).GetAwaiter().GetResult();
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
