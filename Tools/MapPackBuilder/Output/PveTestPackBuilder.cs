using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapPackBuilder.Calibration;
using MapPackBuilder.Sources;
using TarkovMap.Models;
using TarkovMap.Services;

namespace MapPackBuilder.Output;

internal sealed record MapBuildSummary(
    string MapId,
    string Name,
    int MarkerCount,
    IReadOnlyDictionary<string, int> MarkerTypes,
    int ImageWidth,
    int ImageHeight,
    string ImageSource);

internal sealed record TestPackBuildResult(
    string DataDirectory,
    string ManifestFile,
    string ContentHash,
    IReadOnlyList<MapBuildSummary> Maps);

internal static class PveTestPackBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static TestPackBuildResult Build(
        string rootDirectory,
        string dataVersion,
        IReadOnlyList<TarkovDevMap> sourceMaps,
        SvgRepositorySnapshot svgSnapshot,
        MapCalibrationCatalog calibrationCatalog,
        string calibrationFile,
        string fallbackDataDirectory,
        IReadOnlyList<MapDataSourceSnapshot> sourceSnapshots,
        DateTimeOffset generatedAt)
    {
        var sourceById = sourceMaps
            .Where(map => map.Disposition == SourceMapDisposition.Existing)
            .ToDictionary(map => map.MapId, StringComparer.Ordinal);
        var calibrations = calibrationCatalog.Maps.OrderBy(map => map.MapId, StringComparer.Ordinal).ToList();
        var missingSourceMaps = calibrations.Where(map => !sourceById.ContainsKey(map.MapId))
            .Select(map => map.MapId).ToList();
        if (missingSourceMaps.Count > 0)
        {
            throw new InvalidDataException($"现有地图缺少 PvE 数据：{string.Join("、", missingSourceMaps)}。");
        }

        var dataDirectory = Path.Combine(rootDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);
        CopyRuntimeIcons(fallbackDataDirectory, dataDirectory);
        WriteAttribution(dataDirectory, svgSnapshot.CommitSha);

        var summaries = new List<MapBuildSummary>();
        var mapList = new List<object>();
        foreach (var calibration in calibrations)
        {
            var sourceMap = sourceById[calibration.MapId];
            var mapDirectory = Path.Combine(dataDirectory, "maps", calibration.MapId);
            Directory.CreateDirectory(mapDirectory);
            var image = MapImageBuilder.Build(calibration, svgSnapshot, fallbackDataDirectory,
                Path.Combine(mapDirectory, "map.png"));
            var markers = RuntimeMapProjector.Project(sourceMap, calibration);

            var mapDocument = new
            {
                schemaVersion = 1,
                id = calibration.MapId,
                name = sourceMap.Name,
                image = new { file = "map.png", width = image.Width, height = image.Height },
                worldBounds = new
                {
                    x0 = calibration.X0,
                    z0 = calibration.Z0,
                    x1 = calibration.X1,
                    z1 = calibration.Z1,
                    reverseCoordinate = calibration.ReverseCoordinate,
                    coordinateRotation = calibration.CoordinateRotation
                },
                defaultFloor = (string?)null,
                floors = Array.Empty<object>(),
                markers
            };
            WriteJsonAtomic(Path.Combine(mapDirectory, "map.json"), mapDocument);
            mapList.Add(new
            {
                id = calibration.MapId,
                name = sourceMap.Name,
                directory = $"maps/{calibration.MapId}",
                enabled = true
            });

            var markerTypes = markers.GroupBy(marker => marker.Type)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            summaries.Add(new MapBuildSummary(calibration.MapId, sourceMap.Name, markers.Count,
                markerTypes, image.Width, image.Height, image.SourceDescription));
        }

        WriteJsonAtomic(Path.Combine(dataDirectory, "maps.json"),
            new { schemaVersion = 1, maps = mapList });
        var calibrationSnapshot = SaveCalibrationSnapshot(rootDirectory, dataVersion,
            calibrationFile, calibrationCatalog.SourceBaseline, generatedAt);
        var allSnapshots = sourceSnapshots.Append(calibrationSnapshot).ToList();
        var contentHash = MapDataContentHasher.Compute(dataDirectory);
        var manifest = new MapDataManifest
        {
            SchemaVersion = 1,
            DataVersion = dataVersion,
            GameMode = "pve",
            GeneratedAt = generatedAt,
            Sources =
            [
                "json.tarkov.dev/pve",
                GitHubSvgSource.RepositoryName,
                "TarkovMap calibration metadata"
            ],
            SourceSnapshots = allSnapshots,
            ContentHash = contentHash
        };
        var manifestFile = ManifestWriter.Write(dataDirectory, manifest);
        SmokeTestRuntimeData(dataDirectory, summaries.Count);

        WriteJsonAtomic(Path.Combine(rootDirectory, "build-report.json"), new
        {
            schemaVersion = 1,
            dataVersion,
            generatedAt,
            svgRevision = svgSnapshot.CommitSha,
            dataDirectory = "Data",
            contentHash,
            mapCount = summaries.Count,
            maps = summaries,
            discoveredNewMaps = sourceMaps
                .Where(map => map.Disposition == SourceMapDisposition.New)
                .Select(map => new { id = map.MapId, name = map.Name, enabled = false }),
            skippedVariants = sourceMaps
                .Where(map => map.Disposition == SourceMapDisposition.Variant)
                .Select(map => new { id = map.MapId, name = map.Name, enabled = false })
        });

        return new TestPackBuildResult(dataDirectory, manifestFile, contentHash, summaries);
    }

    private static void SmokeTestRuntimeData(string dataDirectory, int expectedMapCount)
    {
        var repository = new MapRepository(dataDirectory);
        _ = repository.LoadManifest();
        var maps = repository.LoadMapList();
        if (maps.Count != expectedMapCount)
        {
            throw new InvalidDataException(
                $"运行时读取地图数量不符：预期 {expectedMapCount}，实际 {maps.Count}。");
        }

        foreach (var entry in maps)
        {
            var definition = repository.LoadMapDefinition(entry.Directory);
            using var image = repository.LoadMapImage(definition);
            if (image.Width != definition.Image.Width || image.Height != definition.Image.Height)
            {
                throw new InvalidDataException($"地图 {entry.Id} 的图片尺寸与 map.json 不一致。");
            }
        }
    }

    private static void CopyRuntimeIcons(string fallbackDataDirectory, string dataDirectory)
    {
        var sourceDirectory = Path.Combine(fallbackDataDirectory, "icons");
        var outputDirectory = Path.Combine(dataDirectory, "icons");
        Directory.CreateDirectory(outputDirectory);
        foreach (var name in new[]
                 {
                     "extract_pmc.png", "extract_scav.png", "extract_shared.png", "extract_transit.png"
                 })
        {
            var source = Path.Combine(sourceDirectory, name);
            if (!File.Exists(source))
            {
                throw new FileNotFoundException($"兼容测试包缺少运行时图标：{name}", source);
            }

            File.Copy(source, Path.Combine(outputDirectory, name), overwrite: true);
        }
    }

    private static void WriteAttribution(string dataDirectory, string commitSha)
    {
        var text = $"""
            # Third-party map asset notice

            The raster map images generated from SVG files are based on
            the-hideout/tarkov-dev-svg-maps at commit {commitSha}.

            Source: https://github.com/the-hideout/tarkov-dev-svg-maps
            License: Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International
            License URL: https://creativecommons.org/licenses/by-nc-sa/4.0/

            Modifications: selected the configured primary floor, rasterized the SVG, resized it,
            and rotated it where required for TarkovMap coordinate compatibility.

            This test MapData is intended for personal, non-commercial use and must not be used
            to facilitate cheating or gain an unfair advantage in Escape from Tarkov.
            """;
        File.WriteAllText(Path.Combine(dataDirectory, "THIRD_PARTY_NOTICES.md"), text);
    }

    private static MapDataSourceSnapshot SaveCalibrationSnapshot(
        string rootDirectory,
        string dataVersion,
        string calibrationFile,
        string revision,
        DateTimeOffset retrievedAt)
    {
        var relative = Path.Combine("snapshots", dataVersion, "TarkovMap", "calibration.json");
        var output = Path.Combine(rootDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var content = File.ReadAllBytes(calibrationFile);
        File.WriteAllBytes(output, content);
        return new MapDataSourceSnapshot
        {
            Name = "TarkovMap calibration metadata",
            Location = relative.Replace('\\', '/'),
            Revision = revision,
            RetrievedAt = retrievedAt,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()
        };
    }

    private static void WriteJsonAtomic<T>(string path, T value)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
