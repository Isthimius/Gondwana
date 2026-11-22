using System.Diagnostics;
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogs;
using SkiaSharp;

namespace Gondwana.Demos.Slider;

/// <summary>Windows-specific bridge for the platform-independent Slider runtime.</summary>
internal sealed class SliderGameHost : WinFormsGpuGameHost
{
    protected override bool EnableNativeTouch => true;
    private readonly SliderGameRuntime _runtime;

    internal SliderGameHost(WinFormGpuRenderSurfaceControl surface) : base(surface)
    {
        _runtime = new SliderGameRuntime(surface.Host,
            () => WidgetInputRouter, new SliderFileStorage(),
            LoadAboutLogo(), new SliderWindowsUriLauncher(),
            (width, height) => surface.Host.RequestLogicalResolution(width, height));
        _runtime.RequestResize(surface.ClientSize);
        surface.SizeChanged += OnSurfaceSizeChanged;
        surface.Adapter.FrameDiagnosticsCalculated += OnFramePresented;
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

    internal bool HasMenuOrDialogOpen => _runtime.HasMenuOrDialogOpen;
    internal void RequestPuzzle(byte[]? imageBytes, int columns, int rows)
        => _runtime.RequestPuzzle(imageBytes, columns, rows);
    internal void RequestRestore(SliderSave save) => _runtime.RequestRestore(save);
    internal void SetShiftHeld(bool held) => _runtime.SetShiftHeld(held);

    private static SKImage? LoadAboutLogo()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", "gondwana-logo-text.png");
        using var bitmap = File.Exists(path) ? SKBitmap.Decode(path) : null;
        return bitmap is null ? null : SKImage.FromBitmap(bitmap);
    }

    private void OnSurfaceSizeChanged(object? sender, EventArgs args)
        => _runtime.RequestResize(RenderSurface.ClientSize);

    private void OnFramePresented(WinFormGpuFrameDiagnostics _)
        => _runtime.NotifyFramePresented();

    protected override void LoadAssets()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "assets");
        _runtime.SetAudioResources(
            Engine.Managers.AudioResources.LoadFromFile(
                "slider-move", Path.Combine(root,
                "75143__willc2-45220__slide-cup-16b-44k-0-747s.wav")),
            Engine.Managers.AudioResources.LoadFromFile(
                "slider-victory", Path.Combine(root,
                "177120__rdholder__2dogsound-tadaa1-3s-2013jan31-cc-by-30-us.wav")));
    }

    protected override Scene CreateInitialScene() => _runtime.CreateInitialScene();
    protected override void OnSceneBound() => _runtime.OnSceneBound();
    protected override void CreateSprites() => _runtime.CreateSprites();
    protected override void CreateDirectDrawings() => _runtime.CreateDirectDrawings();

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        foreach (Keys key in new[]
        {
            Keys.Tab, Keys.Enter, Keys.Escape, Keys.Space,
            Keys.Up, Keys.Down, Keys.Left, Keys.Right,
            Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown,
            Keys.A, Keys.G, Keys.H, Keys.L, Keys.N, Keys.O,
            Keys.P, Keys.S, Keys.U, Keys.X
        })
            keyboard.StartMonitoringKey((int)key);
        _runtime.OnKeyboardAdapterInitialized();
    }

    protected override void OnEngineInitialized() => _runtime.OnEngineInitialized();
    protected override void OnInitialized() => _runtime.OnInitialized();

    protected override void UnhookEvents()
    {
        RenderSurface.SizeChanged -= OnSurfaceSizeChanged;
        RenderSurface.Adapter.FrameDiagnosticsCalculated -= OnFramePresented;
        _runtime.UnhookEvents();
    }
}

internal sealed class SliderWindowsUriLauncher : IExternalUriLauncher
{
    public ValueTask OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });
        return ValueTask.CompletedTask;
    }
}
