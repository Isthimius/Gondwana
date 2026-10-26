namespace Gondwana.Tooling.Assets.WinForms;

internal sealed class InputDialog : Form
{
    private readonly TextBox _textBox;

    /// <summary>Gets the entered text with leading and trailing whitespace removed.</summary>
    public string Value => _textBox.Text.Trim();

    /// <summary>Creates a text-entry dialog with OK and Cancel buttons.</summary>
    /// <param name="title">Dialog window title.</param>
    /// <param name="prompt">Label describing the requested text.</param>
    /// <param name="initialValue">Initial text shown in the input box.</param>
    public InputDialog(string title, string prompt, string initialValue = "")
    {
        Icon = Gondwana.Tooling.WinForms.ToolingBranding.CreateIcon();
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Width = 460;
        Height = 150;

        var promptLabel = new Label
        {
            Left = 12,
            Top = 12,
            Width = 420,
            Text = prompt
        };

        _textBox = new TextBox
        {
            Left = 12,
            Top = 36,
            Width = 420,
            Text = initialValue
        };

        var okButton = new Button
        {
            Text = "OK",
            Left = 276,
            Top = 70,
            Width = 75,
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            Left = 357,
            Top = 70,
            Width = 75,
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(promptLabel);
        Controls.Add(_textBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        ToolingAppearance.Apply(this);
    }

    /// <summary>Shows a modal text-entry dialog and disposes it after closing.</summary>
    /// <param name="owner">Window owning the dialog.</param>
    /// <param name="title">Dialog window title.</param>
    /// <param name="prompt">Label describing the requested text.</param>
    /// <param name="initialValue">Initial input text.</param>
    /// <param name="password">Whether the input is masked.</param>
    /// <returns>Trimmed input on OK, including an empty string; null on cancellation.</returns>
    public static string? Show(IWin32Window owner, string title, string prompt, string initialValue = "", bool password = false)
    {
        using var dialog = new InputDialog(title, prompt, initialValue);
        dialog._textBox.UseSystemPasswordChar = password;
        return dialog.ShowDialog(owner) == DialogResult.OK
            ? dialog.Value
            : null;
    }
}
