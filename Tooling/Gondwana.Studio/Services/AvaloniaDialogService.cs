using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Platform.Storage;
using global::Avalonia;
using Gondwana.Assets;
using Gondwana.Tooling.Studio.Core.Services;

namespace Gondwana.Tooling.Studio.Avalonia.Services;

/// <summary>
/// Avalonia implementation of <see cref="IDialogService"/>.
/// All members must be called from the UI thread.
/// </summary>
public sealed class AvaloniaDialogService : IDialogService
{
    private readonly Window _owner;

    /// <summary>
    /// AvaloniaDialogService.
    /// </summary>
    /// <param name="owner">The owning window used to anchor dialogs.</param>
    public AvaloniaDialogService(Window owner)
    {
        _owner = owner;
    }

    /// <inheritdoc/>
    public async Task<string?> OpenFileAsync(string title, string[] patterns)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Files") { Patterns = patterns }]
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> OpenFilesAsync(string title)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true
        });

        return files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<string?> SaveFileAsync(string title, string suggestedName, string defaultExt, string[] patterns)
    {
        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = defaultExt,
            FileTypeChoices = [new FilePickerFileType("Files") { Patterns = patterns }]
        });

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmAsync(string message, string title)
    {
        var result = false;
        var dialog = BuildDialog(title, out var panel);

        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var yesBtn = new Button { Content = "Yes" };
        var noBtn = new Button { Content = "No" };
        yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
        noBtn.Click += (_, _) => { result = false; dialog.Close(); };
        buttons.Children.Add(yesBtn);
        buttons.Children.Add(noBtn);
        panel.Children.Add(buttons);

        await dialog.ShowDialog(_owner);
        return result;
    }

    /// <inheritdoc/>
    public async Task AlertAsync(string message, string title)
    {
        var dialog = BuildDialog(title, out var panel);

        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        var okBtn = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
        okBtn.Click += (_, _) => dialog.Close();
        panel.Children.Add(okBtn);

        await dialog.ShowDialog(_owner);
    }

    /// <inheritdoc/>
    public async Task<string?> PromptAsync(string message, string title, string? defaultValue = null)
    {
        string? result = null;
        var dialog = BuildDialog(title, out var panel, width: 420, height: 200);

        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        var textBox = new TextBox { Text = defaultValue ?? string.Empty };
        panel.Children.Add(textBox);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var okBtn = new Button { Content = "OK" };
        var cancelBtn = new Button { Content = "Cancel" };
        okBtn.Click += (_, _) => { result = textBox.Text; dialog.Close(); };
        cancelBtn.Click += (_, _) => { result = null; dialog.Close(); };
        buttons.Children.Add(okBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);

        await dialog.ShowDialog(_owner);
        return result;
    }

    /// <inheritdoc/>
    public async Task<string?> PickAssetTypeAsync()
    {
        string? result = null;
        var dialog = BuildDialog("Select Asset Type", out var panel, width: 300, height: 220);

        panel.Children.Add(new TextBlock { Text = "Choose the type for imported assets:" });
        var combo = new ComboBox { Width = 200 };
        foreach (var v in Enum.GetValues<AssetTypes>())
            combo.Items.Add(v.ToString());
        combo.SelectedIndex = 0;
        panel.Children.Add(combo);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var okBtn = new Button { Content = "OK" };
        var cancelBtn = new Button { Content = "Cancel" };
        okBtn.Click += (_, _) => { result = combo.SelectedItem?.ToString(); dialog.Close(); };
        cancelBtn.Click += (_, _) => { result = null; dialog.Close(); };
        buttons.Children.Add(okBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);

        await dialog.ShowDialog(_owner);
        return result;
    }

    private static Window BuildDialog(string title, out StackPanel panel,
        double width = 400, double height = 180)
    {
        var dialog = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        panel = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        dialog.Content = panel;
        return dialog;
    }
}
