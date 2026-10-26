namespace Gondwana.Tooling.Audio.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        ToolingAppearance.Initialize();
        Application.Run(new MainForm());
    }
}
