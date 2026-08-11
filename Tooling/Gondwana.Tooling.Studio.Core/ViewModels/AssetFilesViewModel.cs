using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gondwana.Assets;
using Gondwana.Tooling.Studio.Core.Services;

namespace Gondwana.Tooling.Studio.ViewModels;

/// <summary>
/// View-model for the AssetFiles document panel.
/// Provides full CRUD operations on a <see cref="AssetsFile"/>.
/// </summary>
public sealed partial class AssetFilesViewModel : ViewModelBase, IDisposable
{
    private AssetsFile? _assetsFile;
    private readonly IDialogService _dialogService;

    /// <summary>
    /// Gets the observable collection of asset records displayed in the grid.
    /// </summary>
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

    /// <summary>
    /// Gets a value indicating whether an asset file is open.
    /// </summary>
    public bool HasFile => !string.IsNullOrEmpty(FilePath);

    /// <summary>
    /// Gets items for the type filter combo: "All Types" followed by each AssetTypes value.
    /// </summary>
    public ObservableCollection<string> TypeFilterItems { get; } = new();

    /// <summary>
    /// AssetFilesViewModel.
    /// </summary>
    /// <param name="dialogService">Platform dialog service.</param>
    public AssetFilesViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;

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
        var path = await _dialogService.SaveFileAsync(
            "Create Asset File", "assets", "gaf", ["*.gaf", "*.zip"]);
        if (path is null) return;

        var encrypt = await _dialogService.ConfirmAsync(
            "Enable password protection for this asset file?", "Encryption");
        string? password = null;
        if (encrypt)
        {
            password = await _dialogService.PromptAsync(
                "Enter password for the new asset file:", "Password");
            if (string.IsNullOrWhiteSpace(password))
            {
                await _dialogService.AlertAsync(
                    "A password is required when encryption is enabled.", "Password Required");
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
        var path = await _dialogService.OpenFileAsync(
            "Open Asset File", ["*.gaf", "*.zip"]);
        if (path is null) return;

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
                var password = await _dialogService.PromptAsync(
                    "Enter password for this asset file:", "Password");
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
        var path = await _dialogService.SaveFileAsync(
            "Save Asset File As",
            Path.GetFileName(_assetsFile!.FilePath),
            Path.GetExtension(_assetsFile.FilePath).TrimStart('.'),
            ["*.gaf", "*.zip"]);
        if (path is null) return;

        try
        {
            var encrypt = await _dialogService.ConfirmAsync(
                "Enable password protection for the saved copy?", "Encryption");
            string? password = null;
            if (encrypt)
            {
                password = await _dialogService.PromptAsync(
                    "Enter password for the saved copy:", "Password");
                if (string.IsNullOrWhiteSpace(password))
                {
                    await _dialogService.AlertAsync(
                        "A password is required when encryption is enabled.", "Password Required");
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
        var files = await _dialogService.OpenFilesAsync("Import Asset");
        if (files.Count == 0) return;

        var typeName = await _dialogService.PickAssetTypeAsync();
        if (typeName is null) return;
        var type = Enum.Parse<AssetTypes>(typeName);

        try
        {
            foreach (var filePath in files)
            {
                var defaultName = Path.GetFileName(filePath);
                var customName = await _dialogService.PromptAsync(
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
        var filePath = await _dialogService.OpenFileAsync(
            $"Replace '{selected.AssetName}'", ["*.*"]);
        if (filePath is null) return;

        try
        {
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
        var newName = await _dialogService.PromptAsync(
            "Enter new asset name:", "Rename Asset", selected.AssetName);
        if (string.IsNullOrWhiteSpace(newName) ||
            string.Equals(newName, selected.AssetName, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            using var stream = _assetsFile![selected.AssetType, selected.AssetName];
            if (stream is null)
            {
                await _dialogService.AlertAsync("The selected asset could not be read.", "Read Failed");
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
        var savePath = await _dialogService.SaveFileAsync(
            $"Export '{selected.AssetName}'", selected.AssetName, string.Empty, ["*.*"]);
        if (savePath is null) return;

        try
        {
            using var stream = _assetsFile![selected.AssetType, selected.AssetName];
            if (stream is null)
            {
                await _dialogService.AlertAsync("The selected asset could not be read.", "Read Failed");
                return;
            }
            using var fileStream = File.Create(savePath);
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
        var confirmed = await _dialogService.ConfirmAsync(
            $"Delete '{selected.AssetName}'?", "Confirm Delete");
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

    private async Task ShowErrorAsync(string message, Exception ex)
    {
        StatusText = message;
        await _dialogService.AlertAsync(
            message + Environment.NewLine + Environment.NewLine + ex.Message, "Error");
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    public void Dispose()
    {
        _assetsFile?.Dispose();
    }
}
