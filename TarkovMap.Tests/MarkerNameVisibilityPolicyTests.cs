using TarkovMap.Controls;
using TarkovMap.Models;
using Xunit;

namespace TarkovMap.Tests;

public sealed class MarkerNameVisibilityPolicyTests
{
    [Theory]
    [InlineData(0.24, false)]
    [InlineData(0.25, true)]
    [InlineData(0.39, true)]
    [InlineData(0.70, true)]
    public void MapLabels_AppearAtOrAboveTwentyFivePercent(double zoom, bool expected)
    {
        Assert.Equal(expected, MarkerNameVisibilityPolicy.ShouldDrawName(MarkerType.Label, zoom));
    }

    [Theory]
    [InlineData(MarkerType.ExtractPmc)]
    [InlineData(MarkerType.ExtractScav)]
    [InlineData(MarkerType.ExtractShared)]
    [InlineData(MarkerType.ExtractTransit)]
    [InlineData(MarkerType.Boss)]
    [InlineData(MarkerType.Hazard)]
    public void CriticalTacticalNames_RemainVisibleAtLowZoom(MarkerType markerType)
    {
        Assert.True(MarkerNameVisibilityPolicy.ShouldDrawName(markerType, 0.10));
    }
}
