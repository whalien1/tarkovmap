using TarkovMap.Models;

namespace TarkovMap.Services;

/// <summary>
/// 共享地图视图状态（M0 重构核心）：
/// 当前地图 + 底图 + 玩家位置 + Marker 可见性，集中持有并广播变化事件。
/// 主画布 MapCanvas 与后续悬浮小地图 MiniMapCanvas 都从这里读数据、订阅事件，
/// 严禁任何 View 自己再加载地图图片或维护第二份状态（模块文档 §0.5 / §46 红线）。
///
/// Bitmap 所有权归本类：SetMap 时 Dispose 旧图，Dispose() 时释放当前图。
/// 所有成员只允许在 UI 线程访问（调用方负责线程切换）。
/// </summary>
public sealed class MapViewState : IDisposable
{
    /// <summary>当前地图定义；未加载时为 null。</summary>
    public MapDefinition? Map { get; private set; }

    /// <summary>当前底图；与 Map 同生共死，未加载时为 null。</summary>
    public Bitmap? Bitmap { get; private set; }

    /// <summary>最近一次有效玩家定位；无定位或越界清除时为 null。</summary>
    public PlayerLocation? Player { get; private set; }

    private readonly Dictionary<MarkerType, bool> _visibility = new();

    /// <summary>地图或底图被替换（View 应清空选中、重新适配视口）。</summary>
    public event Action? MapChanged;

    /// <summary>玩家定位更新或被清除。</summary>
    public event Action? PlayerChanged;

    /// <summary>任一类 Marker 的可见性变化。</summary>
    public event Action? MarkerVisibilityChanged;

    /// <summary>切换地图。旧 Bitmap 在此 Dispose；玩家定位随之失效（坐标属于旧地图）。</summary>
    public void SetMap(MapDefinition map, Bitmap bitmap)
    {
        Bitmap?.Dispose();
        Map = map;
        Bitmap = bitmap;
        Player = null;
        MapChanged?.Invoke();
    }

    /// <summary>
    /// 更新玩家定位。坐标不在当前地图 Bounds 内：清除定位并返回 false
    /// （行为与 v1.0 MapCanvas.SetPlayerLocation 完全一致）。
    /// </summary>
    public bool SetPlayerLocation(PlayerLocation location)
    {
        if (Map is null || Bitmap is null)
        {
            return false;
        }

        if (!Map.Bounds.Contains(location.X, location.Z))
        {
            Player = null;
            PlayerChanged?.Invoke();
            return false;
        }

        Player = location;
        PlayerChanged?.Invoke();
        return true;
    }

    /// <summary>设置某类 Marker 是否显示。</summary>
    public void SetMarkerVisibility(MarkerType type, bool visible)
    {
        _visibility[type] = visible;
        MarkerVisibilityChanged?.Invoke();
    }

    /// <summary>查询某个 Marker 当前是否可见（未设置过的类别视为不可见）。</summary>
    public bool IsVisible(Marker m) =>
        _visibility.TryGetValue(m.Type, out var v) && v;

    public void Dispose()
    {
        Bitmap?.Dispose();
        Bitmap = null;
    }
}
