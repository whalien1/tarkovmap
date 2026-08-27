using TarkovMap.Services;
using Xunit;

namespace TarkovMap.Tests;

public sealed class ConfigServiceTests
{
    [Fact]
    public void Load_NormalizesNullNestedSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tarkov-config-tests-{Guid.NewGuid():N}");
        var configDirectory = Path.Combine(directory, "Config");
        Directory.CreateDirectory(configDirectory);
        try
        {
            File.WriteAllText(Path.Combine(configDirectory, "config.json"), """
                {
                  "markerVisibility": null,
                  "miniMap": null
                }
                """);

            var service = new ConfigService(directory);
            service.Load();

            Assert.NotNull(service.Config.MarkerVisibility);
            Assert.Empty(service.Config.MarkerVisibility);
            Assert.NotNull(service.Config.MiniMap);
            Assert.True(service.Config.MiniMap.Visible);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
