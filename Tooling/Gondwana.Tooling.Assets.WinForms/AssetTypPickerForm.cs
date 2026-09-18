using Gondwana.Assets;

namespace Gondwana.Tooling.Assets.WinForms;

internal sealed class AssetTypePickerForm : Form
{
    private readonly ComboBox _comboBox;

    public AssetTypes SelectedType => (AssetTypes)_comboBox.SelectedItem!;

    public AssetTypePickerForm(IEnumerable<string> filePaths)
    {
        var fileNames = filePaths
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        Text = "Select Asset Type";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(440, 0);
        Padding = new Padding(12);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));

        var label = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(400, 0),
            Text = fileNames.Length == 1
                ? "Choose the asset type for this file:"
                : $"Choose one asset type for all {fileNames.Length} selected files:",
            Margin = new Padding(0, 0, 0, 6)
        };

        var files = new ListBox
        {
            Dock = DockStyle.Fill,
            Height = fileNames.Length <= 1 ? 32 : 88,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        files.Items.AddRange(fileNames);

        _comboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 0, 12)
        };

        foreach (var value in Enum.GetValues<AssetTypes>())
            _comboBox.Items.Add(value);
        _comboBox.SelectedIndex = 0;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(6, 0, 0, 0)
        };

        var okButton = new Button
        {
            Text = "OK",
            AutoSize = true,
            DialogResult = DialogResult.OK,
            Margin = Padding.Empty
        };

        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);

        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(files, 0, 1);
        layout.Controls.Add(_comboBox, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        DarkTheme.Apply(this);
    }
}
