using System.Text.Json;
using MapPackBuilder.Validation;
using Xunit;

namespace TarkovMap.Tests;

public sealed class ValidationApprovalWriterTests
{
    [Fact]
    public void WriteFromReport_ApprovesOnlyExactThresholdDiffsAndRepresentativeMaps()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tarkov-approval-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            WriteReport(root, "CORE_COUNT_CHANGE_UNCONFIRMED");
            var output = Path.Combine(root, "approval.json");
            var confirmedAt = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.FromHours(8));

            var result = ValidationApprovalWriter.WriteFromReport(root, output,
                "人工核对代表地图和数量变化后确认。", confirmedAt);

            Assert.Equal(1, result.ApprovedChangeCount);
            var catalog = ValidationApprovalCatalog.Load(output);
            Assert.True(catalog.IsApproved("2026.08.25.6-pve", "customs", "spawn_scav", 100, 40));
            Assert.False(catalog.IsApproved("2026.08.25.6-pve", "customs", "spawn_scav", 100, 41));
            catalog.RequireManualAcceptance("2026.08.25.6-pve",
                ["customs", "ground-zero", "streets-of-tarkov", "the-lab"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WriteFromReport_RefusesStructuralValidationErrors()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tarkov-approval-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            WriteReport(root, "CONTENT_HASH_MISMATCH");
            var exception = Assert.Throws<InvalidDataException>(() =>
                ValidationApprovalWriter.WriteFromReport(root, Path.Combine(root, "approval.json"),
                    "不应放行。", DateTimeOffset.Now));
            Assert.Contains("不能用人工审批绕过", exception.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteReport(string root, string errorCode)
    {
        var report = new
        {
            schemaVersion = 1,
            dataVersion = "2026.08.25.6-pve",
            generatedAt = "2026-08-25T12:00:00+08:00",
            canPackage = false,
            errorCount = 1,
            warningCount = 0,
            infoCount = 0,
            issues = new[]
            {
                new
                {
                    severity = "error",
                    code = errorCode,
                    message = "测试错误",
                    mapId = "customs",
                    markerType = "spawn_scav"
                }
            },
            markerCountDiffs = new[]
            {
                new
                {
                    mapId = "customs",
                    markerType = "spawn_scav",
                    baselineCount = 100,
                    currentCount = 40,
                    delta = -60,
                    percentChange = -60.0,
                    thresholdExceeded = true,
                    approved = false
                }
            }
        };
        File.WriteAllText(Path.Combine(root, "validation-report.json"),
            JsonSerializer.Serialize(report));
    }
}
