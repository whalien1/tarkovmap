namespace TarkovMap.Infrastructure;

/// <summary>
/// 固定普通窗口在不同屏幕工作区中的安全尺寸计算。
/// 普通窗口不允许用户拖拽缩放，但不能超出当前显示器的可用区域。
/// </summary>
public static class WindowSizePolicy
{
    public static Size FitClientSizeToWorkingArea(
        Size preferredClientSize,
        Size workingAreaSize,
        Size nonClientSize)
    {
        var maxClientWidth = Math.Max(1, workingAreaSize.Width - nonClientSize.Width);
        var maxClientHeight = Math.Max(1, workingAreaSize.Height - nonClientSize.Height);

        return new Size(
            Math.Min(preferredClientSize.Width, maxClientWidth),
            Math.Min(preferredClientSize.Height, maxClientHeight));
    }
}
