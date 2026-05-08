using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gondwana.StudioAssets;
using Newtonsoft.Json;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// TilesheetEditorViewModel.
/// </summary>
public sealed partial class TilesheetEditorViewModel : ViewModelBase
{
    private readonly Window _owner;

    /// <summary>
    /// Gets get.
    /// </summary>
    public ObservableCollection<TileCellViewModel> TileCells { get; } = [];

    /// <summary>
    /// Gets t.
    /// </summary>
    public IEnumerable<TileCellViewModel> NamedTiles => TileCells.Where(t => !string.IsNullOrWhiteSpace(t.Name));

    [ObservableProperty]
    private string _metadataPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private string _imagePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanvasWidth))]
    private int _imageWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanvasHeight))]
    private int _imageHeight;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _tileWidth = 16;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _tileHeight = 16;

    [ObservableProperty]
    private Bitmap? _previewBitmap;

    [ObservableProperty]
    private TileCellViewModel? _selectedTile;

    [ObservableProperty]
    private string _selectedTileName = string.Empty;

    [ObservableProperty]
    private string _statusText = "No tilesheet loaded.";

    /// <summary>
    /// Gets ImageWidth.
    /// </summary>
    public double CanvasWidth => ImageWidth;
    /// <summary>
    /// Gets ImageHeight.
    /// </summary>
    public double CanvasHeight => ImageHeight;

    /// <summary>
    /// TilesheetEditorViewModel.
    /// </summary>
    /// <param name="owner">owner.</param>
    public TilesheetEditorViewModel(Window owner)
    {
        _owner = owner;
    }

    partial void OnSelectedTileChanged(TileCellViewModel? value)
    {
        SelectedTileName = value?.Name ?? string.Empty;
    }

    [RelayCommand]
    private async Task OpenImageAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Tilesheet Image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image Files") { Patterns = ["*.png", "*.bmp"] }
            ]
        });

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        LoadImage(path);
    }

    [RelayCommand(CanExecute = nameof(CanRebuild))]
    private void RebuildGrid()
    {
        BuildGrid();
    }

    [RelayCommand]
    private void ApplyTileName()
    {
        if (SelectedTile is null)
            return;

        SelectedTile.Name = SelectedTileName.Trim();
        OnPropertyChanged(nameof(NamedTiles));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        var saveTarget = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Tilesheet Metadata",
            SuggestedFileName = Path.GetFileNameWithoutExtension(ImagePath),
            DefaultExtension = "gondwana-tilesheet",
            FileTypeChoices =
            [
                new FilePickerFileType("Gondwana Tilesheet") { Patterns = ["*.gondwana-tilesheet"] }
            ]
        });

        if (saveTarget is null)
            return;

        var savePath = saveTarget.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(savePath))
            return;

        MetadataPath = savePath;
        SaveTo(MetadataPath);
        StatusText = $"Saved metadata: {MetadataPath}";
    }

    /// <summary>
    /// LoadImage.
    /// </summary>
    /// <param name="path">path.</param>
    public void LoadImage(string path)
    {
        using var stream = File.OpenRead(path);
        PreviewBitmap = new Bitmap(stream);
        ImageWidth = PreviewBitmap.PixelSize.Width;
        ImageHeight = PreviewBitmap.PixelSize.Height;
        ImagePath = path;
        BuildGrid();
        StatusText = $"Loaded image: {Path.GetFileName(path)}";
    }

    /// <summary>
    /// LoadMetadata.
    /// </summary>
    /// <param name="metadataPath">metadataPath.</param>
    public void LoadMetadata(string metadataPath)
    {
        var metadata = TilesheetMetadataLoader.Load(metadataPath);
        var imageFullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(metadataPath) ?? string.Empty, metadata.ImagePath));

        MetadataPath = metadataPath;
        TileWidth = metadata.TileWidth;
        TileHeight = metadata.TileHeight;
        LoadImage(imageFullPath);

        var names = metadata.Tiles.ToDictionary(t => t.Index, t => t.Name);
        foreach (var tile in TileCells)
            tile.Name = names.TryGetValue(tile.Index, out var name) ? name : string.Empty;

        OnPropertyChanged(nameof(NamedTiles));
    }

    /// <summary>
    /// SaveTo.
    /// </summary>
    /// <param name="metadataPath">metadataPath.</param>
    public void SaveTo(string metadataPath)
    {
        var metadataDir = Path.GetDirectoryName(metadataPath) ?? string.Empty;
        var relativeImagePath = Path.GetRelativePath(metadataDir, ImagePath).Replace('\\', '/');
        var model = new TilesheetMetadataAsset
        {
            ImagePath = relativeImagePath,
            TileWidth = TileWidth,
            TileHeight = TileHeight,
            Tiles = TileCells
                .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                .Select(t => new TilesheetTileNameAsset { Index = t.Index, Name = t.Name })
                .ToList()
        };

        var json = JsonConvert.SerializeObject(model, Formatting.Indented);
        File.WriteAllText(metadataPath, json);
    }

    private bool CanRebuild() =>
        !string.IsNullOrWhiteSpace(ImagePath) && TileWidth > 0 && TileHeight > 0;

    private void BuildGrid()
    {
        TileCells.Clear();
        if (!CanRebuild())
            return;

        var columns = Math.Max(1, ImageWidth / TileWidth);
        var rows = Math.Max(1, ImageHeight / TileHeight);
        var index = 0;

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                TileCells.Add(new TileCellViewModel
                {
                    Index = index++,
                    X = x,
                    Y = y,
                    Left = x * TileWidth,
                    Top = y * TileHeight,
                    Width = TileWidth,
                    Height = TileHeight
                });
            }
        }

        OnPropertyChanged(nameof(NamedTiles));
    }
}
