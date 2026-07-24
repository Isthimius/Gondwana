using Gondwana.Assets;
using Gondwana.Studio.Core.Services;

namespace Gondwana.Studio.WinForms.Services;

/// <summary>
/// WinForms implementation of <see cref="IDialogService"/>.
/// All methods run synchronously on the UI thread and wrap the result in a completed Task.
/// </summary>
public sealed class WinFormsDialogService : IDialogService
{
    private readonly IWin32Window? _owner;

    /// <summary>
    /// WinFormsDialogService.
    /// </summary>
    /// <param name="owner">Owner window for dialog anchoring.</param>
    public WinFormsDialogService(IWin32Window? owner = null)
    {
        _owner = owner;
    }

    /// <inheritdoc/>
    public Task<string?> OpenFileAsync(string title, string[] patterns)
    {
        var filter = BuildFilter(patterns);
        using var dialog = new OpenFileDialog { Title = title, Filter = filter, Multiselect = false };
        var result = dialog.ShowDialog(_owner) == DialogResult.OK ? dialog.FileName : null;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> OpenFilesAsync(string title)
    {
        using var dialog = new OpenFileDialog { Title = title, Filter = "All Files (*.*)|*.*", Multiselect = true };
        IReadOnlyList<string> result = dialog.ShowDialog(_owner) == DialogResult.OK
            ? dialog.FileNames
            : [];
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<string?> SaveFileAsync(string title, string suggestedName, string defaultExt, string[] patterns)
    {
        var filter = BuildFilter(patterns);
        using var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = suggestedName,
            DefaultExt = defaultExt,
            Filter = string.IsNullOrEmpty(filter) ? "All Files (*.*)|*.*" : filter
        };
        var result = dialog.ShowDialog(_owner) == DialogResult.OK ? dialog.FileName : null;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<bool> ConfirmAsync(string message, string title)
    {
        var result = MessageBox.Show(
            message, title,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task AlertAsync(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string?> PromptAsync(string message, string title, string? defaultValue = null)
    {
        var result = InputDialog.Show(message, title, defaultValue, _owner);
        return Task.FromResult(result);
    }


    /// <inheritdoc/>
    public Task<string?> PickAssetTypeAsync()
    {
        using var form = new Form
        {
            Text = "Select Asset Type",
            Width = 320,
            Height = 200,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label
        {
            Text = "Choose the type for imported assets:",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 16)
        };

        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new System.Drawing.Point(16, 44),
            Width = 270
        };
        foreach (var v in Enum.GetValues<AssetTypes>())
            combo.Items.Add(v.ToString());
        combo.SelectedIndex = 0;

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new System.Drawing.Point(120, 120), Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new System.Drawing.Point(210, 120), Width = 80 };
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        form.Controls.AddRange([label, combo, ok, cancel]);

        var result = form.ShowDialog(_owner) == DialogResult.OK
            ? combo.SelectedItem?.ToString()
            : null;
        return Task.FromResult(result);
    }

    private static string BuildFilter(string[] patterns)
    {
        if (patterns.Length == 0)
            return "All Files (*.*)|*.*";

        var displayPatterns = string.Join(";", patterns);
        return $"Files ({displayPatterns})|{displayPatterns}|All Files (*.*)|*.*";
    }
}
