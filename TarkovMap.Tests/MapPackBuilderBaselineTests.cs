using System.Text.Json;
using TarkovMap.Services;
using Xunit;

namespace TarkovMap.Tests;

public sealed class MapPackBuilderBaselineTests
{
    [Fact]
    public void CheckedInMapDataMatchesV111Baseline()
    {
        var repoRoot = FindRepositoryRoot();
        var dataDirectory = Path.Combine(repoRoot, "TarkovMap", "Data");
        var baselineFile = Path.Combine(AppContext.BaseDirectory, "TestData", "baseline-v1.1.1.json");

        using var baselineDoc = JsonDocument.Parse(File.ReadAllText(baselineFile));
        using var mapListDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataDirectory, "maps.json")));

        var repository = new MapRepository(dataDirectory);
        Assert.Null(repository.LoadManifest());
        Assert.Equal(11, repository.LoadMapList().Count);

        var expectedMaps = baselineDoc.RootElement.GetProperty("maps")
            .EnumerateArray()
            .ToDictionary(map => map.GetProperty("id").GetString()!, StringComparer.Ordinal);
        var actualEntries = mapListDoc.RootElement.GetProperty("maps").EnumerateArray().ToList();

        Assert.Equal(baselineDoc.RootElement.GetProperty("mapCount").GetInt32(), actualEntries.Count);
        Assert.Equal(expectedMaps.Keys.Order(), actualEntries
            .Select(entry => entry.GetProperty("id").GetString()!)
            .Order());

        foreach (var entry in actualEntries)
        {
            var id = entry.GetProperty("id").GetString()!;
            var directory = entry.GetProperty("directory").GetString()!;
            using var mapDoc = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(dataDirectory, directory, "map.json")));
            var map = mapDoc.RootElement;
            var expected = expectedMaps[id];

            Assert.Equal(expected.GetProperty("imageWidth").GetInt32(),
                map.GetProperty("image").GetProperty("width").GetInt32());
            Assert.Equal(expected.GetProperty("imageHeight").GetInt32(),
                map.GetProperty("image").GetProperty("height").GetInt32());
            var bounds = map.GetProperty("worldBounds");
            Assert.Equal(expected.GetProperty("x0").GetDouble(), bounds.GetProperty("x0").GetDouble());
            Assert.Equal(expected.GetProperty("z0").GetDouble(), bounds.GetProperty("z0").GetDouble());
            Assert.Equal(expected.GetProperty("x1").GetDouble(), bounds.GetProperty("x1").GetDouble());
            Assert.Equal(expected.GetProperty("z1").GetDouble(), bounds.GetProperty("z1").GetDouble());
            Assert.Equal(expected.GetProperty("reverseCoordinate").GetBoolean(),
                bounds.GetProperty("reverseCoordinate").GetBoolean());
            Assert.Equal(expected.GetProperty("coordinateRotation").GetDouble(),
                bounds.GetProperty("coordinateRotation").GetDouble());
            Assert.Equal(expected.GetProperty("markerCount").GetInt32(),
                map.GetProperty("markers").GetArrayLength());

            var expectedTypes = expected.GetProperty("markerTypes").EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.GetInt32(),
                    StringComparer.Ordinal);
            var actualTypes = map.GetProperty("markers").EnumerateArray()
                .GroupBy(marker => marker.GetProperty("type").GetString()!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            Assert.Equal(expectedTypes.OrderBy(pair => pair.Key), actualTypes.OrderBy(pair => pair.Key));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) &&
                Directory.Exists(Path.Combine(directory.FullName, "TarkovMap")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法从测试输出目录定位 TarkovMap 仓库根目录。");
    }
}
