using System.Text.Json;

namespace MapPackBuilder.Validation;

internal sealed record ValidationApprovalWriteResult(
    string OutputFile,
    string DataVersion,
    int ApprovedChangeCount);

internal static class ValidationApprovalWriter
{
    private static readonly string[] RequiredMaps =
    [
        "customs",
        "ground-zero",
        "streets-of-tarkov",
        "the-lab"
    ];

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static ValidationApprovalWriteResult WriteFromReport(
        string packRoot,
        string outputFile,
        string note,
        DateTimeOffset confirmedAt)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("必须填写本次人工验收说明。", nameof(note));
        }

        var reportFile = Path.Combine(Path.GetFullPath(packRoot), "validation-report.json");
        if (!File.Exists(reportFile))
        {
            throw new FileNotFoundException("测试包缺少 validation-report.json。", reportFile);
        }

        var report = JsonSerializer.Deserialize<MapDataValidationReport>(
                         File.ReadAllText(reportFile), ReadOptions)
                     ?? throw new InvalidDataException("Validation 报告无法读取。");
        if (string.IsNullOrWhiteSpace(report.DataVersion))
        {
            throw new InvalidDataException("Validation 报告缺少数据版本。");
        }

        var blockingIssues = report.Issues
            .Where(issue => issue.Severity == ValidationSeverity.Error &&
                            !string.Equals(issue.Code, "CORE_COUNT_CHANGE_UNCONFIRMED",
                                StringComparison.Ordinal))
            .ToList();
        if (blockingIssues.Count > 0)
        {
            throw new InvalidDataException(
                $"仍有 {blockingIssues.Count} 个数据错误，不能用人工审批绕过。请先修复 Validation 报告中的 Error。");
        }

        var approvals = report.MarkerCountDiffs
            .Where(diff => diff.ThresholdExceeded)
            .OrderBy(diff => diff.MapId, StringComparer.Ordinal)
            .ThenBy(diff => diff.MarkerType, StringComparer.Ordinal)
            .Select(diff => new
            {
                diff.MapId,
                diff.MarkerType,
                diff.BaselineCount,
                diff.CurrentCount,
                Reason = note.Trim(),
                ConfirmedAt = confirmedAt
            })
            .ToList();

        var document = new
        {
            SchemaVersion = 1,
            DataVersion = report.DataVersion,
            ManualAcceptance = new
            {
                Result = "passed",
                Maps = RequiredMaps,
                ConfirmedAt = confirmedAt,
                Note = note.Trim()
            },
            Approvals = approvals
        };

        var fullOutput = Path.GetFullPath(outputFile);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
        var temporaryFile = $"{fullOutput}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryFile, JsonSerializer.Serialize(document, WriteOptions));
            _ = ValidationApprovalCatalog.Load(temporaryFile);
            File.Move(temporaryFile, fullOutput, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }

        return new ValidationApprovalWriteResult(fullOutput, report.DataVersion, approvals.Count);
    }
}
