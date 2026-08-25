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

internal sealed record ManualAcceptance(
    string Result,
    IReadOnlyList<string> Maps,
    DateTimeOffset ConfirmedAt,
    string Note);

internal sealed class ValidationApprovalCatalog
{
    private readonly IReadOnlyList<ValidationApproval> approvals;
    private readonly ManualAcceptance? manualAcceptance;
    private readonly string? catalogDataVersion;

    private ValidationApprovalCatalog(
        IReadOnlyList<ValidationApproval> approvals,
        ManualAcceptance? manualAcceptance = null,
        string? catalogDataVersion = null)
    {
        this.approvals = approvals;
        this.manualAcceptance = manualAcceptance;
        this.catalogDataVersion = catalogDataVersion;
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

        ManualAcceptance? manualAcceptance = null;
        if (root.TryGetProperty("manualAcceptance", out var acceptanceNode) &&
            acceptanceNode.ValueKind != JsonValueKind.Null)
        {
            var result = RequiredString(acceptanceNode, "result");
            var note = RequiredString(acceptanceNode, "note");
            if (!string.Equals(result, "passed", StringComparison.Ordinal) ||
                !acceptanceNode.TryGetProperty("confirmedAt", out var confirmedNode) ||
                confirmedNode.ValueKind != JsonValueKind.String ||
                !confirmedNode.TryGetDateTimeOffset(out var confirmedAt) || confirmedAt == default ||
                !acceptanceNode.TryGetProperty("maps", out var mapsNode) ||
                mapsNode.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Validation 人工验收记录无效。");
            }

            var maps = mapsNode.EnumerateArray()
                .Select(map => map.ValueKind == JsonValueKind.String ? map.GetString()?.Trim() : null)
                .Where(map => !string.IsNullOrWhiteSpace(map))
                .Select(map => map!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (maps.Count == 0)
            {
                throw new InvalidDataException("Validation 人工验收记录没有地图。");
            }

            manualAcceptance = new ManualAcceptance(result, maps, confirmedAt, note);
        }

        return new ValidationApprovalCatalog(approvals, manualAcceptance, dataVersion);
    }

    public bool IsApproved(string dataVersion, string mapId, string markerType,
        int baselineCount, int currentCount) => approvals.Any(approval =>
        string.Equals(approval.DataVersion, dataVersion, StringComparison.Ordinal) &&
        string.Equals(approval.MapId, mapId, StringComparison.Ordinal) &&
        string.Equals(approval.MarkerType, markerType, StringComparison.Ordinal) &&
        approval.BaselineCount == baselineCount && approval.CurrentCount == currentCount);

    public void RequireManualAcceptance(string dataVersion, IEnumerable<string> requiredMaps)
    {
        if (manualAcceptance is null ||
            !string.Equals(catalogDataVersion, dataVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("正式打包缺少人工验收记录。");
        }

        var missing = requiredMaps.Where(map => !manualAcceptance.Maps.Contains(map, StringComparer.Ordinal))
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidDataException($"人工验收缺少代表地图：{string.Join("、", missing)}。");
        }
    }

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
