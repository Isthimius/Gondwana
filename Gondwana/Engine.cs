using Gondwana.Configuration;
using Gondwana.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Input.Gamepad;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.Timers;
using Microsoft.Extensions.Logging;
using Timer = Gondwana.Timers.Timer;

namespace Gondwana;

public sealed class Engine : IDisposable
{
    #region static members

    private static readonly Lazy<Engine> _instance = new(() => new Engine());
    public static Engine Instance => _instance.Value;

    public static ILogger<Engine> Logger => EngineLogger.GetLogger<Engine>();

    public static KeyboardEventPoller? KeyboardEventPoller => KeyboardEventPoller.Instance ?? null;

    public static MouseEventPoller? MouseEventPoller => MouseEventPoller.Instance ?? null;

    private static IGamepadManager<IGamepadAdapter>? _gamepadManager = null;

    /// <summary>
    /// Gets or sets the gamepad manager responsible for handling gamepad input.
    /// </summary>
    /// <remarks>Setting this property attaches an update callback to the engine cycle, polling attached adapters</remarks>
    public static IGamepadManager<IGamepadAdapter>? GamepadManager
    {
        get => _gamepadManager;
        set
        {
            GamepadEventPoller.Initialize(value?.ConnectedAdapters);
            _gamepadManager = value;
        }
    }

    public static GamepadEventPoller? GamepadEventPoller => GamepadEventPoller.Instance;

    #endregion

    #region private fields

    private long _startTick;
    private long _lastCPSSamplingTick;
    private long _lastTick = HighResTimer.GetCurrentTick();

    private long _grossCyclesThisMeasure = 0;
    private long _netCyclesThisMeasure = 0;
    private double _grossCPS = 0;
    private double _netFPS = 0;

    #endregion private fields

    #region events

    /// <summary>
    /// Occurs immediately before the engine begins its internal initialization sequence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This event is raised once per engine lifetime, the first time <see cref="Initialize"/> 
    /// is called. It provides an early hook for systems that must perform setup prior to
    /// configuration loading or input subsystem initialization.
    /// </para>
    /// <para>
    /// If a <see cref="UiDispatcher"/> is available, this event is posted to the UI thread;
    /// otherwise, it executes on the calling thread.
    /// </para>
    /// </remarks>
    public event Action? PreInitialization;

    /// <summary>
    /// Occurs after all internal initialization routines have completed, but before
    /// <see cref="InitializationComplete"/> is raised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This event is raised once per engine lifetime, following successful configuration
    /// loading, state restoration, and adapter setup.
    /// </para>
    /// <para>
    /// Use this event for post-initialization logic that depends on fully loaded engine
    /// settings but precedes runtime activation.
    /// </para>
    /// <para>
    /// If a <see cref="UiDispatcher"/> is available, this event is posted to the UI thread;
    /// otherwise, it executes on the calling thread.
    /// </para>
    /// </remarks>
    public event Action? PostInitialization;

    /// <summary>
    /// Occurs after all initialization steps and post-initialization logic have completed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This event is raised at the end of <see cref="Initialize"/>, every time the method is called.
    /// It signifies that the engine and its subsystems are fully active and ready for runtime operations.
    /// </para>
    /// <para>
    /// If a <see cref="UiDispatcher"/> is available, this event is posted to the UI thread;
    /// otherwise, it executes on the calling thread.
    /// </para>
    /// </remarks>
    public event Action? InitializationComplete;

    /// <summary>
    /// Occurs immediately before <see cref="DoBackgroundTasks(long)"/> executes within each engine cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this event to inject custom background logic such as diagnostics, AI updates,
    /// or subsystem polling prior to the engine’s own background operations.
    /// </para>
    /// </remarks>
    public event Action? BeforeBackgroundTasksExecute;

    /// <summary>
    /// Occurs immediately after <see cref="DoBackgroundTasks(long)"/> has completed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this event to perform custom actions or monitoring after all background updates 
    /// (timers, input, animations, surface refreshes, etc.) have been processed.
    /// </para>
    /// </remarks>
    public event Action? AfterBackgroundTasksExecute;

    /// <summary>
    /// Occurs immediately before <see cref="DoForegroundTasks(long)"/> executes within each engine cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this event to perform per-frame setup tasks prior to rendering or to update 
    /// game state that must occur before foreground drawing.
    /// </para>
    /// </remarks>
    public event Action? BeforeEngineCycle;

    /// <summary>
    /// Occurs immediately after <see cref="DoForegroundTasks(long)"/> completes within each engine cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this event to perform logic that depends on a completed render frame,
    /// such as post-render effects, profiling, or scheduling background jobs.
    /// </para>
    /// </remarks>
    public event Action? AfterEngineCycle;

    /// <summary>
    /// Occurs whenever cycles-per-second (CPS) and frames-per-second (FPS) metrics are calculated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Raised at a regular interval defined by <see cref="EngineConfiguration.SamplingTimeForCPS"/>.
    /// Provides a snapshot of gross and net cycle rates, total elapsed time, and sample interval 
    /// through a <see cref="CyclesPerSecondCalculatedEventArgs"/> payload.
    /// </para>
    /// <para>
    /// This event is posted to the UI thread when a <see cref="UiDispatcher"/> is available.
    /// </para>
    /// </remarks>
    public event Action<CyclesPerSecondCalculatedEventArgs>? CPSCalculated;

    /// <summary>
    /// Raised when <see cref="Dispose()"/> begins the explicit disposal sequence.
    /// </summary>
    /// <remarks>
    /// Fired only when <see cref="Dispose()"/> is called (never from the finalizer).
    /// Handlers run before managed cleanup while engine state is still readable.
    /// If a <see cref="UiDispatcher"/> is available, this event is posted to the UI thread.
    /// </remarks>
    public event Action? Disposing;

    /// <summary>
    /// Raised after the engine has completed explicit disposal.
    /// </summary>
    /// <remarks>
    /// Fired only when <see cref="Dispose()"/> is called (never from the finalizer).
    /// Indicates all managed cleanup has completed and <see cref="IsDisposed"/> is <c>true</c>.
    /// If a <see cref="UiDispatcher"/> is available, this event is posted to the UI thread.
    /// </remarks>
    public event Action? Disposed;

    #endregion events

    private Engine()
    { }

    private volatile bool _isInitialized = false;
    private volatile bool _isInitializing = false;
    private readonly ManualResetEventSlim _initDone = new(false);

    /// <summary>
    /// Performs one-time or on-demand initialization of the <see cref="Engine"/> instance, 
    /// loading configuration, state files, and input adapters required for execution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is responsible for preparing all core systems of the engine prior to starting
    /// the main loop. It performs the following operations in order:
    /// </para>
    /// <list type="number">
    ///   <item><description>Raises the <see cref="PreInitialization"/> event (on the UI thread if available).</description></item>
    ///   <item><description>Loads engine configuration settings from file using <see cref="EngineConfigurationFile.Load"/>.</description></item>
    ///   <item><description>Loads any <see cref="EngineState"/> files declared in configuration.</description></item>
    ///   <item><description>Initializes input subsystems for keyboard, mouse, and gamepad polling, 
    ///     if corresponding adapters are provided.</description></item>
    ///   <item><description>Raises <see cref="PostInitialization"/> after all internal setup is complete.</description></item>
    ///   <item><description>Marks the engine as initialized and raises <see cref="InitializationComplete"/>.</description></item>
    /// </list>
    /// <para>
    /// This method is automatically invoked by <see cref="Start(SynchronizationContext)"/> if the engine 
    /// has not yet been initialized. It is safe to call multiple times, but subsequent calls will 
    /// return immediately once initialization has been completed or is in progress.
    /// </para>
    /// <para>
    /// Thread-safe guarantees:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Concurrent calls are prevented by internal <c>_isInitializing</c> and <c>_isInitialized</c> flags.</description></item>
    ///   <item><description>Events that must run on the UI thread are dispatched through <see cref="UiDispatcher"/> if available.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="configFileName">
    /// Optional path to a configuration file to load. If <c>null</c>, the default configuration is used.
    /// </param>
    /// <param name="autoSaveConfig">
    /// Optional flag indicating whether configuration changes should be automatically saved back to disk.
    /// </param>
    /// <param name="keyboardAdapter">
    /// Optional <see cref="IKeyboardAdapter"/> instance used to initialize the keyboard input subsystem.
    /// </param>
    /// <param name="mouseAdapter">
    /// Optional <see cref="IMouseAdapter"/> instance used to initialize the mouse input subsystem.
    /// </param>
    /// <param name="gamepadManager">
    /// Optional <see cref="IGamepadManager{T}"/> instance used to initialize the gamepad subsystem.
    /// </param>
    /// <seealso cref="Start(SynchronizationContext)"/>
    /// <seealso cref="Stop"/>
    /// <seealso cref="EngineConfiguration"/>
    /// <seealso cref="EngineState"/>
    public void Initialize(
        string? configFileName = null,
        bool? autoSaveConfig = null,
        IKeyboardAdapter? keyboardAdapter = null,
        IMouseAdapter? mouseAdapter = null,
        IGamepadManager<IGamepadAdapter>? gamepadManager = null)
    {
        if (_isInitialized || _isInitializing)
            return;

        // reset in case this instance has been initialized before
        _initDone.Reset();

        _isInitializing = true;

        if (UiDispatcher == null)
            PreInitialization?.Invoke();
        else
            UiDispatcher!.Post(() => PreInitialization?.Invoke());

        Configuration = EngineConfigurationFile.Load(configFileName, autoSaveConfig).EngineConfig;

        if (Configuration.StateFiles?.Any() ?? false)
        {
            foreach (var stateFile in Configuration.StateFiles)
            {
                EngineState.LoadFromFile(stateFile);
            }
        }

        if (keyboardAdapter != null)
            KeyboardEventPoller.Initialize(keyboardAdapter);

        if (mouseAdapter != null)
            MouseEventPoller.Initialize(mouseAdapter);

        GamepadManager = gamepadManager;

        if (UiDispatcher == null)
            PostInitialization?.Invoke();
        else
            UiDispatcher!.Post(() => PostInitialization?.Invoke());

        _isInitializing = false;
        _isInitialized = true;

        if (UiDispatcher == null)
            InitializationComplete?.Invoke();
        else
            UiDispatcher!.Post(() => InitializationComplete?.Invoke());

        // signal that init is done
        _initDone.Set();
    }

    /// <summary>
    /// Starts the <see cref="Engine"/> using the current thread’s <see cref="SynchronizationContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This overload is intended for convenience when starting the engine from the UI thread.
    /// It retrieves the current <see cref="SynchronizationContext"/> and forwards it to 
    /// <see cref="Start(SynchronizationContext)"/>.
    /// </para>
    /// <para>
    /// The engine must be started from a thread that has a valid <see cref="SynchronizationContext"/>,
    /// typically the primary UI thread. If no synchronization context is available, an 
    /// <see cref="InvalidOperationException"/> is thrown.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="SynchronizationContext.Current"/> is <c>null</c>.
    /// </exception>
    /// <seealso cref="Start(SynchronizationContext)"/>
    /// <seealso cref="Initialize"/>
    /// <seealso cref="Stop"/>
    public void Start()
    {
        if (SynchronizationContext.Current == null)
            throw new InvalidOperationException("SynchronizationContext cannot be null.");

        Start(SynchronizationContext.Current);
    }

    /// <summary>
    /// Starts the <see cref="Engine"/> main loop using the provided <see cref="SynchronizationContext"/>,
    /// initializing the engine if it has not yet been started.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is the entry point for runtime execution. It ensures the engine is fully initialized
    /// before beginning the continuous background processing loop. The loop runs on a separate worker 
    /// thread and repeatedly invokes <see cref="Cycle"/>, yielding between iterations to allow 
    /// cooperative multitasking.
    /// </para>
    /// <para>
    /// The <paramref name="uiContext"/> argument establishes the <see cref="UiDispatcher"/> used for 
    /// posting events and callbacks to the UI thread. All UI-bound events such as 
    /// <see cref="PreInitialization"/>, <see cref="PostInitialization"/>, 
    /// <see cref="InitializationComplete"/>, and <see cref="CPSCalculated"/> 
    /// will be marshalled through this dispatcher when available.
    /// </para>
    /// <para>
    /// If the engine is already running, this method returns immediately without taking further action.
    /// </para>
    /// <para>
    /// Threading behavior:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>The engine’s main loop runs on a background task, not the UI thread.</description></item>
    ///   <item><description>All rendering and timing operations are controlled through <see cref="Cycle"/>.</description></item>
    ///   <item><description>The <see cref="UiDispatcher"/> guarantees that event notifications 
    ///   targeting the UI are executed safely on the originating thread.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="uiContext">
    /// The <see cref="SynchronizationContext"/> that defines the UI thread context to which 
    /// UI-related operations and events will be dispatched.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="uiContext"/> is <c>null</c>.
    /// </exception>
    /// <seealso cref="Initialize"/>
    /// <seealso cref="Stop"/>
    /// <seealso cref="Cycle"/>
    /// <seealso cref="UiDispatcher"/>
    public void Start(SynchronizationContext uiContext)
    {
        if (IsRunning)
            return;

        UiDispatcher = new UiDispatcher(uiContext);

        if (!IsInitialized)
        {
            if (IsInitializing)
            {
                _initDone.Wait();        // someone else is initializing—wait for it
            }
            else
            {
                Initialize();            // we're the initializer—do it now
            }
        }

        IsRunning = true;

        _startTick = HighResTimer.GetCurrentTick();
        _lastCPSSamplingTick = _startTick;

        Task.Run(() =>
        {
            while (Instance.IsRunning)
            {
                Instance.Cycle();
                Thread.Yield(); // optional
            }
        });
    }

    /// <summary>
    /// Stops the <see cref="Engine"/> main loop and halts all ongoing processing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method cleanly terminates the engine’s background execution cycle started by 
    /// <see cref="Start(SynchronizationContext)"/>. It sets <see cref="IsRunning"/> to <c>false</c>,
    /// signaling the loop in <see cref="Cycle"/> to exit on the next iteration.
    /// </para>
    /// <para>
    /// <b>Stop()</b> does not immediately dispose of resources or clear state. It simply halts
    /// ongoing updates and rendering, allowing the engine’s subsystems (timers, surfaces, 
    /// input pollers, etc.) to remain intact for later reuse or inspection.
    /// </para>
    /// <para>
    /// To fully clean up and release all managed resources, call <see cref="Dispose"/> after
    /// stopping the engine.
    /// </para>
    /// <para>
    /// This method is thread-safe and may be called from any thread.
    /// </para>
    /// </remarks>
    /// <seealso cref="Start()"/>
    /// <seealso cref="Cycle"/>
    /// <seealso cref="Dispose()"/>
    /// <seealso cref="IsRunning"/>
    public void Stop()
    {
        IsRunning = false;
    }

    #region public properties

    public IUiDispatcher? UiDispatcher { get; private set; }

    public bool IsInitialized => _isInitialized;

    public bool IsInitializing => _isInitializing;

    public bool IsRunning { get; private set; }

    public long TotalTicksEngineRunning => HighResTimer.GetCurrentTick() - _startTick;

    public double TotalSecondsEngineRunning => TotalTicksEngineRunning / (double)HighResTimer.TicksPerSecond;

    public double CyclesPerSecond => _grossCPS;

    public double FramesPerSecond => _netFPS;

    public bool IsDisposed { get; private set; } = false;

    public EngineState State { get; } = new EngineState();

    private EngineConfiguration? _config = new();

    public EngineConfiguration Configuration
    {
        get => Volatile.Read(ref _config!);
        private set => Volatile.Write(ref _config, value);
    }

    public bool IsDisposing { get; private set; }

    #endregion public properties

    #region private methods

    private void Cycle()
    {
        long tick = HighResTimer.GetCurrentTick();

        DoBackgroundTasks(tick);

        // if TargetFPS <= 0, render to screen unbounded
        // otherwise, check if throttle time has passed since last tick...
        if ((Configuration.TargetFPS <= 0)
            || (tick - _lastTick) >= HighResTimer.TicksPerSecond / Configuration.TargetFPS)
        {
            DoForegroundTasks(tick);

            // save time of this last tick; increment CPS counter
            _lastTick = tick;
            _netCyclesThisMeasure++;
        }

        // increment CPS counter
        _grossCyclesThisMeasure++;

        // if 0 or negative, sampling is turned off
        if (Configuration.SamplingTimeForCPS > 0)
            CalculateCPS(tick);
    }

    private void DoBackgroundTasks(long tick)
    {
        BeforeBackgroundTasksExecute?.Invoke();

        // raise pre-cycle timer events
        Timer.RaiseTimerEvents(TimerType.PreCycle, tick);

        // check for keyboard events
        KeyboardEventPoller.Instance?.PollForEvents(tick);

        // check for mouse events
        MouseEventPoller.Instance?.PollForEvents(tick);

        // check for gamepad events
        GamepadEventPoller.Instance?.PollForEvents(tick);

        // TODO: camera movement handling
        // perform any timed SceneLayer scrolling
        //foreach (var sceneLayer in SceneLayer.GetAllSceneLayers())
        //    sceneLayer.MoveNext(tick);

        // cycle Animator frames
        for (int i = 0; i < Tile.TilesAnimating.Count; i++)
            Tile.TilesAnimating[i].TileAnimator.CycleAnimation(tick);

        // advance Sprite Movement paths
        SpriteManager.MoveSprites(tick);

        // TODO: this has moved to CollisionManager
        // check for Tile collisions
        //RaiseCollisionEvent(tick);

        // refresh all RenderSurfaceHost backbuffers
        foreach (var surface in RenderSurfaceHostRegistry.All)
            surface.DrawRefreshQueueToBackbuffer(tick);

        // all attached VisibleSurface backbuffers drawn; clear the refresh queues
        ClearRefreshQueues();

        AfterBackgroundTasksExecute?.Invoke();
    }

    private void DoForegroundTasks(long tick)
    {
        // raise event
        BeforeEngineCycle?.Invoke();

        // update the DirectDrawing instances' states
        DirectDrawingManager.Instance.UpdateAll(tick);

        // render all DirectDrawing instances.
        // this will add to the DirtyRects of any Backbuffers,
        // to be picked up next DoBackgroundTasks()
        DirectDrawingManager.Instance.RenderAll();

        // render each Backbuffer to RenderSurfaceHost adapter
        foreach (var surface in RenderSurfaceHostRegistry.All)
            surface.RenderBackbufferToAdapter();

        // update state of gamepad(s)
        GamepadManager?.Update();

        // raise event
        AfterEngineCycle?.Invoke();

        // raise post-cycle timer events
        Timer.RaiseTimerEvents(TimerType.PostCycle, tick);
    }

    private void ClearRefreshQueues()
    {
        // step through all SceneLayers objects
        foreach (var scene in Scene.GetAllScenes())
        {
            // clear each queue, mark as no refresh needed
            foreach (SceneLayer sceneLayer in scene)
                sceneLayer.RefreshQueue.ClearRefreshQueue();

            scene.RefreshNeeded = SceneRefreshType.Tiles;
        }
    }

    private void CalculateCPS(long tick)
    {
        // Has the sampling interval elapsed?
        long elapsedTicks = tick - _lastCPSSamplingTick;
        if (elapsedTicks < Configuration.SamplingTimeForCPSTicks) return;

        // SNAPSHOT the counters BEFORE resetting or posting
        long grossCycles = _grossCyclesThisMeasure;
        long netCycles = _netCyclesThisMeasure;

        // Compute using the snapshot
        double elapsedSec = elapsedTicks / (double)HighResTimer.TicksPerSecond;
        double grossCps = grossCycles * HighResTimer.TicksPerSecond / (double)elapsedTicks;
        double netCps = netCycles * HighResTimer.TicksPerSecond / (double)elapsedTicks;

        // Build immutable args NOW (so lambda doesn’t read changing fields later)
        var args = new CyclesPerSecondCalculatedEventArgs(
            grossCycles,
            netCycles,
            grossCps,
            netCps,
            elapsedSec
        );

        // Post the snapshot
        UiDispatcher!.Post(() => CPSCalculated?.Invoke(args));

        _grossCPS = grossCps;
        _netFPS = netCps;

        // Reset for next window
        _lastCPSSamplingTick = tick;
        _grossCyclesThisMeasure = 0;
        _netCyclesThisMeasure = 0;
    }

    #endregion private methods

    #region IDisposable support

    private void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                IsDisposing = true;

                // Stop the loop first so handlers don't race the cycle thread
                try { Stop(); }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Unhandled exception calling Stop()");
                }

                // Raise Disposing on UI thread if possible; otherwise inline
                if (UiDispatcher is not null)
                    UiDispatcher.Post(() => SafeInvoke(Disposing));
                else
                    SafeInvoke(Disposing);

                // managed cleanup...
                KeyboardEventPoller?.StopMonitoringAllKeys();
                MouseEventPoller?.StopMonitoringMouse();

                if (GamepadManager is not null)
                    foreach (var gamepadAdapter in GamepadManager.ConnectedAdapters)
                        GamepadEventPoller?.StopMonitoringAllButtons(gamepadAdapter.GamepadId);

                Timer.ClearAll();
                State.Clear();
            }

            // unmanaged cleanup...
            IsDisposed = true;

            if (disposing)
            {
                // Now signal we're fully torn down
                if (UiDispatcher is not null)
                    UiDispatcher.Post(() => SafeInvoke(Disposed));
                else
                    SafeInvoke(Disposed);
            }
        }
    }

    private static void SafeInvoke(Action? evnt)
    {
        try { evnt?.Invoke(); }
        catch (Exception ex)
        {
            // Keep disposal robust; log and continue
            Logger.LogError(ex, "Unhandled exception in disposal event handler.");
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~Engine()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    #endregion IDisposable support
}