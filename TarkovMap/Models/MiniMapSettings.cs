using System.Text.Json.Serialization;

namespace TarkovMap.Models;

/// <summary>悬浮小地图外观/位置设置（存于主配置 config.json 的 miniMap 段）。</summary>
public sealed class MiniMapSettings
{
    public const double DefaultZoom = 0.5;

    public enum ShapeKind { Square, Circle }
    public enum SizeKind { Small, Medium, Large }
    public enum OpacityKind { Low, Medium, High }

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("shape")]
    public ShapeKind Shape { get; set; } = ShapeKind.Square;

    [JsonPropertyName("size")]
    public SizeKind Size { get; set; } = SizeKind.Medium;

    [JsonPropertyName("opacity")]
    public OpacityKind Opacity { get; set; } = OpacityKind.Medium;

    /// <summary>更多设置是否在侧栏展开；默认收起。</summary>
    [JsonPropertyName("moreSettingsExpanded")]
    public bool MoreSettingsExpanded { get; set; }

    [JsonPropertyName("zoom")]
    public double Zoom { get; set; } = DefaultZoom;

    /// <summary>窗口位置；-1 表示未设置（用默认右上角）。</summary>
    [JsonPropertyName("x")]
    public int X { get; set; } = -1;

    [JsonPropertyName("y")]
    public int Y { get; set; } = -1;

    public double OpacityValue => Opacity switch
    {
        OpacityKind.Low => 0.50,
        OpacityKind.High => 1.00,
        _ => 0.75
    };

    public int PixelSize => Size switch
    {
        SizeKind.Small => 260,
        SizeKind.Large => 340,
        _ => 300
    };
}
