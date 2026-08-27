using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using MapPackBuilder.Validation;

namespace MapPackBuilder.Gui;

internal sealed class BuilderForm : Form
{
    private const string CurrentBaselineFileName = "baseline-2026.08.26.1-pve.json";
    private static readonly Regex VersionPattern = new(
        "^\\d{4}\\.\\d{2}\\.\\d{2}\\.[1-9]\\d*-pve$",
        RegexOptions.CultureInvariant);

    private readonly TextBox dataDirectory = new();
    private readonly TextBox workDirectory = new();
    private readonly TextBox dataVersion = new();
    private readonly TextBox approvalFile = new();
    private readonly Label currentVersion = new();
    private readonly Label derivedPaths = new();
    private readonly Label status = new();
    private readonly TextBox log = new();
    private readonly List<Button> operationButtons = [];
    private bool _operationRunning;

    public BuilderForm(BuilderWorkspace workspace)
    {
        Text = "TarkovMap MapData Builder";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 650);
        Size = new Size(1050, 760);
        Font = new Font("Microsoft YaHei UI", 9F);

        dataDirectory.Text = workspace.FormalDataDirectory;
        workDirectory.Text = workspace.WorkDirectory;
        dataVersion.Text = workspace.SuggestedDataVersion;

        Controls.Add(BuildLayout());
        dataDirectory.TextChanged += (_, _) => RefreshSummary();
        workDirectory.TextChanged += (_, _) => RefreshSummary();
        dataVersion.TextChanged += (_, _) => RefreshSummary();
        RefreshSummary();
    }

    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 5
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "TarkovMap MapData Builder",
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 17F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(BuildSettingsPanel(), 0, 1);
        layout.Controls.Add(BuildActionsPanel(), 0, 2);

        var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        currentVersion.AutoSize = true;
        currentVersion.Font = new Font(Font, FontStyle.Bold);
        derivedPaths.AutoEllipsis = true;
        derivedPaths.Dock = DockStyle.Fill;
        summary.Controls.Add(currentVersion, 0, 0);
        summary.Controls.Add(derivedPaths, 0, 1);
        layout.Controls.Add(summary, 0, 3);

        var outputPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        outputPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleLeft;
        log.Dock = DockStyle.Fill;
        log.Multiline = true;
        log.ReadOnly = true;
        log.ScrollBars = ScrollBars.Both;
        log.WordWrap = false;
        log.BackColor = Color.FromArgb(30, 32, 36);
        log.ForeColor = Color.Gainsboro;
        log.Font = new Font("Consolas", 9F);
        outputPanel.Controls.Add(status, 0, 0);
        outputPanel.Controls.Add(log, 0, 1);
        layout.Controls.Add(outputPanel, 0, 4);
        return layout;
    }

    private Control BuildSettingsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(0, 4, 0, 4)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        for (var row = 0; row < 4; row++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        }

        AddSettingRow(panel, 0, "正式 Data", dataDirectory, "选择…", BrowseDataDirectory);
        AddSettingRow(panel, 1, "工作目录", workDirectory, "选择…", BrowseWorkDirectory);
        AddSettingRow(panel, 2, "新数据版本", dataVersion, null, null);
        AddSettingRow(panel, 3, "验收文件", approvalFile, "选择…", BrowseApprovalFile);
        return panel;
    }

    private Control BuildActionsPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2 };
        for (var column = 0; column < 5; column++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        }
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        AddAction(panel, "1. 获取数据", 0, 0, FetchAsync);
        AddAction(panel, "2. 构建 MapData", 1, 0, BuildAsync);
        AddAction(panel, "3. 查看变化", 2, 0, ViewChangesAsync);
        AddAction(panel, "4. 重新校验", 3, 0, ValidateAsync);
        AddAction(panel, "5. 确认验收", 4, 0, ApproveAsync);
        AddAction(panel, "6. 导出 ZIP", 0, 1, PackageAsync);
        AddAction(panel, "7. 应用到正式程序", 1, 1, ApplyAsync);
        AddAction(panel, "8. 恢复上一版本", 2, 1, RestoreAsync);
        AddAction(panel, "打开构建报告", 3, 1, OpenBuildReportAsync);
        AddAction(panel, "打开工作目录", 4, 1, OpenWorkDirectoryAsync);
        return panel;
    }

    private static void AddSettingRow(TableLayoutPanel panel, int row, string labelText,
        TextBox textBox, string? buttonText, EventHandler? click)
    {
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(3, 7, 3, 7);
        panel.Controls.Add(label, 0, row);
        panel.Controls.Add(textBox, 1, row);
        if (buttonText is not null && click is not null)
        {
            var button = new Button { Text = buttonText, Dock = DockStyle.Fill, Margin = new Padding(4, 5, 0, 5) };
            button.Click += click;
            panel.Controls.Add(button, 2, row);
        }
    }

    private void AddAction(TableLayoutPanel panel, string text, int column, int row,
        Func<Task> action)
    {
        var button = new Button { Text = text, Dock = DockStyle.Fill, Margin = new Padding(4) };
        button.Click += async (_, _) =>
        {
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                SetBusy(false, "状态：操作失败");
                ShowError(exception.Message);
            }
        };
        operationButtons.Add(button);
        panel.Controls.Add(button, column, row);
    }

    private string PackRoot => Path.Combine(FullWorkDirectory, $"MapData-{Version}-test");
    private string FetchRoot => Path.Combine(FullWorkDirectory, $"MapData-{Version}-sources");
    private string PackageDirectory => Path.Combine(FullWorkDirectory, "packages");
    private string PackageFile => Path.Combine(PackageDirectory, $"MapData-{Version}.zip");
    private string FullDataDirectory => Path.GetFullPath(dataDirectory.Text.Trim());
    private string FullWorkDirectory => Path.GetFullPath(workDirectory.Text.Trim());
    private string Version => dataVersion.Text.Trim();
    private string BaselineFile => Path.Combine(AppContext.BaseDirectory, CurrentBaselineFileName);

    private async Task FetchAsync()
    {
        if (!ValidateInputs(requireData: false) || !RequireNewDirectory(FetchRoot, "数据快照目录")) return;
        await RunCommandAsync("获取上游数据", ["pve-fetch", FetchRoot, Version]);
    }

    private async Task BuildAsync()
    {
        if (!ValidateInputs(requireData: true) || !RequireNewDirectory(PackRoot, "测试包目录")) return;
        var args = new List<string> { "pve-build", PackRoot, Version, FullDataDirectory };
        if (File.Exists(approvalFile.Text.Trim())) args.Add(Path.GetFullPath(approvalFile.Text.Trim()));
        await RunCommandAsync("构建 MapData", args.ToArray());
    }

    private Task ViewChangesAsync()
    {
        OpenFile(Path.Combine(PackRoot, "validation-report.md"), "请先构建并校验测试包。");
        return Task.CompletedTask;
    }

    private async Task ValidateAsync()
    {
        if (!Directory.Exists(PackRoot))
        {
            ShowError("请先构建测试包。");
            return;
        }

        var args = new List<string> { "pve-validate", PackRoot };
        if (File.Exists(approvalFile.Text.Trim()))
        {
            args.Add(BaselineFile);
            args.Add(Path.GetFullPath(approvalFile.Text.Trim()));
        }
        await RunCommandAsync("校验测试包", args.ToArray());
    }

    private async Task ApproveAsync()
    {
        var reportFile = Path.Combine(PackRoot, "validation-report.json");
        if (!File.Exists(reportFile))
        {
            ShowError("请先构建并查看 Validation 报告。");
            return;
        }

        var diffCount = CountThresholdDiffs(reportFile);
        var message = $"请确认你已在独立客户端检查海关、中心区、街区和实验室。\n\n" +
                      $"本次将精确批准 {diffCount} 个超过 30% 的数量变化；任何其他数据错误都不会被绕过。\n\n" +
                      "是否确认本次人工验收通过？";
        if (MessageBox.Show(this, message, "确认人工验收", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var output = Path.Combine(FullWorkDirectory, "approvals",
                $"validation-approvals-{Version}.json");
            var result = ValidationApprovalWriter.WriteFromReport(PackRoot, output,
                "项目所有者通过 Builder GUI 确认代表地图和本次数量变化没有明显问题。",
                DateTimeOffset.Now);
            approvalFile.Text = result.OutputFile;
            AppendLog($"已生成验收文件：{result.OutputFile}");
            await ValidateAsync();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private async Task PackageAsync()
    {
        if (!Directory.Exists(PackRoot)) { ShowError("请先构建测试包。"); return; }
        if (!File.Exists(approvalFile.Text.Trim())) { ShowError("请先点击“确认验收”或选择有效验收文件。"); return; }
        await RunCommandAsync("导出正式 ZIP",
        [
            "pve-package", PackRoot, Path.GetFullPath(approvalFile.Text.Trim()), PackageDirectory
        ]);
    }

    private async Task ApplyAsync()
    {
        if (!ValidateInputs(requireData: true)) return;
        if (!File.Exists(PackageFile)) { ShowError("找不到正式 ZIP，请先导出 ZIP。"); return; }
        if (MessageBox.Show(this,
                $"请先关闭正在运行的 TarkovMap 客户端。\n\n将校验并应用 {Version}。\n当前 Data 会进入唯一备份槽，更早的备份会被替换。\n\n是否继续？",
                "应用正式 MapData", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        await RunCommandAsync("应用正式 MapData", ["pve-apply", PackageFile, FullDataDirectory], RefreshSummary);
    }

    private async Task RestoreAsync()
    {
        if (!ValidateInputs(requireData: true)) return;
        var backup = Path.Combine(Path.GetDirectoryName(FullDataDirectory)!, "Data.backup");
        if (!Directory.Exists(backup)) { ShowError("没有可恢复的 Data.backup。"); return; }
        if (MessageBox.Show(this,
                "请先关闭正在运行的 TarkovMap 客户端。\n\n将恢复上一个已验证版本，当前版本会被替换，恢复完成后备份槽会清空。\n\n是否继续？",
                "恢复上一版本", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        await RunCommandAsync("恢复上一版本", ["pve-restore", FullDataDirectory], RefreshSummary);
    }

    private Task OpenBuildReportAsync()
    {
        OpenFile(Path.Combine(PackRoot, "build-report.json"), "请先构建测试包。");
        return Task.CompletedTask;
    }

    private Task OpenWorkDirectoryAsync()
    {
        Directory.CreateDirectory(FullWorkDirectory);
        OpenFile(FullWorkDirectory, "工作目录不存在。");
        return Task.CompletedTask;
    }

    private async Task RunCommandAsync(string title, string[] args, Action? onSuccess = null)
    {
        _operationRunning = true;
        SetBusy(true, $"状态：正在{title}……");
        AppendLog("");
        AppendLog($"> {title}");
        try
        {
            var executable = Path.Combine(AppContext.BaseDirectory, "MapPackBuilder.exe");
            if (!File.Exists(executable))
            {
                throw new FileNotFoundException("GUI 旁缺少 MapPackBuilder.exe。", executable);
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = AppContext.BaseDirectory
                },
                EnableRaisingEvents = true
            };
            foreach (var argument in args)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null) AppendLog(eventArgs.Data);
            };
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null) AppendLog(eventArgs.Data);
            };
            if (!process.Start())
            {
                throw new InvalidOperationException("无法启动 MapPackBuilder CLI。");
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            var exitCode = process.ExitCode;
            if (!IsDisposed && !Disposing && exitCode == 0)
            {
                status.Text = $"状态：{title}完成";
                onSuccess?.Invoke();
            }
            else if (!IsDisposed && !Disposing)
            {
                status.Text = $"状态：{title}未通过（代码 {exitCode}）";
                MessageBox.Show(this, "操作未通过，请查看下方日志。", title,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                status.Text = $"状态：{title}失败";
                ShowError(exception.Message);
            }
        }
        finally
        {
            _operationRunning = false;
            if (!IsDisposed && !Disposing)
            {
                SetBusy(false, status.Text);
            }
        }
    }

    private void AppendLog(string line)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => AppendLog(line));
            }
            catch (InvalidOperationException)
            {
                // 窗口已关闭时忽略晚到的子进程输出。
            }
            return;
        }

        log.AppendText(line + Environment.NewLine);
    }

    private void SetBusy(bool busy, string statusText)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        status.Text = statusText;
        foreach (var button in operationButtons) button.Enabled = !busy;
        UseWaitCursor = busy;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_operationRunning && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            MessageBox.Show(this, "当前操作仍在进行。请等待完成后再关闭窗口，避免丢失执行结果。",
                "MapData Builder", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        base.OnFormClosing(e);
    }

    private bool ValidateInputs(bool requireData)
    {
        if (!VersionPattern.IsMatch(Version))
        {
            ShowError("数据版本格式应为 YYYY.MM.DD.N-pve，例如 2026.08.25.6-pve。");
            return false;
        }
        if (string.IsNullOrWhiteSpace(workDirectory.Text))
        {
            ShowError("请选择工作目录。");
            return false;
        }
        try
        {
            _ = FullWorkDirectory;
            if (requireData && !File.Exists(Path.Combine(FullDataDirectory, "maps.json")))
            {
                ShowError("正式 Data 目录无效，请选择包含 maps.json 的 Data 文件夹。");
                return false;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            ShowError($"路径无效：{exception.Message}");
            return false;
        }
        return true;
    }

    private bool RequireNewDirectory(string path, string label)
    {
        if (!Directory.Exists(path) && !File.Exists(path)) return true;
        ShowError($"{label}已经存在。为保护旧结果，请修改数据版本或工作目录，不会自动删除：\n{path}");
        return false;
    }

    private void RefreshSummary()
    {
        try
        {
            var version = string.IsNullOrWhiteSpace(dataDirectory.Text)
                ? null
                : BuilderWorkspace.ReadDataVersion(Path.GetFullPath(dataDirectory.Text.Trim()));
            currentVersion.Text = $"当前正式数据：{version ?? "未识别"}";
            derivedPaths.Text = $"测试包：{PackRoot}    ZIP：{PackageFile}";
        }
        catch (Exception)
        {
            currentVersion.Text = "当前正式数据：未识别";
            derivedPaths.Text = "请填写有效路径。";
        }
        status.Text = status.Text.Length == 0 ? "状态：就绪" : status.Text;
    }

    private void BrowseDataDirectory(object? sender, EventArgs e) => BrowseFolder(dataDirectory);
    private void BrowseWorkDirectory(object? sender, EventArgs e) => BrowseFolder(workDirectory);

    private void BrowseFolder(TextBox target)
    {
        using var dialog = new FolderBrowserDialog
        {
            InitialDirectory = Directory.Exists(target.Text) ? target.Text : "",
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
    }

    private void BrowseApprovalFile(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Validation 验收文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) approvalFile.Text = dialog.FileName;
    }

    private static int CountThresholdDiffs(string reportFile)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(reportFile));
        return document.RootElement.GetProperty("markerCountDiffs").EnumerateArray()
            .Count(node => node.GetProperty("thresholdExceeded").GetBoolean());
    }

    private void OpenFile(string path, string missingMessage)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            ShowError(missingMessage);
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void ShowError(string message) => MessageBox.Show(this, message, "MapData Builder",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
}
