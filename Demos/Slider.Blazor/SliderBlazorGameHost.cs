using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Input.Keyboard;
using Gondwana.Blazor.Rendering;
using Gondwana.Scenes;
using Microsoft.JSInterop;
using SkiaSharp;

namespace Gondwana.Demos.Slider;

/// <summary>Browser-specific host for the shared Slider game runtime.</summary>
internal sealed class SliderBlazorGameHost : BlazorGpuGameHost
{
    private readonly SliderGameRuntime _runtime;
    private readonly AssetsFile _assets;

    internal SliderBlazorGameHost(
        BlazorGpuRenderSurfaceComponent surface,
        IJSRuntime jsRuntime,
        ISliderScoreStore scoreStore,
        Func<SKImage?> loadAboutLogo,
        AssetsFile assets)
        : base(surface, jsRuntime)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _runtime = new SliderGameRuntime(
            surface.Host,
            () => WidgetInputRouter,
            scoreStore,
            loadAboutLogo,
            new Gondwana.Blazor.BlazorExternalUriLauncher(jsRuntime),
            (width, height) => surface.Host.RequestLogicalResolution(width, height),
            new SliderArrowKeys(
                (int)BlazorKey.ArrowLeft,
                (int)BlazorKey.ArrowRight,
                (int)BlazorKey.ArrowUp,
                (int)BlazorKey.ArrowDown));
        surface.FramePresented += OnFramePresented;
    }

    internal event Action<int, int>? OpenPictureRequested
    {
        add => _runtime.OpenPictureRequested += value;
        remove => _runtime.OpenPictureRequested -= value;
    }

    internal event Action<SliderSave>? SaveGameRequested
    {
        add => _runtime.SaveGameRequested += value;
        remove => _runtime.SaveGameRequested -= value;
    }

    internal event Action? LoadGameRequested
    {
        add => _runtime.LoadGameRequested += value;
        remove => _runtime.LoadGameRequested -= value;
    }

    internal event Action? ExitRequested
    {
        add => _runtime.ExitRequested += value;
        remove => _runtime.ExitRequested -= value;
    }

    internal void RequestPuzzle(byte[] imageBytes, int columns, int rows)
        => _runtime.RequestPuzzle(imageBytes, columns, rows);

    internal void RequestRestore(SliderSave savedGame) => _runtime.RequestRestore(savedGame);
    internal void RequestResize(System.Drawing.Size size) => _runtime.RequestResize(size);
    internal void SetShiftHeld(bool held) => _runtime.SetShiftHeld(held);

    private void OnFramePresented(bool sceneRendered)
    {
        // Cached GPU re-blits do not prove that the loading widgets were drawn.
        if (sceneRendered)
            _runtime.NotifyFramePresented();
    }

    protected override void OnConfigurePlatform() => Engine.UseBrowserAudio();

    protected override void LoadAssets()
    {
        _runtime.SetAudioResources(
            LoadAudio("slider-move", "75143__willc2-45220__slide-cup-16b-44k-0-747s.wav"),
            LoadAudio("slider-victory", "177120__rdholder__2dogsound-tadaa1-3s-2013jan31-cc-by-30-us.wav"));
    }

    // Identical resource lookup and stream loading pattern to Spot.Blazor.
    private AudioResource LoadAudio(string key, string assetName)
    {
        using var stream = _assets.Get(AssetTypes.Audio, assetName)
            ?? throw new InvalidDataException($"Slider audio asset '{assetName}' is missing from slider.gaf.");
        return Engine.Managers.AudioResources.LoadFromStream(
            key, stream, Path.GetExtension(assetName));
    }

    protected override Scene CreateInitialScene() => _runtime.CreateInitialScene();
    protected override void OnSceneBound() => _runtime.OnSceneBound();
    protected override void CreateSprites() => _runtime.CreateSprites();
    protected override void CreateDirectDrawings() => _runtime.CreateDirectDrawings();

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        foreach (BlazorKey key in new[]
        {
            BlazorKey.Tab, BlazorKey.Enter, BlazorKey.Escape, BlazorKey.Space,
            BlazorKey.ArrowUp, BlazorKey.ArrowDown,
            BlazorKey.ArrowLeft, BlazorKey.ArrowRight,
            BlazorKey.Home, BlazorKey.End, BlazorKey.PageUp, BlazorKey.PageDown,
            BlazorKey.KeyA, BlazorKey.KeyG, BlazorKey.KeyH,
            BlazorKey.KeyL, BlazorKey.KeyN, BlazorKey.KeyO,
            BlazorKey.KeyP, BlazorKey.KeyS, BlazorKey.KeyU, BlazorKey.KeyX
        })
            keyboard.StartMonitoringKey((int)key);
        _runtime.OnKeyboardAdapterInitialized();
    }

    protected override void OnEngineInitialized() => _runtime.OnEngineInitialized();
    protected override void OnInitialized()
    {
        if (WidgetInputRouter is not null)
            WidgetInputRouter.KeyboardCodeMapper = ToWidgetVirtualKey;

        _runtime.OnInitialized();
    }

    // Widgets currently use Windows virtual keys for navigation and mnemonics.
    // Only widget routing needs translation; gameplay receives native BlazorKey values.
    private static int ToWidgetVirtualKey(int key)
    {
        var browserKey = (BlazorKey)key;
        if (browserKey is >= BlazorKey.KeyA and <= BlazorKey.KeyZ)
            return 'A' + key - (int)BlazorKey.KeyA;

        if (browserKey is >= BlazorKey.Digit1 and <= BlazorKey.Digit9)
            return '1' + key - (int)BlazorKey.Digit1;

        return browserKey switch
        {
            BlazorKey.Digit0 => '0',
            BlazorKey.ArrowLeft => 37,
            BlazorKey.ArrowUp => 38,
            BlazorKey.ArrowRight => 39,
            BlazorKey.ArrowDown => 40,
            BlazorKey.Enter => 13,
            BlazorKey.Escape => 27,
            BlazorKey.Space => 32,
            BlazorKey.Tab => 9,
            BlazorKey.Home => 36,
            BlazorKey.End => 35,
            BlazorKey.PageUp => 33,
            BlazorKey.PageDown => 34,
            BlazorKey.Backspace => 8,
            BlazorKey.Delete => 46,
            _ => key
        };
    }

    protected override void UnhookEvents()
    {
        RenderSurface.FramePresented -= OnFramePresented;
        _runtime.UnhookEvents();
    }

    protected override void OnBlazorDisposed() => _assets.Dispose();
}
