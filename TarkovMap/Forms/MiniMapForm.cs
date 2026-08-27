using System.Runtime.InteropServices;
using TarkovMap.Controls;
using TarkovMap.Models;
using TarkovMap.Services;

namespace TarkovMap.Forms;

/// <summary>
/// 悬浮小地图窗口：无边框、置顶、不占任务栏。
/// 支持左键拖动、位置记忆（屏幕边缘保护）、圆/方、大/小、三档透明度。
/// 本窗口只是 MapViewState 的 View，不持有任何业务数据。
/// </summary>
public sealed class MiniMapForm : Form
{
    // 原生窗口拖动（无边框窗口的标准方案）
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int WM_MOUSEACTIVATE = 0x21;
    private const int HTCAPTION = 0x2;
    private const int MA_NOACTIVATE = 0x3;
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly MiniMapSettings _settings;
    private readonly MiniMapCanvas _canvas;
    private bool _ready;

    /// <summary>用户通过 Alt+F4 等方式关窗时触发（窗口实际转为隐藏，由主界面同步取消勾选）。</summary>
    public event Action? UserClosed;

    /// <summary>主程序退出时置 true，允许真正关闭。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AllowClose { get; set; }

    public MiniMapForm(MapViewState state, IconCache icons, MiniMapSettings settings)
    {
        _settings = settings;

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(30, 30, 30);

        _canvas = new MiniMapCanvas { Dock = DockStyle.Fill };
        _canvas.SetViewState(state);
        _canvas.SetIconCache(icons);
        _canvas.ZoomLevel = settings.Zoom;
        _canvas.ZoomChanged += z => _settings.Zoom = z; // 写内存，退出时统一落盘
        _canvas.DragRequested += _ =>
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        };
        _canvas.DragEnded += SavePosition;
        Controls.Add(_canvas);

        ApplySettings();
    }

    /// <summary>
    /// 小地图始终置顶但不能因鼠标操作抢走游戏焦点。
    /// MA_NOACTIVATE 保留后续鼠标消息，因此拖动和滚轮缩放仍然有效。
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }

        base.WndProc(ref m);
    }

    /// <summary>应用外观设置（形状/尺寸/透明度），立即生效。</summary>
    public void ApplySettings()
    {
        Opacity = _settings.OpacityValue;

        var size = _settings.PixelSize;
        var keepCenter = _ready;
        var center = new Point(Left + Width / 2, Top + Height / 2);

        // 圆形模式：窗口 Region 裁成圆（仅形状/尺寸变化时重建）
        var sizeChanged = ClientSize.Width != size || ClientSize.Height != size;
        if (sizeChanged)
        {
            ClientSize = new Size(size, size);
        }
        if (_settings.Shape == MiniMapSettings.ShapeKind.Circle)
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, size, size);
            Region?.Dispose();
            Region = new Region(path);
        }
        else
        {
            Region?.Dispose();
            Region = null;
        }
        _canvas.SetCircle(_settings.Shape == MiniMapSettings.ShapeKind.Circle);

        if (!_ready)
        {
            RestorePosition();
            _ready = true;
        }
        else if (keepCenter)
        {
            // 尺寸变化时保持窗口中心不动，避免突然跳位
            Location = new Point(center.X - Width / 2, center.Y - Height / 2);
        }
    }

    /// <summary>将悬浮小地图放回主屏右上角，并清除已保存的位置。</summary>
    public void ResetPosition()
    {
        _settings.X = -1;
        _settings.Y = -1;
        RestorePosition();
    }

    /// <summary>恢复默认缩放，便于从过度放大或缩小中快速回到可用视野。</summary>
    public void ResetZoom()
    {
        _settings.Zoom = MiniMapSettings.DefaultZoom;
        _canvas.ZoomLevel = _settings.Zoom;
    }

    /// <summary>恢复位置：优先上次保存值；越出屏幕则回到主屏右上角。</summary>
    private void RestorePosition()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        var pos = new Point(area.Right - Width - 16, area.Top + 16);

        if (_settings.X >= 0 && _settings.Y >= 0)
        {
            var saved = new Rectangle(_settings.X, _settings.Y, Width, Height);
            if (saved.IntersectsWith(area))
            {
                pos = saved.Location;
            }
        }
        Location = pos;
    }

    /// <summary>拖动结束：位置写进设置（内存），退出时统一保存。</summary>
    private void SavePosition()
    {
        _settings.X = Left;
        _settings.Y = Top;
    }

    /// <summary>防止用户误关：Alt+F4 等只隐藏窗口，由主界面同步取消勾选；主程序退出才真正关闭。</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            UserClosed?.Invoke();
            return;
        }
        base.OnFormClosing(e);
    }

    /// <summary>防止拖动出屏幕边缘太远（至少留 48px 可见）。</summary>
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (!_ready)
        {
            return;
        }
        var area = Screen.PrimaryScreen?.WorkingArea;
        if (area is null)
        {
            return;
        }
        const int margin = 48;
        var x = Math.Clamp(Left, area.Value.Left - Width + margin, area.Value.Right - margin);
        var y = Math.Clamp(Top, area.Value.Top, area.Value.Bottom - margin);
        if (x != Left || y != Top)
        {
            Location = new Point(x, y);
        }
    }
}
