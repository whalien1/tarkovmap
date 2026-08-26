using TarkovMap.Models;
using Xunit;

namespace TarkovMap.Tests;

public sealed class MiniMapSettingsTests
{
    [Fact]
    public void Defaults_AreVisibleSquareMediumAndSeventyFivePercent()
    {
        var settings = new MiniMapSettings();

        Assert.True(settings.Visible);
        Assert.Equal(MiniMapSettings.ShapeKind.Square, settings.Shape);
        Assert.Equal(MiniMapSettings.SizeKind.Medium, settings.Size);
        Assert.Equal(MiniMapSettings.OpacityKind.Medium, settings.Opacity);
        Assert.Equal(300, settings.PixelSize);
        Assert.Equal(0.75, settings.OpacityValue);
        Assert.Equal(MiniMapSettings.DefaultZoom, settings.Zoom);
        Assert.False(settings.MoreSettingsExpanded);
    }
}
