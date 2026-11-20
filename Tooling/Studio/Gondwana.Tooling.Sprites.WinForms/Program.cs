using Gondwana.Tooling.Studio.Shared.WinForms.Theming;
namespace Gondwana.Tooling.Studio.Sprites.WinForms;

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
