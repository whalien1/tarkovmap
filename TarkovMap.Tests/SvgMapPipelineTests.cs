using System.Net;
using System.Text;
using System.Xml.Linq;
using MapPackBuilder.Calibration;
using MapPackBuilder.Output;
using MapPackBuilder.Sources;
using Xunit;

namespace TarkovMap.Tests;

public sealed class SvgMapPipelineTests
{
    private const string SimpleSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 50">
          <style>.land { fill: #123456; }</style>
          <g id="Ground_Level"><rect class="land" width="100" height="50" /></g>
          <g id="First_Floor" data-keep-with-group="Ground_Level"><circle cx="10" cy="10" r="4" /></g>
          <g id="Second_Floor"><rect width="20" height="20" /></g>
        </svg>
        """;

    [Fact]
    public async Task SvgSource_PinsCommitAndDownloadsRequestedAssets()
    {
        var commit = new string('a', 40);
        using var client = new HttpClient(new StubHandler(request =>
        {
            var body = request.RequestUri == GitHubSvgSource.HeadCommitUri
                ? $"{{\"sha\":\"{commit}\"}}"
                : request.RequestUri!.AbsolutePath.EndsWith("/LICENSE.md", StringComparison.Ordinal)
                    ? "license"
                    : SimpleSvg;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8)
            };
        }));

        var snapshot = await new GitHubSvgSource(client).FetchAsync(["Customs.svg"]);

        Assert.Equal(commit, snapshot.CommitSha);
        Assert.Equal(Encoding.UTF8.GetBytes(SimpleSvg), snapshot.Assets["Customs.svg"]);
        Assert.Equal("license", Encoding.UTF8.GetString(snapshot.License));
    }

    [Fact]
    public async Task SvgSource_UsesPublicPatchWhenGitHubApiIsRateLimited()
    {
        var commit = new string('b', 40);
        using var client = new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri == GitHubSvgSource.HeadCommitUri)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            var body = request.RequestUri == GitHubSvgSource.HeadCommitPatchUri
                ? $"From {commit} Mon Sep 17 00:00:00 2001\n"
                : request.RequestUri!.AbsolutePath.EndsWith("/LICENSE.md", StringComparison.Ordinal)
                    ? "license"
                    : SimpleSvg;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8)
            };
        }));

        var snapshot = await new GitHubSvgSource(client).FetchAsync(["Customs.svg"]);

        Assert.Equal(commit, snapshot.CommitSha);
        Assert.Equal(Encoding.UTF8.GetBytes(SimpleSvg), snapshot.Assets["Customs.svg"]);
    }

    [Fact]
    public void PrimaryLayerFilter_KeepsConfiguredAndLinkedGroupsOnly()
    {
        var filtered = MapImageBuilder.KeepPrimaryLayer(SimpleSvg, "customs", "Ground_Level");
        var groups = XDocument.Parse(filtered).Root!.Elements()
            .Where(element => element.Name.LocalName == "g")
            .Select(element => (string?)element.Attribute("id"))
            .ToList();

        Assert.Contains("Ground_Level", groups);
        Assert.Contains("First_Floor", groups);
        Assert.DoesNotContain("Second_Floor", groups);
    }

    [Fact]
    public void ImageBuilder_RasterizesAndRotatesSvg()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tarkov-svg-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var snapshot = new SvgRepositorySnapshot(
                new string('b', 40),
                new Dictionary<string, byte[]> { ["Customs.svg"] = Encoding.UTF8.GetBytes(SimpleSvg) },
                Encoding.UTF8.GetBytes("license"),
                DateTimeOffset.UtcNow);
            var calibration = new MapCalibration(
                "customs", "Customs.svg", "Ground_Level", 90,
                10, 20, -10, -20, false, 90, null, null);
            var output = Path.Combine(directory, "map.png");

            var result = MapImageBuilder.Build(calibration, snapshot, directory, output);

            Assert.Equal(1500, result.Width);
            Assert.Equal(3000, result.Height);
            Assert.True(File.Exists(output));
            using var image = Image.FromFile(output);
            Assert.Equal(result.Width, image.Width);
            Assert.Equal(result.Height, image.Height);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SvgSnapshotStore_ReloadsVerifiedFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tarkov-svg-snapshot-{Guid.NewGuid():N}");
        try
        {
            var snapshot = new SvgRepositorySnapshot(
                new string('c', 40),
                new Dictionary<string, byte[]> { ["Customs.svg"] = Encoding.UTF8.GetBytes(SimpleSvg) },
                Encoding.UTF8.GetBytes("license"),
                DateTimeOffset.Parse("2026-08-25T00:00:00Z"));

            SvgSnapshotStore.Save(directory, "2026.08.25.1-pve", snapshot);
            var loaded = SvgSnapshotStore.Load(directory, "2026.08.25.1-pve");

            Assert.Equal(snapshot.CommitSha, loaded.CommitSha);
            Assert.Equal(snapshot.Assets["Customs.svg"], loaded.Assets["Customs.svg"]);
            Assert.Equal(snapshot.License, loaded.License);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
