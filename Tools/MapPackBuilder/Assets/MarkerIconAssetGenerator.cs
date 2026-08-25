using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MapPackBuilder.Assets;

internal static class MarkerIconAssetGenerator
{
    public const int AssetSize = 96;

    private static readonly IReadOnlyDictionary<string, Action<Graphics>> Renderers =
        new Dictionary<string, Action<Graphics>>(StringComparer.Ordinal)
        {
            ["extract_pmc.png"] = graphics => DrawExit(graphics, Color.FromArgb(66, 160, 92)),
            ["extract_scav.png"] = graphics => DrawExit(graphics, Color.FromArgb(19, 145, 137)),
            ["extract_shared.png"] = graphics => DrawSharedExit(graphics, Color.FromArgb(48, 130, 201)),
            ["extract_transit.png"] = graphics => DrawTransit(graphics, Color.FromArgb(126, 87, 194)),
            ["spawn_pmc.png"] = graphics => DrawSpawn(graphics, Color.FromArgb(104, 159, 56), true),
            ["spawn_scav.png"] = graphics => DrawSpawn(graphics, Color.FromArgb(0, 137, 123), false),
            ["boss.png"] = graphics => DrawBoss(graphics),
            ["hazard.png"] = DrawHazard
        };

    public static IReadOnlyList<string> Generate(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var files = new List<string>();
        foreach (var (fileName, render) in Renderers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            using var bitmap = new Bitmap(AssetSize, AssetSize, PixelFormat.Format32bppArgb);
            bitmap.SetResolution(96, 96);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Transparent);
                render(graphics);
            }

            var output = Path.Combine(outputDirectory, fileName);
            bitmap.Save(output, ImageFormat.Png);
            files.Add(output);
        }

        return files;
    }

    private static void DrawExit(Graphics graphics, Color color)
    {
        DrawCircleBadge(graphics, color);
        using var white = RoundPen(Color.White, 8);
        graphics.DrawLines(white,
        [
            new PointF(51, 25), new PointF(31, 25), new PointF(31, 71), new PointF(51, 71)
        ]);
        graphics.DrawLine(white, 42, 48, 72, 48);
        graphics.DrawLines(white,
        [
            new PointF(61, 36), new PointF(73, 48), new PointF(61, 60)
        ]);
    }

    private static void DrawSharedExit(Graphics graphics, Color color)
    {
        DrawCircleBadge(graphics, color);
        using var white = RoundPen(Color.White, 7);
        graphics.DrawLine(white, 25, 39, 70, 39);
        graphics.DrawLines(white,
        [
            new PointF(60, 29), new PointF(70, 39), new PointF(60, 49)
        ]);
        graphics.DrawLine(white, 70, 58, 25, 58);
        graphics.DrawLines(white,
        [
            new PointF(35, 48), new PointF(25, 58), new PointF(35, 68)
        ]);
    }

    private static void DrawTransit(Graphics graphics, Color color)
    {
        DrawCircleBadge(graphics, color);
        using var white = RoundPen(Color.White, 8);
        graphics.DrawLines(white,
        [
            new PointF(25, 31), new PointF(42, 48), new PointF(25, 65)
        ]);
        graphics.DrawLines(white,
        [
            new PointF(50, 31), new PointF(67, 48), new PointF(50, 65)
        ]);
    }

    private static void DrawSpawn(Graphics graphics, Color color, bool fourPointStar)
    {
        using var shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
        graphics.FillEllipse(shadow, 13, 16, 70, 70);
        using var fill = new SolidBrush(color);
        using var border = new Pen(Color.FromArgb(235, 255, 255, 255), 5);
        var diamond = new[]
        {
            new PointF(48, 11), new PointF(85, 48), new PointF(48, 85), new PointF(11, 48)
        };
        graphics.FillPolygon(fill, diamond);
        graphics.DrawPolygon(border, diamond);
        using var white = new SolidBrush(Color.White);
        if (fourPointStar)
        {
            graphics.FillPolygon(white,
            [
                new PointF(48, 25), new PointF(54, 42), new PointF(71, 48),
                new PointF(54, 54), new PointF(48, 71), new PointF(42, 54),
                new PointF(25, 48), new PointF(42, 42)
            ]);
        }
        else
        {
            graphics.FillEllipse(white, 35, 35, 26, 26);
            using var inner = new SolidBrush(Color.FromArgb(210, 15, 45, 45));
            graphics.FillEllipse(inner, 43, 43, 10, 10);
        }
    }

    private static void DrawBoss(Graphics graphics)
    {
        DrawCircleBadge(graphics, Color.FromArgb(198, 54, 54));
        using var crown = new SolidBrush(Color.FromArgb(255, 225, 92));
        using var outline = new Pen(Color.FromArgb(220, 78, 30, 20), 4)
        {
            LineJoin = LineJoin.Round
        };
        var points = new[]
        {
            new PointF(22, 35), new PointF(36, 48), new PointF(48, 27),
            new PointF(60, 48), new PointF(74, 35), new PointF(68, 67),
            new PointF(28, 67)
        };
        graphics.FillPolygon(crown, points);
        graphics.DrawPolygon(outline, points);
        graphics.DrawLine(outline, 28, 67, 68, 67);
    }

    private static void DrawHazard(Graphics graphics)
    {
        using var shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
        graphics.FillPolygon(shadow,
        [
            new PointF(50, 10), new PointF(91, 82), new PointF(9, 82)
        ]);
        using var fill = new SolidBrush(Color.FromArgb(255, 193, 7));
        using var border = new Pen(Color.FromArgb(235, 45, 38, 20), 6)
        {
            LineJoin = LineJoin.Round
        };
        var triangle = new[]
        {
            new PointF(48, 8), new PointF(88, 79), new PointF(8, 79)
        };
        graphics.FillPolygon(fill, triangle);
        graphics.DrawPolygon(border, triangle);
        using var dark = new SolidBrush(Color.FromArgb(235, 35, 35, 28));
        graphics.FillRectangle(dark, 43, 31, 10, 27);
        graphics.FillEllipse(dark, 42, 64, 12, 12);
    }

    private static void DrawCircleBadge(Graphics graphics, Color color)
    {
        using var shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
        graphics.FillEllipse(shadow, 8, 10, 80, 80);
        using var fill = new SolidBrush(color);
        graphics.FillEllipse(fill, 7, 7, 80, 80);
        using var border = new Pen(Color.FromArgb(235, 255, 255, 255), 5);
        graphics.DrawEllipse(border, 9.5f, 9.5f, 75, 75);
    }

    private static Pen RoundPen(Color color, float width) => new(color, width)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round
    };
}
