using System.Text.Json;
using TarkovMap.Models;
using TarkovMap.Services;

namespace RotationCalibrator;

/// <summary>
/// 逐张地图校准 coordinateRotation 的开发期工具（不进客户端，不修改客户端功能）。
/// 方法见《TarkovMap_coordinateRotation_实测校准方法.md》：两张截图之间玩家沿面朝方向直线移动，
/// 由「地图上的实际移动方向」与「截图记录的朝向」之差反推 coordinateRotation。
/// 本工具只测算与提示，绝不自动写入 RotationOverrides。
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[错误] {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            PrintUsage();
            return 2;
        }

        var mapPath = args[0];
        var shotArgs = args[1..];
        if (shotArgs.Length % 2 != 0)
        {
            Console.Error.WriteLine("[用法错误] 截图文件名必须成对给出（每对 = 一组 A/B 样本）。");
            PrintUsage();
            return 2;
        }

        var map = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(mapPath), JsonOptions)
                  ?? throw new InvalidDataException($"无法解析地图数据：{mapPath}");
        var b = map.Bounds;
        var imageW = map.Image.Width;
        var imageH = map.Image.Height;

        Console.WriteLine($"地图：{map.Name}（id={map.Id}）");
        Console.WriteLine($"图片：{imageW}×{imageH}");
        Console.WriteLine($"worldBounds：[{b.X0},{b.Z0}] → [{b.X1},{b.Z1}]，reverseCoordinate={b.ReverseCoordinate}，源 coordinateRotation={b.CoordinateRotation}");
        Console.WriteLine();

        var samples = new List<Sample>();
        var rejected = new List<(string Label, string Reason)>();

        for (var i = 0; i < shotArgs.Length; i += 2)
        {
            var aName = shotArgs[i];
            var bName = shotArgs[i + 1];
            var label = $"样本 {(i / 2) + 1}";

            if (!ScreenshotLocationParser.TryParse(aName, out var locA))
            {
                rejected.Add((label, $"截图A无法解析：{Path.GetFileName(aName)}"));
                continue;
            }
            if (!ScreenshotLocationParser.TryParse(bName, out var locB))
            {
                rejected.Add((label, $"截图B无法解析：{Path.GetFileName(bName)}"));
                continue;
            }

            var mapBearing = ComputeMapBearing(b, imageW, imageH, locA, locB);
            var distance = Math.Sqrt(Sq(locB.X - locA.X) + Sq(locB.Z - locA.Z));
            var yawDelta = CircularDelta(locA.YawDegrees, locB.YawDegrees);
            var avgYaw = CircularMean(locA.YawDegrees, locB.YawDegrees);
            var rotation = Normalize(mapBearing - avgYaw - 90.0);

            var sample = new Sample(label, locA.X, locA.Z, locA.YawDegrees, locB.X, locB.Z, locB.YawDegrees,
                distance, yawDelta, mapBearing, avgYaw, rotation);
            samples.Add(sample);
        }

        // 逐样本输出
        foreach (var s in samples)
        {
            s.Print();
            Console.WriteLine();
        }
        foreach (var r in rejected)
        {
            Console.WriteLine($"[弃用] {r.Label}：{r.Reason}（已跳过，不参与聚合）");
        }

        // 聚合（剔除被拒样本）
        var usable = samples.Where(s => s.QualityKind != SampleQuality.Reject).ToList();
        if (usable.Count == 0)
        {
            Console.WriteLine("\n没有可用的有效样本，无法给出推荐。请参照方法文档重新采集（移动距离≥20m、Yaw变化≤5°）。");
            return 1;
        }

        Console.WriteLine("===== 聚合 =====");
        foreach (var s in usable)
        {
            Console.WriteLine($"样本 {s.Label}：{s.Rotation:F1}°（质量：{s.QualityLabel}）");
        }

        var avg = CircularMean(usable.Select(s => s.Rotation));
        var maxDev = usable.Max(s => CircularDelta(s.Rotation, avg));
        var nearest = NearestStandard(avg);

        Console.WriteLine();
        Console.WriteLine($"平均结果：{avg:F2}°");
        Console.WriteLine($"最大离差：{maxDev:F1}°");
        Console.WriteLine($"最近标准角：{nearest:F0}°");
        Console.WriteLine($"推荐：[\"{map.Id}\"] = {nearest:F0}.0");
        Console.WriteLine("（提示：该值仅为测算建议，写入 RotationOverrides 前请人工确认后重跑 MapPackBuilder。）");

        return 0;
    }

    /// <summary>地图上的移动方向（图片坐标，上=0°、顺时针）。</summary>
    private static double ComputeMapBearing(WorldBounds b, int imageW, int imageH, PlayerLocation a, PlayerLocation c)
    {
        var pa = MapCoordinateService.WorldToImage(b, imageW, imageH, a.X, a.Z);
        var pb = MapCoordinateService.WorldToImage(b, imageW, imageH, c.X, c.Z);
        var dx = pb.X - pa.X;
        var dy = pb.Y - pa.Y;
        return Normalize(Math.Atan2(dx, -dy) * 180.0 / Math.PI);
    }

    private static double Sq(double v) => v * v;

    private static double Normalize(double deg)
    {
        deg %= 360.0;
        if (deg < 0) deg += 360.0;
        return deg;
    }

    private static double CircularMean(params double[] degs) => CircularMean((IEnumerable<double>)degs);

    private static double CircularMean(IEnumerable<double> degs)
    {
        double sx = 0, sy = 0;
        var n = 0;
        foreach (var d in degs)
        {
            var r = d * Math.PI / 180.0;
            sx += Math.Cos(r);
            sy += Math.Sin(r);
            n++;
        }
        if (n == 0) return double.NaN;
        return Normalize(Math.Atan2(sy, sx) * 180.0 / Math.PI);
    }

    /// <summary>两个角度间的最小夹角（0～180°）。</summary>
    private static double CircularDelta(double a, double b)
    {
        var d = Math.Abs(Normalize(a - b));
        return d > 180.0 ? 360.0 - d : d;
    }

    private static double NearestStandard(double deg)
    {
        double best = 0, bestDist = double.MaxValue;
        foreach (var s in new[] { 0.0, 90.0, 180.0, 270.0 })
        {
            var d = CircularDelta(deg, s);
            if (d < bestDist)
            {
                bestDist = d;
                best = s;
            }
        }
        return best;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("用法：RotationCalibrator <map.json> <截图A> <截图B> [<截图A2> <截图B2> ...]");
        Console.WriteLine();
        Console.WriteLine("  map.json  目标地图的 Data/maps/<id>/map.json 路径");
        Console.WriteLine("  截图A/B   游戏截图文件名（含路径亦可，仅解析文件名，不读图片内容）");
        Console.WriteLine();
        Console.WriteLine("示例：");
        Console.WriteLine("  dotnet run --project Tools/RotationCalibrator -- TarkovMap/Data/maps/woods/map.json \"shotA_..._55 (0).png\" \"shotB_..._55 (1).png\"");
        Console.WriteLine("  （每对截图 = 一组样本；建议每图 2～3 组、至少两个不同移动方向）");
    }

    private enum SampleQuality { High, Ok, Marginal, Reject }

    private sealed record Sample(
        string Label,
        double X1, double Z1, double Yaw1,
        double X2, double Z2, double Yaw2,
        double Distance, double YawDelta, double MapBearing, double AvgYaw, double Rotation)
    {
        public SampleQuality QualityKind
        {
            get
            {
                if (Distance < 10 || YawDelta > 10) return SampleQuality.Reject;
                if (Distance >= 20 && YawDelta <= 5) return SampleQuality.High;
                if (Distance >= 15 && YawDelta <= 8) return SampleQuality.Ok;
                return SampleQuality.Marginal;
            }
        }

        public string QualityLabel => QualityKind switch
        {
            SampleQuality.High => "高",
            SampleQuality.Ok => "可用",
            SampleQuality.Marginal => "勉强",
            _ => "拒绝"
        };

        public void Print()
        {
            Console.WriteLine($"{Label}（质量：{QualityLabel}）");
            Console.WriteLine($"  截图A：X={X1:F2} Z={Z1:F2} Yaw={Yaw1:F1}°");
            Console.WriteLine($"  截图B：X={X2:F2} Z={Z2:F2} Yaw={Yaw2:F1}°");
            Console.WriteLine($"  移动距离：{Distance:F1} m");
            Console.WriteLine($"  Yaw 变化：{YawDelta:F1}°");
            Console.WriteLine($"  地图移动方向：{MapBearing:F1}°");
            Console.WriteLine($"  推算 coordinateRotation：{Rotation:F1}°");
            Console.WriteLine($"  最近标准角：{NearestStandard(Rotation):F0}°");
        }
    }
}
