using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Microsoft.Extensions.Logging;

namespace Gondwana.Hosting;

/// <summary>
/// Provides a base class for hosting and managing the lifecycle of a Gondwana game application.
/// </summary>
public abstract class GameHostBase : IDisposable
{
    /// <summary>
    /// Gets the singleton instance of the engine.
    /// </summary>
    public Engine Engine => Engine.Instance;

    /// <summary>
    /// Gets the current active scene.
    /// </summary>
    public Scene? Scene { get; protected set; }

    private bool _disposed;

    /// <summary>
    /// Initializes the game host by setting up logging, platform, input, game content, and then the engine.
    /// </summary>
    /// <param name="configPath">Optional path to the configuration file.</param>
    /// <param name="autoSaveConfig">Optional flag indicating whether to automatically save configuration changes.</param>
    /// <param name="logLevel">The log level to use for engine logging. Default is <see cref="LogLevel.Warning"/>.</param>
    public void Initialize(string? configPath = null, bool? autoSaveConfig = null, LogLevel logLevel = LogLevel.Warning)
    {
        EnsureNotDisposed();

        ConfigureLogging(logLevel);
        ConfigurePlatform();
        ConfigureInput();
        InitializeGameContent();

        InitializeEngine(configPath, autoSaveConfig);

        StartEngine();
    }

    /// <summary>
    /// Initializes the game host, then shows a Gondwana-native splash screen (if one is provided
    /// by <see cref="CreateSplash"/>) that fades in, holds, and fades out over the running game.
    /// </summary>
    /// <param name="configPath">Optional path to the configuration file.</param>
    /// <param name="autoSaveConfig">Optional flag indicating whether to automatically save configuration changes.</param>
    /// <param name="logLevel">The log level to use for engine logging. Default is <see cref="LogLevel.Warning"/>.</param>
    /// <remarks>
    /// <para>
    /// Initialization is performed synchronously (identical to <see cref="Initialize"/>), after which
    /// the engine is already running.  The splash is then created—as a <see cref="Gondwana.Drawing.Direct.DirectImage"/>
    /// overlay on the primary render surface—and animated using the engine's own fade system, making
    /// it fully platform-agnostic and reusable across projects.
    /// </para>
    /// <para>
    /// Override <see cref="CreateSplash"/> in a subclass to provide a custom splash image.
    /// Override <see cref="GetPrimaryRenderSurfaceHost"/> in platform-specific subclasses to expose
    /// the render surface host that the splash will be attached to.
    /// </para>
    /// </remarks>
    public async Task InitializeAsync(string? configPath = null, bool? autoSaveConfig = null, LogLevel logLevel = LogLevel.Warning)
    {
        Initialize(configPath, autoSaveConfig, logLevel);

        var host = GetPrimaryRenderSurfaceHost();
        if (host == null || host.ViewManager.Views.Count == 0)
            return;

        using var splash = CreateSplash(host);
        if (splash == null)
            return;

        await splash.ShowAsync();
        await splash.HideAsync();
    }

    /// <summary>
    /// Configures the logging level for the engine.
    /// </summary>
    /// <param name="logLevel">The log level to set.</param>
    protected virtual void ConfigureLogging(LogLevel logLevel)
    {
        EngineLogger.SetLogLevel(logLevel);
    }

    /// <summary>
    /// Returns the primary <see cref="RenderSurfaceHostBase"/> for this host, or
    /// <see langword="null"/> if none is available.
    /// </summary>
    /// <remarks>
    /// Override in platform-specific subclasses to expose the host's render surface so that
    /// <see cref="InitializeAsync"/> can attach the <see cref="SplashScreen"/> overlay.
    /// </remarks>
    protected virtual RenderSurfaceHostBase? GetPrimaryRenderSurfaceHost() => null;

    /// <summary>
    /// Creates the splash screen to display during <see cref="InitializeAsync"/>.
    /// Override to supply a game-specific image; return <see langword="null"/> for no splash.
    /// </summary>
    /// <param name="host">The render surface host that will own the splash overlay.</param>
    /// <returns>A configured <see cref="SplashScreen"/>, or <see langword="null"/>.</returns>
    protected virtual SplashScreen? CreateSplash(RenderSurfaceHostBase host) => null;

    /// <summary>
    /// Initializes the engine with the specified configuration settings.
    /// </summary>
    /// <param name="configPath">Optional path to the configuration file.</param>
    /// <param name="autoSaveConfig">Optional flag indicating whether to automatically save configuration changes.</param>
    protected virtual void InitializeEngine(string? configPath, bool? autoSaveConfig)
    {
        Engine.Instance.Initialize(configPath, autoSaveConfig);
    }

    /// <summary>
    /// Configures platform-specific features and adapters for the game host.
    /// </summary>
    protected abstract void ConfigurePlatform();

    /// <summary>
    /// Configures all input devices including keyboard, mouse, gamepads, and touch.
    /// </summary>
    protected virtual void ConfigureInput()
    {
        ConfigureKeyboard();
        ConfigureMouse();
        ConfigureGamepads();
        ConfigureTouch();
    }

    /// <summary>
    /// Configures the keyboard input adapter. Override to set up keyboard-specific configuration.
    /// </summary>
    protected virtual void ConfigureKeyboard() { }

    /// <summary>
    /// Configures the mouse input adapter. Override to set up mouse-specific configuration.
    /// </summary>
    protected virtual void ConfigureMouse() { }

    /// <summary>
    /// Configures the gamepad manager. Override to set up gamepad-specific configuration.
    /// </summary>
    protected virtual void ConfigureGamepads() { }

    /// <summary>
    /// Configures the touch input adapter. Override to set up touch-specific configuration.
    /// </summary>
    protected virtual void ConfigureTouch() { }

    /// <summary>
    /// Initializes game-specific content including assets, scenes, and game objects.
    /// </summary>
    protected virtual void InitializeGameContent()
    {
        LoadContent();
        CreateSceneGraph();
        BindScene();
        InitializeSceneObjects();
    }

    /// <summary>
    /// Loads all game content including assets, tilesheets, and animation cycles.
    /// </summary>
    protected virtual void LoadContent()
    {
        LoadAssets();
        LoadTilesheets();
        LoadAnimationCycles();
    }

    /// <summary>
    /// Loads game assets. Override to load textures, sounds, and other resources.
    /// </summary>
    protected virtual void LoadAssets() { }

    /// <summary>
    /// Loads tilesheet definitions. Override to load tileset data for tile-based rendering.
    /// </summary>
    protected virtual void LoadTilesheets() { }

    /// <summary>
    /// Loads animation cycle definitions. Override to load sprite animation data.
    /// </summary>
    protected virtual void LoadAnimationCycles() { }

    /// <summary>
    /// Creates the scene graph including the initial scene and views.
    /// </summary>
    protected virtual void CreateSceneGraph()
    {
        Scene = CreateInitialScene();
        CreateInitialViews();
    }

    /// <summary>
    /// Creates the initial scene for the game. Override to provide a custom starting scene.
    /// </summary>
    /// <returns>The initial scene to display.</returns>
    protected virtual Scene CreateInitialScene()
    {
        return Scene.Empty;
    }

    /// <summary>
    /// Creates initial views for rendering the scene. Override to set up camera views and viewports.
    /// </summary>
    protected virtual void CreateInitialViews() { }

    /// <summary>
    /// Binds the current scene to the render surface. Override to customize scene binding behavior.
    /// </summary>
    protected virtual void BindScene() { }

    /// <summary>
    /// Initializes scene objects including sprites and direct drawings.
    /// </summary>
    protected virtual void InitializeSceneObjects()
    {
        CreateSprites();
        CreateDirectDrawings();
    }

    /// <summary>
    /// Creates sprite objects in the scene. Override to populate the scene with sprites.
    /// </summary>
    protected virtual void CreateSprites() { }

    /// <summary>
    /// Creates direct drawing objects in the scene. Override to add custom rendering primitives.
    /// </summary>
    protected virtual void CreateDirectDrawings() { }

    /// <summary>
    /// Starts the engine with the current synchronization context.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the current thread does not have a synchronization context.</exception>
    protected virtual void StartEngine()
    {
        var syncContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException(
                $"{nameof(Initialize)} must be called on a thread with a current {nameof(SynchronizationContext)}.");

        Engine.Instance.Start(syncContext);
        OnStartEngine();
    }

    protected virtual void OnStartEngine() { }

    /// <summary>
    /// Unhooks event handlers during disposal. Override to clean up custom event subscriptions.
    /// </summary>
    protected virtual void UnhookEvents() { }

    /// <summary>
    /// Stops the engine during disposal.
    /// </summary>
    protected virtual void StopEngine()
    {
        Engine.Instance.Stop();
    }

    /// <summary>
    /// Disposes the engine instance during disposal.
    /// </summary>
    protected virtual void DisposeEngine()
    {
        Engine.Instance.Dispose();
    }

    /// <summary>
    /// Ensures the object has not been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    protected void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="GameHostBase"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            UnhookEvents();
            StopEngine();
            DisposeEngine();
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="GameHostBase"/> class.
    /// </summary>
    ~GameHostBase()
    {
        Dispose(false);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="GameHostBase"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
