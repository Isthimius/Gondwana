using Gondwana.Tooling.Tilesheets.Sources;

namespace Gondwana.Tooling.Tilesheets.WinForms;

internal static class PackedImagePicker
{
    public static PackedImageSource? Pick(
        IWin32Window owner,
        AssetPackageCatalog catalog,
        string initialDirectory)
    {
        using var open = new OpenFileDialog
        {
            Title = "Choose Gondwana asset package",
            Filter = "Gondwana asset packages|*.gaf;*.zip|All files|*.*",
            InitialDirectory = initialDirectory
        };

        if (open.ShowDialog(owner) != DialogResult.OK)
            return null;

        IReadOnlyList<PackedImageSource> images;

        try
        {
            images = catalog.GetImages(open.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                owner,
                ex.Message,
                "GAF image source",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return null;
        }

        if (images.Count == 0)
        {
            MessageBox.Show(
                owner,
                "The selected package does not contain any Image assets.",
                "GAF image source",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return null;
        }

        using var dialog = new Form
        {
            Text = "Choose GAF image",
            Size = new Size(620, 450),
            MinimumSize = new Size(420, 300),
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false
        };

        var list = new ListBox
        {
            Dock = DockStyle.Fill
        };

        list.Items.AddRange(images.Cast<object>().ToArray());
        list.SelectedIndex = 0;

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

        dialog.Controls.Add(list);
        dialog.Controls.Add(buttons);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        DarkTheme.Apply(dialog);

        list.DoubleClick += (_, _) =>
        {
            if (list.SelectedItem is not null)
            {
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
        };

        return dialog.ShowDialog(owner) == DialogResult.OK
            ? list.SelectedItem as PackedImageSource
            : null;
    }
}
