namespace Gondwana.Tooling.Assets.WinForms;

/// <summary>Displays the fully prepared import before the archive is modified.</summary>
internal sealed class AssetImportPreviewForm : Form
{
    /// <summary>Creates the final review dialog.</summary>
    /// <param name="plan">The validated plan.</param>
    internal AssetImportPreviewForm(AssetImportPlan plan)
    {
        Icon = Gondwana.Tooling.WinForms.ToolingBranding.CreateIcon();
        Text = "Import plan";
        Size = new Size(900, 500);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        var summary = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Text = $"Add {plan.AdditionCount} entries. Changes remain unsaved until Save.\r\n\r\n" + string.Join("\r\n", plan.Preview)
        };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var apply = new Button { Text = "Import", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.AddRange([cancel, apply]);
        Controls.Add(summary);
        Controls.Add(buttons);
        AcceptButton = apply;
        CancelButton = cancel;
        DarkTheme.Apply(this);
    }
}
