namespace Gondwana.Studio.WinForms.Services;

/// <summary>
/// Simple text-input dialog.
/// </summary>
internal static class InputDialog
{
    /// <summary>
    /// Shows a modal text-input dialog and returns the entered text, or
    /// <see langword="null"/> if the user cancelled.
    /// </summary>
    public static string? Show(string message, string title, string? defaultValue = null, IWin32Window? owner = null)
    {
        using var form = new Form
        {
            Text = title,
            Width = 430,
            Height = 175,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label
        {
            Text = message,
            AutoSize = false,
            Width = 390,
            Height = 40,
            Location = new System.Drawing.Point(16, 12)
        };

        var textBox = new TextBox
        {
            Text = defaultValue ?? string.Empty,
            Width = 390,
            Location = new System.Drawing.Point(16, 58)
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new System.Drawing.Point(240, 100), Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new System.Drawing.Point(330, 100), Width = 80 };
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        form.Controls.AddRange([label, textBox, ok, cancel]);

        return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text : null;
    }
}
