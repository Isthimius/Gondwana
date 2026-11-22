using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Input.Keyboard;
using Gondwana.Blazor.Rendering;
using Gondwana.Scenes;
using Gondwana.Widgets.Controls;
using Microsoft.JSInterop;
using SkiaSharp;

namespace Gondwana.Demos.Slider;

/// <summary>Browser-specific host for the shared Slider game runtime.</summary>
internal sealed class SliderBlazorGameHost : BlazorGpuGameHost
{
    private readonly SliderGameRuntime _runtime;
    private readonly byte[] _slideAudio;
    private readonly byte[] _victoryAudio;

    internal SliderBlazorGameHost(
        BlazorGpuRenderSurfaceComponent surface,
        IJSRuntime jsRuntime,
        ISliderScoreStore scoreStore,
        SKImage? aboutLogo,
        byte[] slideAudio,
        byte[] victoryAudio)
        : base(surface, jsRuntime)
    {
        _slideAudio = slideAudio;
        _victoryAudio = victoryAudio;
        _runtime = new SliderGameRuntime(
            surface.Host,
            () => WidgetInputRouter,
            scoreStore,
            aboutLogo,
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

    private void OnFramePresented() => _runtime.NotifyFramePresented();

    protected override void OnConfigurePlatform() => Engine.UseBrowserAudio();

    protected override void LoadAssets()
    {
        using var slideStream = new MemoryStream(_slideAudio, writable: false);
        using var victoryStream = new MemoryStream(_victoryAudio, writable: false);
        _runtime.SetAudioResources(
            Engine.Managers.AudioResources.LoadFromStream("slider-move", slideStream, ".wav"),
            Engine.Managers.AudioResources.LoadFromStream("slider-victory", victoryStream, ".wav"));
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
    protected override void OnInitialized() => _runtime.OnInitialized();

    protected override void UnhookEvents()
    {
        RenderSurface.FramePresented -= OnFramePresented;
        _runtime.UnhookEvents();
    }
}
