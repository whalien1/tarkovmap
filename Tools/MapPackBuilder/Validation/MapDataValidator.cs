using System.Drawing;
using System.Security.Cryptography;
using System.Text.Json;
using MapPackBuilder.Output;
using TarkovMap.Models;
using TarkovMap.Services;

namespace MapPackBuilder.Validation;

internal static class MapDataValidator
{
    private const double CoreChangeThreshold = 30.0;
    private static readonly HashSet<string> CoreMarkerTypes = new(StringComparer.Ordinal)
    {
        "extract_pmc", "extract_scav", "extract_shared", "extract_transit",
        "spawn_pmc", "spawn_scav", "boss", "hazard"
    };

    private static readonly HashSet<string> KnownMarkerTypes = new(StringComparer.Ordinal)
    {
        "extract_pmc", "extract_scav", "extract_shared", "extract_transit",
        "spawn_pmc", "spawn_scav", "boss", "loot_container", "lock", "hazard",
        "stationary_weapon", "label"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static MapDataValidationReport Validate(
        string packRoot,
        string baselineFile,
        string? approvalsFile = null,
        DateTimeOffset? generatedAt = null)
    {
        packRoot = Path.GetFullPath(packRoot);
        var issues = new List<ValidationIssue>();
        var diffs = new List<MarkerCountDiff>();
        var baseline = MapDataBaseline.Load(baselineFile);
        var approvals = ValidationApprovalCatalog.Load(approvalsFile);
        var dataDirectory = Path.Combine(packRoot, "Data");
        var dataVersion = "unknown";

        if (!Directory.Exists(dataDirectory))
        {
            Add(ValidationSeverity.Error, "DATA_DIRECTORY_MISSING",
                "测试包缺少 Data 目录，无法正式打包。");
            return CreateReport();
        }

        ValidateManifest();
        var mapCounts = ValidateMaps();
        CompareBaseline(mapCounts);
        return CreateReport();

        void ValidateManifest()
        {
            var file = Path.Combine(dataDirectory, "manifest.json");
            if (!File.Exists(file))
            {
                Add(ValidationSeverity.Error, "MANIFEST_MISSING", "Data 缺少 manifest.json。");
                return;
            }

            try
            {
                var manifest = JsonSerializer.Deserialize<MapDataManifest>(File.ReadAllText(file), JsonOptions)
                               ?? throw new InvalidDataException("manifest.json 内容为空。");
                dataVersion = string.IsNullOrWhiteSpace(manifest.DataVersion) ? "unknown" : manifest.DataVersion;
                MapDataManifestValidator.Validate(manifest);

                var actualContentHash = MapDataContentHasher.Compute(dataDirectory);
                if (!string.Equals(actualContentHash, manifest.ContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    Add(ValidationSeverity.Error, "CONTENT_HASH_MISMATCH",
                        $"运行时文件内容哈希不一致：manifest={manifest.ContentHash}，实际={actualContentHash}。");
                }

                foreach (var snapshot in manifest.SourceSnapshots)
                {
                    if (!TryResolveUnder(packRoot, snapshot.Location, out var snapshotFile))
                    {
                        Add(ValidationSeverity.Error, "SNAPSHOT_PATH_INVALID",
                            $"来源快照路径越出测试包：{snapshot.Location}。");
                        continue;
                    }

                    if (!File.Exists(snapshotFile))
                    {
                        Add(ValidationSeverity.Error, "SNAPSHOT_MISSING",
                            $"来源快照不存在：{snapshot.Location}。");
                        continue;
                    }

                    var actualSha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshotFile)))
                        .ToLowerInvariant();
                    if (!string.Equals(actualSha, snapshot.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        Add(ValidationSeverity.Error, "SNAPSHOT_HASH_MISMATCH",
                            $"来源快照哈希不一致：{snapshot.Location}。");
                    }
                }
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                Add(ValidationSeverity.Error, "MANIFEST_INVALID",
                    $"manifest.json 校验失败：{exception.Message}");
            }
        }

        Dictionary<string, IReadOnlyDictionary<string, int>> ValidateMaps()
        {
            var result = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);
            var listFile = Path.Combine(dataDirectory, "maps.json");
            if (!File.Exists(listFile))
            {
                Add(ValidationSeverity.Error, "MAP_LIST_MISSING", "Data 缺少 maps.json。");
                return result;
            }

            try
            {
                using var listDocument = JsonDocument.Parse(File.ReadAllText(listFile));
                var root = listDocument.RootElement;
                if (!TrySchemaOne(root) || !root.TryGetProperty("maps", out var mapsNode) ||
                    mapsNode.ValueKind != JsonValueKind.Array)
                {
                    Add(ValidationSeverity.Error, "MAP_LIST_INVALID",
                        "maps.json 必须使用 schemaVersion=1 并包含 maps 数组。");
                    return result;
                }

                var seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in mapsNode.EnumerateArray())
                {
                    var id = ReadRequiredString(entry, "id");
                    var directory = ReadRequiredString(entry, "directory");
                    if (id is null || directory is null)
                    {
                        Add(ValidationSeverity.Error, "MAP_ENTRY_INVALID", "maps.json 存在缺少 id 或 directory 的条目。");
                        continue;
                    }

                    if (result.ContainsKey(id))
                    {
                        Add(ValidationSeverity.Error, "DUPLICATE_MAP_ID", $"maps.json 存在重复地图：{id}。", id);
                        continue;
                    }

                    if (!seenDirectories.Add(directory))
                    {
                        Add(ValidationSeverity.Error, "DUPLICATE_MAP_DIRECTORY",
                            $"maps.json 存在重复目录：{directory}。", id);
                        continue;
                    }

                    var enabled = !entry.TryGetProperty("enabled", out var enabledNode) ||
                                  enabledNode.ValueKind != JsonValueKind.False;
                    if (enabled && !baseline.Maps.ContainsKey(id))
                    {
                        Add(ValidationSeverity.Error, "UNCALIBRATED_MAP_ENABLED",
                            $"新地图 {id} 未进入基线校准，不得启用。", id);
                    }

                    result[id] = ValidateMap(id, directory);
                }
            }
            catch (JsonException exception)
            {
                Add(ValidationSeverity.Error, "MAP_LIST_PARSE_FAILED",
                    $"maps.json 解析失败：{exception.Message}");
            }

            return result;
        }

        IReadOnlyDictionary<string, int> ValidateMap(string mapId, string relativeDirectory)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!TryResolveUnder(dataDirectory, relativeDirectory, out var mapDirectory))
            {
                Add(ValidationSeverity.Error, "MAP_DIRECTORY_INVALID",
                    $"地图目录越出 Data：{relativeDirectory}。", mapId);
                return counts;
            }

            var mapFile = Path.Combine(mapDirectory, "map.json");
            if (!File.Exists(mapFile))
            {
                Add(ValidationSeverity.Error, "MAP_JSON_MISSING", "地图缺少 map.json。", mapId);
                return counts;
            }

            try
            {
                using var mapDocument = JsonDocument.Parse(File.ReadAllText(mapFile));
                var root = mapDocument.RootElement;
                if (!TrySchemaOne(root))
                {
                    Add(ValidationSeverity.Error, "MAP_SCHEMA_INVALID",
                        "map.json schemaVersion 必须为 1。", mapId);
                }

                var documentId = ReadRequiredString(root, "id");
                if (documentId is null || !string.Equals(documentId, mapId, StringComparison.Ordinal))
                {
                    Add(ValidationSeverity.Error, "MAP_ID_MISMATCH",
                        $"map.json id 与 maps.json 不一致：{documentId ?? "<缺失>"}。", mapId);
                }

                if (ReadRequiredString(root, "name") is null)
                {
                    Add(ValidationSeverity.Error, "MAP_NAME_MISSING", "map.json 缺少地图名称。", mapId);
                }

                var bounds = ValidateBounds(root, mapId);
                ValidateImage(root, mapId, mapDirectory);
                if (!root.TryGetProperty("markers", out var markersNode) || markersNode.ValueKind != JsonValueKind.Array)
                {
                    Add(ValidationSeverity.Error, "MARKERS_MISSING", "map.json 缺少 markers 数组。", mapId);
                    return counts;
                }

                if (markersNode.GetArrayLength() == 0)
                {
                    Add(ValidationSeverity.Error, "MARKERS_EMPTY", "地图 Marker 数据为空。", mapId);
                }

                var ids = new HashSet<string>(StringComparer.Ordinal);
                var outsideBounds = 0;
                foreach (var marker in markersNode.EnumerateArray())
                {
                    var markerId = ReadRequiredString(marker, "id");
                    var type = ReadRequiredString(marker, "type");
                    var name = ReadRequiredString(marker, "name");
                    if (markerId is null || type is null || name is null ||
                        !TryFiniteNumber(marker, "x", out var x) || !TryFiniteNumber(marker, "z", out var z))
                    {
                        Add(ValidationSeverity.Error, "MARKER_FIELDS_INVALID",
                            "Marker 缺少有效的 id/type/name/x/z。", mapId, type);
                        continue;
                    }

                    if (!ids.Add(markerId))
                    {
                        Add(ValidationSeverity.Error, "DUPLICATE_MARKER_ID",
                            $"Marker ID 重复：{markerId}。", mapId, type);
                    }

                    counts[type] = counts.GetValueOrDefault(type) + 1;
                    if (!KnownMarkerTypes.Contains(type))
                    {
                        Add(ValidationSeverity.Warning, "UNKNOWN_MARKER_TYPE",
                            $"发现未知 Marker 类型：{type}。", mapId, type);
                    }

                    if (bounds is not null && !bounds.Value.Contains(x, z))
                    {
                        outsideBounds++;
                    }

                    ValidateOutline(marker, mapId, type);
                }

                if (outsideBounds > 0)
                {
                    var total = markersNode.GetArrayLength();
                    var ratio = total == 0 ? 0 : (double)outsideBounds / total;
                    var severity = outsideBounds > 10 || ratio > 0.10
                        ? ValidationSeverity.Error
                        : ValidationSeverity.Warning;
                    Add(severity, severity == ValidationSeverity.Error
                            ? "MARKERS_OUT_OF_BOUNDS_MANY"
                            : "MARKERS_OUT_OF_BOUNDS_FEW",
                        $"有 {outsideBounds}/{total} 个 Marker 位于 Bounds 外（{ratio:P1}）。",
                        mapId);
                }
            }
            catch (JsonException exception)
            {
                Add(ValidationSeverity.Error, "MAP_JSON_PARSE_FAILED",
                    $"map.json 解析失败：{exception.Message}", mapId);
            }

            return counts;
        }

        BoundsValue? ValidateBounds(JsonElement root, string mapId)
        {
            if (!root.TryGetProperty("worldBounds", out var node) || node.ValueKind != JsonValueKind.Object ||
                !TryFiniteNumber(node, "x0", out var x0) || !TryFiniteNumber(node, "z0", out var z0) ||
                !TryFiniteNumber(node, "x1", out var x1) || !TryFiniteNumber(node, "z1", out var z1))
            {
                Add(ValidationSeverity.Error, "BOUNDS_INVALID", "地图缺少有效的 worldBounds。", mapId);
                return null;
            }

            if (x0 == x1 || z0 == z1)
            {
                Add(ValidationSeverity.Error, "BOUNDS_DEGENERATE", "worldBounds 宽或高为 0。", mapId);
                return null;
            }

            if (!node.TryGetProperty("coordinateRotation", out var rotationNode))
            {
                Add(ValidationSeverity.Warning, "ROTATION_MISSING",
                    "worldBounds 缺少 coordinateRotation。", mapId);
            }
            else if (rotationNode.ValueKind != JsonValueKind.Number ||
                     !rotationNode.TryGetDouble(out var rotation) || !double.IsFinite(rotation))
            {
                Add(ValidationSeverity.Error, "ROTATION_INVALID",
                    "coordinateRotation 不是有限数值。", mapId);
            }

            return new BoundsValue(x0, z0, x1, z1);
        }

        void ValidateImage(JsonElement root, string mapId, string mapDirectory)
        {
            if (!root.TryGetProperty("image", out var imageNode) || imageNode.ValueKind != JsonValueKind.Object ||
                ReadRequiredString(imageNode, "file") is not { } relativeImage ||
                !imageNode.TryGetProperty("width", out var widthNode) || !widthNode.TryGetInt32(out var width) || width <= 0 ||
                !imageNode.TryGetProperty("height", out var heightNode) || !heightNode.TryGetInt32(out var height) || height <= 0)
            {
                Add(ValidationSeverity.Error, "IMAGE_METADATA_INVALID", "地图图片元数据不完整。", mapId);
                return;
            }

            if (!TryResolveUnder(mapDirectory, relativeImage, out var imageFile))
            {
                Add(ValidationSeverity.Error, "IMAGE_PATH_INVALID", "地图图片路径越出地图目录。", mapId);
                return;
            }

            if (!File.Exists(imageFile))
            {
                Add(ValidationSeverity.Error, "IMAGE_MISSING", $"地图图片不存在：{relativeImage}。", mapId);
                return;
            }

            try
            {
                using var image = Image.FromFile(imageFile);
                if (image.Width != width || image.Height != height)
                {
                    Add(ValidationSeverity.Error, "IMAGE_SIZE_MISMATCH",
                        $"地图图片尺寸不一致：JSON={width}×{height}，文件={image.Width}×{image.Height}。", mapId);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or OutOfMemoryException)
            {
                Add(ValidationSeverity.Error, "IMAGE_INVALID", $"地图图片无法读取：{exception.Message}", mapId);
            }
        }

        void ValidateOutline(JsonElement marker, string mapId, string type)
        {
            if (!marker.TryGetProperty("outline", out var outline) || outline.ValueKind == JsonValueKind.Null)
            {
                return;
            }

            if (outline.ValueKind != JsonValueKind.Array || outline.GetArrayLength() < 3 ||
                outline.EnumerateArray().Any(point => point.ValueKind != JsonValueKind.Array ||
                    point.GetArrayLength() < 2 || !point[0].TryGetDouble(out var x) || !double.IsFinite(x) ||
                    !point[1].TryGetDouble(out var z) || !double.IsFinite(z)))
            {
                Add(ValidationSeverity.Error, "MARKER_OUTLINE_INVALID",
                    "Marker outline 必须包含至少 3 个有限坐标点。", mapId, type);
            }
        }

        void CompareBaseline(Dictionary<string, IReadOnlyDictionary<string, int>> currentMaps)
        {
            foreach (var (mapId, baselineMap) in baseline.Maps.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!currentMaps.TryGetValue(mapId, out var currentTypes))
                {
                    Add(ValidationSeverity.Error, "BASELINE_MAP_MISSING",
                        $"输出缺少基线地图 {mapId}。", mapId);
                    continue;
                }

                foreach (var type in CoreMarkerTypes.Order(StringComparer.Ordinal))
                {
                    var baselineCount = baselineMap.MarkerTypes.GetValueOrDefault(type);
                    var currentCount = currentTypes.GetValueOrDefault(type);
                    if (baselineCount == currentCount)
                    {
                        continue;
                    }

                    double? percentChange = baselineCount == 0
                        ? null
                        : (currentCount - baselineCount) * 100.0 / baselineCount;
                    var exceeded = percentChange is not null && Math.Abs(percentChange.Value) > CoreChangeThreshold;
                    var approved = exceeded && approvals.IsApproved(dataVersion, mapId, type,
                        baselineCount, currentCount);
                    diffs.Add(new MarkerCountDiff(mapId, type, baselineCount, currentCount,
                        currentCount - baselineCount, percentChange, exceeded, approved));

                    if (exceeded && !approved)
                    {
                        Add(ValidationSeverity.Error, "CORE_COUNT_CHANGE_UNCONFIRMED",
                            $"核心类别数量变化超过 30% 且未确认：{baselineCount} → {currentCount}（{percentChange:+0.0;-0.0;0.0}%）。",
                            mapId, type);
                    }
                    else if (exceeded)
                    {
                        Add(ValidationSeverity.Info, "CORE_COUNT_CHANGE_APPROVED",
                            $"核心类别大幅变化已有精确审批：{baselineCount} → {currentCount}（{percentChange:+0.0;-0.0;0.0}%）。",
                            mapId, type);
                    }
                    else if (percentChange is null)
                    {
                        Add(ValidationSeverity.Info, "CORE_COUNT_ADDED",
                            $"基线没有此核心类别，当前新增 {currentCount} 个。", mapId, type);
                    }
                    else
                    {
                        Add(ValidationSeverity.Warning, "CORE_COUNT_CHANGED",
                            $"核心类别数量变化未超过 30%：{baselineCount} → {currentCount}（{percentChange:+0.0;-0.0;0.0}%）。",
                            mapId, type);
                    }
                }
            }
        }

        void Add(ValidationSeverity severity, string code, string message,
            string? mapId = null, string? markerType = null) =>
            issues.Add(new ValidationIssue(severity, code, message, mapId, markerType));

        MapDataValidationReport CreateReport()
        {
            var orderedIssues = issues
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.MapId, StringComparer.Ordinal)
                .ThenBy(issue => issue.MarkerType, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ToList();
            var errorCount = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
            return new MapDataValidationReport
            {
                DataVersion = dataVersion,
                GeneratedAt = generatedAt ?? DateTimeOffset.UtcNow,
                CanPackage = errorCount == 0,
                ErrorCount = errorCount,
                WarningCount = issues.Count(issue => issue.Severity == ValidationSeverity.Warning),
                InfoCount = issues.Count(issue => issue.Severity == ValidationSeverity.Info),
                Issues = orderedIssues,
                MarkerCountDiffs = diffs.OrderBy(diff => diff.MapId, StringComparer.Ordinal)
                    .ThenBy(diff => diff.MarkerType, StringComparer.Ordinal).ToList()
            };
        }
    }

    private static bool TrySchemaOne(JsonElement root) =>
        root.TryGetProperty("schemaVersion", out var node) && node.TryGetInt32(out var version) && version == 1;

    private static string? ReadRequiredString(JsonElement node, string propertyName) =>
        node.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static bool TryFiniteNumber(JsonElement node, string propertyName, out double value)
    {
        value = 0;
        return node.TryGetProperty(propertyName, out var number) && number.ValueKind == JsonValueKind.Number &&
               number.TryGetDouble(out value) && double.IsFinite(value);
    }

    private static bool TryResolveUnder(string root, string relativePath, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                             Path.DirectorySeparatorChar;
        fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct BoundsValue(double X0, double Z0, double X1, double Z1)
    {
        public bool Contains(double x, double z) =>
            x >= Math.Min(X0, X1) && x <= Math.Max(X0, X1) &&
            z >= Math.Min(Z0, Z1) && z <= Math.Max(Z0, Z1);
    }
}
