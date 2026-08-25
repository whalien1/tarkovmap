using System.Text.Json;

namespace MapPackBuilder.Validation;

internal sealed record ValidationApproval(
    string DataVersion,
    string MapId,
    string MarkerType,
    int BaselineCount,
    int CurrentCount,
    string Reason,
    DateTimeOffset ConfirmedAt);

internal sealed class ValidationApprovalCatalog
{
    private readonly IReadOnlyList<ValidationApproval> approvals;

    private ValidationApprovalCatalog(IReadOnlyList<ValidationApproval> approvals)
    {
        this.approvals = approvals;
    }

    public static ValidationApprovalCatalog Empty { get; } = new([]);

    public static ValidationApprovalCatalog Load(string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return Empty;
        }

        if (!File.Exists(file))
        {
            throw new FileNotFoundException("找不到 Validation 审批文件。", file);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var root = document.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var schemaNode) ||
            !schemaNode.TryGetInt32(out var schemaVersion) || schemaVersion != 1)
        {
            throw new InvalidDataException("Validation 审批文件 schemaVersion 必须为 1。");
        }

        var dataVersion = RequiredString(root, "dataVersion");
        if (!root.TryGetProperty("approvals", out var approvalsNode) ||
            approvalsNode.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Validation 审批文件缺少 approvals 数组。");
        }

        var approvals = new List<ValidationApproval>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in approvalsNode.EnumerateArray())
        {
            var mapId = RequiredString(node, "mapId");
            var markerType = RequiredString(node, "markerType");
            var reason = RequiredString(node, "reason");
            if (!node.TryGetProperty("baselineCount", out var baselineNode) ||
                !baselineNode.TryGetInt32(out var baselineCount) || baselineCount < 0 ||
                !node.TryGetProperty("currentCount", out var currentNode) ||
                !currentNode.TryGetInt32(out var currentCount) || currentCount < 0 ||
                !node.TryGetProperty("confirmedAt", out var confirmedNode) ||
                confirmedNode.ValueKind != JsonValueKind.String ||
                !confirmedNode.TryGetDateTimeOffset(out var confirmedAt) || confirmedAt == default)
            {
                throw new InvalidDataException($"Validation 审批项 {mapId}/{markerType} 无效。");
            }

            if (!keys.Add($"{mapId}\0{markerType}"))
            {
                throw new InvalidDataException($"Validation 审批项重复：{mapId}/{markerType}。");
            }

            approvals.Add(new ValidationApproval(dataVersion, mapId, markerType,
                baselineCount, currentCount, reason, confirmedAt));
        }

        return new ValidationApprovalCatalog(approvals);
    }

    public bool IsApproved(string dataVersion, string mapId, string markerType,
        int baselineCount, int currentCount) => approvals.Any(approval =>
        string.Equals(approval.DataVersion, dataVersion, StringComparison.Ordinal) &&
        string.Equals(approval.MapId, mapId, StringComparison.Ordinal) &&
        string.Equals(approval.MarkerType, markerType, StringComparison.Ordinal) &&
        approval.BaselineCount == baselineCount && approval.CurrentCount == currentCount);

    private static string RequiredString(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"Validation 审批文件缺少有效的 {propertyName}。");
        }

        return value.GetString()!.Trim();
    }
}
