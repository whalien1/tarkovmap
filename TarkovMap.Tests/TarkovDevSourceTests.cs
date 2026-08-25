using System.Net;
using System.Security.Cryptography;
using System.Text;
using MapPackBuilder.Sources;
using Xunit;

namespace TarkovMap.Tests;

public sealed class TarkovDevSourceTests
{
    private const string MapsJson = """
        {
          "data": {
            "maps": {
              "customs-id": {
                "id": "customs-id",
                "normalizedName": "customs",
                "name": "map_customs",
                "coordinateToCardinalRotation": 180,
                "extracts": [
                  { "id": "extract-1", "name": "extract_key", "faction": "PMC", "position": { "x": 12.5, "y": 1.5, "z": -9 } }
                ],
                "transits": [
                  { "id": "transit-1", "description": "transit_key", "position": { "x": 2, "y": 0, "z": 3 } }
                ],
                "spawns": [
                  { "zoneName": "zone-a", "position": { "x": 4, "y": 2, "z": 5 }, "sides": ["PMC"], "categories": ["Player"] }
                ],
                "bosses": [
                  { "mob": "boss_key", "spawnLocations": [{ "name": "boss_zone_key", "spawnKey": "zone-a" }] }
                ],
                "hazards": [
                  {
                    "id": "hazard-1",
                    "name": "hazard_key",
                    "position": { "x": 6, "y": -1, "z": 7 },
                    "outline": [{ "x": 5, "z": 6 }, { "x": 7, "z": 6 }, { "x": 6, "z": 8 }]
                  }
                ]
              },
              "night-id": {
                "id": "night-id", "normalizedName": "night-factory", "name": "night_factory",
                "extracts": [], "transits": [], "spawns": [], "bosses": [], "hazards": []
              },
              "icebreaker-id": {
                "id": "icebreaker-id", "normalizedName": "icebreaker", "name": "icebreaker_name",
                "extracts": [], "transits": [], "spawns": [], "bosses": [], "hazards": []
              }
            }
          }
        }
        """;

    private const string TranslationsJson = """
        {
          "data": {
            "map_customs": "海关",
            "extract_key": "十字路口",
            "transit_key": "转移到工厂",
            "boss_key": "Boss 测试",
            "boss_zone_key": "测试区域",
            "hazard_key": "雷区",
            "night_factory": "夜间工厂",
            "icebreaker_name": "破冰船"
          }
        }
        """;

    [Fact]
    public void Parser_TranslatesCoreMarkersAndClassifiesMaps()
    {
        var snapshot = CreateSnapshot();

        var maps = TarkovDevMapParser.Parse(snapshot);

        Assert.Equal(3, maps.Count);
        var customs = Assert.Single(maps, map => map.MapId == "customs");
        Assert.Equal(SourceMapDisposition.Existing, customs.Disposition);
        Assert.Equal("海关", customs.Name);
        Assert.Equal(180, customs.ApiCardinalRotation);
        Assert.Equal("十字路口", Assert.Single(customs.Extracts).Name);
        Assert.Equal("pmc", Assert.Single(customs.Extracts).Faction);
        Assert.Equal(1.5, Assert.Single(customs.Extracts).Position.Y);
        Assert.Equal("转移到工厂", Assert.Single(customs.Transits).Name);
        Assert.Equal("Boss 测试", Assert.Single(customs.Bosses).Name);
        Assert.Equal("测试区域", Assert.Single(customs.Bosses).SpawnLocations.Single().Name);
        Assert.Equal("雷区", Assert.Single(customs.Hazards).Name);
        Assert.Equal(3, Assert.Single(customs.Hazards).Outline.Count);
        Assert.Equal(SourceMapDisposition.Variant,
            Assert.Single(maps, map => map.MapId == "night-factory").Disposition);
        Assert.Equal(SourceMapDisposition.New,
            Assert.Single(maps, map => map.MapId == "icebreaker").Disposition);
    }

    [Fact]
    public async Task FetchAsync_DownloadsBothRequiredDocuments()
    {
        var requested = new List<Uri>();
        using var client = new HttpClient(new StubHandler(request =>
        {
            requested.Add(request.RequestUri!);
            var body = request.RequestUri == TarkovDevSource.MapsUri ? MapsJson : TranslationsJson;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }));

        var snapshot = await new TarkovDevSource(client).FetchAsync();

        Assert.Equal(2, requested.Count);
        Assert.Contains(TarkovDevSource.MapsUri, requested);
        Assert.Contains(TarkovDevSource.ChineseTranslationsUri, requested);
        Assert.NotEmpty(snapshot.MapsJson);
        Assert.NotEmpty(snapshot.ChineseTranslationsJson);
    }

    [Fact]
    public void SnapshotStore_PreservesRawFilesAndHashes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tarkov-source-tests-{Guid.NewGuid():N}");
        try
        {
            var snapshot = CreateSnapshot();
            var records = SourceSnapshotStore.Save(directory, "2026.08.25.1-pve", snapshot);

            Assert.Equal(2, records.Count);
            foreach (var record in records)
            {
                var path = Path.Combine(directory, record.Location.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(path));
                Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                    record.Sha256);
            }

            Assert.True(File.Exists(Path.Combine(directory, "snapshots", "2026.08.25.1-pve",
                "json.tarkov.dev", "snapshot.json")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static TarkovDevRawSnapshot CreateSnapshot() => new(
        Encoding.UTF8.GetBytes(MapsJson),
        Encoding.UTF8.GetBytes(TranslationsJson),
        new DateTimeOffset(2026, 8, 25, 1, 2, 3, TimeSpan.Zero));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
