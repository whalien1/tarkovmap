using System.Text.Json.Serialization;

namespace MapPackBuilder.Validation;

[JsonConverter(typeof(JsonStringEnumConverter<ValidationSeverity>))]
internal enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

internal sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? MapId = null,
    string? MarkerType = null);

internal sealed record MarkerCountDiff(
    string MapId,
    string MarkerType,
    int BaselineCount,
    int CurrentCount,
    int Delta,
    double? PercentChange,
    bool ThresholdExceeded,
    bool Approved);

internal sealed class MapDataValidationReport
{
    public int SchemaVersion { get; init; } = 1;
    public string DataVersion { get; init; } = "";
    public DateTimeOffset GeneratedAt { get; init; }
    public bool CanPackage { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
    public IReadOnlyList<MarkerCountDiff> MarkerCountDiffs { get; init; } = [];
}
