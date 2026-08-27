using TarkovMap.Models;

namespace TarkovMap.Services;

/// <summary>
/// Marker 图标缓存：每种类型只加载/生成一次，退出时统一 Dispose。
/// 核心类别使用 TarkovMap 自有 Data/icons PNG；
/// 非核心兼容类别缺少资产时程序生成 22×22 字母图标。
/// </summary>
public sealed class IconCache : IDisposable
{
    public const int IconSize = 22;
    public const int ExtractIconSize = 34;

    /// <summary>撤离点/转移点用大图标，方便观察；其余用小图标。</summary>
    public static int SizeFor(MarkerType type) => type switch
    {
        MarkerType.ExtractPmc or MarkerType.ExtractScav or
        MarkerType.ExtractShared or MarkerType.ExtractTransit => ExtractIconSize,
        MarkerType.Hazard => 26,
        MarkerType.Boss => 28,
        _ => IconSize
    };

    private readonly Dictionary<MarkerType, Image> _icons = new();
    private readonly string _iconsDirectory;

    public IconCache(string iconsDirectory)
    {
        _iconsDirectory = iconsDirectory;
    }

    public Image Get(MarkerType type)
    {
        if (_icons.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var icon = type switch
        {
            MarkerType.ExtractPmc => LoadFile("extract_pmc.png"),
            MarkerType.ExtractScav => LoadFile("extract_scav.png"),
            MarkerType.ExtractShared => LoadFile("extract_shared.png"),
            MarkerType.ExtractTransit => LoadFile("extract_transit.png"),
            MarkerType.SpawnPmc => LoadFile("spawn_pmc.png"),
            MarkerType.SpawnScav => LoadFile("spawn_scav.png"),
            MarkerType.Boss => LoadFile("boss.png"),
            MarkerType.Hazard => LoadFile("hazard.png"),
            _ => null
        } ?? GenerateLetterIcon(type);

        _icons[type] = icon;
        return icon;
    }

    private Image? LoadFile(string fileName)
    {
        var path = Path.Combine(_iconsDirectory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var source = new Bitmap(stream);
        return new Bitmap(source);
    }

    private static Image GenerateLetterIcon(MarkerType type)
    {
        var (bg, letter) = type switch
        {
            MarkerType.SpawnPmc => (Color.FromArgb(76, 175, 80), "P"),
            MarkerType.SpawnScav => (Color.FromArgb(0, 150, 136), "S"),
            MarkerType.Boss => (Color.FromArgb(211, 47, 47), "B"),
            MarkerType.LootContainer => (Color.FromArgb(121, 85, 72), "L"),
            MarkerType.Lock => (Color.FromArgb(245, 124, 0), "K"),
            MarkerType.Hazard => (Color.FromArgb(251, 192, 45), "!"),
            MarkerType.StationaryWeapon => (Color.FromArgb(123, 31, 162), "W"),
            _ => (Color.FromArgb(117, 117, 117), "?")
        };

        var bmp = new Bitmap(IconSize, IconSize);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(bg);
            g.FillEllipse(brush, 0, 0, IconSize - 1, IconSize - 1);
            using var border = new Pen(Color.FromArgb(180, 0, 0, 0), 1f);
            g.DrawEllipse(border, 0, 0, IconSize - 1, IconSize - 1);
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            using var textBrush = new SolidBrush(Color.White);
            var size = g.MeasureString(letter, font);
            g.DrawString(letter, font, textBrush,
                (IconSize - size.Width) / 2, (IconSize - size.Height) / 2);
        }
        return bmp;
    }

    public void Dispose()
    {
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }
        _icons.Clear();
    }
}
