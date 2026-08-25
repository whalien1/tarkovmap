namespace MapPackBuilder.Packaging;

internal sealed record MapDataInstallResult(
    string DataVersion,
    string DataDirectory,
    string BackupDirectory,
    int MapCount);

internal static class MapDataInstaller
{
    public static MapDataInstallResult Apply(
        string packageFile,
        string dataDirectory,
        string backupDirectory,
        string baselineFile)
    {
        var targets = ValidateTargets(dataDirectory, backupDirectory);
        if (!Directory.Exists(targets.DataDirectory) ||
            !File.Exists(Path.Combine(targets.DataDirectory, "maps.json")))
        {
            throw new InvalidDataException($"正式 Data 目录无效：{targets.DataDirectory}");
        }

        _ = RuntimeDataSmokeValidator.Validate(targets.DataDirectory);
        var stagingRoot = Path.Combine(targets.ParentDirectory,
            $".mapdata-install-{Guid.NewGuid():N}");
        var version = MapDataPackageService.ExtractAndValidate(
            packageFile, stagingRoot, baselineFile);
        var stagedData = Path.Combine(stagingRoot, "Data");
        var mapCount = RuntimeDataSmokeValidator.Validate(stagedData);

        try
        {
            if (Directory.Exists(targets.BackupDirectory))
            {
                Directory.Delete(targets.BackupDirectory, recursive: true);
            }

            Directory.Move(targets.DataDirectory, targets.BackupDirectory);
            try
            {
                Directory.Move(stagedData, targets.DataDirectory);
                _ = RuntimeDataSmokeValidator.Validate(targets.DataDirectory);
            }
            catch
            {
                if (Directory.Exists(targets.DataDirectory))
                {
                    Directory.Move(targets.DataDirectory, Path.Combine(stagingRoot, "failed-data"));
                }

                if (Directory.Exists(targets.BackupDirectory))
                {
                    Directory.Move(targets.BackupDirectory, targets.DataDirectory);
                }

                throw;
            }

            return new MapDataInstallResult(version, targets.DataDirectory,
                targets.BackupDirectory, mapCount);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    public static int Restore(string dataDirectory, string backupDirectory)
    {
        var targets = ValidateTargets(dataDirectory, backupDirectory);
        if (!Directory.Exists(targets.BackupDirectory))
        {
            throw new DirectoryNotFoundException($"没有可恢复的 MapData 备份：{targets.BackupDirectory}");
        }

        var mapCount = RuntimeDataSmokeValidator.Validate(targets.BackupDirectory);
        var replacedDirectory = Path.Combine(targets.ParentDirectory,
            $".mapdata-replaced-{Guid.NewGuid():N}");
        Directory.Move(targets.DataDirectory, replacedDirectory);
        try
        {
            Directory.Move(targets.BackupDirectory, targets.DataDirectory);
            _ = RuntimeDataSmokeValidator.Validate(targets.DataDirectory);
            Directory.Delete(replacedDirectory, recursive: true);
            return mapCount;
        }
        catch
        {
            if (Directory.Exists(targets.DataDirectory))
            {
                Directory.Move(targets.DataDirectory, targets.BackupDirectory);
            }

            if (Directory.Exists(replacedDirectory))
            {
                Directory.Move(replacedDirectory, targets.DataDirectory);
            }

            throw;
        }
    }

    private static InstallTargets ValidateTargets(string dataDirectory, string backupDirectory)
    {
        dataDirectory = Path.GetFullPath(dataDirectory).TrimEnd(Path.DirectorySeparatorChar);
        backupDirectory = Path.GetFullPath(backupDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(Path.GetFileName(dataDirectory), "Data", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("正式目录必须明确指向名为 Data 的目录。");
        }

        var parent = Path.GetDirectoryName(dataDirectory)
                     ?? throw new InvalidDataException("正式 Data 目录不能位于文件系统根目录。");
        if (!string.Equals(Path.GetDirectoryName(backupDirectory), parent,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dataDirectory, backupDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("备份目录必须是正式 Data 的同级独立目录。");
        }

        return new InstallTargets(parent, dataDirectory, backupDirectory);
    }

    private sealed record InstallTargets(
        string ParentDirectory,
        string DataDirectory,
        string BackupDirectory);
}
