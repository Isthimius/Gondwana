using Gondwana.Blazor.Rendering;
using Gondwana.Hosting;
using Gondwana.Rendering;

namespace Gondwana.Blazor.Hosting;

/// <summary>
/// Provides a base class for hosting Gondwana games in Blazor WebAssembly applications.
/// </summary>
/// <remarks>
/// <para>
/// On browser/WASM targets the engine runs in timer-driven mode via <see cref="PeriodicTimer"/>,
/// because Blazor WebAssembly does not support background threads. The timer targets 60 frames
/// per second by default. On non-browser targets (e.g. Blazor Server) the standard background-
/// thread engine loop is used.
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
    private PeriodicTimer? _engineTimer;

    /// <summary>
    /// Gets the render surface component used for displaying game content.
    /// </summary>
    public BlazorBitmapRenderSurfaceComponent RenderSurface { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorGameHost"/> class.
    /// </summary>
    /// <param name="renderSurface">The render surface component to use for rendering.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurface"/> is null.</exception>
    protected BlazorGameHost(BlazorBitmapRenderSurfaceComponent renderSurface)
    {
        RenderSurface = renderSurface ?? throw new ArgumentNullException(nameof(renderSurface));
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
    /// Starts the engine. On browser/WASM targets, uses a timer-driven loop via
    /// <see cref="PeriodicTimer"/>. On all other targets, the default background-thread
    /// loop is used.
    /// </summary>
    protected override void StartEngine()
    {
        var syncContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException(
                $"{nameof(Initialize)} must be called on a thread with a current {nameof(SynchronizationContext)}.");

        if (OperatingSystem.IsBrowser())
        {
            Engine.Instance.StartTimerDriven(syncContext);

            _engineTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000.0 / 60.0));
            _ = RunEngineLoopAsync(_engineTimer);
        }
        else
        {
            Engine.Instance.Start(syncContext);
        }

        OnStartEngine();
    }

    private static async Task RunEngineLoopAsync(PeriodicTimer timer)
    {
        while (await timer.WaitForNextTickAsync())
        {
            if (!Engine.Instance.IsRunning) break;
            Engine.Instance.Tick();
        }
    }

    /// <summary>
    /// Stops the engine and, on browser/WASM targets, stops the timer that was driving the loop.
    /// </summary>
    protected override void StopEngine()
    {
        _engineTimer?.Dispose();
        _engineTimer = null;

        base.StopEngine();
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
