namespace TarkovMap.Infrastructure;

/// <summary>
/// 本地错误日志：Logs/errors.log，仅发生错误时创建/写入。
/// 不记录截图内容、账号、玩家位置历史。日志失败不得导致二次崩溃。
/// </summary>
public static class ErrorLogger
{
    private static string? _logFile;

    public static void Init(string baseDirectory)
    {
        _logFile = Path.Combine(baseDirectory, "Logs", "errors.log");
    }

    public static void Log(string module, Exception ex, string? mapId = null)
    {
        try
        {
            if (_logFile is null)
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(_logFile)!);
            File.AppendAllText(_logFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{module}]" +
                (mapId is null ? "" : $" [map:{mapId}]") +
                $" {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
        }
        catch
        {
            // 日志失败不允许导致程序崩溃
        }
    }
}
