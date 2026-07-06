using Gondwana.Blazor;
using Gondwana.Blazor.Rendering;
using Gondwana.Hosting;
using Microsoft.JSInterop;

namespace Gondwana.Blazor.Hosting;

/// <summary>
/// Provides a base class for hosting Gondwana games in Blazor applications.
/// </summary>
/// <remarks>
/// <para>
/// On browser/WASM targets, the engine runs in timer-driven mode via JavaScript's
/// requestAnimationFrame because Blazor WebAssembly does not support the standard
/// background-thread engine loop.
/// </para>
/// <para>
/// On non-browser targets, such as Blazor Server, the standard background-thread
/// engine loop is used.
/// </para>
/// </remarks>
public abstract class BlazorGameHost : GameHostBase
{
    private readonly IJSRuntime _jsRuntime;

    private DotNetObjectReference<BlazorGameHost>? _dotNetRef;
    private IJSObjectReference? _module;

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
    protected BlazorGameHost(
        BlazorBitmapRenderSurfaceComponent renderSurface,
        IJSRuntime jsRuntime)
    {
        RenderSurface = renderSurface ?? throw new ArgumentNullException(nameof(renderSurface));
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <summary>
    /// Configures Blazor-specific platform features.
    /// </summary>
    protected sealed override void ConfigurePlatform()
    {
        OnConfigurePlatform();
    }

    /// <summary>
    /// Configures the keyboard adapter for the Blazor render surface.
    /// </summary>
    protected sealed override void ConfigureKeyboard()
    {
        Engine.InitializeBlazorKeyboardAdapter(RenderSurface);
        OnKeyboardAdapterInitialized();
    }

    /// <summary>
    /// Configures the mouse adapter for the Blazor render surface.
    /// </summary>
    protected sealed override void ConfigureMouse()
    {
        Engine.InitializeBlazorMouseAdapter(RenderSurface);
        OnMouseAdapterInitialized();
    }

    /// <summary>
    /// Configures gamepad support.
    /// </summary>
    protected sealed override void ConfigureGamepads()
    {
        OnConfigureGamepads();
    }

    /// <summary>
    /// Configures the touch adapter for the Blazor render surface.
    /// </summary>
    protected sealed override void ConfigureTouch()
    {
        Engine.InitializeBlazorTouchAdapter(RenderSurface);
        OnTouchAdapterInitialized();
    }

    /// <summary>
    /// Binds the current scene to the Blazor render surface host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no scene has been created.</exception>
    protected sealed override void BindScene()
    {
        var scene = Scene
            ?? throw new InvalidOperationException(
                $"{nameof(BindScene)} cannot be called before {nameof(Scene)} has been created.");

        RenderSurface.Host.Bind(scene, false);
    }

    /// <summary>
    /// Gets the synchronization context used to start the engine.
    /// </summary>
    /// <returns>The synchronization context used by the engine.</returns>
    protected sealed override SynchronizationContext? GetSynchronizationContext()
    {
        var syncContext = base.GetSynchronizationContext();

        if (syncContext is not null)
            return syncContext;

        if (!OperatingSystem.IsBrowser())
            return null;

        syncContext = new SynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(syncContext);

        return syncContext;
    }

    /// <summary>
    /// Starts the engine using the platform-appropriate Blazor execution model.
    /// </summary>
    /// <param name="syncContext">The synchronization context used by the engine.</param>
    protected sealed override void StartEngineCore(SynchronizationContext syncContext)
    {
        if (OperatingSystem.IsBrowser())
        {
            Engine.StartTimerDriven(syncContext);
            _ = StartBrowserRenderLoopAsync();
            return;
        }

        base.StartEngineCore(syncContext);
    }

    private async Task StartBrowserRenderLoopAsync()
    {
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/Gondwana.Blazor/gondwana-blazor.js");

        _module = module;
        _dotNetRef = DotNetObjectReference.Create(this);

        await module.InvokeVoidAsync("startRenderLoop", _dotNetRef);
    }

    /// <summary>
    /// Called by JavaScript on each animation frame.
    /// </summary>
    [JSInvokable]
    public void OnAnimationFrame()
    {
        if (Engine.IsRunning)
        {
            Engine.Tick();
        }
    }

    /// <summary>
    /// Stops the engine and, on browser/WASM targets, stops the JavaScript render loop.
    /// </summary>
    protected sealed override void StopEngineCore()
    {
        if (_module is not null)
        {
            _ = _module.InvokeVoidAsync("stopRenderLoop");
        }

        base.StopEngineCore();
    }

    /// <summary>
    /// Releases Blazor interop resources after the host has been disposed.
    /// </summary>
    protected sealed override void OnDisposed()
    {
        _dotNetRef?.Dispose();
        _dotNetRef = null;

        if (_module is not null)
        {
            _ = _module.DisposeAsync();
            _module = null;
        }

        OnBlazorDisposed();
    }

    /// <summary>
    /// Provides a hook for configuring additional Blazor-specific platform settings during initialization.
    /// </summary>
    protected virtual void OnConfigurePlatform()
    {
    }

    /// <summary>
    /// Called after the keyboard adapter has been initialized.
    /// </summary>
    protected virtual void OnKeyboardAdapterInitialized()
    {
    }

    /// <summary>
    /// Called after the mouse adapter has been initialized.
    /// </summary>
    protected virtual void OnMouseAdapterInitialized()
    {
    }

    /// <summary>
    /// Provides a hook for configuring gamepad support.
    /// </summary>
    protected virtual void OnConfigureGamepads()
    {
    }

    /// <summary>
    /// Called after the touch adapter has been initialized.
    /// </summary>
    protected virtual void OnTouchAdapterInitialized()
    {
    }

    /// <summary>
    /// Runs after Blazor-specific disposal has completed.
    /// </summary>
    protected virtual void OnBlazorDisposed()
    {
    }
}