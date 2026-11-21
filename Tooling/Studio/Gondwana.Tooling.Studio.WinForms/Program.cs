using Gondwana.Tooling.Studio.Shared.WinForms;
using Gondwana.Tooling.Studio.Shared.WinForms.Theming;
namespace Gondwana.Tooling.Studio.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Keep the existing per-user settings directory across the assembly rename.
        // This preserves docking, workspace sessions, and disabled-plugin choices.
        if (AppContext.GetData(DockLayoutStore.ApplicationSetting) is null)
            AppContext.SetData(DockLayoutStore.ApplicationSetting, "Gondwana.Studio.WinForms");
        ApplicationConfiguration.Initialize();
        ToolingAppearance.Initialize();
        Application.Run(new MainForm());
    }
}
