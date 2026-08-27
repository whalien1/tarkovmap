using TarkovMap.Models;
using TarkovMap.Services;
using Xunit;

namespace TarkovMap.Tests;

public sealed class ImageLoadingTests
{
    [Fact]
    public void LoadMapImage_ReturnsBitmapThatCanBeDrawnAfterLoadCompletes()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var file = Path.Combine(directory, "map.png");
            WriteTestPng(file);
            var map = new MapDefinition
            {
                Directory = directory,
                Image = new MapImageInfo { File = "map.png" }
            };

            using var image = new MapRepository(directory).LoadMapImage(map);

            Assert.Equal(3, image.Width);
            Assert.Equal(2, image.Height);
            AssertCanDraw(image);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IconCache_LoadedFileIconCanBeDrawnAfterGetReturns()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            WriteTestPng(Path.Combine(directory, "extract_pmc.png"));
            using var cache = new IconCache(directory);

            var image = cache.Get(MarkerType.ExtractPmc);

            Assert.Equal(3, image.Width);
            Assert.Equal(2, image.Height);
            AssertCanDraw(image);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertCanDraw(Image image)
    {
        using var target = new Bitmap(6, 4);
        using var graphics = Graphics.FromImage(target);
        graphics.Clear(Color.Transparent);
        graphics.DrawImage(image, 1, 1, image.Width, image.Height);

        Assert.True(target.GetPixel(2, 1).A > 0);
    }

    private static void WriteTestPng(string path)
    {
        using var bitmap = new Bitmap(3, 2);
        bitmap.SetPixel(1, 0, Color.FromArgb(180, 20, 40, 60));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tarkov-image-load-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
