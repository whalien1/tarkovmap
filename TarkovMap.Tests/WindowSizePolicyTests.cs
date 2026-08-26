using TarkovMap.Infrastructure;
using Xunit;

namespace TarkovMap.Tests;

public sealed class WindowSizePolicyTests
{
    [Fact]
    public void FitClientSizeToWorkingArea_KeepsPreferredSizeWhenTwoKWorkingAreaFits()
    {
        var actual = WindowSizePolicy.FitClientSizeToWorkingArea(
            new Size(1800, 1000), new Size(2560, 1400), new Size(16, 86));

        Assert.Equal(new Size(1800, 1000), actual);
    }

    [Fact]
    public void FitClientSizeToWorkingArea_ReducesClientSizeToStayInsideSmallWorkingArea()
    {
        var actual = WindowSizePolicy.FitClientSizeToWorkingArea(
            new Size(1800, 1000), new Size(1366, 728), new Size(16, 86));

        Assert.Equal(new Size(1350, 642), actual);
    }
}
