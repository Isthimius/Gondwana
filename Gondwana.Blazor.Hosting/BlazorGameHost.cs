using Gondwana.Blazor;
using Gondwana.Blazor.Rendering;
using Gondwana.Hosting;
using Gondwana.Rendering;
using Microsoft.JSInterop;
namespace Gondwana.Blazor.Hosting;

/// <summary>
/// Provides a base class for hosting Gondwana games in Blazor WebAssembly applications.
/// </summary>
/// <remarks>
/// <para>
/// On browser/WASM targets the engine runs in timer-driven mode via JavaScript's requestAnimationFrame,
/// because Blazor WebAssembly does not support background threads. On non-browser targets (e.g. Blazor Server)
/// the standard background-thread engine loop is used.
/// </para>
/// <para>
/// Usage: derive from this class, override <see cref="GameHostBase.CreateInitialScene"/>,
/// <see cref="GameHostBase.CreateSprites"/>, etc., then construct your subclass with the
/// <see cref="BlazorBitmapRenderSurfaceComponent"/> instance from your Blazor page and call
/// <see cref="GameHostBase.Initialize"/>.
/// </para>
/// </remarks>
public abstract class BlazorGameHost : GameHostBase
{
    private DotNetObjectReference<BlazorGameHost>? _dotNetRef;
    private IJSObjectReference? _module;
    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// Gets the render surface component used for displaying game content.
    /// </summary>
    public BlazorBitmapRenderSurfaceComponent RenderSurface { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorGameHost"/> class.
    /// </summary>
    /// <param name="renderSurface">The render surface component to use for rendering.</param>
    /// <param name="jsRuntime">The JavaScript runtime for interop.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurface"/> or <paramref name="jsRuntime"/> is null.</exception>
    protected BlazorGameHost(BlazorBitmapRenderSurfaceComponent renderSurface, IJSRuntime jsRuntime)
    {
        RenderSurface = renderSurface ?? throw new ArgumentNullException(nameof(renderSurface));
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <summary>
    /// Configures Blazor-specific platform features.
    /// </summary>
    protected override void ConfigurePlatform()
    {
        OnConfigurePlatform();
    }

    /// <summary>
    /// Configures the keyboard adapter for the Blazor render surface.
    /// </summary>
    protected override void ConfigureKeyboard()
    {
        Engine.Instance.InitializeBlazorKeyboardAdapter(RenderSurface);
        OnKeyboardAdapterInitialized();
    }

    /// <summary>
    /// Configures the mouse adapter for the Blazor render surface.
    /// </summary>
    protected override void ConfigureMouse()
    {
        Engine.Instance.InitializeBlazorMouseAdapter(RenderSurface);
        OnMouseAdapterInitialized();
    }

    /// <summary>
    /// Configures gamepad support. Override to provide platform-specific gamepad integration.
    /// </summary>
    protected override void ConfigureGamepads()
    {
        OnGamepadManagerInitialized();
    }

    /// <summary>
    /// Configures the touch adapter for the Blazor render surface.
    /// </summary>
    protected override void ConfigureTouch()
    {
        Engine.Instance.InitializeBlazorTouchAdapter(RenderSurface);
        OnTouchAdapterInitialized();
    }

    /// <summary>
    /// Binds the current scene to the render surface host.
    /// </summary>
    protected override void BindScene()
    {
        RenderSurface.Host.Bind(Scene!, false);
    }

    /// <inheritdoc/>
    protected override RenderSurfaceHostBase GetPrimaryRenderSurfaceHost() => RenderSurface.Host;

    /// <summary>
    /// Starts the engine. On browser/WASM targets, uses a JavaScript requestAnimationFrame loop.
    /// On all other targets, the default background-thread loop is used.
    /// </summary>
    protected override void StartEngine()
    {
        var syncContext = SynchronizationContext.Current;

        if (OperatingSystem.IsBrowser())
        {
            // For browser/WASM, ensure we have a context (create a default if needed)
            if (syncContext == null)
            {
                syncContext = new SynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(syncContext);
            }

            Engine.Instance.StartTimerDriven(syncContext);

            // Start the requestAnimationFrame loop
            _ = StartBrowserRenderLoopAsync();
        }
        else
        {
            // Non-browser platforms require a SynchronizationContext
            if (syncContext == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(Initialize)} must be called on a thread with a current {nameof(SynchronizationContext)}.");
            }

            Engine.Instance.Start(syncContext);
        }

        base.OnStartEngine();
    }

    private async Task StartBrowserRenderLoopAsync()
    {
        // Get the JS module
        var js = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Gondwana.Blazor/gondwana-blazor.js");
        
        _module = js;
        _dotNetRef = DotNetObjectReference.Create(this);

        // Start the JavaScript requestAnimationFrame loop
        await js.InvokeVoidAsync("startRenderLoop", _dotNetRef);
    }

    /// <summary>
    /// Called by JavaScript on each animation frame.
    /// </summary>
    [JSInvokable]
    public void OnAnimationFrame()
    {
        if (Engine.Instance.IsRunning)
        {
            Engine.Instance.Tick();
        }
    }

    /// <summary>
    /// Stops the engine and, on browser/WASM targets, stops the render loop.
    /// </summary>
    protected override void StopEngine()
    {
        if (_module != null)
        {
            _ = _module.InvokeVoidAsync("stopRenderLoop");
        }

        base.StopEngine();
    }

    /// <summary>
    /// Disposes resources used by this host.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dotNetRef?.Dispose();
            _dotNetRef = null;
            
            _ = _module?.DisposeAsync();
            _module = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Provides a hook for configuring platform-specific settings during initialization.
    /// </summary>
    protected virtual void OnConfigurePlatform() { }

    /// <summary>
    /// Called after the keyboard adapter has been initialized. Override to perform additional keyboard setup.
    /// </summary>
    protected virtual void OnKeyboardAdapterInitialized() { }

    /// <summary>
    /// Called after the mouse adapter has been initialized. Override to perform additional mouse setup.
    /// </summary>
    protected virtual void OnMouseAdapterInitialized() { }

    /// <summary>
    /// Called after the gamepad manager has been initialized. Override to perform additional gamepad setup.
    /// </summary>
    protected virtual void OnGamepadManagerInitialized() { }

    /// <summary>
    /// Called after the touch adapter has been initialized. Override to perform additional touch setup.
    /// </summary>
    protected virtual void OnTouchAdapterInitialized() { }
}
