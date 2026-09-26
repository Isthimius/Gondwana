using Gondwana.Assets;
using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Rendering;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Microsoft.JSInterop;

namespace Gondwana.Demos.Spot;

/// <summary>Hosts the Spot demo on Gondwana's Blazor WebGL runtime.</summary>
internal sealed partial class SpotGameHost : BlazorGpuGameHost
{
    private const int PersistentMenuHeight = 32;
    private readonly AssetsFile _assets;

    private Scene ActiveScene => Scene
        ?? throw new InvalidOperationException("Spot scene has not been created.");

    private RenderSurfaceHostBase SurfaceHost => RenderSurface.Host;
    private int SurfaceWidth => SurfaceHost.Backbuffer.Width;
    private int SurfaceHeight => SurfaceHost.Backbuffer.Height;

    internal SpotGameHost(
        BlazorGpuRenderSurfaceComponent renderSurface,
        IJSRuntime jsRuntime,
        AssetsFile assets)
        : base(renderSurface, jsRuntime)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
    }

    protected override void OnConfigurePlatform()
    {
        Engine.UseBrowserAudio();
    }

    protected override void CreateDirectDrawings()
    {
        // Startup presentation is created after the splash completes.
    }

    protected override void OnEngineStarted()
    {
        // Music starts after the splash completes.
    }

    protected override void OnBlazorDisposed()
    {
        _assets.Dispose();
    }
}
