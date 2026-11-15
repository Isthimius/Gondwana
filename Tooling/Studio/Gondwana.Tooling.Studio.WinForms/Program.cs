namespace Gondwana.Tooling.Studio.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        ToolingAppearance.Initialize();
        Application.Run(new MainForm());
    }
}
