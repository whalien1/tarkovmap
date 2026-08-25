using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MapPackBuilder.Gui;

internal sealed record BuilderWorkspace(
    string FormalDataDirectory,
    string WorkDirectory,
    string SuggestedDataVersion)
{
    private static readonly Regex VersionPattern = new(
        "^(?<date>\\d{4}\\.\\d{2}\\.\\d{2})\\.(?<number>[1-9]\\d*)-pve$",
        RegexOptions.CultureInvariant);
    private static readonly Regex EmbeddedVersionPattern = new(
        "(?<date>\\d{4}\\.\\d{2}\\.\\d{2})\\.(?<number>[1-9]\\d*)-pve",
        RegexOptions.CultureInvariant);

    public static BuilderWorkspace Discover(string startDirectory)
    {
        var formalData = FindFormalData(startDirectory)
                         ?? FindFormalData(Directory.GetCurrentDirectory())
                         ?? Path.Combine(Directory.GetCurrentDirectory(), "TarkovMap", "Data");
        var workDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "TarkovMap MapData Builds");
        return new BuilderWorkspace(formalData, workDirectory,
            SuggestNextAvailableVersion(ReadDataVersion(formalData), DateTime.Today, workDirectory));
    }

    public static string SuggestNextVersion(string? currentVersion, DateTime today)
    {
        var date = today.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
        var match = currentVersion is null ? Match.Empty : VersionPattern.Match(currentVersion);
        var number = match.Success && string.Equals(match.Groups["date"].Value, date,
            StringComparison.Ordinal) &&
                     int.TryParse(match.Groups["number"].Value, NumberStyles.None,
                         CultureInfo.InvariantCulture, out var currentNumber)
            ? currentNumber + 1
            : 1;
        return $"{date}.{number}-pve";
    }

    public static string SuggestNextAvailableVersion(
        string? currentVersion,
        DateTime today,
        string workDirectory)
    {
        var suggested = SuggestNextVersion(currentVersion, today);
        var date = today.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
        var highest = int.Parse(VersionPattern.Match(suggested).Groups["number"].Value,
            CultureInfo.InvariantCulture) - 1;
        if (Directory.Exists(workDirectory))
        {
            var entries = Directory.EnumerateFileSystemEntries(workDirectory).ToList();
            var packageDirectory = Path.Combine(workDirectory, "packages");
            if (Directory.Exists(packageDirectory))
            {
                entries.AddRange(Directory.EnumerateFiles(packageDirectory));
            }

            foreach (var entry in entries)
            {
                var match = EmbeddedVersionPattern.Match(Path.GetFileName(entry));
                if (match.Success && string.Equals(match.Groups["date"].Value, date,
                        StringComparison.Ordinal) &&
                    int.TryParse(match.Groups["number"].Value, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var number))
                {
                    highest = Math.Max(highest, number);
                }
            }
        }

        return $"{date}.{highest + 1}-pve";
    }

    public static string? ReadDataVersion(string dataDirectory)
    {
        var manifestFile = Path.Combine(dataDirectory, "manifest.json");
        if (!File.Exists(manifestFile))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestFile));
            return document.RootElement.TryGetProperty("dataVersion", out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindFormalData(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            var repositoryData = Path.Combine(directory.FullName, "TarkovMap", "Data");
            if (File.Exists(Path.Combine(repositoryData, "maps.json")))
            {
                return repositoryData;
            }

            var adjacentData = Path.Combine(directory.FullName, "Data");
            if (string.Equals(directory.Name, "TarkovMap", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(adjacentData, "maps.json")))
            {
                return adjacentData;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
