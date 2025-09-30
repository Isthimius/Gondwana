using Gondwana.Configuration;
using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.Input.Gamepad;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Rendering.Direct;
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

    private long _grossCycles = 0;
    private long _grossCyclesThisMeasure = 0;
    private long _netCycles = 0;
    private long _netCyclesThisMeasure = 0;
    private double _grossCPS = 0;
    private double _netFPS = 0;

    private bool _hasBackgroundRun = false;

    #endregion private fields

    #region events

    /// <summary>
    /// Runs when Initialize() is called, prior to internal initialization.
    /// This event will only be raised the first time Initialize() is called.
    /// </summary>
    public event Action PreInitialization;

    /// <summary>
    /// Runs when Initalize() is called, after all other internal initialization is complete.
    /// This event will only be raised the first time Initialize() is called.
    /// </summary>
    public event Action PostInitialization;

    /// <summary>
    /// Runs when Initalize() is called, after all other internal initialization and
    /// PostInitialization is complete. This event will be raised each time Initialize() is called.
    /// </summary>
    public event Action InitializationComplete;

    public event Action BeforeBackgroundTasksExecute;

    public event Action AfterBackgroundTasksExecute;

    public event Action<EngineCycleEventArgs> BeforeEngineCycle;

    public event Action<EngineCycleEventArgs> AfterEngineCycle;

    public event Action<CyclesPerSecondCalculatedEventArgs> CPSCalculated;

    #endregion events

    #region constructor

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

    #endregion constructor

    #region public methods

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

    #endregion public methods

    #region public properties

    public IUiDispatcher? UiDispatcher { get; private set; }

    public bool IsInitialized => _isInitialized;

    public bool IsInitializing => _isInitializing;

    public bool IsRunning { get; private set; }

    public double TotalSecondsEngineRunning
    {
        get { return (double)(HighResTimer.GetCurrentTick() - _startTick) / (double)HighResTimer.TicksPerSecond; }
    }

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

        // throttle time hasn't passed; just do background tasks
        if ((Configuration.TargetFPS > 0) && ((double)(tick - _lastTick) < (((double)1 / (double)Configuration.TargetFPS)) * (double)HighResTimer.TicksPerSecond))
        {
            DoBackgroundTasks(tick);

            // flag that the background tasks have been run this "tick"
            _hasBackgroundRun = true;
        }
        else        // Settings.Throttle time has passed since last tick...
        {
            // make sure background rendering done at least once
            if (!_hasBackgroundRun)
                DoBackgroundTasks(tick);

            DoForegroundTasks(tick);

            // this "tick" complete, reset flag for next tick
            _hasBackgroundRun = false;
        }

        // increment CPS counter
        _grossCyclesThisMeasure++;
        _grossCycles++;

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
        foreach (SceneLayer matrix in SceneLayer.GetAllSceneLayers())
            matrix.MoveNext(tick);

        // TODO: re-enable this... also, should be before or after other Tile animations? assuming before, since that worked before...

        // perform any timed DirectDrawing scrolling
        //foreach (DirectDrawing drawing in DirectDrawingManager._instances)
        //    drawing.MoveNext(tick);

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
        BeforeEngineCycle?.Invoke(new EngineCycleEventArgs(_grossCyclesThisMeasure, _grossCycles, _netCyclesThisMeasure, _netCycles, _grossCPS, _netFPS));

        // render all DirectDrawing objects;
        // this will add to the DirtyRects of any Backbuffers,
        // to be picked up next DoBackgroundTasks()
        DirectDrawingManager.Instance.RenderAll();

        // render each Backbuffer to RenderSurfaceHost adapter
        foreach (var surface in RenderSurfaceHostRegistry.All)
            surface.RenderBackbufferToAdapter();

        // poll state of gamepad(s)
        GamepadManager?.Update();

        // save time of this last tick; increment CPS counter
        _lastTick = tick;
        _netCyclesThisMeasure++;
        _netCycles++;

        // raise event
        AfterEngineCycle?.Invoke(new EngineCycleEventArgs(_grossCyclesThisMeasure, _grossCycles, _netCyclesThisMeasure, _netCycles, _grossCPS, _netFPS));

        // raise post-cycle timer events
        Timer.RaiseTimerEvents(TimerType.PostCycle, tick);
    }

    private void ClearRefreshQueues()
    {
        // step through all SceneLayeres objects
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
        // check if CPS Sampling time has passed
        if (tick - _lastCPSSamplingTick >= Configuration.SamplingTimeForCPSTicks)
        {
            _grossCPS = (double)(_grossCyclesThisMeasure * HighResTimer.TicksPerSecond) / (double)(tick - _lastCPSSamplingTick);
            _netFPS = (double)(_netCyclesThisMeasure * HighResTimer.TicksPerSecond) / (double)(tick - _lastCPSSamplingTick);

            // raise the event
            UiDispatcher!.Post(() => CPSCalculated?.Invoke(new CyclesPerSecondCalculatedEventArgs(
                    _grossCyclesThisMeasure, _netCyclesThisMeasure, _grossCPS, _netFPS, Configuration.SamplingTimeForCPS)));

            // reset values for next calculation
            _lastCPSSamplingTick = tick;
            _grossCyclesThisMeasure = 0;
            _netCyclesThisMeasure = 0;
        }
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