using System.Drawing.Imaging;
using System.Globalization;
using System.Xml.Linq;
using MapPackBuilder.Calibration;
using MapPackBuilder.Sources;
using Svg;

namespace MapPackBuilder.Output;

internal sealed record MapImageBuildResult(int Width, int Height, string SourceDescription);

internal static class MapImageBuilder
{
    private const int MaxImageSide = 3000;

    public static MapImageBuildResult Build(
        MapCalibration calibration,
        SvgRepositorySnapshot svgSnapshot,
        string fallbackDataDirectory,
        string outputFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)
            ?? throw new ArgumentException("图片输出路径缺少目录。", nameof(outputFile)));

        if (calibration.SvgAsset is null)
        {
            return CopyFallback(calibration.MapId, fallbackDataDirectory, outputFile);
        }

        if (!svgSnapshot.Assets.TryGetValue(calibration.SvgAsset, out var svgBytes))
        {
            throw new InvalidDataException(
                $"地图 {calibration.MapId} 缺少 SVG 快照：{calibration.SvgAsset}。");
        }

        var svgText = System.Text.Encoding.UTF8.GetString(svgBytes);
        var filteredSvg = KeepPrimaryLayer(svgText, calibration.MapId,
            calibration.SvgLayer ?? throw new InvalidDataException(
                $"地图 {calibration.MapId} 没有配置 SVG 主楼层。"));
        _ = ReadViewBox(filteredSvg, calibration.MapId);
        var projectedWidth = calibration.ReverseCoordinate
            ? Math.Abs(calibration.Z1 - calibration.Z0)
            : Math.Abs(calibration.X1 - calibration.X0);
        var projectedHeight = calibration.ReverseCoordinate
            ? Math.Abs(calibration.X1 - calibration.X0)
            : Math.Abs(calibration.Z1 - calibration.Z0);
        var scale = MaxImageSide / Math.Max(projectedWidth, projectedHeight);
        var outputWidth = Math.Max(1, (int)Math.Round(projectedWidth * scale));
        var outputHeight = Math.Max(1, (int)Math.Round(projectedHeight * scale));
        var swapBeforeRotation = calibration.SvgRotationDegrees is 90 or 270;
        var rasterWidth = swapBeforeRotation ? outputHeight : outputWidth;
        var rasterHeight = swapBeforeRotation ? outputWidth : outputHeight;

        var document = SvgDocument.FromSvg<SvgDocument>(filteredSvg);
        using var bitmap = document.Draw(rasterWidth, rasterHeight)
            ?? throw new InvalidDataException($"SVG 渲染失败：{calibration.MapId}。");
        Rotate(bitmap, calibration.SvgRotationDegrees);
        SavePngAtomic(bitmap, outputFile);

        return new MapImageBuildResult(bitmap.Width, bitmap.Height,
            $"{GitHubSvgSource.RepositoryName}@{svgSnapshot.CommitSha[..12]}/{calibration.SvgAsset}" +
            $"#{calibration.SvgLayer}");
    }

    internal static string KeepPrimaryLayer(string svgText, string mapId, string layerId)
    {
        var xml = XDocument.Parse(svgText, LoadOptions.PreserveWhitespace);
        var root = xml.Root ?? throw new InvalidDataException($"SVG 缺少根节点：{mapId}。");
        root.SetAttributeValue("preserveAspectRatio", "none");
        var groups = root.Elements().Where(element => element.Name.LocalName == "g").ToList();
        if (!groups.Any(group => string.Equals((string?)group.Attribute("id"), layerId,
                StringComparison.Ordinal)))
        {
            throw new InvalidDataException($"地图 {mapId} 的 SVG 缺少主楼层 {layerId}。");
        }

        foreach (var group in groups)
        {
            var id = (string?)group.Attribute("id");
            var keepWith = ((string?)group.Attribute("data-keep-with-group") ?? "")
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!string.Equals(id, layerId, StringComparison.Ordinal) &&
                !keepWith.Contains(layerId, StringComparer.Ordinal))
            {
                group.Remove();
            }
        }

        return xml.ToString(SaveOptions.DisableFormatting);
    }

    private static (double Width, double Height) ReadViewBox(string svgText, string mapId)
    {
        var root = XDocument.Parse(svgText).Root
            ?? throw new InvalidDataException($"SVG 缺少根节点：{mapId}。");
        var values = ((string?)root.Attribute("viewBox") ?? "")
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length != 4 ||
            !double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ||
            !double.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var height) ||
            !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"地图 {mapId} 的 SVG viewBox 无效。");
        }

        return (width, height);
    }

    private static void Rotate(Bitmap bitmap, int degrees)
    {
        var rotateFlip = degrees switch
        {
            0 => RotateFlipType.RotateNoneFlipNone,
            90 => RotateFlipType.Rotate90FlipNone,
            180 => RotateFlipType.Rotate180FlipNone,
            270 => RotateFlipType.Rotate270FlipNone,
            _ => throw new InvalidDataException($"不支持的 SVG 图像旋转角：{degrees}。")
        };
        bitmap.RotateFlip(rotateFlip);
    }

    private static MapImageBuildResult CopyFallback(
        string mapId,
        string fallbackDataDirectory,
        string outputFile)
    {
        var source = Path.Combine(fallbackDataDirectory, "maps", mapId, "map.png");
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"地图 {mapId} 没有 SVG，也找不到兼容 PNG。", source);
        }

        File.Copy(source, outputFile, overwrite: true);
        using var image = Image.FromFile(source);
        return new MapImageBuildResult(image.Width, image.Height,
            $"TarkovMap v1.1.1 fallback/{mapId}/map.png");
    }

    private static void SavePngAtomic(Bitmap bitmap, string outputFile)
    {
        var temporaryFile = $"{outputFile}.{Guid.NewGuid():N}.tmp";
        try
        {
            bitmap.Save(temporaryFile, ImageFormat.Png);
            File.Move(temporaryFile, outputFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }
}
