namespace Gondwana.Tooling.Tilesheets.WinForms;

internal static class PasswordPrompt
{
    public static string? Show(
        IWin32Window owner,
        string assetsFilePath)
    {
        using var dialog = new Form
        {
            Text = "Asset package password",
            Size = new Size(480, 160),
            MinimumSize = new Size(400, 160),
            MaximumSize = new Size(900, 160),
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false
        };

        var label = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = $"Enter the password for {Path.GetFileName(assetsFilePath)}:",
            Padding = new Padding(8),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var input = new TextBox
        {
            Dock = DockStyle.Top,
            UseSystemPasswordChar = true,
            Margin = new Padding(8)
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6)
        };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true
        };

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };

        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        dialog.Controls.Add(input);
        dialog.Controls.Add(label);
        dialog.Controls.Add(buttons);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        DarkTheme.Apply(dialog);

        return dialog.ShowDialog(owner) == DialogResult.OK
            ? input.Text
            : null;
    }
}
