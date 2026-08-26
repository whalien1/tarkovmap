using System.Reflection;
using TarkovMap.Controls;
using TarkovMap.Infrastructure;
using TarkovMap.Models;
using TarkovMap.Services;

namespace TarkovMap;

/// <summary>
/// 主窗口：只做 UI 协调（地图切换、Marker 开关、截图定位、置顶、菜单）。
/// 坐标数学、JSON、Regex、图标加载都在各自 Service / 控件里。
/// </summary>
public sealed class MainForm : Form
{
    private static readonly Size PreferredNormalClientSize = new(1800, 1000);
    private const int SidePanelWidth = 235;
    private const int SidePanelContentWidth = 215;

    private readonly MapCanvas _canvas;
    private readonly IconCache _icons;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _mapLabel;
    private readonly ToolStripStatusLabel _zoomLabel;
    private readonly ToolStripStatusLabel _coordLabel;
    private readonly ToolStripStatusLabel _infoLabel;
    private FormWindowState _lastWindowState = FormWindowState.Normal;

    private MapRepository? _repo;
    private IReadOnlyList<MapListEntry> _mapEntries = [];
    private ComboBox? _mapCombo;
    private ToolStripMenuItem? _topMostItem;
    private ToolStripMenuItem? _mapMenu;
    private bool _loading;

    private readonly ConfigService _config;
    private readonly ScreenshotWatcher _watcher = new();
    private readonly MapViewState _state = new();
    private Forms.MiniMapForm? _miniMap;
    private CheckBox? _miniMapToggle;
    private Label? _dirLabel;
    private Label? _locateStatusLabel;

    public MainForm()
    {
        Text = "TarkovMap";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = true;
        MinimizeBox = true;

        _config = new ConfigService(AppContext.BaseDirectory);
        _config.Load();
        ErrorLogger.Init(AppContext.BaseDirectory);
        _watcher.LocationFound += OnLocationFound;

        ClientSize = PreferredNormalClientSize;
        TopMost = _config.Config.TopMost;

        _mapLabel = new ToolStripStatusLabel { Text = "-" };
        _zoomLabel = new ToolStripStatusLabel { Text = "Zoom -" };
        _coordLabel = new ToolStripStatusLabel { Text = "X:- Z:-" };
        _infoLabel = new ToolStripStatusLabel { Text = "" };

        _statusStrip = new StatusStrip();
        _statusStrip.Items.Add(_mapLabel);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_zoomLabel);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_coordLabel);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_infoLabel);
        _statusStrip.Dock = DockStyle.Bottom;

        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        _icons = new IconCache(Path.Combine(dataDir, "icons"));

        _canvas = new MapCanvas { Dock = DockStyle.Fill };
        _canvas.SetIconCache(_icons);
        _canvas.SetViewState(_state);
        _canvas.ZoomChanged += z => _zoomLabel.Text = $"Zoom {z * 100:0}%";
        _canvas.CursorWorldChanged += (x, z) => _coordLabel.Text = $"X:{x:0.0} Z:{z:0.0}";
        _canvas.MarkerClicked += (name, typeName) =>
            _infoLabel.Text = name is null ? "" : $"{name} · {typeName}";

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            IsSplitterFixed = true,
            Panel1MinSize = SidePanelWidth
        };
        split.Panel1.Controls.Add(BuildSidePanel());
        split.Panel2.Controls.Add(_canvas);

        Controls.Add(split);
        Controls.Add(_statusStrip);
        Controls.Add(BuildMenuStrip());

        Load += OnFormLoad;
        Shown += OnFormShown;
        Resize += OnFormResize;
        FormClosing += OnFormClosing;
        FormClosed += (_, _) =>
        {
            _watcher.Dispose();
            _state.Dispose();
            _icons.Dispose();
        };
    }

    // ── 菜单栏 ─────────────────────────────────────────────

    private MenuStrip BuildMenuStrip()
    {
        var menu = new MenuStrip { Dock = DockStyle.Top };

        var fileMenu = new ToolStripMenuItem("文件(&F)");
        fileMenu.DropDownItems.Add("重新加载地图数据(&R)", null, (_, _) => ReloadCurrentMap());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("退出(&X)", null, (_, _) => Close());

        _mapMenu = new ToolStripMenuItem("地图(&M)");

        var viewMenu = new ToolStripMenuItem("视图(&V)");
        _topMostItem = new ToolStripMenuItem("窗口置顶(&T)")
        {
            Checked = _config.Config.TopMost,
            CheckOnClick = true
        };
        _topMostItem.CheckedChanged += (_, _) =>
        {
            TopMost = _topMostItem.Checked;
            if (!_loading)
            {
                _config.Config.TopMost = _topMostItem.Checked;
                _config.Save();
            }
        };
        viewMenu.DropDownItems.Add(_topMostItem);
        viewMenu.DropDownItems.Add("重置地图视图(&Z)", null, (_, _) => _canvas.FitToWindow());

        var helpMenu = new ToolStripMenuItem("帮助(&H)");
        helpMenu.DropDownItems.Add("关于(&A)", null, (_, _) =>
        {
            // 版本号单一来源：csproj 的 <Version>，此处从程序集读取，避免硬编码。
            var version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "unknown";
            MessageBox.Show(this,
                $"TarkovMap v{version}\n\n《逃离塔科夫》本地互动地图\n\n" +
                "· 纯本地运行，不联网\n" +
                "· 只读截图文件名，不碰游戏进程\n" +
                "· 地图数据来源：tarkov-dev 社区数据",
                "关于 TarkovMap", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });

        menu.Items.Add(fileMenu);
        menu.Items.Add(_mapMenu);
        menu.Items.Add(viewMenu);
        menu.Items.Add(helpMenu);
        return menu;
    }

    // ── 左侧功能区 ─────────────────────────────────────────

    private Control BuildSidePanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var mapGroup = new GroupBox
        {
            Text = "地图",
            Dock = DockStyle.Top,
            Height = 66
        };
        _mapCombo = new ComboBox
        {
            Left = 10,
            Top = 24,
            Width = SidePanelContentWidth,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _mapCombo.SelectedIndexChanged += (_, _) => OnMapSelected();
        mapGroup.Controls.Add(_mapCombo);

        var group = new GroupBox
        {
            Text = "地图标记",
            Dock = DockStyle.Top,
            Height = 300
        };

        // 默认开启：撤离点/出生点/Boss/危险/地图标注；其余低频类别默认关闭。
        var items = new (MarkerType Type, string Text, bool DefaultOn)[]
        {
            (MarkerType.ExtractPmc, "PMC 撤离点", true),
            (MarkerType.ExtractScav, "Scav 撤离点", true),
            (MarkerType.ExtractShared, "共用撤离点", true),
            (MarkerType.ExtractTransit, "转移点", true),
            (MarkerType.SpawnPmc, "PMC 出生点", true),
            (MarkerType.SpawnScav, "Scav 出生点", true),
            (MarkerType.Boss, "Boss", true),
            (MarkerType.LootContainer, "物资容器", false),
            (MarkerType.Lock, "门锁 / 钥匙", false),
            (MarkerType.Hazard, "危险区域", true),
            (MarkerType.StationaryWeapon, "固定武器", false),
            (MarkerType.Label, "地图标注", true),
        };

        _loading = true;
        for (var i = 0; i < items.Length; i++)
        {
            var (type, text, defaultOn) = items[i];
            // 有历史配置则用历史状态
            var visible = _config.Config.MarkerVisibility.TryGetValue(type.ToString(), out var saved)
                ? saved
                : defaultOn;

            var box = new CheckBox
            {
                Text = text,
                Checked = visible,
                AutoSize = true,
                Left = 10,
                Top = 22 + i * 22
            };
            var captured = type;
            box.CheckedChanged += (_, _) =>
            {
                _state.SetMarkerVisibility(captured, box.Checked);
                if (!_loading)
                {
                    _config.Config.MarkerVisibility[captured.ToString()] = box.Checked;
                    _config.Save();
                }
            };
            group.Controls.Add(box);
            _state.SetMarkerVisibility(type, visible);
        }
        _loading = false;

        // WinForms Dock 顺序：后加入的控件排在更靠上的位置
        panel.Controls.Add(BuildLocatePanel());
        panel.Controls.Add(group);
        panel.Controls.Add(BuildMiniMapPanel());
        panel.Controls.Add(mapGroup);
        return panel;
    }

    /// <summary>悬浮小地图功能区：高频的开关/大小/透明度常驻，形状收进更多设置。</summary>
    private Control BuildMiniMapPanel()
    {
        var group = new GroupBox
        {
            Text = "悬浮小地图",
            Dock = DockStyle.Top,
            Height = _config.Config.MiniMap.MoreSettingsExpanded ? 238 : 148
        };

        var settings = _config.Config.MiniMap;

        var toggle = new CheckBox
        {
            Text = "显示悬浮小地图",
            Left = 10,
            Top = 22,
            AutoSize = true,
            Checked = settings.Visible
        };
        _miniMapToggle = toggle;
        toggle.CheckedChanged += (_, _) =>
        {
            if (toggle.Checked)
            {
                ShowMiniMap();
            }
            else
            {
                _miniMap?.Hide();
            }
            if (!_loading)
            {
                settings.Visible = toggle.Checked;
                _config.Save();
            }
        };
        group.Controls.Add(toggle);

        // 上次退出时小地图是开着的：本次启动自动恢复显示
        if (settings.Visible)
        {
            ShowMiniMap();
        }

        BuildComboRow(group, 50, "大小",
            ("小", MiniMapSettings.SizeKind.Small), ("中", MiniMapSettings.SizeKind.Medium),
            settings.Size, v => { settings.Size = v; _miniMap?.ApplySettings(); },
            third: ("大", MiniMapSettings.SizeKind.Large));
        BuildComboRow(group, 78, "透明度",
            ("低（50%）", MiniMapSettings.OpacityKind.Low), ("中（75%）", MiniMapSettings.OpacityKind.Medium),
            settings.Opacity, v => { settings.Opacity = v; _miniMap?.ApplySettings(); },
            third: ("高（100%）", MiniMapSettings.OpacityKind.High));

        var shapeCaption = new Label
        {
            Text = "形状",
            Left = 8,
            Top = 142,
            AutoSize = true,
            Visible = settings.MoreSettingsExpanded
        };
        var shape = new ComboBox
        {
            Left = 62,
            Top = 138,
            Width = 110,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible = settings.MoreSettingsExpanded
        };
        shape.Items.AddRange(["方形", "圆形"]);
        shape.SelectedIndex = settings.Shape == MiniMapSettings.ShapeKind.Circle ? 1 : 0;
        shape.SelectedIndexChanged += (_, _) =>
        {
            settings.Shape = shape.SelectedIndex == 1
                ? MiniMapSettings.ShapeKind.Circle
                : MiniMapSettings.ShapeKind.Square;
            _miniMap?.ApplySettings();
            if (!_loading)
            {
                _config.Save();
            }
        };

        var more = new Button
        {
            Text = settings.MoreSettingsExpanded ? "收起更多设置" : "更多设置",
            Left = 10,
            Top = 108,
            Width = SidePanelContentWidth,
            Height = 26
        };
        var resetPosition = new Button
        {
            Text = "重置小地图位置",
            Left = 10,
            Top = 170,
            Width = SidePanelContentWidth,
            Height = 26,
            Visible = settings.MoreSettingsExpanded
        };
        resetPosition.Click += (_, _) =>
        {
            settings.X = -1;
            settings.Y = -1;
            _miniMap?.ResetPosition();
            _config.Save();
        };
        var resetZoom = new Button
        {
            Text = "重置小地图缩放",
            Left = 10,
            Top = 200,
            Width = SidePanelContentWidth,
            Height = 26,
            Visible = settings.MoreSettingsExpanded
        };
        resetZoom.Click += (_, _) =>
        {
            settings.Zoom = MiniMapSettings.DefaultZoom;
            _miniMap?.ResetZoom();
            _config.Save();
        };
        more.Click += (_, _) =>
        {
            var expanded = !shape.Visible;
            shape.Visible = expanded;
            shapeCaption.Visible = expanded;
            resetPosition.Visible = expanded;
            resetZoom.Visible = expanded;
            group.Height = expanded ? 238 : 148;
            more.Text = expanded ? "收起更多设置" : "更多设置";
            settings.MoreSettingsExpanded = expanded;
            if (!_loading)
            {
                _config.Save();
            }
        };
        group.Controls.Add(shapeCaption);
        group.Controls.Add(shape);
        group.Controls.Add(more);
        group.Controls.Add(resetPosition);
        group.Controls.Add(resetZoom);
        return group;
    }

    /// <summary>创建（如需要）并显示小地图；用户误关时同步取消勾选。</summary>
    private void ShowMiniMap()
    {
        if (_miniMap is null)
        {
            _miniMap = new Forms.MiniMapForm(_state, _icons, _config.Config.MiniMap);
            _miniMap.UserClosed += () =>
            {
                if (_miniMapToggle is not null)
                {
                    _miniMapToggle.Checked = false; // 触发 CheckedChanged → 隐藏 + 写配置
                }
            };
        }
        _miniMap.Show();
    }

    /// <summary>生成一行"标签 + 下拉框"，切换即时生效并写入配置。</summary>
    private void BuildComboRow<T>(GroupBox group, int top, string label,
        (string Text, T Value) first, (string Text, T Value) second,
        T current, Action<T> apply, (string Text, T Value)? third = null) where T : struct
    {
        var caption = new Label { Text = label, Left = 8, Top = top + 4, AutoSize = true };
        group.Controls.Add(caption);

        var options = third is null ? new[] { first, second } : new[] { first, second, third.Value };
        var combo = new ComboBox
        {
            Left = 62,
            Top = top,
            Width = 110,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        var selectedIndex = 0;
        for (var i = 0; i < options.Length; i++)
        {
            combo.Items.Add(options[i].Text);
            if (EqualityComparer<T>.Default.Equals(current, options[i].Value))
            {
                selectedIndex = i;
            }
        }
        combo.SelectedIndex = selectedIndex;
        combo.SelectedIndexChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0)
            {
                return;
            }
            apply(options[combo.SelectedIndex].Value);
            if (!_loading)
            {
                _config.Save();
            }
        };
        group.Controls.Add(combo);
    }

    /// <summary>玩家定位功能区：截图目录选择 + 状态显示。</summary>
    private Control BuildLocatePanel()
    {
        var group = new GroupBox
        {
            Text = "玩家定位",
            Dock = DockStyle.Top,
            Height = 130
        };

        _dirLabel = new Label
        {
            Left = 10,
            Top = 22,
            Width = SidePanelContentWidth,
            Height = 32,
            Text = "截图目录未配置",
            ForeColor = Color.Gray
        };

        var browse = new Button
        {
            Text = "选择目录...",
            Left = 10,
            Top = 58,
            Width = SidePanelContentWidth
        };
        browse.Click += (_, _) => OnBrowseScreenshotDirectory();

        _locateStatusLabel = new Label
        {
            Left = 10,
            Top = 92,
            Width = SidePanelContentWidth,
            Text = "状态：未配置",
            ForeColor = Color.Gray
        };

        group.Controls.Add(_dirLabel);
        group.Controls.Add(browse);
        group.Controls.Add(_locateStatusLabel);
        return group;
    }

    // ── 截图定位 ───────────────────────────────────────────

    private void OnBrowseScreenshotDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择塔科夫截图目录（游戏自带截图保存的文件夹）",
            UseDescriptionForTitle = true
        };
        if (!string.IsNullOrEmpty(_config.Config.ScreenshotDirectory))
        {
            dialog.InitialDirectory = _config.Config.ScreenshotDirectory;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _config.Config.ScreenshotDirectory = dialog.SelectedPath;
            _config.Save();
            StartWatching();
        }
    }

    private void StartWatching()
    {
        var dir = _config.Config.ScreenshotDirectory;
        if (string.IsNullOrEmpty(dir))
        {
            SetLocateStatus("状态：未配置", Color.Gray);
            return;
        }

        try
        {
            _watcher.Start(dir);
            SetLocateStatus("状态：等待新截图", Color.DarkGreen);
            _infoLabel.Text = "";
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("ScreenshotWatcher", ex);
            SetLocateStatus("状态：目录不存在，请重新选择", Color.DarkRed);
            _infoLabel.Text = "截图目录不存在";
        }
    }

    private void SetLocateStatus(string text, Color color)
    {
        if (_locateStatusLabel is not null)
        {
            _locateStatusLabel.Text = text;
            _locateStatusLabel.ForeColor = color;
        }
        if (_dirLabel is not null)
        {
            _dirLabel.Text = string.IsNullOrEmpty(_config.Config.ScreenshotDirectory)
                ? "截图目录未配置"
                : _config.Config.ScreenshotDirectory;
            _dirLabel.ForeColor = string.IsNullOrEmpty(_config.Config.ScreenshotDirectory)
                ? Color.Gray : Color.Black;
        }
    }

    /// <summary>截图事件（线程池线程）→ 切到 UI 线程处理。</summary>
    private void OnLocationFound(PlayerLocation location)
    {
        if (IsDisposed)
        {
            return;
        }
        BeginInvoke(() =>
        {
            var ok = _state.SetPlayerLocation(location);
            if (ok)
            {
                SetLocateStatus($"状态：已定位 X:{location.X:0.0} Z:{location.Z:0.0}", Color.DarkGreen);
                _infoLabel.Text = $"已定位（{location.FileName}）";
            }
            else
            {
                // 坐标与当前地图不匹配：不绘制、不切图、只提示
                SetLocateStatus("状态：位置与当前地图不匹配", Color.DarkOrange);
                _infoLabel.Text = "当前位置与当前地图不匹配";
            }
        });
    }

    // ── 地图加载 ───────────────────────────────────────────

    private void OnMapSelected()
    {
        if (_loading || _mapCombo is null || _mapCombo.SelectedIndex < 0 ||
            _mapCombo.SelectedIndex >= _mapEntries.Count)
        {
            return;
        }
        LoadMap(_mapEntries[_mapCombo.SelectedIndex]);
    }

    private void LoadMap(MapListEntry entry)
    {
        if (_repo is null)
        {
            return;
        }
        try
        {
            // 切换时 Dispose 旧 Bitmap（MapViewState.SetMap 内部处理），一次只持有一张大地图
            var map = _repo.LoadMapDefinition(entry.Directory);
            var bitmap = _repo.LoadMapImage(map);
            _state.SetMap(map, bitmap);
            _mapLabel.Text = map.Name;
            _infoLabel.Text = "";

            _config.Config.LastMapId = map.Id;
            if (!_loading)
            {
                _config.Save();
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("MapRepository", ex, entry.Id);
            _infoLabel.Text = $"地图 {entry.Name} 加载失败";
            MessageBox.Show(this, $"地图 {entry.Name} 加载失败。\n\n{ex.Message}",
                "TarkovMap", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ReloadCurrentMap()
    {
        if (_mapCombo is not null && _mapCombo.SelectedIndex >= 0 &&
            _mapCombo.SelectedIndex < _mapEntries.Count)
        {
            LoadMap(_mapEntries[_mapCombo.SelectedIndex]);
        }
    }

    // ── 窗口生命周期 ───────────────────────────────────────

    private void OnFormLoad(object? sender, EventArgs e)
    {
        // 控件已完成布局，此时设置分隔条位置不会被初始尺寸挤压
        var split = Controls.OfType<SplitContainer>().FirstOrDefault();
        if (split is not null && split.Width > 260)
        {
            split.SplitterDistance = SidePanelWidth;
        }

        try
        {
            var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
            _repo = new MapRepository(dataDir);
            _mapEntries = _repo.LoadMapList().Where(m => m.Enabled).ToList();
            if (_mapEntries.Count == 0)
            {
                throw new InvalidDataException("maps.json 中没有可用地图");
            }

            _loading = true;
            foreach (var entry in _mapEntries)
            {
                _mapCombo?.Items.Add(entry.Name);
            }

            // 地图菜单同步生成
            if (_mapMenu is not null)
            {
                for (var i = 0; i < _mapEntries.Count; i++)
                {
                    var index = i;
                    _mapMenu.DropDownItems.Add(_mapEntries[i].Name, null, (_, _) =>
                    {
                        if (_mapCombo is not null)
                        {
                            _mapCombo.SelectedIndex = index;
                        }
                    });
                }
            }
            _loading = false;

            // 初始地图：优先上次使用的地图，否则第一张
            var initialIndex = 0;
            for (var i = 0; i < _mapEntries.Count; i++)
            {
                if (_mapEntries[i].Id == _config.Config.LastMapId)
                {
                    initialIndex = i;
                    break;
                }
            }
            if (_mapCombo is not null)
            {
                _mapCombo.SelectedIndex = initialIndex;
            }
            LoadMap(_mapEntries[initialIndex]);

            // 恢复截图目录监听（未配置则状态提示，不影响看地图）
            StartWatching();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Startup", ex);
            MessageBox.Show(
                this,
                $"地图数据无法加载。\n请重新解压完整 TarkovMap 压缩包。\n\n{ex.Message}",
                "TarkovMap",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    private void OnFormShown(object? sender, EventArgs e)
    {
        // 固定普通窗口优先使用 1800×1000 ClientSize；小屏则自动缩到工作区内。
        // 仅首次显示时处理，最大化后由 WinForms 保留正确的 RestoreBounds。
        if (WindowState != FormWindowState.Normal)
        {
            return;
        }

        var screen = Screen.FromControl(this);
        var nonClientSize = new Size(Width - ClientSize.Width, Height - ClientSize.Height);
        ClientSize = WindowSizePolicy.FitClientSizeToWorkingArea(
            PreferredNormalClientSize, screen.WorkingArea.Size, nonClientSize);
        Location = new Point(
            screen.WorkingArea.Left + Math.Max(0, (screen.WorkingArea.Width - Width) / 2),
            screen.WorkingArea.Top + Math.Max(0, (screen.WorkingArea.Height - Height) / 2));
    }

    /// <summary>窗口最大化 / 还原时，地图重新适配视口；普通拖边框保持当前视图。</summary>
    private void OnFormResize(object? sender, EventArgs e)
    {
        if (WindowState != _lastWindowState)
        {
            _lastWindowState = WindowState;
            if (WindowState != FormWindowState.Minimized)
            {
                _canvas.FitToWindow();
            }
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // 关闭：停止监听 → 关闭小地图 → 保存配置 → 完全退出（无托盘、无后台）
        _watcher.Stop();
        if (_miniMap is not null)
        {
            _miniMap.AllowClose = true;
            _miniMap.Close();
        }

        _config.Save();
    }
}
