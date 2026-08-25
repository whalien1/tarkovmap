using TarkovMap.Services;

namespace MapPackBuilder.Packaging;

internal static class RuntimeDataSmokeValidator
{
    public static int Validate(string dataDirectory)
    {
        if (!Directory.Exists(dataDirectory))
        {
            throw new DirectoryNotFoundException($"MapData 目录不存在：{dataDirectory}");
        }

        var repository = new MapRepository(dataDirectory);
        _ = repository.LoadManifest();
        var maps = repository.LoadMapList();
        if (maps.Count == 0)
        {
            throw new InvalidDataException("MapData maps.json 没有地图。");
        }

        if (maps.Select(map => map.Id).Distinct(StringComparer.Ordinal).Count() != maps.Count)
        {
            throw new InvalidDataException("MapData maps.json 存在重复地图 ID。");
        }

        foreach (var entry in maps)
        {
            var definition = repository.LoadMapDefinition(entry.Directory);
            using var image = repository.LoadMapImage(definition);
            if (image.Width != definition.Image.Width || image.Height != definition.Image.Height)
            {
                throw new InvalidDataException($"地图 {entry.Id} 图片尺寸与 map.json 不一致。");
            }
        }

        return maps.Count;
    }
}
