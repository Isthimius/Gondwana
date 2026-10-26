namespace Gondwana.Tooling.WinForms.Theming;

/// <summary>Tooling-owned modal messages use the same appearance and standard WinForms dialog results.</summary>
public static class ThemedMessageBox
{
    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        using var dialog = Create(text, caption, buttons, icon, defaultButton);
        return dialog.ShowDialog(owner);
    }

    public static DialogResult Show(string text, string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1) =>
        Show(Form.ActiveForm, text, caption, buttons, icon, defaultButton);

    /// <summary>Builds a message dialog without showing it, allowing hosts to inspect accessibility and button semantics.</summary>
    public static Form Create(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        var results = buttons switch
        {
            MessageBoxButtons.OKCancel => new[] { DialogResult.OK, DialogResult.Cancel },
            MessageBoxButtons.YesNo => [DialogResult.Yes, DialogResult.No],
            MessageBoxButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
            MessageBoxButtons.RetryCancel => [DialogResult.Retry, DialogResult.Cancel],
            MessageBoxButtons.AbortRetryIgnore => [DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore],
            MessageBoxButtons.CancelTryContinue => [DialogResult.Cancel, DialogResult.TryAgain, DialogResult.Continue],
            _ => [DialogResult.OK]
        };

        var form = new Form
        {
            Text = caption,
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false,
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            AutoScaleMode = AutoScaleMode.Font,
            ClientSize = new Size(540, 190),
            ControlBox = results.Contains(DialogResult.Cancel) || buttons == MessageBoxButtons.OK
        };

        var message = new TextBox
        {
            Text = text,
            ReadOnly = true,
            Multiline = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            AccessibleName = caption,
            TabIndex = 0
        };

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18) };
        body.Controls.Add(message);
        Icon? symbol = icon switch
        {
            MessageBoxIcon.Error => SystemIcons.Error,
            MessageBoxIcon.Warning => SystemIcons.Warning,
            MessageBoxIcon.Question => SystemIcons.Question,
            MessageBoxIcon.Information => SystemIcons.Information,
            _ => null
        };

        if (symbol is not null)
        {
            var picture = new PictureBox
            {
                Image = symbol.ToBitmap(),
                Dock = DockStyle.Left,
                Width = 48,
                SizeMode = PictureBoxSizeMode.Normal
            };

            picture.Disposed += (_, _) => picture.Image?.Dispose();
            body.Controls.Add(picture);
        }

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var choices = results.Select(result => new Button
        {
            Text = result == DialogResult.TryAgain ? "Try again" : result.ToString(),
            DialogResult = result,
            AutoSize = true,
            MinimumSize = new Size(85, 28)
        }).ToArray();

        foreach (var button in choices.Reverse())
            bar.Controls.Add(button);

        form.Controls.Add(body);
        form.Controls.Add(bar);

        var selected = choices[Math.Clamp((int)defaultButton / 256, 0, choices.Length - 1)];
        form.AcceptButton = selected;
        form.CancelButton = choices.FirstOrDefault(button => button.DialogResult == DialogResult.Cancel)
            ?? (buttons == MessageBoxButtons.OK ? choices[0] : null);
        form.Shown += (_, _) => selected.Focus();

        ToolingAppearance.Apply(form);

        return form;
    }
}
