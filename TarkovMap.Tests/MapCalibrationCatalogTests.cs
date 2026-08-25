using MapPackBuilder.Calibration;
using Xunit;

namespace TarkovMap.Tests;

public sealed class MapCalibrationCatalogTests
{
    private static string CalibrationPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "calibration-v1.1.1.json");

    [Fact]
    public void CurrentCatalog_CoversAllExistingMapsAndPreservesManualCorrections()
    {
        var catalog = MapCalibrationCatalog.Load(CalibrationPath);

        Assert.Equal("TarkovMap-v1.1.1", catalog.SourceBaseline);
        Assert.Equal(11, catalog.Maps.Count);

        Assert.True(catalog.TryGet("customs", out var customs));
        Assert.Equal(90, customs.CoordinateRotation);
        Assert.Equal("Customs.svg", customs.SvgAsset);

        Assert.True(catalog.TryGet("the-lab", out var lab));
        Assert.Equal(-0.9, lab.MinY);
        Assert.Equal(3, lab.MaxY);
        Assert.True(lab.ReverseCoordinate);

        Assert.True(catalog.TryGet("the-labyrinth", out var labyrinth));
        Assert.Null(labyrinth.SvgAsset);
        Assert.Null(labyrinth.MinY);
        Assert.Null(labyrinth.MaxY);
    }

    [Fact]
    public void InvalidCatalog_IsRejected()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tarkov-calibration-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "invalid.json");
        try
        {
            File.WriteAllText(path, """
                {
                  "schemaVersion": 1,
                  "sourceBaseline": "test",
                  "maps": [
                    { "mapId": "customs", "x0": 1, "z0": 2, "x1": 1, "z1": 3, "coordinateRotation": 90 }
                  ]
                }
                """);

            Assert.Throws<InvalidDataException>(() => MapCalibrationCatalog.Load(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
