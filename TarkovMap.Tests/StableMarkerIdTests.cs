using System.Globalization;
using MapPackBuilder;
using Xunit;

namespace TarkovMap.Tests;

public sealed class StableMarkerIdTests
{
    [Fact]
    public void SameInputAlwaysProducesSameId()
    {
        var first = StableMarkerId.Create("legacy", "customs", "spawn_pmc", "出生点", 12.345, -67.891);
        var second = StableMarkerId.Create("legacy", "customs", "spawn_pmc", "出生点", 12.345, -67.891);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IdIsIndependentOfCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
            var zh = StableMarkerId.Create("legacy", "customs", "boss", "Reshala", 12.5, -6.25);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var fr = StableMarkerId.Create("legacy", "customs", "boss", "Reshala", 12.5, -6.25);

            Assert.Equal(zh, fr);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void MapIdParticipatesInIdentity()
    {
        var customs = StableMarkerId.Create("legacy", "customs", "label", "仓库", 10, 20);
        var woods = StableMarkerId.Create("legacy", "woods", "label", "仓库", 10, 20);

        Assert.NotEqual(customs, woods);
    }

    [Fact]
    public void CoordinatesUseRuntimePrecision()
    {
        var first = StableMarkerId.Create("legacy", "customs", "spawn_scav", "Zone", 1.2341, 9.8761);
        var second = StableMarkerId.Create("legacy", "customs", "spawn_scav", "Zone", 1.2342, 9.8762);

        Assert.Equal(first, second);
    }
}
