namespace MapPackBuilder.Gui;

internal static class GuiProgram
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new BuilderForm(BuilderWorkspace.Discover(AppContext.BaseDirectory)));
    }
}
