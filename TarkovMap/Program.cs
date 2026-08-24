namespace TarkovMap;

static class Program
{
    /// <summary>
    ///  入口。单实例：已有实例在运行时新实例直接退出。
    /// </summary>
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Global\TarkovMap.SingleInstance", out var created);
        if (!created)
        {
            return; // 已有实例在运行，直接退出（两个窗口监听同一截图目录没有意义）
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
