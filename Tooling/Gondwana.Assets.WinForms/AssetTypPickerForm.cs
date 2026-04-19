using Gondwana.Assets;

namespace Gondwana.Assets.WinForms;

internal sealed class AssetTypePickerForm : Form
{
    private readonly ComboBox _comboBox;

    public AssetTypes SelectedType => (AssetTypes)_comboBox.SelectedItem!;

    public AssetTypePickerForm()
    {
        Text = "Select Asset Type";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Width = 320;
        Height = 140;

        var label = new Label
        {
            Left = 12,
            Top = 16,
            Width = 280,
            Text = "Choose the asset type for the imported file(s):"
        };

        _comboBox = new ComboBox
        {
            Left = 12,
            Top = 42,
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        foreach (var value in Enum.GetValues<AssetTypes>())
            _comboBox.Items.Add(value);
        _comboBox.SelectedIndex = 0;

        var okButton = new Button
        {
            Text = "OK",
            Left = 136,
            Top = 76,
            Width = 75,
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            Left = 217,
            Top = 76,
            Width = 75,
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(label);
        Controls.Add(_comboBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
