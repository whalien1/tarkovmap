using MapPackBuilder.Gui;
using Xunit;

namespace TarkovMap.Tests;

public sealed class BuilderWorkspaceTests
{
    [Theory]
    [InlineData("2026.08.25.5-pve", "2026.08.25.6-pve")]
    [InlineData("2026.08.24.9-pve", "2026.08.25.1-pve")]
    [InlineData(null, "2026.08.25.1-pve")]
    public void SuggestNextVersion_UsesTodayAndIncrementsSameDayBuild(
        string? currentVersion,
        string expected)
    {
        Assert.Equal(expected,
            BuilderWorkspace.SuggestNextVersion(currentVersion, new DateTime(2026, 8, 25)));
    }

    [Fact]
    public void SuggestNextAvailableVersion_SkipsExistingWorkResults()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tarkov-gui-work-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "MapData-2026.08.25.6-pve-test"));
        Directory.CreateDirectory(Path.Combine(root, "packages"));
        File.WriteAllBytes(Path.Combine(root, "packages", "MapData-2026.08.25.8-pve.zip"), []);
        try
        {
            Assert.Equal("2026.08.25.9-pve",
                BuilderWorkspace.SuggestNextAvailableVersion("2026.08.25.5-pve",
                    new DateTime(2026, 8, 25), root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
