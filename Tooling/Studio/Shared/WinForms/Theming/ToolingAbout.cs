namespace Gondwana.Tooling.Studio.Shared.WinForms.Theming;

/// <summary>Provides a consistent Help menu and attribution dialog for the authoring tools.</summary>
public static class ToolingAbout
{
    /// <summary>Creates the Help menu for a Studio or standalone editor window.</summary>
    public static ToolStripMenuItem CreateHelpMenu(Form owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var help = new ToolStripMenuItem("&Help");
        var about = new ToolStripMenuItem("&About…") { Name = "About" };
        about.Click += (_, _) =>
        {
            using var dialog = CreateDialog(owner.Text);
            dialog.ShowDialog(owner);
        };

        help.DropDownItems.Add(about);
        return help;
    }

    internal static Form CreateDialog(string applicationName) =>
        ThemedMessageBox.Create(
            applicationName + Environment.NewLine +
            "Gondwana source code: MIT License" + Environment.NewLine + Environment.NewLine +
            "Icons8 artwork: https://icons8.com/" + Environment.NewLine +
            "KDE Oxygen icon artwork: © KDE and the Oxygen contributors; LGPL v3 or later." +
            Environment.NewLine + Environment.NewLine +
            "See THIRD-PARTY-NOTICES.md and assets/icons/oxygen/ for credits and licenses.",
            "About " + applicationName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
}
