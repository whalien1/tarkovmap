using MapPackBuilder.Calibration;
using MapPackBuilder.Output;
using MapPackBuilder.Sources;
using Xunit;

namespace TarkovMap.Tests;

public sealed class RuntimeMapProjectorTests
{
    [Fact]
    public void Project_CoreMarkersUseHeightFilterAndStableIds()
    {
        var map = new TarkovDevMap(
            "upstream-customs", "customs", "海关", SourceMapDisposition.Existing, 180,
            [new TarkovDevExtract("extract-id", "撤离点", "pmc", new SourcePosition(1, 0, 2))],
            [new TarkovDevTransit("transit-id", "转移点", new SourcePosition(2, 0, 3))],
            [
                new TarkovDevSpawn("pmc-zone", new SourcePosition(3, 0, 4), ["pmc"], ["player"]),
                new TarkovDevSpawn("outside", new SourcePosition(4, 8, 5), ["scav"], ["player"]),
                new TarkovDevSpawn("boss-zone", new SourcePosition(5, 0, 6), ["scav"], ["boss"]),
                new TarkovDevSpawn("sniper", new SourcePosition(6, 0, 7), ["scav"], ["sniper"])
            ],
            [new TarkovDevBoss("Boss 测试", [new TarkovDevBossLocation("宿舍", "boss-zone")])],
            [new TarkovDevHazard("hazard-id", "雷区", new SourcePosition(7, 0, 8),
                [new SourcePosition(6, 0, 7), new SourcePosition(8, 0, 7), new SourcePosition(7, 0, 9)]),
             new TarkovDevHazard("hazard-id", "雷区", new SourcePosition(8, 0, 9),
                [new SourcePosition(7, 0, 8), new SourcePosition(9, 0, 8), new SourcePosition(8, 0, 10)])]);
        var calibration = new MapCalibration(
            "customs", "Customs.svg", "Ground_Level", 0,
            10, 20, -10, -20, false, 90, -1, 3);

        var first = RuntimeMapProjector.Project(map, calibration);
        var second = RuntimeMapProjector.Project(map, calibration);

        Assert.Equal(6, first.Count);
        Assert.Single(first, marker => marker.Type == "spawn_pmc");
        Assert.DoesNotContain(first, marker => marker.Type == "spawn_scav");
        Assert.Equal("Boss 测试（宿舍）", Assert.Single(first, marker => marker.Type == "boss").Name);
        Assert.All(first.Where(marker => marker.Type == "hazard"),
            marker => Assert.Equal(3, marker.Outline!.Count));
        Assert.Equal(first.Count, first.Select(marker => marker.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first.Select(marker => marker.Id), second.Select(marker => marker.Id));
    }

    [Fact]
    public void ContentHash_IsDeterministicAndDetectsChanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tarkov-hash-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "maps"));
        try
        {
            File.WriteAllText(Path.Combine(directory, "maps.json"), "one");
            File.WriteAllText(Path.Combine(directory, "maps", "map.json"), "two");
            File.WriteAllText(Path.Combine(directory, "manifest.json"), "ignored");

            var first = MapDataContentHasher.Compute(directory);
            File.WriteAllText(Path.Combine(directory, "manifest.json"), "still ignored");
            var second = MapDataContentHasher.Compute(directory);
            File.WriteAllText(Path.Combine(directory, "maps.json"), "changed");
            var changed = MapDataContentHasher.Compute(directory);

            Assert.Equal(first, second);
            Assert.NotEqual(first, changed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
