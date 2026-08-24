using TarkovMap.Services;
using Xunit;

namespace TarkovMap.Tests;

/// <summary>
/// 截图文件名解析器的核心回归测试。保护最容易被后续改动破坏的格式解析。
/// 真实文件名格式：YYYY-MM-DD[HH-mm]_X, Y, Z_qx, qy, qz, qw_FOV (序号).png
/// </summary>
public class ScreenshotLocationParserTests
{
    [Fact]
    public void ValidFilename_ParsesAllFields()
    {
        var ok = ScreenshotLocationParser.TryParse(
            "2026-08-25[14-30]_-239.36, 15.5, 109.8_0.1, 0.2, 0.3, 0.9_55 (0).png", out var loc);
        Assert.True(ok);
        Assert.Equal(-239.36, loc.X, 6);
        Assert.Equal(15.5, loc.Y, 6);
        Assert.Equal(109.8, loc.Z, 6);
        Assert.Equal(0.1, loc.Rotation.X, 6);
        Assert.Equal(0.2, loc.Rotation.Y, 6);
        Assert.Equal(0.3, loc.Rotation.Z, 6);
        Assert.Equal(0.9, loc.Rotation.W, 6);
    }

    [Fact]
    public void FilenameWithSequenceIndex_Parses()
    {
        var ok = ScreenshotLocationParser.TryParse(
            "2026-08-25[14-30]_-100, 20, 300_0, 0.7, 0, 0.7_55 (3).png", out var loc);
        Assert.True(ok);
        Assert.Equal(-100, loc.X, 6);
        Assert.Equal(300, loc.Z, 6);
    }

    [Fact]
    public void NegativeCoordsAndNoWhitespace_Parses()
    {
        // 文件名字段间无空格也应解析
        var ok = ScreenshotLocationParser.TryParse(
            "2026-08-25[14-30]_100.25,-20.5,33_0.1,0.2,0.3,0.9_55 (0).png", out var loc);
        Assert.True(ok);
        Assert.Equal(100.25, loc.X, 6);
        Assert.Equal(-20.5, loc.Y, 6);
        Assert.Equal(33, loc.Z, 6);
    }

    [Fact]
    public void NoCoordinate_ReturnsFalse()
    {
        // 结算/仓库画面这类无坐标截图，应静默跳过（TryParse=false）
        Assert.False(ScreenshotLocationParser.TryParse("2026-08-25[14-30]图片.png", out _));
    }

    [Fact]
    public void NonScreenshotFile_ReturnsFalse()
    {
        Assert.False(ScreenshotLocationParser.TryParse("IMG_0001.jpg", out _));
        Assert.False(ScreenshotLocationParser.TryParse("random.txt", out _));
    }
}
