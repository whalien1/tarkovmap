using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapPackBuilder.Validation;

internal static class ValidationReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void Write(string packRoot, MapDataValidationReport report)
    {
        Directory.CreateDirectory(packRoot);
        WriteAtomic(Path.Combine(packRoot, "validation-report.json"),
            JsonSerializer.SerializeToUtf8Bytes(report, JsonOptions));
        WriteAtomic(Path.Combine(packRoot, "validation-report.md"),
            Encoding.UTF8.GetBytes(CreateMarkdown(report)));
    }

    internal static string CreateMarkdown(MapDataValidationReport report)
    {
        var result = report.CanPackage ? "通过：允许进入正式打包" : "未通过：已阻止正式打包";
        var builder = new StringBuilder();
        builder.AppendLine("# MapData Validation + Diff 报告");
        builder.AppendLine();
        builder.AppendLine($"- 数据版本：`{report.DataVersion}`");
        builder.AppendLine($"- 生成时间：`{report.GeneratedAt:O}`");
        builder.AppendLine($"- 结论：**{result}**");
        builder.AppendLine($"- 汇总：Error {report.ErrorCount} / Warning {report.WarningCount} / Info {report.InfoCount}");

        var blockers = report.Issues.Where(issue => issue.Severity == ValidationSeverity.Error).ToList();
        builder.AppendLine();
        builder.AppendLine("## 阻断项");
        builder.AppendLine();
        if (blockers.Count == 0)
        {
            builder.AppendLine("无。");
        }
        else
        {
            foreach (var issue in blockers)
            {
                builder.AppendLine($"- `{Scope(issue)}` `{issue.Code}`：{issue.Message}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 核心类别数量差异");
        builder.AppendLine();
        if (report.MarkerCountDiffs.Count == 0)
        {
            builder.AppendLine("与基线一致。");
        }
        else
        {
            builder.AppendLine("| 地图 | 类别 | 基线 | 当前 | 变化 | 阈值 | 审批 |");
            builder.AppendLine("|---|---:|---:|---:|---:|---|---|");
            foreach (var diff in report.MarkerCountDiffs)
            {
                var change = diff.PercentChange is null
                    ? $"+{diff.Delta}（基线为 0）"
                    : $"{diff.Delta:+#;-#;0} / {diff.PercentChange:+0.0;-0.0;0.0}%";
                builder.AppendLine($"| {diff.MapId} | {diff.MarkerType} | {diff.BaselineCount} | {diff.CurrentCount} | {change} | {(diff.ThresholdExceeded ? ">30%" : "≤30%")} | {(diff.Approved ? "已确认" : "—")} |");
            }
        }

        AppendIssues(builder, "Warning", report.Issues
            .Where(issue => issue.Severity == ValidationSeverity.Warning));
        AppendIssues(builder, "Info", report.Issues
            .Where(issue => issue.Severity == ValidationSeverity.Info));
        return builder.ToString();
    }

    private static void AppendIssues(StringBuilder builder, string heading,
        IEnumerable<ValidationIssue> issues)
    {
        var list = issues.ToList();
        builder.AppendLine();
        builder.AppendLine($"## {heading}");
        builder.AppendLine();
        if (list.Count == 0)
        {
            builder.AppendLine("无。");
            return;
        }

        foreach (var issue in list)
        {
            builder.AppendLine($"- `{Scope(issue)}` `{issue.Code}`：{issue.Message}");
        }
    }

    private static string Scope(ValidationIssue issue) =>
        issue.MapId is null ? "全局" : issue.MarkerType is null
            ? issue.MapId
            : $"{issue.MapId}/{issue.MarkerType}";

    private static void WriteAtomic(string path, byte[] content)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
