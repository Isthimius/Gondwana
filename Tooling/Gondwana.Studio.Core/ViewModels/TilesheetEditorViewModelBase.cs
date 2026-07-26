using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Studio.Core.Services;
using Gondwana.StudioAssets;
using SkiaSharp;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// Platform-neutral base for the tilesheet editor.
/// </summary>
public partial class TilesheetEditorViewModelBase : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly Dictionary<(int x, int y), CollisionAdjust>
        _loadedFrameCollisionAdjustments = [];
    private bool _suppressCollisionAdjustPropagation;

    public ObservableCollection<TileCellViewModel> TileCells { get; } = [];

    public IEnumerable<TileCellViewModel> NamedTiles =>
        TileCells.Where(t => !string.IsNullOrWhiteSpace(t.Name));

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
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _regionX;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _regionY;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _regionWidth;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _regionHeight;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _tilePaddingLeft;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _tilePaddingTop;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _tilePaddingRight;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _tilePaddingBottom;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _regionMarginLeft;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _regionMarginTop;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _regionMarginRight;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildGridCommand))]
    private int _regionMarginBottom;

    [ObservableProperty]
    private int _overhangLeft;

    [ObservableProperty]
    private int _overhangTop;

    [ObservableProperty]
    private int _overhangRight;

    [ObservableProperty]
    private int _overhangBottom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegionCollisionAdjust))]
    private int _collisionAdjustTop;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegionCollisionAdjust))]
    private int _collisionAdjustBottom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegionCollisionAdjust))]
    private int _collisionAdjustLeft;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegionCollisionAdjust))]
    private int _collisionAdjustRight;

    [ObservableProperty]
    private bool _premultiplyAlpha;

    [ObservableProperty]
    private TileCellViewModel? _selectedTile;

    [ObservableProperty]
    private string _selectedTileName = string.Empty;

    [ObservableProperty]
    private string _statusText = "No tilesheet loaded.";

    public double CanvasWidth => ImageWidth;
    public double CanvasHeight => ImageHeight;

    /// <summary>
    /// Gets the current region-level collision adjustment.
    /// </summary>
    public CollisionAdjust RegionCollisionAdjust => new(
        CollisionAdjustTop,
        CollisionAdjustBottom,
        CollisionAdjustLeft,
        CollisionAdjustRight);

    public TilesheetEditorViewModelBase(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    partial void OnSelectedTileChanged(TileCellViewModel? value)
    {
        SelectedTileName = value?.Name ?? string.Empty;
    }

    partial void OnCollisionAdjustTopChanged(int value) =>
        PropagateRegionCollisionAdjust();

    partial void OnCollisionAdjustBottomChanged(int value) =>
        PropagateRegionCollisionAdjust();

    partial void OnCollisionAdjustLeftChanged(int value) =>
        PropagateRegionCollisionAdjust();

    partial void OnCollisionAdjustRightChanged(int value) =>
        PropagateRegionCollisionAdjust();

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
            "Save Tilesheet Definition",
            Path.GetFileNameWithoutExtension(
                MetadataPath.Length > 0 ? MetadataPath : ImagePath),
            "gts",
            ["*.gts"]);

        if (string.IsNullOrWhiteSpace(savePath))
            return;

        MetadataPath = savePath;
        SaveTo(MetadataPath);
        StatusText = $"Saved metadata: {MetadataPath}";
    }

    public virtual void LoadImage(string path)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(path);
            if (bitmap is null)
            {
                StatusText = $"Failed to load image: {Path.GetFileName(path)}";
                return;
            }

            ImageWidth = bitmap.Width;
            ImageHeight = bitmap.Height;
        }
        catch (Exception ex)
        {
            StatusText =
                $"Failed to load image: {Path.GetFileName(path)} ({ex.Message})";
            return;
        }

        ImagePath = path;
        BuildGrid();
        StatusText = $"Loaded image: {Path.GetFileName(path)}";
    }

    public void LoadMetadata(string metadataPath)
    {
        if (metadataPath.EndsWith(
            ".gondwana-tilesheet",
            StringComparison.OrdinalIgnoreCase))
        {
            LoadLegacyMetadata(metadataPath);
            return;
        }

        var definition = TilesheetDefinitionSerializer.Load(metadataPath);
        var imagePath = ResolveGtsImagePath(metadataPath, definition);
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            StatusText =
                "GTS image source must use Image.FilePath for editor preview.";
            return;
        }

        MetadataPath = metadataPath;
        ApplyRegionSettings(definition.Regions.FirstOrDefault());
        PremultiplyAlpha = definition.PremultiplyAlpha;
        LoadImage(imagePath);

        StatusText = $"Loaded definition: {Path.GetFileName(metadataPath)}";
    }

    public void SaveTo(string metadataPath)
    {
        if (metadataPath.EndsWith(
            ".gondwana-tilesheet",
            StringComparison.OrdinalIgnoreCase))
        {
            SaveLegacyMetadata(metadataPath);
            return;
        }

        var metadataDir = Path.GetDirectoryName(metadataPath) ?? string.Empty;
        var relativeImagePath = Path
            .GetRelativePath(metadataDir, ImagePath)
            .Replace('\\', '/');

        var (areaWidth, areaHeight) = GetEffectiveRegionSize();
        var definition = new TilesheetDefinition
        {
            Name = Path.GetFileNameWithoutExtension(metadataPath),
            Image = new TilesheetImageDefinition
            {
                FilePath = relativeImagePath
            },
            PremultiplyAlpha = PremultiplyAlpha,
            Regions =
            [
                new TilesheetRegionDefinition
                {
                    Name = TilesheetRegion.DefaultRegionName,
                    Area = new Rectangle(RegionX, RegionY, areaWidth, areaHeight),
                    TileSize = new Size(TileWidth, TileHeight),
                    TilePadding = new Spacing(
                        TilePaddingLeft,
                        TilePaddingTop,
                        TilePaddingRight,
                        TilePaddingBottom),
                    RegionMargin = new Spacing(
                        RegionMarginLeft,
                        RegionMarginTop,
                        RegionMarginRight,
                        RegionMarginBottom),
                    Overhang = new Spacing(
                        OverhangLeft,
                        OverhangTop,
                        OverhangRight,
                        OverhangBottom),
                    CollisionAdjust = RegionCollisionAdjust,
                    Frames = TileCells
                        .Select(tile => new TilesheetFrameDefinition
                        {
                            XTile = tile.X,
                            YTile = tile.Y,
                            CollisionAdjust = tile.CollisionAdjust
                        })
                        .ToList()
                }
            ]
        };

        TilesheetDefinitionSerializer.Save(metadataPath, definition);
    }

    private void SaveLegacyMetadata(string metadataPath)
    {
        var metadataDir = Path.GetDirectoryName(metadataPath) ?? string.Empty;
        var relativeImagePath = Path
            .GetRelativePath(metadataDir, ImagePath)
            .Replace('\\', '/');

        var model = new TilesheetMetadataAsset
        {
            ImagePath = relativeImagePath,
            TileWidth = TileWidth,
            TileHeight = TileHeight,
            Tiles = TileCells
                .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                .Select(t => new TilesheetTileNameAsset
                {
                    Index = t.Index,
                    Name = t.Name
                })
                .ToList()
        };

        File.WriteAllText(
            metadataPath,
            Newtonsoft.Json.JsonConvert.SerializeObject(
                model,
                Newtonsoft.Json.Formatting.Indented));
    }

    private bool CanRebuild() =>
        !string.IsNullOrWhiteSpace(ImagePath) &&
        TileWidth > 0 &&
        TileHeight > 0 &&
        RegionX >= 0 &&
        RegionY >= 0 &&
        TilePaddingLeft >= 0 &&
        TilePaddingTop >= 0 &&
        TilePaddingRight >= 0 &&
        TilePaddingBottom >= 0 &&
        RegionMarginLeft >= 0 &&
        RegionMarginTop >= 0 &&
        RegionMarginRight >= 0 &&
        RegionMarginBottom >= 0;

    private void BuildGrid()
    {
        var existingTiles = TileCells.ToDictionary(
            tile => (tile.X, tile.Y),
            tile => (tile.Name, tile.CollisionAdjust));

        TileCells.Clear();
        if (!CanRebuild())
            return;

        var (areaWidth, areaHeight) = GetEffectiveRegionSize();
        if (areaWidth <= 0 || areaHeight <= 0)
            return;

        var tileWidthWithPadding =
            TileWidth + TilePaddingLeft + TilePaddingRight;
        var tileHeightWithPadding =
            TileHeight + TilePaddingTop + TilePaddingBottom;

        if (tileWidthWithPadding <= 0 || tileHeightWithPadding <= 0)
            return;

        var columns =
            (areaWidth - RegionMarginLeft - RegionMarginRight) /
            tileWidthWithPadding;
        var rows =
            (areaHeight - RegionMarginTop - RegionMarginBottom) /
            tileHeightWithPadding;

        if (columns <= 0 || rows <= 0)
            return;

        var index = 0;

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var left =
                    RegionX +
                    RegionMarginLeft +
                    (x * tileWidthWithPadding) +
                    TilePaddingLeft;
                var top =
                    RegionY +
                    RegionMarginTop +
                    (y * tileHeightWithPadding) +
                    TilePaddingTop;

                var tile = new TileCellViewModel
                {
                    Index = index++,
                    X = x,
                    Y = y,
                    Left = left,
                    Top = top,
                    Width = TileWidth,
                    Height = TileHeight
                };

                if (_loadedFrameCollisionAdjustments.TryGetValue(
                    (x, y),
                    out var loadedCollisionAdjust))
                {
                    tile.CollisionAdjust = loadedCollisionAdjust;
                }
                else if (existingTiles.TryGetValue(
                    (x, y),
                    out var existingTile))
                {
                    tile.Name = existingTile.Name;
                    tile.CollisionAdjust = existingTile.CollisionAdjust;
                }
                else
                {
                    tile.CollisionAdjust = RegionCollisionAdjust;
                }

                TileCells.Add(tile);
            }
        }

        _loadedFrameCollisionAdjustments.Clear();
        OnPropertyChanged(nameof(NamedTiles));
    }

    private static string? ResolveGtsImagePath(
        string metadataPath,
        TilesheetDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Image.FilePath))
            return null;

        var path = definition.Image.FilePath!;
        if (Path.IsPathRooted(path))
            return path;

        var metadataDir = Path.GetDirectoryName(metadataPath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(metadataDir, path));
    }

    private void ApplyRegionSettings(TilesheetRegionDefinition? region)
    {
        if (region is null)
            return;

        TileWidth = region.TileSize.Width > 0
            ? region.TileSize.Width
            : TileWidth;
        TileHeight = region.TileSize.Height > 0
            ? region.TileSize.Height
            : TileHeight;

        RegionX = Math.Max(0, region.Area.X);
        RegionY = Math.Max(0, region.Area.Y);
        RegionWidth = Math.Max(0, region.Area.Width);
        RegionHeight = Math.Max(0, region.Area.Height);

        TilePaddingLeft = Math.Max(0, region.TilePadding.Left);
        TilePaddingTop = Math.Max(0, region.TilePadding.Top);
        TilePaddingRight = Math.Max(0, region.TilePadding.Right);
        TilePaddingBottom = Math.Max(0, region.TilePadding.Bottom);

        RegionMarginLeft = Math.Max(0, region.RegionMargin.Left);
        RegionMarginTop = Math.Max(0, region.RegionMargin.Top);
        RegionMarginRight = Math.Max(0, region.RegionMargin.Right);
        RegionMarginBottom = Math.Max(0, region.RegionMargin.Bottom);

        OverhangLeft = region.Overhang.Left;
        OverhangTop = region.Overhang.Top;
        OverhangRight = region.Overhang.Right;
        OverhangBottom = region.Overhang.Bottom;

        SetRegionCollisionAdjust(region.CollisionAdjust, propagate: false);

        _loadedFrameCollisionAdjustments.Clear();
        foreach (var frame in region.Frames ?? [])
        {
            _loadedFrameCollisionAdjustments[(frame.XTile, frame.YTile)] =
                frame.CollisionAdjust ?? region.CollisionAdjust;
        }
    }

    private void LoadLegacyMetadata(string metadataPath)
    {
        var metadata = TilesheetMetadataLoader.Load(metadataPath);
        var imageFullPath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(metadataPath) ?? string.Empty,
                metadata.ImagePath));

        MetadataPath = metadataPath;
        TileWidth = metadata.TileWidth;
        TileHeight = metadata.TileHeight;
        RegionX = 0;
        RegionY = 0;
        RegionWidth = 0;
        RegionHeight = 0;
        TilePaddingLeft = 0;
        TilePaddingTop = 0;
        TilePaddingRight = 0;
        TilePaddingBottom = 0;
        RegionMarginLeft = 0;
        RegionMarginTop = 0;
        RegionMarginRight = 0;
        RegionMarginBottom = 0;
        OverhangLeft = 0;
        OverhangTop = 0;
        OverhangRight = 0;
        OverhangBottom = 0;
        _loadedFrameCollisionAdjustments.Clear();
        SetRegionCollisionAdjust(CollisionAdjust.None, propagate: false);
        PremultiplyAlpha = false;
        LoadImage(imageFullPath);

        var names = metadata.Tiles.ToDictionary(t => t.Index, t => t.Name);
        foreach (var tile in TileCells)
        {
            tile.Name = names.TryGetValue(tile.Index, out var name)
                ? name
                : string.Empty;
        }

        OnPropertyChanged(nameof(NamedTiles));
    }

    private void PropagateRegionCollisionAdjust()
    {
        if (_suppressCollisionAdjustPropagation)
            return;

        var collisionAdjust = RegionCollisionAdjust;
        foreach (var tile in TileCells)
            tile.CollisionAdjust = collisionAdjust;
    }

    private void SetRegionCollisionAdjust(
        CollisionAdjust collisionAdjust,
        bool propagate)
    {
        _suppressCollisionAdjustPropagation = true;
        try
        {
            CollisionAdjustTop = collisionAdjust.Top;
            CollisionAdjustBottom = collisionAdjust.Bottom;
            CollisionAdjustLeft = collisionAdjust.Left;
            CollisionAdjustRight = collisionAdjust.Right;
        }
        finally
        {
            _suppressCollisionAdjustPropagation = false;
        }

        if (propagate)
            PropagateRegionCollisionAdjust();
    }

    private (int width, int height) GetEffectiveRegionSize()
    {
        var width = RegionWidth > 0
            ? RegionWidth
            : Math.Max(0, ImageWidth - RegionX);
        var height = RegionHeight > 0
            ? RegionHeight
            : Math.Max(0, ImageHeight - RegionY);

        return (width, height);
    }
}
