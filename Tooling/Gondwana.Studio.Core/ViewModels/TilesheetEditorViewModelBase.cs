using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gondwana.Studio.Core.Services;
using Gondwana.StudioAssets;
using Newtonsoft.Json;
using SkiaSharp;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// Platform-neutral base for the tilesheet editor.
/// Subclasses (e.g. the Avalonia project) extend this with UI-framework–specific
/// image preview properties.
/// </summary>
public partial class TilesheetEditorViewModelBase : ViewModelBase
{
    private readonly IDialogService _dialogService;

    /// <summary>
    /// Gets the collection of tile cells in the tilesheet grid.
    /// </summary>
    public ObservableCollection<TileCellViewModel> TileCells { get; } = [];

    /// <summary>
    /// Gets the subset of tile cells that have been assigned a name.
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
    /// TilesheetEditorViewModelBase.
    /// </summary>
    /// <param name="dialogService">Platform dialog service.</param>
    public TilesheetEditorViewModelBase(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    partial void OnSelectedTileChanged(TileCellViewModel? value)
    {
        SelectedTileName = value?.Name ?? string.Empty;
    }

    [RelayCommand]
    private async Task OpenImageAsync()
    {
        var path = await _dialogService.OpenFileAsync(
            "Open Tilesheet Image",
            ["*.png", "*.bmp"]);

        if (!string.IsNullOrWhiteSpace(path))
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

        var savePath = await _dialogService.SaveFileAsync(
            "Save Tilesheet Metadata",
            Path.GetFileNameWithoutExtension(ImagePath),
            "gondwana-tilesheet",
            ["*.gondwana-tilesheet"]);

        if (string.IsNullOrWhiteSpace(savePath))
            return;

        MetadataPath = savePath;
        SaveTo(MetadataPath);
        StatusText = $"Saved metadata: {MetadataPath}";
    }

    /// <summary>
    /// Loads the image at <paramref name="path"/> and rebuilds the tile grid.
    /// Subclasses may override to also load a platform-specific image preview.
    /// </summary>
    /// <param name="path">Absolute path to the image file.</param>
    public virtual void LoadImage(string path)
    {
        using var bitmap = SKBitmap.Decode(path);
        if (bitmap is null)
        {
            StatusText = $"Failed to load image: {Path.GetFileName(path)}";
            return;
        }

        ImageWidth = bitmap.Width;
        ImageHeight = bitmap.Height;
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
