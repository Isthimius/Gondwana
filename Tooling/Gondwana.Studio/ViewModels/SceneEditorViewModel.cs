using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gondwana.StudioAssets;
using Newtonsoft.Json;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// SceneEditorViewModel.
/// </summary>
public sealed partial class SceneEditorViewModel : ViewModelBase
{
    private readonly Window _owner;

    /// <summary>
    /// Gets get.
    /// </summary>
    public ObservableCollection<TileCellViewModel> TilePalette { get; } = [];
    /// <summary>
    /// Gets get.
    /// </summary>
    public ObservableCollection<ScenePaintedTileViewModel> PaintedTiles { get; } = [];
    /// <summary>
    /// Gets get.
    /// </summary>
    public ObservableCollection<SceneEntityViewModel> Entities { get; } = [];
    /// <summary>
    /// Gets get.
    /// </summary>
    public ObservableCollection<SceneColliderViewModel> Colliders { get; } = [];

    [ObservableProperty]
    private string _scenePath = string.Empty;

    [ObservableProperty]
    private string _tilesheetPath = string.Empty;

    [ObservableProperty]
    private string _activeLayerName = "main";

    [ObservableProperty]
    private float _activeLayerParallax = 1f;

    [ObservableProperty]
    private int _tileWidth = 16;

    [ObservableProperty]
    private int _tileHeight = 16;

    [ObservableProperty]
    private double _zoom = 1d;

    [ObservableProperty]
    private double _cameraX;

    [ObservableProperty]
    private double _cameraY;

    [ObservableProperty]
    private string _activeTool = "Tile";

    [ObservableProperty]
    private TileCellViewModel? _selectedPaletteTile;

    [ObservableProperty]
    private string _statusText = "Scene editor ready.";

    /// <summary>
    /// SceneEditorViewModel.
    /// </summary>
    /// <param name="owner">owner.</param>
    public SceneEditorViewModel(Window owner)
    {
        _owner = owner;
    }

    [RelayCommand]
    private async Task OpenTilesheetAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Tilesheet Metadata",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Gondwana Tilesheet") { Patterns = ["*.gondwana-tilesheet"] }
            ]
        });

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            LoadTilesheet(path);
    }

    [RelayCommand]
    private async Task SaveSceneAsync()
    {
        if (string.IsNullOrWhiteSpace(TilesheetPath))
        {
            StatusText = "A tilesheet must be loaded before saving the scene.";
            return;
        }

        var saveTarget = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Scene",
            SuggestedFileName = string.IsNullOrWhiteSpace(ScenePath) ? "scene" : Path.GetFileNameWithoutExtension(ScenePath),
            DefaultExtension = "gondwana-scene",
            FileTypeChoices =
            [
                new FilePickerFileType("Gondwana Scene") { Patterns = ["*.gondwana-scene"] }
            ]
        });

        if (saveTarget is null)
            return;

        var path = saveTarget.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        ScenePath = path;
        SaveTo(path);
        StatusText = $"Saved scene: {path}";
    }

    /// <summary>
    /// LoadTilesheet.
    /// </summary>
    /// <param name="tilesheetMetadataPath">tilesheetMetadataPath.</param>
    public void LoadTilesheet(string tilesheetMetadataPath)
    {
        var metadata = TilesheetMetadataLoader.Load(tilesheetMetadataPath);
        TilesheetPath = tilesheetMetadataPath;
        TileWidth = metadata.TileWidth;
        TileHeight = metadata.TileHeight;
        TilePalette.Clear();

        var maxNamed = metadata.Tiles.Count == 0 ? 0 : metadata.Tiles.Max(t => t.Index + 1);
        var sheet = TilesheetMetadataLoader.LoadAndRegisterTilesheet(tilesheetMetadataPath);
        var columns = Math.Max(1, sheet.SkBitmap.Width / Math.Max(1, metadata.TileWidth));
        var rows = Math.Max(1, sheet.SkBitmap.Height / Math.Max(1, metadata.TileHeight));
        var count = Math.Max(columns * rows, maxNamed);
        var names = metadata.Tiles.ToDictionary(t => t.Index, t => t.Name);

        for (var i = 0; i < count; i++)
        {
            TilePalette.Add(new TileCellViewModel
            {
                Index = i,
                Name = names.TryGetValue(i, out var n) ? n : $"Tile {i}"
            });
        }
    }

    /// <summary>
    /// ApplyToolAt.
    /// </summary>
    /// <param name="worldX">worldX.</param>
    /// <param name="worldY">worldY.</param>
    public void ApplyToolAt(double worldX, double worldY)
    {
        if (ActiveTool == "Tile")
        {
            if (SelectedPaletteTile is null)
                return;

            var gx = (int)Math.Floor(worldX / Math.Max(1, TileWidth));
            var gy = (int)Math.Floor(worldY / Math.Max(1, TileHeight));
            var existing = PaintedTiles.FirstOrDefault(t => t.GridX == gx && t.GridY == gy && t.LayerName == ActiveLayerName);
            if (existing is not null)
                PaintedTiles.Remove(existing);

            PaintedTiles.Add(new ScenePaintedTileViewModel
            {
                GridX = gx,
                GridY = gy,
                PixelX = gx * TileWidth,
                PixelY = gy * TileHeight,
                Width = TileWidth,
                Height = TileHeight,
                TileIndex = SelectedPaletteTile.Index,
                LayerName = ActiveLayerName
            });
            return;
        }

        if (ActiveTool == "Entity")
        {
            Entities.Add(new SceneEntityViewModel
            {
                Name = $"entity_{Entities.Count + 1}",
                X = worldX,
                Y = worldY
            });
        }
    }

    /// <summary>
    /// AddCollider.
    /// </summary>
    /// <param name="worldRect">worldRect.</param>
    public void AddCollider(Rect worldRect)
    {
        if (worldRect.Width <= 0 || worldRect.Height <= 0)
            return;

        Colliders.Add(new SceneColliderViewModel { Rect = worldRect });
    }

    /// <summary>
    /// LoadScene.
    /// </summary>
    /// <param name="scenePath">scenePath.</param>
    public void LoadScene(string scenePath)
    {
        var scene = SceneLoader.LoadAsset(scenePath);
        ScenePath = scenePath;
        PaintedTiles.Clear();
        Entities.Clear();
        Colliders.Clear();

        var layer = scene.Layers.FirstOrDefault();
        if (layer is not null)
        {
            ActiveLayerName = layer.Name;
            ActiveLayerParallax = layer.Parallax;

            var tilesheet = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(scenePath) ?? string.Empty, layer.Tilesheet));
            LoadTilesheet(tilesheet);

            foreach (var tile in layer.Tiles)
            {
                PaintedTiles.Add(new ScenePaintedTileViewModel
                {
                    GridX = tile.X,
                    GridY = tile.Y,
                    PixelX = tile.X * TileWidth,
                    PixelY = tile.Y * TileHeight,
                    Width = TileWidth,
                    Height = TileHeight,
                    TileIndex = tile.TileIndex,
                    LayerName = layer.Name
                });
            }
        }

        foreach (var entity in scene.Entities)
            Entities.Add(new SceneEntityViewModel { Name = entity.Name, X = entity.X, Y = entity.Y });

        foreach (var collider in scene.Colliders)
            Colliders.Add(new SceneColliderViewModel { Rect = new Rect(collider.X, collider.Y, collider.Width, collider.Height) });
    }

    /// <summary>
    /// SaveTo.
    /// </summary>
    /// <param name="scenePath">scenePath.</param>
    public void SaveTo(string scenePath)
    {
        var layer = new SceneLayerAsset
        {
            Name = ActiveLayerName,
            Parallax = ActiveLayerParallax,
            Tilesheet = Path.GetRelativePath(Path.GetDirectoryName(scenePath) ?? string.Empty, TilesheetPath).Replace('\\', '/'),
            Tiles = PaintedTiles.Select(t => new SceneLayerTileAsset
            {
                X = t.GridX,
                Y = t.GridY,
                TileIndex = t.TileIndex
            }).ToList()
        };

        var scene = new SceneAsset
        {
            Layers = [layer],
            Entities = Entities.Select(e => new SceneEntityAsset { Name = e.Name, X = (float)e.X, Y = (float)e.Y }).ToList(),
            Colliders = Colliders.Select(c => new SceneColliderAsset
            {
                X = (float)c.Rect.X,
                Y = (float)c.Rect.Y,
                Width = (float)c.Rect.Width,
                Height = (float)c.Rect.Height
            }).ToList()
        };

        File.WriteAllText(scenePath, JsonConvert.SerializeObject(scene, Formatting.Indented));
    }
}
