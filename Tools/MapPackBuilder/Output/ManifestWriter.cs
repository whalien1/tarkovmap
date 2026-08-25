using System.Text.Json;
using TarkovMap.Models;
using TarkovMap.Services;

namespace MapPackBuilder.Output;

internal static class ManifestWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Write(string outputDirectory, MapDataManifest manifest)
    {
        MapDataManifestValidator.Validate(manifest);
        Directory.CreateDirectory(outputDirectory);
        var file = Path.Combine(outputDirectory, "manifest.json");
        File.WriteAllText(file, JsonSerializer.Serialize(manifest, JsonOptions));
        return file;
    }
}
