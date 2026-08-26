using TarkovMap.Models;

namespace TarkovMap.Controls;

/// <summary>按缩放级别控制地图上的文字密度，保证关键战术信息始终可读。</summary>
public static class MarkerNameVisibilityPolicy
{
    /// <summary>地图区域标注开始显示的最小缩放比例（25%）。</summary>
    public const double MapLabelMinimumZoom = 0.25;

    /// <summary>
    /// 判断 Marker 名称是否绘制。
    /// 撤离、转移、Boss 与危险区域属于关键战术信息，始终显示；
    /// 普通地图区域标注仅在缩小到 25% 以下时隐藏；常用的全图缩放仍完整保留地名。
    /// </summary>
    public static bool ShouldDrawName(MarkerType type, double zoom)
        => type != MarkerType.Label || zoom >= MapLabelMinimumZoom;
}
