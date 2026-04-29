using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gondwana.Assets;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// View-model for the AssetFiles document panel.
/// Provides full CRUD operations on a <see cref="AssetsFile"/>.
/// </summary>
public sealed partial class AssetFilesViewModel : ViewModelBase, IDisposable
{
    private AssetsFile? _assetsFile;
    private readonly Window _owner;

    public ObservableCollection<AssetRecordViewModel> Records { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _statusText = "No asset file loaded.";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedTypeIndex;   // 0 = All Types, 1+ = AssetTypes values

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReplaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private AssetRecordViewModel? _selectedRecord;

    public bool HasFile => !string.IsNullOrEmpty(FilePath);

    // Items for the type filter combo: "All Types" followed by each AssetTypes value.
    public ObservableCollection<string> TypeFilterItems { get; } = new();

    public AssetFilesViewModel(Window owner)
    {
        _owner = owner;

        TypeFilterItems.Add("All Types");
        foreach (var value in Enum.GetValues<AssetTypes>())
            TypeFilterItems.Add(value.ToString());

        SelectedTypeIndex = 0;
    }

    partial void OnSearchTextChanged(string value) => ReloadRecords();
    partial void OnSelectedTypeIndexChanged(int value) => ReloadRecords();

    // ------------------------------------------------------------------ Commands

    [RelayCommand(CanExecute = nameof(CanAlwaysExecute))]
    private async Task NewAsync()
    {
        var sp = _owner.StorageProvider;
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Create Asset File",
            SuggestedFileName = "assets",
            DefaultExtension = "gaf",
            FileTypeChoices = AssetFileTypes()
        });
        if (file is null) return;

        var path = file.TryGetLocalPath()!;
        var encrypt = await ConfirmAsync("Enable password protection for this asset file?", "Encryption");
        string? password = null;
        if (encrypt)
        {
            password = await PromptAsync("Enter password for the new asset file:", "Password");
            if (string.IsNullOrWhiteSpace(password))
            {
                await AlertAsync("A password is required when encryption is enabled.", "Password Required");
                return;
            }
        }

        try
        {
            _assetsFile?.Dispose();
            _assetsFile = AssetsFile.LoadOrCreate(path, password, encrypt);
            _assetsFile.Save();
            FilePath = path;
            ReloadRecords();
            StatusText = $"Created: {path}";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to create asset file.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAlwaysExecute))]
    private async Task OpenAsync()
    {
        var sp = _owner.StorageProvider;
        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Asset File",
            AllowMultiple = false,
            FileTypeFilter = AssetFileTypes()
        });
        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath()!;

        try
        {
            _assetsFile?.Dispose();
            _assetsFile = null;

            try
            {
                _assetsFile = AssetsFile.LoadOrCreate(path);
                _ = _assetsFile.GetAllEntries().ToList();
            }
            catch
            {
                var password = await PromptAsync("Enter password for this asset file:", "Password");
                _assetsFile?.Dispose();
                _assetsFile = AssetsFile.LoadOrCreate(path, password, encrypt: true);
                _ = _assetsFile.GetAllEntries().ToList();
            }

            FilePath = path;
            ReloadRecords();
            StatusText = $"Opened: {path}";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to open asset file.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(HasFile))]
    private async Task SaveAsync()
    {
        try
        {
            _assetsFile!.Save();
            StatusText = $"Saved: {_assetsFile.FilePath}";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to save asset file.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(HasFile))]
    private async Task SaveAsAsync()
    {
        var sp = _owner.StorageProvider;
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Asset File As",
            SuggestedFileName = Path.GetFileName(_assetsFile!.FilePath),
            DefaultExtension = Path.GetExtension(_assetsFile.FilePath).TrimStart('.'),
            FileTypeChoices = AssetFileTypes()
        });
        if (file is null) return;

        var path = file.TryGetLocalPath()!;

        try
        {
            var encrypt = await ConfirmAsync("Enable password protection for the saved copy?", "Encryption");
            string? password = null;
            if (encrypt)
            {
                password = await PromptAsync("Enter password for the saved copy:", "Password");
                if (string.IsNullOrWhiteSpace(password))
                {
                    await AlertAsync("A password is required when encryption is enabled.", "Password Required");
                    return;
                }
            }

            var copy = AssetsFile.LoadOrCreate(path, password, encrypt);
            foreach (var entry in _assetsFile.GetAllEntries())
            {
                using var stream = _assetsFile[entry.AssetType, entry.AssetName];
                if (stream is null) continue;
                copy.Add(entry.AssetType, entry.AssetName, stream);
            }
            copy.Save();
            copy.Dispose();
            StatusText = $"Saved copy: {path}";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to save copy of asset file.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(HasFile))]
    private void Refresh() => ReloadRecords();

    [RelayCommand(CanExecute = nameof(HasFile))]
    private async Task AddAsync()
    {
        var sp = _owner.StorageProvider;
        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Asset",
            AllowMultiple = true
        });
        if (files.Count == 0) return;

        var typeName = await PickAssetTypeAsync();
        if (typeName is null) return;
        var type = Enum.Parse<AssetTypes>(typeName);

        try
        {
            foreach (var storageFile in files)
            {
                var filePath = storageFile.TryGetLocalPath()!;
                var defaultName = Path.GetFileName(filePath);
                var customName = await PromptAsync(
                    $"Enter asset name for '{defaultName}' (leave as-is to keep original):",
                    "Asset Name",
                    defaultName);
                if (string.IsNullOrWhiteSpace(customName)) continue;
                _assetsFile!.Add(type, filePath, customName);
            }
            ReloadRecords();
            StatusText = $"Imported {files.Count} asset(s).";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to import one or more assets.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectionAndFile))]
    private async Task ReplaceAsync()
    {
        var selected = SelectedRecord!;
        var sp = _owner.StorageProvider;
        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Replace '{selected.AssetName}'"
        });
        if (files.Count == 0) return;

        try
        {
            var filePath = files[0].TryGetLocalPath()!;
            using var stream = File.OpenRead(filePath);
            _assetsFile!.Add(selected.AssetType, selected.AssetName, stream);
            ReloadRecords();
            StatusText = $"Replaced: {selected.AssetName}";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to replace asset.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectionAndFile))]
    private async Task RenameAsync()
    {
        var selected = SelectedRecord!;
        var newName = await PromptAsync("Enter new asset name:", "Rename Asset", selected.AssetName);
        if (string.IsNullOrWhiteSpace(newName) ||
            string.Equals(newName, selected.AssetName, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            using var stream = _assetsFile![selected.AssetType, selected.AssetName];
            if (stream is null)
            {
                await AlertAsync("The selected asset could not be read.", "Read Failed");
                return;
            }
            _assetsFile.Add(selected.AssetType, newName, stream);
            _assetsFile.Remove(selected.AssetType, selected.AssetName);
            ReloadRecords();
            StatusText = $"Renamed '{selected.AssetName}' to '{newName}'.";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to rename asset.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectionAndFile))]
    private async Task ExportAsync()
    {
        var selected = SelectedRecord!;
        var sp = _owner.StorageProvider;
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Export '{selected.AssetName}'",
            SuggestedFileName = selected.AssetName
        });
        if (file is null) return;

        try
        {
            using var stream = _assetsFile![selected.AssetType, selected.AssetName];
            if (stream is null)
            {
                await AlertAsync("The selected asset could not be read.", "Read Failed");
                return;
            }
            using var fileStream = File.Create(file.TryGetLocalPath()!);
            stream.CopyTo(fileStream);
            StatusText = $"Exported: {selected.AssetName}";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to export asset.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectionAndFile))]
    private async Task DeleteAsync()
    {
        var selected = SelectedRecord!;
        var confirmed = await ConfirmAsync($"Delete '{selected.AssetName}'?", "Confirm Delete");
        if (!confirmed) return;

        try
        {
            _assetsFile!.Remove(selected.AssetType, selected.AssetName);
            ReloadRecords();
            StatusText = $"Deleted: {selected.AssetName}";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to delete asset.", ex);
        }
    }

    // ------------------------------------------------------------------ Helpers

    private bool CanAlwaysExecute() => true;

    private bool HasSelectionAndFile() => HasFile && SelectedRecord is not null;

    private void ReloadRecords()
    {
        Records.Clear();
        if (_assetsFile is null) return;

        try
        {
            var entries = _assetsFile.GetAllEntries();
            var search = SearchText.Trim();
            AssetTypes? typeFilter = SelectedTypeIndex > 0
                ? (AssetTypes)(SelectedTypeIndex - 1)
                : null;

            foreach (var entry in entries.OrderBy(e => e.AssetType).ThenBy(e => e.AssetName))
            {
                if (typeFilter.HasValue && entry.AssetType != typeFilter.Value)
                    continue;

                if (!string.IsNullOrWhiteSpace(search) &&
                    entry.AssetName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                long size = 0;
                using var stream = _assetsFile[entry.AssetType, entry.AssetName];
                if (stream is not null && stream.CanSeek)
                    size = stream.Length;
                else if (stream is not null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    size = ms.Length;
                }

                Records.Add(new AssetRecordViewModel
                {
                    AssetType = entry.AssetType,
                    AssetName = entry.AssetName,
                    SizeBytes = size
                });
            }

            StatusText = $"{_assetsFile.FilePath} ({Records.Count} asset(s))";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading assets: {ex.Message}";
        }
    }

    private async Task<bool> ConfirmAsync(string message, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var result = false;
        var panel = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var buttons = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };
        var yesBtn = new Button { Content = "Yes" };
        var noBtn = new Button { Content = "No" };
        yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
        noBtn.Click += (_, _) => { result = false; dialog.Close(); };
        buttons.Children.Add(yesBtn);
        buttons.Children.Add(noBtn);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(_owner);
        return result;
    }

    private async Task AlertAsync(string message, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var panel = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var okBtn = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        okBtn.Click += (_, _) => dialog.Close();
        panel.Children.Add(okBtn);
        dialog.Content = panel;

        await dialog.ShowDialog(_owner);
    }

    private async Task<string?> PromptAsync(string message, string title, string? defaultValue = null)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        string? result = null;
        var panel = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var textBox = new TextBox { Text = defaultValue ?? string.Empty };
        panel.Children.Add(textBox);
        var buttons = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };
        var okBtn = new Button { Content = "OK" };
        var cancelBtn = new Button { Content = "Cancel" };
        okBtn.Click += (_, _) => { result = textBox.Text; dialog.Close(); };
        cancelBtn.Click += (_, _) => { result = null; dialog.Close(); };
        buttons.Children.Add(okBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(_owner);
        return result;
    }

    private async Task<string?> PickAssetTypeAsync()
    {
        var dialog = new Window
        {
            Title = "Select Asset Type",
            Width = 300,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        string? result = null;
        var panel = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Choose the type for imported assets:" });
        var combo = new ComboBox { Width = 200 };
        foreach (var v in Enum.GetValues<AssetTypes>())
            combo.Items.Add(v.ToString());
        combo.SelectedIndex = 0;
        panel.Children.Add(combo);

        var buttons = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };
        var okBtn = new Button { Content = "OK" };
        var cancelBtn = new Button { Content = "Cancel" };
        okBtn.Click += (_, _) => { result = combo.SelectedItem?.ToString(); dialog.Close(); };
        cancelBtn.Click += (_, _) => { result = null; dialog.Close(); };
        buttons.Children.Add(okBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(_owner);
        return result;
    }

    private async Task ShowErrorAsync(string message, Exception ex)
    {
        StatusText = message;
        await AlertAsync(message + Environment.NewLine + Environment.NewLine + ex.Message, "Error");
    }

    private static FilePickerFileType[] AssetFileTypes() =>
    [
        new FilePickerFileType("Asset Files") { Patterns = ["*.gaf", "*.zip"] },
        new FilePickerFileType("All Files") { Patterns = ["*.*"] }
    ];

    public void Dispose()
    {
        _assetsFile?.Dispose();
    }
}
