namespace Gondwana.Tooling.Audio.WinForms;

internal sealed class UriPrompt : Form
{
    private readonly TextBox _key = new() { Dock = DockStyle.Top };
    private readonly TextBox _uri = new() { Dock = DockStyle.Top };

    public string ResourceKey => _key.Text.Trim();
    public string SourceUri => _uri.Text.Trim();

    public UriPrompt()
    {
        Text = "Add URI audio";
        Width = 520;
        Height = 190;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var ok = new Button { Text = "Add", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 90 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 90 };
        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        Controls.Add(buttons);
        Controls.Add(_uri);
        Controls.Add(new Label { Text = "URI", Dock = DockStyle.Top, Height = 24 });
        Controls.Add(_key);
        Controls.Add(new Label { Text = "Resource key", Dock = DockStyle.Top, Height = 24 });

        AcceptButton = ok;
        CancelButton = cancel;
        DarkTheme.Apply(this);
    }
}
