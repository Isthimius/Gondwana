using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.Widgets;
using Microsoft.Extensions.Logging;

namespace Gondwana.Hosting;

/// <summary>
/// Provides a base class for hosting and managing the lifecycle of a Gondwana game application.
/// </summary>
public abstract class GameHostBase : IDisposable
{
    private bool _disposed;
    private bool _initialized;
    private bool _engineInitialized;
    private bool _engineStarted;

    private WidgetInputRouter? _widgetInputRouter;

    /// <summary>
    /// Gets the singleton instance of the Gondwana engine.
    /// </summary>
    public Engine Engine => Engine.Instance;

    /// <summary>
    /// Gets the current active scene.
    /// </summary>
    public Scene? Scene { get; protected set; }

    /// <summary>
    /// Gets the widget input router, if widget input has been initialized.
    /// </summary>
    protected WidgetInputRouter? WidgetInputRouter => _widgetInputRouter;

    /// <summary>
    /// Initializes the game host by configuring logging, platform services, input,
    /// game content, and the Gondwana engine.
    /// </summary>
    /// <param name="configPath">Optional path to the engine configuration file.</param>
    /// <param name="autoSaveConfig">
    /// Optional value indicating whether configuration changes should be saved automatically.
    /// </param>
    /// <param name="logLevel">
    /// The minimum log level used by Gondwana. The default is <see cref="LogLevel.Warning"/>.
    /// </param>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the host has already been disposed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the host has already been initialized or no synchronization context is available.
    /// </exception>
    public void Initialize(
        string? configPath = null,
        bool? autoSaveConfig = null,
        LogLevel logLevel = LogLevel.Warning)
    {
        EnsureNotDisposed();
        EnsureNotInitialized();

        OnInitializing();

        ConfigureLogging(logLevel);
        ConfigurePlatform();
        ConfigureInput();
        InitializeGameContent();

        InitializeEngine(configPath, autoSaveConfig);
        StartEngine();

        _initialized = true;

        OnInitialized();
    }

    /// <summary>
    /// Initializes widget input routing for the supplied render surface host.
    /// </summary>
    /// <param name="renderSurfaceHost">
    /// The render surface host whose widgets should receive keyboard, mouse, and touch input.
    /// </param>
    /// <remarks>
    /// Call this only after the render surface host and the desired input pollers have been created.
    /// Reinitializing widget input disposes the previously configured router.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="renderSurfaceHost"/> is <see langword="null"/>.
    /// </exception>
    protected void InitializeWidgetInput(RenderSurfaceHostBase renderSurfaceHost)
    {
        ArgumentNullException.ThrowIfNull(renderSurfaceHost);

        _widgetInputRouter?.Dispose();

        var keyboardPoller = Engine.Input.KeyboardEventPoller;
        var mousePoller = Engine.Input.MouseEventPoller;
        var touchPoller = Engine.Input.TouchEventPoller;

        mousePoller?.StartMonitoringMouse(
            trackMouseMovement: true);

        _widgetInputRouter = new WidgetInputRouter(
            renderSurfaceHost,
            keyboardPoller,
            mousePoller,
            touchPoller);

        _widgetInputRouter.Start();
    }

    /// <summary>
    /// Runs before the host initialization sequence begins.
    /// </summary>
    protected virtual void OnInitializing()
    {
    }

    /// <summary>
    /// Configures the logging level used by Gondwana.
    /// </summary>
    /// <param name="logLevel">The minimum log level to use.</param>
    protected void ConfigureLogging(
        LogLevel logLevel)
    {
        EngineLogger.SetLogLevel(logLevel);
    }

    /// <summary>
    /// Configures platform-specific services, adapters, and render infrastructure.
    /// </summary>
    protected abstract void ConfigurePlatform();

    /// <summary>
    /// Configures keyboard, mouse, gamepad, and touch input.
    /// </summary>
    protected void ConfigureInput()
    {
        ConfigureKeyboard();
        ConfigureMouse();
        ConfigureGamepads();
        ConfigureTouch();

        OnInputConfigured();
    }

    /// <summary>
    /// Configures the keyboard input adapter.
    /// </summary>
    protected virtual void ConfigureKeyboard()
    {
    }

    /// <summary>
    /// Configures the mouse input adapter.
    /// </summary>
    protected virtual void ConfigureMouse()
    {
    }

    /// <summary>
    /// Configures the gamepad manager.
    /// </summary>
    protected virtual void ConfigureGamepads()
    {
    }

    /// <summary>
    /// Configures the touch input adapter.
    /// </summary>
    protected virtual void ConfigureTouch()
    {
    }

    /// <summary>
    /// Runs after all input devices have been configured.
    /// </summary>
    /// <remarks>
    /// Platform hosts may override this hook to call
    /// <see cref="InitializeWidgetInput(RenderSurfaceHostBase)"/> once their render surface host is available.
    /// </remarks>
    protected virtual void OnInputConfigured()
    {
    }

    /// <summary>
    /// Initializes game-specific content, the scene graph, and scene objects.
    /// </summary>
    protected void InitializeGameContent()
    {
        LoadContent();
        CreateSceneGraph();
        BindScene();
        OnSceneBound();
        InitializeSceneObjects();
    }

    /// <summary>
    /// Loads assets, tilesheets, and animation cycles.
    /// </summary>
    protected void LoadContent()
    {
        LoadAssets();
        LoadTilesheets();
        LoadAnimationCycles();
    }

    /// <summary>
    /// Loads textures, sounds, and other game assets.
    /// </summary>
    protected virtual void LoadAssets()
    {
    }

    /// <summary>
    /// Loads tilesheet definitions.
    /// </summary>
    protected virtual void LoadTilesheets()
    {
    }

    /// <summary>
    /// Loads animation cycle definitions.
    /// </summary>
    protected virtual void LoadAnimationCycles()
    {
    }

    /// <summary>
    /// Creates the initial scene and its views.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="CreateInitialScene"/> returns <see langword="null"/>.
    /// </exception>
    protected void CreateSceneGraph()
    {
        Scene = CreateInitialScene()
            ?? throw new InvalidOperationException(
                $"{nameof(CreateInitialScene)} returned null.");

        CreateInitialViews();
        OnSceneGraphCreated();
    }

    /// <summary>
    /// Runs after the scene graph has been created but before the scene is bound.
    /// </summary>
    protected virtual void OnSceneGraphCreated()
    {
    }

    /// <summary>
    /// Creates the initial scene for the game.
    /// </summary>
    /// <returns>The initial scene.</returns>
    protected virtual Scene CreateInitialScene()
    {
        return Scene.Empty;
    }

    /// <summary>
    /// Creates the initial views and viewports used to render the scene.
    /// </summary>
    protected virtual void CreateInitialViews()
    {
    }

    /// <summary>
    /// Binds the current scene to the platform render surface.
    /// </summary>
    protected virtual void BindScene()
    {
    }

    /// <summary>
    /// Runs after the current scene has been bound to the render surface host.
    /// </summary>
    protected virtual void OnSceneBound()
    {
    }

    /// <summary>
    /// Creates sprites and direct drawings for the current scene.
    /// </summary>
    protected void InitializeSceneObjects()
    {
        CreateSprites();
        CreateDirectDrawings();
    }

    /// <summary>
    /// Creates sprite objects for the current scene.
    /// </summary>
    protected virtual void CreateSprites()
    {
    }

    /// <summary>
    /// Creates direct drawings and widgets for the current scene.
    /// </summary>
    protected virtual void CreateDirectDrawings()
    {
    }

    /// <summary>
    /// Initializes the Gondwana engine.
    /// </summary>
    /// <param name="configPath">Optional path to the engine configuration file.</param>
    /// <param name="autoSaveConfig">
    /// Optional value indicating whether configuration changes should be saved automatically.
    /// </param>
    protected void InitializeEngine(
        string? configPath,
        bool? autoSaveConfig)
    {
        Engine.Instance.Initialize(configPath, autoSaveConfig);
        _engineInitialized = true;

        OnEngineInitialized();
    }

    /// <summary>
    /// Runs after the engine has been initialized but before it has been started.
    /// </summary>
    protected virtual void OnEngineInitialized()
    {
    }

    /// <summary>
    /// Starts the engine using the synchronization context returned by
    /// <see cref="GetSynchronizationContext"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no synchronization context is available.
    /// </exception>
    protected void StartEngine()
    {
        var syncContext = GetSynchronizationContext()
            ?? throw new InvalidOperationException(
                $"{nameof(Initialize)} must be called on a thread with a current {nameof(SynchronizationContext)}.");

        StartEngineCore(syncContext);
        _engineStarted = true;

        OnEngineStarted();
    }

    /// <summary>
    /// Starts the engine using the supplied synchronization context.
    /// </summary>
    /// <param name="syncContext">The synchronization context used by the engine.</param>
    protected virtual void StartEngineCore(
        SynchronizationContext syncContext)
    {
        Engine.Instance.Start(syncContext);
    }

    /// <summary>
    /// Gets the synchronization context used to start the engine.
    /// </summary>
    /// <returns>The synchronization context, or <see langword="null"/> when unavailable.</returns>
    protected virtual SynchronizationContext? GetSynchronizationContext()
    {
        return SynchronizationContext.Current;
    }

    /// <summary>
    /// Runs after the engine has been started.
    /// </summary>
    protected virtual void OnEngineStarted()
    {
    }

    /// <summary>
    /// Runs after the full host initialization sequence has completed.
    /// </summary>
    protected virtual void OnInitialized()
    {
    }

    /// <summary>
    /// Unhooks custom event handlers during disposal.
    /// </summary>
    protected virtual void UnhookEvents()
    {
    }

    /// <summary>
    /// Stops the engine during host disposal.
    /// </summary>
    protected void StopEngine()
    {
        StopEngineCore();
        _engineStarted = false;
    }

    /// <summary>
    /// Performs platform-specific engine shutdown work.
    /// </summary>
    protected virtual void StopEngineCore()
    {
        Engine.Instance.Stop();
    }

    /// <summary>
    /// Disposes the engine instance.
    /// </summary>
    protected void DisposeEngine()
    {
        Engine.Instance.Dispose();
        _engineInitialized = false;
    }

    /// <summary>
    /// Runs before managed resources are disposed.
    /// </summary>
    protected virtual void OnDisposing()
    {
    }

    /// <summary>
    /// Runs after managed resources have been disposed.
    /// </summary>
    protected virtual void OnDisposed()
    {
    }

    /// <summary>
    /// Ensures that the host has not already been initialized.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the host has already been initialized.
    /// </exception>
    protected void EnsureNotInitialized()
    {
        if (_initialized)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} has already been initialized.");
        }
    }

    /// <summary>
    /// Ensures that the host has not been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the host has already been disposed.
    /// </exception>
    protected void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Releases resources owned by the game host.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        OnDisposing();

        _widgetInputRouter?.Dispose();
        _widgetInputRouter = null;

        UnhookEvents();

        if (_engineStarted)
            StopEngine();

        if (_engineInitialized)
            DisposeEngine();

        _disposed = true;

        OnDisposed();

        GC.SuppressFinalize(this);
    }
}