using System.Security.Cryptography;
using MapPackBuilder.Assets;
using Xunit;

namespace TarkovMap.Tests;

public sealed class MarkerIconAssetGeneratorTests
{
    [Fact]
    public void Generate_CreatesEightDeterministicTransparentPngAssets()
    {
        var firstDirectory = Path.Combine(Path.GetTempPath(), $"tarkov-icons-a-{Guid.NewGuid():N}");
        var secondDirectory = Path.Combine(Path.GetTempPath(), $"tarkov-icons-b-{Guid.NewGuid():N}");
        try
        {
            var first = MarkerIconAssetGenerator.Generate(firstDirectory);
            var second = MarkerIconAssetGenerator.Generate(secondDirectory);

            Assert.Equal(8, first.Count);
            Assert.Equal(first.Select(Path.GetFileName).Order(), second.Select(Path.GetFileName).Order());
            foreach (var firstFile in first)
            {
                var secondFile = Path.Combine(secondDirectory, Path.GetFileName(firstFile));
                Assert.Equal(SHA256.HashData(File.ReadAllBytes(firstFile)),
                    SHA256.HashData(File.ReadAllBytes(secondFile)));
                using var bitmap = new Bitmap(firstFile);
                Assert.Equal(MarkerIconAssetGenerator.AssetSize, bitmap.Width);
                Assert.Equal(MarkerIconAssetGenerator.AssetSize, bitmap.Height);
                Assert.Equal(0, bitmap.GetPixel(0, 0).A);
                Assert.True(bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).A > 0);
            }
        }
        finally
        {
            if (Directory.Exists(firstDirectory))
            {
                Directory.Delete(firstDirectory, recursive: true);
            }

            if (Directory.Exists(secondDirectory))
            {
                Directory.Delete(secondDirectory, recursive: true);
            }
        }
    }
}
