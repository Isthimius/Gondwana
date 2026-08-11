namespace Gondwana.Tooling.Assets.WinForms;

internal sealed class InputDialog : Form
{
    private readonly TextBox _textBox;

    public string Value => _textBox.Text.Trim();

    public InputDialog(string title, string prompt, string initialValue = "")
    {
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
    }

    public static string? Show(IWin32Window owner, string title, string prompt, string initialValue = "")
    {
        using var dialog = new InputDialog(title, prompt, initialValue);
        return dialog.ShowDialog(owner) == DialogResult.OK
            ? dialog.Value
            : null;
    }
}