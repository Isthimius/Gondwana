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
    private readonly double _grossCPS = 0;
    private readonly double _netFPS = 0;

    #endregion private fields

    #region events

    /// <summary>
    /// Runs when Initialize() is called, prior to internal initialization.
    /// This event will only be raised the first time Initialize() is called.
    /// </summary>
    public event Action? PreInitialization;

    /// <summary>
    /// Runs when Initalize() is called, after all other internal initialization is complete.
    /// This event will only be raised the first time Initialize() is called.
    /// </summary>
    public event Action? PostInitialization;

    /// <summary>
    /// Runs when Initalize() is called, after all other internal initialization and
    /// PostInitialization is complete. This event will be raised each time Initialize() is called.
    /// </summary>
    public event Action? InitializationComplete;

    public event Action? BeforeBackgroundTasksExecute;

    public event Action? AfterBackgroundTasksExecute;

    public event Action? BeforeEngineCycle;

    public event Action? AfterEngineCycle;

    public event Action<CyclesPerSecondCalculatedEventArgs>? CPSCalculated;
    
    #endregion events

    private Engine()
    { }

    private bool _isInitialized = false;
    private bool _isInitializing = false;

    public void Initialize(
        string? configFileName = null,
        bool? autoSaveConfig = null,
        IKeyboardAdapter? keyboardAdapter = null,
        IMouseAdapter? mouseAdapter = null,
        IGamepadManager<IGamepadAdapter>? gamepadManager = null)
    {
        if (_isInitialized || _isInitializing)
            return;

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
    }

    /// <summary>
    /// Starts the operation using the current <see cref="SynchronizationContext"/>.
    /// Must be called from the UI thread.
    /// </summary>
    /// <remarks>This method requires a non-null <see cref="SynchronizationContext"/> to be present.  If no
    /// <see cref="SynchronizationContext"/> is available, an exception is thrown.</remarks>
    /// <exception cref="InvalidOperationException">Thrown if the current <see cref="SynchronizationContext"/> is <c>null</c>.</exception>
    public void Start()
    {
        if (SynchronizationContext.Current == null)
            throw new InvalidOperationException("SynchronizationContext cannot be null.");

        Start(SynchronizationContext.Current);
    }

    /// <summary>
    /// Starts the main processing loop, initializing the instance if necessary.
    /// </summary>
    /// <remarks>This method ensures that the instance is initialized before starting the processing loop.  If
    /// the instance is already running, the method returns immediately without performing any action.</remarks>
    /// <param name="uiContext">The <see cref="SynchronizationContext"/> used to synchronize UI-related operations.</param>
    public void Start(SynchronizationContext uiContext)
    {
        if (IsRunning)
            return;

        UiDispatcher = new UiDispatcher(uiContext);

        if (!IsInitialized)
        {
            // wait for any previous initialization to complete
            while (IsInitializing) { }

            Initialize();
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

    #endregion public properties

    #region private methods

    private void Cycle()
    {
        long tick = HighResTimer.GetCurrentTick();

        DoBackgroundTasks(tick);

        // if TargetFPS <= 0, render to screen unbounded;
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

        // perform any timed SceneLayer scrolling
        foreach (var sceneLayer in SceneLayer.GetAllSceneLayers())
            sceneLayer.MoveNext(tick);

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
            surface.DrawRefreshQueueToBackbuffer();

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

            scene.RefreshNeeded = SceneRefreshType.None;
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

        // Reset for next window
        _lastCPSSamplingTick = tick;
        _grossCyclesThisMeasure = 0;
        _netCyclesThisMeasure = 0;
    }

    private void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                // managed cleanup...
                Timer.ClearAll();
                State.Clear();
            }

            // unmanaged cleanup...
            IsDisposed = true;
        }
    }

    void IDisposable.Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~Engine()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    #endregion private methods
}