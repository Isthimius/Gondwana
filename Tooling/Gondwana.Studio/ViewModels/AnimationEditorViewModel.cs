using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gondwana.StudioAssets;
using Newtonsoft.Json;

namespace Gondwana.Studio.ViewModels;

public sealed partial class AnimationEditorViewModel : ViewModelBase
{
    private readonly Window _owner;
    private readonly DispatcherTimer _previewTimer;
    private int _previewFrameIndex;
    private DateTime _lastFrameTime;

    public ObservableCollection<TileCellViewModel> TilePalette { get; } = [];
    public ObservableCollection<AnimationFrameViewModel> Frames { get; } = [];

    [ObservableProperty]
    private string _animationName = "animation";

    [ObservableProperty]
    private string _tilesheetPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "No tilesheet selected.";

    [ObservableProperty]
    private TileCellViewModel? _selectedPaletteTile;

    [ObservableProperty]
    private AnimationFrameViewModel? _selectedFrame;

    [ObservableProperty]
    private string _cycleType = "Loop";

    [ObservableProperty]
    private string _previewText = "Preview idle";

    [ObservableProperty]
    private bool _isPreviewPlaying;

    public AnimationEditorViewModel(Window owner)
    {
        _owner = owner;
        _previewTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Normal, (_, _) => TickPreview());
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
        if (string.IsNullOrWhiteSpace(path))
            return;

        LoadTilesheet(path);
    }

    [RelayCommand]
    private void AddFrame()
    {
        if (SelectedPaletteTile is null)
            return;

        Frames.Add(new AnimationFrameViewModel
        {
            TileIndex = SelectedPaletteTile.Index,
            TileName = string.IsNullOrWhiteSpace(SelectedPaletteTile.Name) ? $"Tile {SelectedPaletteTile.Index}" : SelectedPaletteTile.Name,
            DurationMs = 100
        });
    }

    [RelayCommand]
    private void RemoveFrame()
    {
        if (SelectedFrame is null)
            return;

        Frames.Remove(SelectedFrame);
    }

    [RelayCommand]
    private void MoveFrameUp()
    {
        if (SelectedFrame is null)
            return;

        var i = Frames.IndexOf(SelectedFrame);
        if (i <= 0)
            return;

        Frames.Move(i, i - 1);
    }

    [RelayCommand]
    private void MoveFrameDown()
    {
        if (SelectedFrame is null)
            return;

        var i = Frames.IndexOf(SelectedFrame);
        if (i < 0 || i >= Frames.Count - 1)
            return;

        Frames.Move(i, i + 1);
    }

    [RelayCommand]
    private void StartPreview()
    {
        if (Frames.Count == 0)
            return;

        IsPreviewPlaying = true;
        _previewFrameIndex = 0;
        _lastFrameTime = DateTime.UtcNow;
        _previewTimer.Start();
        UpdatePreviewText();
    }

    [RelayCommand]
    private void StopPreview()
    {
        IsPreviewPlaying = false;
        _previewTimer.Stop();
        PreviewText = "Preview stopped";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Frames.Count == 0 || string.IsNullOrWhiteSpace(TilesheetPath))
            return;

        var saveTarget = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Animation",
            SuggestedFileName = AnimationName,
            DefaultExtension = "gondwana-animation",
            FileTypeChoices =
            [
                new FilePickerFileType("Gondwana Animation") { Patterns = ["*.gondwana-animation"] }
            ]
        });

        if (saveTarget is null)
            return;

        var path = saveTarget.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        SaveTo(path);
        StatusText = $"Saved animation: {path}";
    }

    public void LoadTilesheet(string tilesheetMetadataPath)
    {
        TilesheetPath = tilesheetMetadataPath;
        TilePalette.Clear();

        var metadata = TilesheetMetadataLoader.Load(tilesheetMetadataPath);
        var tileCount = metadata.Tiles.Count > 0 ? metadata.Tiles.Max(t => t.Index) + 1 : 0;
        if (tileCount == 0)
        {
            var sheet = TilesheetMetadataLoader.LoadAndRegisterTilesheet(tilesheetMetadataPath);
            var columns = Math.Max(1, sheet.SkBitmap.Width / Math.Max(1, metadata.TileWidth));
            var rows = Math.Max(1, sheet.SkBitmap.Height / Math.Max(1, metadata.TileHeight));
            tileCount = columns * rows;
        }

        var nameLookup = metadata.Tiles.ToDictionary(t => t.Index, t => t.Name);
        for (var i = 0; i < tileCount; i++)
        {
            TilePalette.Add(new TileCellViewModel
            {
                Index = i,
                Name = nameLookup.TryGetValue(i, out var name) ? name : $"Tile {i}"
            });
        }

        StatusText = $"Loaded {tileCount} tiles from {Path.GetFileName(tilesheetMetadataPath)}";
    }

    public void LoadAnimation(string animationPath)
    {
        var animation = AnimationAssetLoader.Load(animationPath);
        var tilesheetPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(animationPath) ?? string.Empty, animation.TilesheetPath));
        LoadTilesheet(tilesheetPath);

        AnimationName = animation.Name;
        CycleType = animation.CycleType;
        Frames.Clear();
        foreach (var frame in animation.Frames)
        {
            var name = TilePalette.FirstOrDefault(t => t.Index == frame.TileIndex)?.Name ?? $"Tile {frame.TileIndex}";
            Frames.Add(new AnimationFrameViewModel
            {
                TileIndex = frame.TileIndex,
                DurationMs = frame.DurationMs,
                TileName = name
            });
        }

        StatusText = $"Loaded animation: {Path.GetFileName(animationPath)}";
    }

    public void SaveTo(string path)
    {
        var animationDir = Path.GetDirectoryName(path) ?? string.Empty;
        var model = new AnimationAsset
        {
            Name = AnimationName,
            CycleType = CycleType,
            TilesheetPath = Path.GetRelativePath(animationDir, TilesheetPath).Replace('\\', '/'),
            Frames = Frames.Select(f => new AnimationFrameAsset
            {
                TileIndex = f.TileIndex,
                DurationMs = Math.Max(1, f.DurationMs)
            }).ToList()
        };

        File.WriteAllText(path, JsonConvert.SerializeObject(model, Formatting.Indented));
    }

    private void TickPreview()
    {
        if (!IsPreviewPlaying || Frames.Count == 0)
            return;

        var current = Frames[_previewFrameIndex];
        var elapsedMs = (DateTime.UtcNow - _lastFrameTime).TotalMilliseconds;
        if (elapsedMs < Math.Max(1, current.DurationMs))
            return;

        _lastFrameTime = DateTime.UtcNow;
        _previewFrameIndex = GetNextFrameIndex(_previewFrameIndex);
        UpdatePreviewText();
    }

    private int GetNextFrameIndex(int currentIndex)
    {
        if (Frames.Count == 0)
            return 0;

        return CycleType switch
        {
            "Once" => Math.Min(Frames.Count - 1, currentIndex + 1),
            "PingPong" => (currentIndex + 1) % Frames.Count,
            _ => (currentIndex + 1) % Frames.Count
        };
    }

    private void UpdatePreviewText()
    {
        if (Frames.Count == 0)
        {
            PreviewText = "Preview idle";
            return;
        }

        var frame = Frames[_previewFrameIndex];
        PreviewText = $"▶ Frame {_previewFrameIndex + 1}/{Frames.Count} — Tile {frame.TileIndex} ({frame.DurationMs}ms)";
    }
}
