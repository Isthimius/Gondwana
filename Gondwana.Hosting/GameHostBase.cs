using Microsoft.Extensions.Logging;
using Gondwana.Logging;
using Gondwana.Scenes;

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

    /// <summary>
    /// Gets the singleton instance of the engine.
    /// </summary>
    public Engine Engine => Engine.Instance;

    /// <summary>
    /// Gets the current active scene.
    /// </summary>
    public Scene? Scene { get; protected set; }

    /// <summary>
    /// Initializes the game host by setting up logging, platform, input, game content, and then the engine.
    /// </summary>
    /// <param name="configPath">Optional path to the configuration file.</param>
    /// <param name="autoSaveConfig">Optional flag indicating whether to automatically save configuration changes.</param>
    /// <param name="logLevel">The log level to use for engine logging. Default is <see cref="LogLevel.Warning"/>.</param>
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
        OnEngineInitialized();

        StartEngine();
        OnEngineStarted();

        _initialized = true;

        OnInitialized();
    }

    /// <summary>
    /// Runs before the host initialization sequence begins.
    /// </summary>
    protected virtual void OnInitializing()
    {
    }

    /// <summary>
    /// Configures the logging level for the engine.
    /// </summary>
    /// <param name="logLevel">The log level to set.</param>
    protected void ConfigureLogging(LogLevel logLevel)
    {
        EngineLogger.SetLogLevel(logLevel);
    }

    /// <summary>
    /// Configures platform-specific features and adapters for the game host.
    /// </summary>
    protected abstract void ConfigurePlatform();

    /// <summary>
    /// Configures all input devices including keyboard, mouse, gamepads, and touch.
    /// </summary>
    protected void ConfigureInput()
    {
        ConfigureKeyboard();
        ConfigureMouse();
        ConfigureGamepads();
        ConfigureTouch();
    }

    /// <summary>
    /// Configures the keyboard input adapter. Override to set up keyboard-specific configuration.
    /// </summary>
    protected virtual void ConfigureKeyboard()
    {
    }

    /// <summary>
    /// Configures the mouse input adapter. Override to set up mouse-specific configuration.
    /// </summary>
    protected virtual void ConfigureMouse()
    {
    }

    /// <summary>
    /// Configures the gamepad manager. Override to set up gamepad-specific configuration.
    /// </summary>
    protected virtual void ConfigureGamepads()
    {
    }

    /// <summary>
    /// Configures the touch input adapter. Override to set up touch-specific configuration.
    /// </summary>
    protected virtual void ConfigureTouch()
    {
    }

    /// <summary>
    /// Initializes game-specific content including assets, scenes, and game objects.
    /// </summary>
    protected void InitializeGameContent()
    {
        LoadContent();
        CreateSceneGraph();
        OnSceneGraphCreated();
        BindScene();
        OnSceneBound();
        InitializeSceneObjects();
    }

    /// <summary>
    /// Loads all game content including assets, tilesheets, and animation cycles.
    /// </summary>
    protected void LoadContent()
    {
        LoadAssets();
        LoadTilesheets();
        LoadAnimationCycles();
    }

    /// <summary>
    /// Loads game assets. Override to load textures, sounds, and other resources.
    /// </summary>
    protected virtual void LoadAssets()
    {
    }

    /// <summary>
    /// Loads tilesheet definitions. Override to load tileset data for tile-based rendering.
    /// </summary>
    protected virtual void LoadTilesheets()
    {
    }

    /// <summary>
    /// Loads animation cycle definitions. Override to load sprite animation data.
    /// </summary>
    protected virtual void LoadAnimationCycles()
    {
    }

    /// <summary>
    /// Creates the scene graph including the initial scene and views.
    /// </summary>
    protected void CreateSceneGraph()
    {
        Scene = CreateInitialScene()
            ?? throw new InvalidOperationException($"{nameof(CreateInitialScene)} returned null.");

        CreateInitialViews();
    }

    /// <summary>
    /// Runs after the scene graph has been created but before the current scene is bound to the render surface host.
    /// </summary>
    protected virtual void OnSceneGraphCreated()
    {
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
    protected virtual void CreateInitialViews()
    {
    }

    /// <summary>
    /// Binds the current scene to the render surface. Override to customize scene binding behavior.
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
    /// Initializes scene objects including sprites and direct drawings.
    /// </summary>
    protected void InitializeSceneObjects()
    {
        CreateSprites();
        CreateDirectDrawings();
    }

    /// <summary>
    /// Creates sprite objects in the scene. Override to populate the scene with sprites.
    /// </summary>
    protected virtual void CreateSprites()
    {
    }

    /// <summary>
    /// Creates direct drawing objects in the scene. Override to add custom rendering primitives.
    /// </summary>
    protected virtual void CreateDirectDrawings()
    {
    }

    /// <summary>
    /// Initializes the engine with the specified configuration settings.
    /// </summary>
    /// <param name="configPath">Optional path to the configuration file.</param>
    /// <param name="autoSaveConfig">Optional flag indicating whether to automatically save configuration changes.</param>
    protected void InitializeEngine(string? configPath, bool? autoSaveConfig)
    {
        Engine.Instance.Initialize(configPath, autoSaveConfig);
        _engineInitialized = true;
    }

    /// <summary>
    /// Runs after the engine has been initialized but before it has been started.
    /// </summary>
    protected virtual void OnEngineInitialized()
    {
    }

    /// <summary>
    /// Starts the engine with the configured synchronization context.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no synchronization context is available.</exception>
    protected void StartEngine()
    {
        var syncContext = GetSynchronizationContext()
            ?? throw new InvalidOperationException(
                $"{nameof(Initialize)} must be called on a thread with a current {nameof(SynchronizationContext)}.");

        StartEngineCore(syncContext);
        _engineStarted = true;
    }

    /// <summary>
    /// Starts the engine using the supplied synchronization context.
    /// Override to customize the platform-specific engine start mechanism.
    /// </summary>
    /// <param name="syncContext">The synchronization context used by the engine.</param>
    protected virtual void StartEngineCore(SynchronizationContext syncContext)
    {
        Engine.Instance.Start(syncContext);
    }

    /// <summary>
    /// Gets the synchronization context used to start the engine.
    /// </summary>
    /// <returns>The synchronization context used by the engine.</returns>
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
    /// Unhooks event handlers during disposal. Override to clean up custom event subscriptions.
    /// </summary>
    protected virtual void UnhookEvents()
    {
    }

    /// <summary>
    /// Stops the engine during disposal.
    /// </summary>
    protected void StopEngine()
    {
        StopEngineCore();
        _engineStarted = false;
    }

    /// <summary>
    /// Stops the engine. Override to perform platform-specific engine stop work.
    /// </summary>
    protected virtual void StopEngineCore()
    {
        Engine.Instance.Stop();
    }

    /// <summary>
    /// Disposes the engine instance during disposal.
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
    /// Ensures the object has not been initialized.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the object has already been initialized.</exception>
    protected void EnsureNotInitialized()
    {
        if (_initialized)
            throw new InvalidOperationException($"{GetType().Name} has already been initialized.");
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
    /// Releases all resources used by the <see cref="GameHostBase"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        OnDisposing();

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
