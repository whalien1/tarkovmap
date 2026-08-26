using TarkovMap.Models;
using TarkovMap.Services;

namespace TarkovMap.Controls;

/// <summary>
/// 悬浮小地图画布：MapViewState 的第二个 View。
/// 玩家居中、固定北向、箭头随朝向旋转；滚轮缩放视野范围。
/// Marker 固定显示 撤离点 + Boss + 危险区 + 地区名标注（与主图勾选解耦，§0.3）。
/// 只从共享底图 DrawImage 局部区域，绝不复制/裁剪出新 Bitmap（性能红线 §38）。
/// </summary>
public sealed class MiniMapCanvas : Control
{
    private const double ZoomStep = 1.10;
    private const double MinZoom = 0.10;
    private const double MaxZoom = 4.0;
    private const double DefaultZoom = 0.5;

    private MapViewState? _state;
    private IconCache? _icons;
    private double _zoom = DefaultZoom;
    private bool _circle;

    private MapDefinition? Map => _state?.Map;
    private Bitmap? Bitmap => _state?.Bitmap;

    /// <summary>当前缩放（供设置持久化读写）。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double ZoomLevel
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, MinZoom, MaxZoom);
            Invalidate();
        }
    }

    /// <summary>缩放变化（用于写入设置）。</summary>
    public event Action<double>? ZoomChanged;

    /// <summary>请求拖动窗口（左键按下时触发，由宿主管件执行原生拖动）。</summary>
    public event Action<Point>? DragRequested;

    /// <summary>拖动结束（用于保存窗口位置）。</summary>
    public event Action? DragEnded;

    /// <summary>圆形模式：绘制时裁剪到内切圆并画圆形边框。</summary>
    public void SetCircle(bool circle)
    {
        _circle = circle;
        Invalidate();
    }

    public MiniMapCanvas()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = Color.FromArgb(30, 30, 30);
    }

    public void SetIconCache(IconCache icons)
    {
        _icons = icons;
    }

    /// <summary>挂接共享状态；状态变化时按需重绘（不用 Timer）。</summary>
    public void SetViewState(MapViewState state)
    {
        _state = state;
        _state.MapChanged += Invalidate;
        _state.PlayerChanged += Invalidate;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var player = _state?.Player;
        var map = Map;
        var bitmap = Bitmap;

        // 圆形模式：所有内容裁剪到内切圆
        if (_circle)
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(1, 1, ClientSize.Width - 2, ClientSize.Height - 2);
            g.SetClip(path);
        }

        if (player is null || map is null || bitmap is null)
        {
            // 无定位：不展示默认地图区域，只显示等待提示
            using var font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold, GraphicsUnit.Point);
            using var brush = new SolidBrush(Color.FromArgb(180, 180, 180));
            const string text = "等待截图定位";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, brush,
                (ClientSize.Width - size.Width) / 2, (ClientSize.Height - size.Height) / 2);
            DrawBorder(g);
            return;
        }

        g.InterpolationMode = _zoom >= 1.0
            ? System.Drawing.Drawing2D.InterpolationMode.Bilinear
            : System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

        // 玩家图片坐标 → 屏幕中心
        var playerImagePt = MapCoordinateService.WorldToImage(
            map.Bounds, bitmap.Width, bitmap.Height, player.X, player.Z);
        var panX = (float)(ClientSize.Width / 2.0 - playerImagePt.X * _zoom);
        var panY = (float)(ClientSize.Height / 2.0 - playerImagePt.Y * _zoom);

        // 只绘制窗口可见区域对应的源图片部分（玩家可能靠近地图边缘）
        var dest = new RectangleF(panX, panY,
            (float)(bitmap.Width * _zoom), (float)(bitmap.Height * _zoom));
        if (dest.IntersectsWith(ClientRectangle))
        {
            var vis = RectangleF.Intersect(dest, ClientRectangle);
            var srcX = (float)((vis.X - panX) / _zoom);
            var srcY = (float)((vis.Y - panY) / _zoom);
            g.DrawImage(
                bitmap,
                Rectangle.Round(vis),
                new RectangleF(srcX, srcY, (float)(vis.Width / _zoom), (float)(vis.Height / _zoom)),
                GraphicsUnit.Pixel);
        }

        DrawHazards(g, map, bitmap, panX, panY);
        DrawMarkers(g, map, bitmap, panX, panY);
        DrawPlayer(g, player, map);

        g.ResetClip();
        DrawBorder(g);
    }

    /// <summary>窗口边框：方形画矩形、圆形画圆，2px 半透明浅色。</summary>
    private void DrawBorder(Graphics g)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(160, 200, 200, 200), 2f);
        if (_circle)
        {
            g.DrawEllipse(pen, 1, 1, ClientSize.Width - 2, ClientSize.Height - 2);
        }
        else
        {
            g.DrawRectangle(pen, 1, 1, ClientSize.Width - 2, ClientSize.Height - 2);
        }
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
    }

    /// <summary>世界坐标 → 小地图屏幕坐标。</summary>
    private PointF WorldToScreen(MapDefinition map, Bitmap bitmap, float panX, float panY,
        double worldX, double worldZ)
    {
        var imagePt = MapCoordinateService.WorldToImage(
            map.Bounds, bitmap.Width, bitmap.Height, worldX, worldZ);
        return new PointF(
            (float)(imagePt.X * _zoom + panX),
            (float)(imagePt.Y * _zoom + panY));
    }

    /// <summary>危险区：红色半透明多边形 + 红边框（与主图同风格），画在 Marker 之下。</summary>
    private void DrawHazards(Graphics g, MapDefinition map, Bitmap bitmap, float panX, float panY)
    {
        using var fill = new SolidBrush(Color.FromArgb(64, 211, 47, 47));
        using var border = new Pen(Color.FromArgb(200, 229, 57, 53), 2f);

        foreach (var m in map.Markers)
        {
            if (m.Type != MarkerType.Hazard || m.Outline is null || m.Outline.Count < 3)
            {
                continue;
            }

            var points = new PointF[m.Outline.Count];
            for (var i = 0; i < m.Outline.Count; i++)
            {
                points[i] = WorldToScreen(map, bitmap, panX, panY, m.Outline[i][0], m.Outline[i][1]);
            }

            var bounds = BoundingBox(points);
            if (!bounds.IntersectsWith(ClientRectangle))
            {
                continue;
            }

            g.FillPolygon(fill, points);
            g.DrawPolygon(border, points);
        }
    }

    private static RectangleF BoundingBox(PointF[] points)
    {
        float minX = points[0].X, maxX = points[0].X, minY = points[0].Y, maxY = points[0].Y;
        foreach (var p in points)
        {
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
        }
        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Marker：固定只画 撤离点/转移点 + Boss + 危险区 + 地区名标注。
    /// 样式与主图一致（名称小字、Boss 红圈红名、危险区红名、标注白字描边）。
    /// </summary>
    private void DrawMarkers(Graphics g, MapDefinition map, Bitmap bitmap, float panX, float panY)
    {
        if (_icons is null)
        {
            return;
        }

        var clip = ClientRectangle;
        clip.Inflate(40, 40);

        using var labelFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
        using var labelBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
        using var labelShadow = new SolidBrush(Color.FromArgb(220, 0, 0, 0));
        using var nameFont = new Font("Microsoft YaHei UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point);
        using var pmcExtractBrush = new SolidBrush(Color.FromArgb(172, 235, 181));
        using var scavExtractBrush = new SolidBrush(Color.FromArgb(255, 209, 128));
        using var extractBrush = new SolidBrush(Color.FromArgb(255, 255, 214));
        using var hazardBrush = new SolidBrush(Color.FromArgb(255, 82, 82));
        using var bossBrush = new SolidBrush(Color.FromArgb(255, 112, 112));

        foreach (var m in map.Markers)
        {
            var isExtract = m.Type is MarkerType.ExtractPmc or MarkerType.ExtractScav or
                MarkerType.ExtractShared or MarkerType.ExtractTransit;
            var isWanted = isExtract || m.Type is MarkerType.Boss or MarkerType.Hazard
                or MarkerType.Label;
            if (!isWanted)
            {
                continue;
            }

            var s = WorldToScreen(map, bitmap, panX, panY, m.X, m.Z);
            if (!clip.Contains((int)s.X, (int)s.Y))
            {
                continue;
            }

            if (m.Type == MarkerType.Label)
            {
                g.DrawString(m.Name, labelFont, labelShadow, s.X + 1, s.Y);
                g.DrawString(m.Name, labelFont, labelShadow, s.X - 1, s.Y);
                g.DrawString(m.Name, labelFont, labelShadow, s.X, s.Y + 1);
                g.DrawString(m.Name, labelFont, labelShadow, s.X, s.Y - 1);
                g.DrawString(m.Name, labelFont, labelBrush, s.X, s.Y);
                continue;
            }

            var icon = _icons.Get(m.Type);
            var size = IconCache.SizeFor(m.Type);
            int half = size / 2;
            g.DrawImage(icon, s.X - half, s.Y - half, size, size);

            if (isExtract || m.Type == MarkerType.Hazard || m.Type == MarkerType.Boss)
            {
                var brush = m.Type == MarkerType.Hazard ? hazardBrush
                    : m.Type == MarkerType.Boss ? bossBrush
                    : m.Type == MarkerType.ExtractPmc ? pmcExtractBrush
                    : m.Type == MarkerType.ExtractScav ? scavExtractBrush
                    : extractBrush;
                var nameSize = g.MeasureString(m.Name, nameFont);
                var nx = s.X - nameSize.Width / 2;
                var ny = s.Y + half + 1;
                g.DrawString(m.Name, nameFont, labelShadow, nx + 1, ny + 1);
                g.DrawString(m.Name, nameFont, brush, nx, ny);
            }

            if (m.Type == MarkerType.Boss)
            {
                using var ringPen = new Pen(Color.FromArgb(255, 82, 82), 2f);
                g.DrawEllipse(ringPen, s.X - half - 2, s.Y - half - 2, size + 4, size + 4);
            }
        }
    }

    /// <summary>玩家箭头：固定画在窗口正中心，方向公式与主画布一致（Yaw + 地图旋转角 + 90°）。</summary>
    private void DrawPlayer(Graphics g, PlayerLocation player, MapDefinition map)
    {
        var sx = ClientSize.Width / 2f;
        var sy = ClientSize.Height / 2f;

        var state = g.Save();
        g.TranslateTransform(sx, sy);
        g.RotateTransform((float)(player.YawDegrees + map.Bounds.CoordinateRotation + 90.0));
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        PointF[] arrow =
        [
            new PointF(0, -14),
            new PointF(9, 10),
            new PointF(0, 5),
            new PointF(-9, 10),
        ];
        using (var fill = new SolidBrush(Color.FromArgb(255, 193, 7)))
        using (var outline = new Pen(Color.FromArgb(230, 0, 0, 0), 1.5f))
        {
            g.FillPolygon(fill, arrow);
            g.DrawPolygon(outline, arrow);
        }
        g.Restore(state);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;

        using var dotBrush = new SolidBrush(Color.White);
        using var dotPen = new Pen(Color.FromArgb(230, 0, 0, 0), 1.5f);
        g.FillEllipse(dotBrush, sx - 4, sy - 4, 8, 8);
        g.DrawEllipse(dotPen, sx - 4, sy - 4, 8, 8);
    }

    /// <summary>滚轮缩放视野范围；玩家始终居中，只需改 Zoom。</summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
        var newZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _zoom) < 1e-9)
        {
            return;
        }
        _zoom = newZoom;
        ZoomChanged?.Invoke(_zoom);
        Invalidate();
    }

    /// <summary>左键按下 = 请求拖动窗口（小地图 Marker 不响应点击）。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            DragRequested?.Invoke(e.Location);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            DragEnded?.Invoke();
        }
    }
}
