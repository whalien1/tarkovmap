using System.Drawing;
using System.Security.Cryptography;
using System.Text.Json;
using MapPackBuilder.Output;
using MapPackBuilder.Validation;
using TarkovMap.Models;
using Xunit;

namespace TarkovMap.Tests;

public sealed class MapDataValidatorTests
{
    [Fact]
    public void UnchangedCoreCountsPassValidation()
    {
        using var fixture = ValidationFixture.Create(currentSpawnCount: 10);

        var report = MapDataValidator.Validate(fixture.Root, fixture.BaselineFile);

        Assert.True(report.CanPackage);
        Assert.Equal(0, report.ErrorCount);
        Assert.Empty(report.MarkerCountDiffs);
    }

    [Fact]
    public void UnconfirmedCoreCountChangeAboveThirtyPercentBlocksPackaging()
    {
        using var fixture = ValidationFixture.Create(currentSpawnCount: 6);

        var report = MapDataValidator.Validate(fixture.Root, fixture.BaselineFile);

        Assert.False(report.CanPackage);
        var issue = Assert.Single(report.Issues,
            issue => issue.Code == "CORE_COUNT_CHANGE_UNCONFIRMED");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        var diff = Assert.Single(report.MarkerCountDiffs);
        Assert.Equal(-40, diff.PercentChange);
        Assert.False(diff.Approved);
    }

    [Fact]
    public void ExactVersionAndCountsApprovalAllowsLargeChange()
    {
        using var fixture = ValidationFixture.Create(currentSpawnCount: 6);
        var approvalFile = fixture.WriteApprovals(baselineCount: 10, currentCount: 6);

        var report = MapDataValidator.Validate(fixture.Root, fixture.BaselineFile, approvalFile);

        Assert.True(report.CanPackage);
        var diff = Assert.Single(report.MarkerCountDiffs);
        Assert.True(diff.ThresholdExceeded);
        Assert.True(diff.Approved);
        Assert.Contains(report.Issues, issue => issue.Code == "CORE_COUNT_CHANGE_APPROVED");
    }

    [Fact]
    public void ManualAcceptanceMustContainEveryRequiredRepresentativeMap()
    {
        using var fixture = ValidationFixture.Create(currentSpawnCount: 6);
        var approvalFile = fixture.WriteApprovals(baselineCount: 10, currentCount: 6);
        var catalog = ValidationApprovalCatalog.Load(approvalFile);

        catalog.RequireManualAcceptance("2026.08.25.1-pve", ["test-map"]);
        Assert.Throws<InvalidDataException>(() =>
            catalog.RequireManualAcceptance("2026.08.25.1-pve", ["test-map", "customs"]));
        Assert.Throws<InvalidDataException>(() =>
            catalog.RequireManualAcceptance("2026.08.25.2-pve", ["test-map"]));
    }

    [Fact]
    public void DuplicateMarkerIdBlocksPackaging()
    {
        using var fixture = ValidationFixture.Create(currentSpawnCount: 10, duplicateId: true);

        var report = MapDataValidator.Validate(fixture.Root, fixture.BaselineFile);

        Assert.False(report.CanPackage);
        Assert.Contains(report.Issues, issue => issue.Code == "DUPLICATE_MARKER_ID");
    }

    [Fact]
    public void AFewOutOfBoundsMarkersAreWarnings()
    {
        using var fixture = ValidationFixture.Create(currentSpawnCount: 10, oneOutOfBounds: true);

        var report = MapDataValidator.Validate(fixture.Root, fixture.BaselineFile);

        Assert.True(report.CanPackage);
        var issue = Assert.Single(report.Issues,
            issue => issue.Code == "MARKERS_OUT_OF_BOUNDS_FEW");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void ChangedSourceSnapshotBlocksPackaging()
    {
        using var fixture = ValidationFixture.Create(currentSpawnCount: 10);
        File.AppendAllText(fixture.SnapshotFile, "tampered");

        var report = MapDataValidator.Validate(fixture.Root, fixture.BaselineFile);

        Assert.False(report.CanPackage);
        Assert.Contains(report.Issues, issue => issue.Code == "SNAPSHOT_HASH_MISMATCH");
    }

    [Fact]
    public void MissingMapImageBlocksPackaging()
    {
        using var fixture = ValidationFixture.Create(currentSpawnCount: 10);
        File.Delete(fixture.MapImageFile);

        var report = MapDataValidator.Validate(fixture.Root, fixture.BaselineFile);

        Assert.False(report.CanPackage);
        Assert.Contains(report.Issues, issue => issue.Code == "IMAGE_MISSING");
    }

    [Fact]
    public void DegenerateBoundsBlockPackaging()
    {
        using var fixture = ValidationFixture.Create(currentSpawnCount: 10, degenerateBounds: true);

        var report = MapDataValidator.Validate(fixture.Root, fixture.BaselineFile);

        Assert.False(report.CanPackage);
        Assert.Contains(report.Issues, issue => issue.Code == "BOUNDS_DEGENERATE");
    }

    [Fact]
    public void EmptyMarkerDataBlockPackaging()
    {
        using var fixture = ValidationFixture.Create(currentSpawnCount: 0);

        var report = MapDataValidator.Validate(fixture.Root, fixture.BaselineFile);

        Assert.False(report.CanPackage);
        Assert.Contains(report.Issues, issue => issue.Code == "MARKERS_EMPTY");
    }

    private sealed class ValidationFixture : IDisposable
    {
        private const string DataVersion = "2026.08.25.1-pve";

        private ValidationFixture(string root, string baselineFile, string snapshotFile,
            string mapImageFile)
        {
            Root = root;
            BaselineFile = baselineFile;
            SnapshotFile = snapshotFile;
            MapImageFile = mapImageFile;
        }

        public string Root { get; }
        public string BaselineFile { get; }
        public string SnapshotFile { get; }
        public string MapImageFile { get; }

        public static ValidationFixture Create(int currentSpawnCount,
            bool duplicateId = false, bool oneOutOfBounds = false,
            bool degenerateBounds = false)
        {
            var root = Path.Combine(Path.GetTempPath(), $"tarkov-map-validation-{Guid.NewGuid():N}");
            var data = Path.Combine(root, "Data");
            var mapDirectory = Path.Combine(data, "maps", "test-map");
            Directory.CreateDirectory(mapDirectory);

            var baselineFile = Path.Combine(root, "baseline.json");
            WriteJson(baselineFile, new
            {
                baselineVersion = "test-v1",
                mapCount = 1,
                maps = new[]
                {
                    new
                    {
                        id = "test-map",
                        markerTypes = new Dictionary<string, int> { ["spawn_scav"] = 10 }
                    }
                }
            });

            WriteJson(Path.Combine(data, "maps.json"), new
            {
                schemaVersion = 1,
                maps = new[]
                {
                    new { id = "test-map", name = "测试地图", directory = "maps/test-map", enabled = true }
                }
            });

            var markers = Enumerable.Range(0, currentSpawnCount).Select(index => new
            {
                id = duplicateId && index == 1 ? "marker-0" : $"marker-{index}",
                type = "spawn_scav",
                name = $"出生点 {index}",
                x = oneOutOfBounds && index == 0 ? 101.0 : 10.0 + index,
                z = 20.0
            }).ToList();
            WriteJson(Path.Combine(mapDirectory, "map.json"), new
            {
                schemaVersion = 1,
                id = "test-map",
                name = "测试地图",
                image = new { file = "map.png", width = 100, height = 100 },
                worldBounds = new
                {
                    x0 = 0.0,
                    z0 = 0.0,
                    x1 = degenerateBounds ? 0.0 : 100.0,
                    z1 = 100.0,
                    reverseCoordinate = false,
                    coordinateRotation = 0.0
                },
                markers
            });
            var mapImageFile = Path.Combine(mapDirectory, "map.png");
            using (var image = new Bitmap(100, 100))
            {
                image.Save(mapImageFile, System.Drawing.Imaging.ImageFormat.Png);
            }

            var snapshotFile = Path.Combine(root, "snapshots", DataVersion, "source.json");
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotFile)!);
            File.WriteAllText(snapshotFile, "source snapshot");
            var snapshotHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshotFile)))
                .ToLowerInvariant();
            var contentHash = MapDataContentHasher.Compute(data);
            ManifestWriter.Write(data, new MapDataManifest
            {
                DataVersion = DataVersion,
                GameMode = "pve",
                GeneratedAt = DateTimeOffset.Parse("2026-08-25T00:00:00Z"),
                Sources = ["test source"],
                SourceSnapshots =
                [
                    new MapDataSourceSnapshot
                    {
                        Name = "test source",
                        Location = $"snapshots/{DataVersion}/source.json",
                        Revision = "test-revision",
                        RetrievedAt = DateTimeOffset.Parse("2026-08-25T00:00:00Z"),
                        Sha256 = snapshotHash
                    }
                ],
                ContentHash = contentHash
            });

            return new ValidationFixture(root, baselineFile, snapshotFile, mapImageFile);
        }

        public string WriteApprovals(int baselineCount, int currentCount)
        {
            var file = Path.Combine(Root, "approvals.json");
            WriteJson(file, new
            {
                schemaVersion = 1,
                dataVersion = DataVersion,
                manualAcceptance = new
                {
                    result = "passed",
                    maps = new[] { "test-map" },
                    confirmedAt = "2026-08-25T01:00:00Z",
                    note = "代表地图人工验收通过"
                },
                approvals = new[]
                {
                    new
                    {
                        mapId = "test-map",
                        markerType = "spawn_scav",
                        baselineCount,
                        currentCount,
                        reason = "人工复核数据源后确认",
                        confirmedAt = "2026-08-25T01:00:00Z"
                    }
                }
            });
            return file;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static void WriteJson<T>(string file, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(value));
        }
    }
}
