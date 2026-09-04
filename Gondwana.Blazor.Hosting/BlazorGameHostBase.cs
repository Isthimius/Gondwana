using Gondwana.Blazor;
using Gondwana.Blazor.Rendering;
using Gondwana.Hosting;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Microsoft.JSInterop;

namespace Gondwana.Blazor.Hosting;

/// <summary>
/// Provides the shared Gondwana lifecycle integration for Blazor bitmap and WebGL game hosts.
/// </summary>
/// <remarks>
/// Browser/WASM targets use JavaScript <c>requestAnimationFrame</c> to call
/// <see cref="OnAnimationFrame"/>. Non-browser targets retain the standard background engine
/// loop supplied by <see cref="GameHostBase"/>.
/// </remarks>
public abstract class BlazorGameHostBase : GameHostBase
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<BlazorGameHostBase>? _dotNetRef;
    private IJSObjectReference? _module;

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorGameHostBase"/>.
    /// </summary>
    /// <param name="jsRuntime">The JavaScript runtime used to drive browser animation frames.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsRuntime"/> is null.</exception>
    protected BlazorGameHostBase(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <summary>Gets the concrete Blazor component that supplies DOM input events.</summary>
    protected abstract BlazorRenderSurfaceComponentBase RenderSurfaceComponent { get; }

    /// <summary>Gets the render-surface host used for scene binding and widget input.</summary>
    protected abstract RenderSurfaceHostBase RenderSurfaceHost { get; }

    /// <summary>Binds a scene to the concrete typed render-surface host.</summary>
    /// <param name="scene">The scene to bind.</param>
    protected abstract void BindSceneCore(Scene scene);

    /// <inheritdoc/>
    protected sealed override void ConfigurePlatform()
    {
        OnConfigurePlatform();
    }

    /// <inheritdoc/>
    protected sealed override void ConfigureKeyboard()
    {
        Engine.InitializeBlazorKeyboardAdapter(RenderSurfaceComponent);
        OnKeyboardAdapterInitialized();
    }

    /// <inheritdoc/>
    protected sealed override void ConfigureMouse()
    {
        Engine.InitializeBlazorMouseAdapter(RenderSurfaceComponent);
        OnMouseAdapterInitialized();
    }

    /// <inheritdoc/>
    protected sealed override void ConfigureGamepads()
    {
        OnConfigureGamepads();
    }

    /// <inheritdoc/>
    protected sealed override void ConfigureTouch()
    {
        Engine.InitializeBlazorTouchAdapter(RenderSurfaceComponent);
        OnTouchAdapterInitialized();
    }

    /// <inheritdoc/>
    protected sealed override void BindScene()
    {
        var scene = Scene
            ?? throw new InvalidOperationException(
                $"{nameof(BindScene)} cannot be called before {nameof(Scene)} has been created.");

        BindSceneCore(scene);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <summary>Advances the timer-driven engine from a browser animation frame.</summary>
    [JSInvokable]
    public void OnAnimationFrame()
    {
        if (Engine.IsRunning)
            Engine.Tick();
    }

    /// <inheritdoc/>
    protected sealed override void StopEngineCore()
    {
        if (_module is not null)
            _ = _module.InvokeVoidAsync("stopRenderLoop");

        base.StopEngineCore();
    }

    /// <inheritdoc/>
    protected sealed override void OnInputConfigured()
    {
        base.OnInputConfigured();
        InitializeWidgetInput(RenderSurfaceHost);
    }

    /// <inheritdoc/>
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

    /// <summary>Provides a hook for configuring additional Blazor platform settings.</summary>
    protected virtual void OnConfigurePlatform()
    {
    }

    /// <summary>Called after the Blazor keyboard adapter is initialized.</summary>
    protected virtual void OnKeyboardAdapterInitialized()
    {
    }

    /// <summary>Called after the Blazor mouse adapter is initialized.</summary>
    protected virtual void OnMouseAdapterInitialized()
    {
    }

    /// <summary>Provides a hook for configuring browser gamepad support.</summary>
    protected virtual void OnConfigureGamepads()
    {
    }

    /// <summary>Called after the Blazor touch adapter is initialized.</summary>
    protected virtual void OnTouchAdapterInitialized()
    {
    }

    /// <summary>Runs after Blazor interop resources have been released.</summary>
    protected virtual void OnBlazorDisposed()
    {
    }
}
