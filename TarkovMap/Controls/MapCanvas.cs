using TarkovMap.Models;
using TarkovMap.Services;

namespace TarkovMap.Controls;

/// <summary>
/// 主地图画布：MapViewState 的 View。
/// 地图数据 / 底图 / 玩家位置 / Marker 可见性全部来自共享状态（M0 重构），
/// 自己只维护视图变换（Zoom + PanOffset）、拖拽交互与 Marker 点击选中。
/// 事件驱动绘制：仅在状态变化时 Invalidate，空闲不消耗 CPU。
/// </summary>
public sealed class MapCanvas : Control
{
    private const double ZoomStep = 1.15;
    private const double MaxZoom = 32.0;
    private const double HitRadiusScreenPx = 14.0;
    private const double DragThresholdPx = 5.0;

    private MapViewState? _state;
    private IconCache? _icons;

    private double _zoom = 1.0;
    private PointF _pan = PointF.Empty;

    private bool _dragging;
    private Point _lastMouse;
    private Point _mouseDownPos;

    private double _minZoom = 0.01;

    private Marker? _selected;

    private MapDefinition? Map => _state?.Map;
    private Bitmap? Bitmap => _state?.Bitmap;

    /// <summary>鼠标世界坐标变化（X, Z）。</summary>
    public event Action<double, double>? CursorWorldChanged;

    /// <summary>缩放比例变化。</summary>
    public event Action<double>? ZoomChanged;

    /// <summary>Marker 被点击（名称, 类别名）；参数为 null 表示点击空白。</summary>
    public event Action<string?, string?>? MarkerClicked;

    public MapCanvas()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = Color.FromArgb(30, 30, 30);
    }

    public double Zoom => _zoom;

    public void SetIconCache(IconCache icons)
    {
        _icons = icons;
    }

    /// <summary>挂接共享状态；此后画布跟随状态事件自动重绘。</summary>
    public void SetViewState(MapViewState state)
    {
        _state = state;
        _state.MapChanged += OnStateMapChanged;
        _state.PlayerChanged += OnStatePlayerChanged;
        _state.MarkerVisibilityChanged += OnStateVisibilityChanged;
    }

    /// <summary>地图切换：清空选中并适配窗口（行为同 v1.0 SetMap）。</summary>
    private void OnStateMapChanged()
    {
        _selected = null;
        FitToWindow();
    }

    /// <summary>定位更新：自动居中；被清除（越界/切图）则只重绘。</summary>
    private void OnStatePlayerChanged()
    {
        var player = _state?.Player;
        if (player is not null)
        {
            CenterOn(player.X, player.Z);
        }
        else
        {
            Invalidate();
        }
    }

    /// <summary>可见性变化：若选中项被隐藏则取消选中（行为同 v1.0）。</summary>
    private void OnStateVisibilityChanged()
    {
        if (_selected is not null && _state is not null && !_state.IsVisible(_selected))
        {
            _selected = null;
            MarkerClicked?.Invoke(null, null);
        }
        Invalidate();
    }

    /// <summary>平移到指定世界坐标居中，保持当前 Zoom。</summary>
    public void CenterOn(double worldX, double worldZ)
    {
        if (Map is null || Bitmap is null)
        {
            return;
        }
        var imagePt = MapCoordinateService.WorldToImage(
            Map.Bounds, Bitmap.Width, Bitmap.Height, worldX, worldZ);
        _pan.X = (float)(ClientSize.Width / 2.0 - imagePt.X * _zoom);
        _pan.Y = (float)(ClientSize.Height / 2.0 - imagePt.Y * _zoom);
        Invalidate();
    }

    /// <summary>整张地图适配当前视口并居中。</summary>
    public void FitToWindow()
    {
        if (Bitmap is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        var zx = (double)ClientSize.Width / Bitmap.Width;
        var zy = (double)ClientSize.Height / Bitmap.Height;
        _zoom = Math.Min(zx, zy);
        _minZoom = _zoom * 0.5;

        _pan = new PointF(
            (float)((ClientSize.Width - Bitmap.Width * _zoom) / 2),
            (float)((ClientSize.Height - Bitmap.Height * _zoom) / 2));

        ZoomChanged?.Invoke(_zoom);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var bitmap = Bitmap;
        if (bitmap is null)
        {
            return;
        }

        var g = e.Graphics;
        g.InterpolationMode = _zoom >= 1.0
            ? System.Drawing.Drawing2D.InterpolationMode.Bilinear
            : System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

        var dest = new RectangleF(
            _pan.X,
            _pan.Y,
            (float)(bitmap.Width * _zoom),
            (float)(bitmap.Height * _zoom));

        if (dest.IntersectsWith(ClientRectangle))
        {
            // 只绘制可见区域对应的图片部分（大图性能关键）
            var vis = RectangleF.Intersect(dest, ClientRectangle);
            var srcX = (float)((vis.X - _pan.X) / _zoom);
            var srcY = (float)((vis.Y - _pan.Y) / _zoom);
            var srcW = (float)(vis.Width / _zoom);
            var srcH = (float)(vis.Height / _zoom);
            g.DrawImage(
                bitmap,
                Rectangle.Round(vis),
                new RectangleF(srcX, srcY, srcW, srcH),
                GraphicsUnit.Pixel);
        }

        DrawHazards(g);
        DrawMarkers(g);
        DrawPlayer(g);
        DrawSelection(g);
    }

    /// <summary>危险区（地雷区/狙击区等即死机制）：红色半透明多边形 + 红边框，画在 Marker 之下。</summary>
    private void DrawHazards(Graphics g)
    {
        var map = Map;
        var bitmap = Bitmap;
        if (map is null || bitmap is null || _state is null)
        {
            return;
        }

        using var fill = new SolidBrush(Color.FromArgb(64, 211, 47, 47));
        using var border = new Pen(Color.FromArgb(200, 229, 57, 53), 2f);

        foreach (var m in map.Markers)
        {
            if (m.Type != MarkerType.Hazard || m.Outline is null || m.Outline.Count < 3 ||
                !_state.IsVisible(m))
            {
                continue;
            }

            var points = new PointF[m.Outline.Count];
            for (var i = 0; i < m.Outline.Count; i++)
            {
                var imagePt = MapCoordinateService.WorldToImage(
                    map.Bounds, bitmap.Width, bitmap.Height, m.Outline[i][0], m.Outline[i][1]);
                points[i] = new PointF(
                    (float)(imagePt.X * _zoom + _pan.X),
                    (float)(imagePt.Y * _zoom + _pan.Y));
            }

            // 粗略视口裁剪
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

    /// <summary>玩家箭头：固定屏幕像素大小（不随 Zoom 变化），方向 = Yaw + 地图坐标旋转角。</summary>
    private void DrawPlayer(Graphics g)
    {
        var player = _state?.Player;
        var map = Map;
        var bitmap = Bitmap;
        if (player is null || map is null || bitmap is null)
        {
            return;
        }

        var imagePt = MapCoordinateService.WorldToImage(
            map.Bounds, bitmap.Width, bitmap.Height, player.X, player.Z);
        var sx = (float)(imagePt.X * _zoom + _pan.X);
        var sy = (float)(imagePt.Y * _zoom + _pan.Y);

        var state = g.Save();
        g.TranslateTransform(sx, sy);
        // 朝向 = Yaw + 地图坐标旋转角 + 90°（2026-08-24 海岸线实测校准：需顺时针补 90°）
        g.RotateTransform((float)(player.YawDegrees + map.Bounds.CoordinateRotation + 90.0));
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // 箭头指向上方（屏幕北），随朝向旋转
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

    private void DrawMarkers(Graphics g)
    {
        var map = Map;
        var bitmap = Bitmap;
        if (map is null || bitmap is null || _icons is null || _state is null)
        {
            return;
        }

        // 视口裁剪：留一圈边距，屏幕外的 Marker 不画
        var clip = ClientRectangle;
        clip.Inflate(40, 40);

        // 地图标注：加粗加大 + 深色描边（白色小字在复杂底图上不易读）
        using var labelFont = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
        using var labelBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
        using var labelShadow = new SolidBrush(Color.FromArgb(220, 0, 0, 0));
        // 撤离点名称：图标旁的小字标注
        using var extractFont = new Font("Microsoft YaHei UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point);
        using var extractBrush = new SolidBrush(Color.FromArgb(255, 255, 214));
        using var extractShadow = new SolidBrush(Color.FromArgb(220, 0, 0, 0));

        foreach (var m in map.Markers)
        {
            if (!_state.IsVisible(m))
            {
                continue;
            }

            var imagePt = MapCoordinateService.WorldToImage(
                map.Bounds, bitmap.Width, bitmap.Height, m.X, m.Z);
            var sx = (float)(imagePt.X * _zoom + _pan.X);
            var sy = (float)(imagePt.Y * _zoom + _pan.Y);

            if (!clip.Contains((int)sx, (int)sy))
            {
                continue;
            }

            if (m.Type == MarkerType.Label)
            {
                // 描边：四个方向画阴影再画正文
                g.DrawString(m.Name, labelFont, labelShadow, sx + 1, sy);
                g.DrawString(m.Name, labelFont, labelShadow, sx - 1, sy);
                g.DrawString(m.Name, labelFont, labelShadow, sx, sy + 1);
                g.DrawString(m.Name, labelFont, labelShadow, sx, sy - 1);
                g.DrawString(m.Name, labelFont, labelBrush, sx, sy);
                continue;
            }

            var icon = _icons.Get(m.Type);
            var size = IconCache.SizeFor(m.Type);
            int half = size / 2;
            g.DrawImage(icon, sx - half, sy - half, size, size);

            // 撤离点/转移点：图标下方显示名称
            if (m.Type is MarkerType.ExtractPmc or MarkerType.ExtractScav or
                MarkerType.ExtractShared or MarkerType.ExtractTransit)
            {
                var nameSize = g.MeasureString(m.Name, extractFont);
                var nx = sx - nameSize.Width / 2;
                var ny = sy + half + 1;
                g.DrawString(m.Name, extractFont, extractShadow, nx + 1, ny + 1);
                g.DrawString(m.Name, extractFont, extractBrush, nx, ny);
            }

            // 危险区：图标下方红色醒目名称（即死机制）
            if (m.Type == MarkerType.Hazard)
            {
                var nameSize = g.MeasureString(m.Name, extractFont);
                var nx = sx - nameSize.Width / 2;
                var ny = sy + half + 1;
                using var hazardBrush = new SolidBrush(Color.FromArgb(255, 82, 82));
                g.DrawString(m.Name, extractFont, extractShadow, nx + 1, ny + 1);
                g.DrawString(m.Name, extractFont, hazardBrush, nx, ny);
            }

            // Boss：图标外圈红环 + 图标下方红色名称（高威胁目标）
            if (m.Type == MarkerType.Boss)
            {
                using var ringPen = new Pen(Color.FromArgb(255, 82, 82), 2f);
                g.DrawEllipse(ringPen, sx - half - 2, sy - half - 2, size + 4, size + 4);

                var nameSize = g.MeasureString(m.Name, extractFont);
                var nx = sx - nameSize.Width / 2;
                var ny = sy + half + 3;
                using var bossBrush = new SolidBrush(Color.FromArgb(255, 112, 112));
                g.DrawString(m.Name, extractFont, extractShadow, nx + 1, ny + 1);
                g.DrawString(m.Name, extractFont, bossBrush, nx, ny);
            }
        }
    }

    private void DrawSelection(Graphics g)
    {
        var map = Map;
        var bitmap = Bitmap;
        if (_selected is null || map is null || bitmap is null)
        {
            return;
        }

        var imagePt = MapCoordinateService.WorldToImage(
            map.Bounds, bitmap.Width, bitmap.Height, _selected.X, _selected.Z);
        var sx = (float)(imagePt.X * _zoom + _pan.X);
        var sy = (float)(imagePt.Y * _zoom + _pan.Y);

        using var pen = new Pen(Color.White, 2f);
        g.DrawEllipse(pen, sx - 15, sy - 15, 30, 30);

        var text = $"{_selected.Name}\n{MarkerTypeNames.Of(_selected.Type)}";
        using var font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
        var size = g.MeasureString(text, font);
        var box = new RectangleF(sx + 18, sy - size.Height / 2, size.Width + 10, size.Height + 6);
        using var bg = new SolidBrush(Color.FromArgb(220, 20, 20, 20));
        using var border = new Pen(Color.FromArgb(255, 200, 200, 200));
        using var fg = new SolidBrush(Color.White);
        g.FillRectangle(bg, box);
        g.DrawRectangle(border, box.X, box.Y, box.Width, box.Height);
        g.DrawString(text, font, fg, box.X + 5, box.Y + 3);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _lastMouse = e.Location;
            _mouseDownPos = e.Location;
            Cursor = Cursors.SizeAll;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging)
        {
            _pan.X += e.X - _lastMouse.X;
            _pan.Y += e.Y - _lastMouse.Y;
            _lastMouse = e.Location;
            Invalidate();
        }

        ReportCursorWorld(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = false;
        Cursor = Cursors.Default;

        // 移动距离小于阈值视为"点击"，做 Marker 命中测试
        var dx = e.X - _mouseDownPos.X;
        var dy = e.Y - _mouseDownPos.Y;
        if (Math.Sqrt(dx * dx + dy * dy) <= DragThresholdPx)
        {
            HandleClick(e.Location);
        }
    }

    private void HandleClick(Point screen)
    {
        var hit = HitTest(screen);
        _selected = hit;
        MarkerClicked?.Invoke(hit?.Name, hit is null ? null : MarkerTypeNames.Of(hit.Type));
        Invalidate();
    }

    private Marker? HitTest(Point screen)
    {
        var map = Map;
        var bitmap = Bitmap;
        if (map is null || bitmap is null || _state is null)
        {
            return null;
        }

        Marker? best = null;
        var bestDist = double.MaxValue;

        foreach (var m in map.Markers)
        {
            if (!_state.IsVisible(m) || m.Type == MarkerType.Label)
            {
                continue;
            }

            var imagePt = MapCoordinateService.WorldToImage(
                map.Bounds, bitmap.Width, bitmap.Height, m.X, m.Z);
            var sx = imagePt.X * _zoom + _pan.X;
            var sy = imagePt.Y * _zoom + _pan.Y;
            var dist = Math.Sqrt(
                (sx - screen.X) * (sx - screen.X) +
                (sy - screen.Y) * (sy - screen.Y));

            var radius = IconCache.SizeFor(m.Type) / 2.0 + 6.0;
            if (dist <= radius && (best is null || dist < bestDist))
            {
                bestDist = dist;
                best = m;
            }
        }
        return best;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var bitmap = Bitmap;
        if (bitmap is null)
        {
            return;
        }

        var factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
        var newZoom = Math.Clamp(_zoom * factor, _minZoom, MaxZoom);
        if (Math.Abs(newZoom - _zoom) < 1e-9)
        {
            return;
        }

        // 缩放中心 = 鼠标位置：缩放前后，鼠标指向的图片像素保持在同一屏幕位置
        var imageX = (e.X - _pan.X) / _zoom;
        var imageY = (e.Y - _pan.Y) / _zoom;
        _zoom = newZoom;
        _pan.X = (float)(e.X - imageX * _zoom);
        _pan.Y = (float)(e.Y - imageY * _zoom);

        ZoomChanged?.Invoke(_zoom);
        ReportCursorWorld(e.Location);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        var bitmap = Bitmap;
        if (bitmap is not null && !_dragging)
        {
            if (ClientSize.Width > 0 && ClientSize.Height > 0)
            {
                var zx = (double)ClientSize.Width / bitmap.Width;
                var zy = (double)ClientSize.Height / bitmap.Height;
                _minZoom = Math.Min(zx, zy) * 0.5;
                _zoom = Math.Max(_zoom, _minZoom);
            }
            Invalidate();
        }
    }

    private void ReportCursorWorld(Point screen)
    {
        var map = Map;
        var bitmap = Bitmap;
        if (bitmap is null || map is null || CursorWorldChanged is null)
        {
            return;
        }

        var imageX = (screen.X - _pan.X) / _zoom;
        var imageY = (screen.Y - _pan.Y) / _zoom;
        var (x, z) = MapCoordinateService.ImageToWorld(
            map.Bounds, bitmap.Width, bitmap.Height, imageX, imageY);
        CursorWorldChanged(x, z);
    }

    // 底图 Bitmap 归 MapViewState 所有，画布不 Dispose（模块文档：共享资源，View 只引用）。
}
