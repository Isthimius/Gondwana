namespace Gondwana.Tooling.Studio.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        ToolingAppearance.Initialize();
        Application.Run(new MainForm());
    }
}
