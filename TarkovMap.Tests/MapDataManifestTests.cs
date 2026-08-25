using MapPackBuilder.Output;
using TarkovMap.Models;
using TarkovMap.Services;
using Xunit;

namespace TarkovMap.Tests;

public sealed class MapDataManifestTests
{
    [Theory]
    [InlineData("2026.08.25.1-pve")]
    [InlineData("2027.01.01.12-pve")]
    public void DataVersionAcceptsExpectedFormat(string text)
    {
        Assert.True(MapDataVersion.TryParse(text, out var version));
        Assert.Equal(text, version.ToString());
    }

    [Theory]
    [InlineData("2026.08.25.0-pve")]
    [InlineData("2026.02.30.1-pve")]
    [InlineData("2026.08.25.1")]
    [InlineData("v1.0.0")]
    public void DataVersionRejectsInvalidFormat(string text)
    {
        Assert.False(MapDataVersion.TryParse(text, out _));
    }

    [Fact]
    public void LegacyDataWithoutManifestRemainsSupported()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var repository = new MapRepository(directory);
            Assert.Null(repository.LoadManifest());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WriterAndRepositoryRoundTripValidManifest()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var expected = CreateValidManifest();
            var file = ManifestWriter.Write(directory, expected);
            var actual = new MapRepository(directory).LoadManifest();

            Assert.True(File.Exists(file));
            Assert.NotNull(actual);
            Assert.Equal(expected.DataVersion, actual.DataVersion);
            Assert.Equal("pve", actual.GameMode);
            Assert.Single(actual.SourceSnapshots);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void UnsupportedSchemaIsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.SchemaVersion = 2;

        var error = Assert.Throws<InvalidDataException>(() => MapDataManifestValidator.Validate(manifest));
        Assert.Contains("Schema", error.Message);
    }

    [Fact]
    public void NonPveManifestIsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.DataVersion = "2026.08.25.1-regular";
        manifest.GameMode = "regular";

        var error = Assert.Throws<InvalidDataException>(() => MapDataManifestValidator.Validate(manifest));
        Assert.Contains("pve", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidSnapshotHashIsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.SourceSnapshots[0].Sha256 = "not-a-hash";

        Assert.Throws<InvalidDataException>(() => MapDataManifestValidator.Validate(manifest));
    }

    private static MapDataManifest CreateValidManifest() => new()
    {
        SchemaVersion = 1,
        DataVersion = "2026.08.25.1-pve",
        GameMode = "pve",
        GeneratedAt = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.FromHours(8)),
        Sources = ["json.tarkov.dev", "the-hideout/tarkov-dev-svg-maps"],
        SourceSnapshots =
        [
            new MapDataSourceSnapshot
            {
                Name = "json.tarkov.dev/pve/maps",
                Location = "snapshots/2026.08.25.1-pve/maps.json",
                Revision = "sha256:test-fixture",
                RetrievedAt = new DateTimeOffset(2026, 8, 25, 11, 30, 0, TimeSpan.FromHours(8)),
                Sha256 = new string('b', 64)
            }
        ],
        ContentHash = new string('a', 64)
    };

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tarkov-mapdata-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
