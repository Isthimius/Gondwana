namespace Gondwana.Tooling.Studio.WinForms.Services;

/// <summary>
/// Simple text-input dialog.
/// </summary>
internal static class InputDialog
{
    /// <summary>Shows a modal text-entry dialog.</summary>
    /// <param name="message">Prompt displayed above the text box.</param>
    /// <param name="title">Dialog window caption.</param>
    /// <param name="defaultValue">Initial text, or <see langword="null"/> for an empty text box.</param>
    /// <param name="owner">The owner window, or <see langword="null"/> to use the default owner.</param>
    /// <param name="password">Whether to mask the entered text with the system password character.</param>
    /// <returns>The untrimmed text, including an empty string, on OK; otherwise <see langword="null"/>.</returns>
    public static string? Show(string message, string title, string? defaultValue = null, IWin32Window? owner = null, bool password = false)
    {
        using var form = new Form
        {
            Icon = Gondwana.Tooling.WinForms.ToolingBranding.CreateIcon(),
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
            UseSystemPasswordChar = password,
            Text = defaultValue ?? string.Empty,
            Width = 390,
            Location = new System.Drawing.Point(16, 58)
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new System.Drawing.Point(240, 100), Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new System.Drawing.Point(330, 100), Width = 80 };
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        form.Controls.AddRange([label, textBox, ok, cancel]);

        ToolingAppearance.Apply(form);
        return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text : null;
    }
}
