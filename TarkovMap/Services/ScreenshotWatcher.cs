using TarkovMap.Models;

namespace TarkovMap.Services;

/// <summary>
/// 截图目录监听：FileSystemWatcher 事件驱动（不轮询），
/// 带"同路径短时间窗口"防抖。只解析文件名，不读取图片内容。
/// </summary>
public sealed class ScreenshotWatcher : IDisposable
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(500);

    private FileSystemWatcher? _watcher;
    private readonly Dictionary<string, DateTime> _recent = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    /// <summary>解析出合法的玩家位置时触发（在线程池线程上，UI 使用需 Invoke）。</summary>
    public event Action<PlayerLocation>? LocationFound;

    public string? Directory { get; private set; }
    public bool IsWatching => _watcher?.EnableRaisingEvents == true;

    public void Start(string directory)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(directory) || !System.IO.Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"截图目录不存在：{directory}");
        }

        _watcher = new FileSystemWatcher(directory, "*.png")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        Directory = directory;
    }

    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileEvent;
            _watcher.Renamed -= OnFileEvent;
            _watcher.Dispose();
            _watcher = null;
        }
        Directory = null;
        lock (_gate)
        {
            _recent.Clear();
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (IsDuplicate(e.FullPath))
        {
            return;
        }

        if (ScreenshotLocationParser.TryParse(e.Name ?? "", out var location))
        {
            LocationFound?.Invoke(location);
        }
        // 不符合命名格式的文件（如结算画面截图）静默跳过
    }

    private bool IsDuplicate(string path)
    {
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (_recent.TryGetValue(path, out var last) && now - last < DebounceWindow)
            {
                return true;
            }
            _recent[path] = now;

            // 顺手清理过期条目，避免长期运行积累
            if (_recent.Count > 64)
            {
                var expired = _recent.Where(kv => now - kv.Value > TimeSpan.FromMinutes(5))
                    .Select(kv => kv.Key).ToList();
                foreach (var key in expired)
                {
                    _recent.Remove(key);
                }
            }
        }
        return false;
    }

    public void Dispose() => Stop();
}
