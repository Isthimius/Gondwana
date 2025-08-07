using System.Drawing;
using Gondwana.Configuration;
using Gondwana.Drawing;
using Gondwana.Drawing.Collisions;
using Gondwana.Drawing.Sprites;
using Gondwana.Grid;
using Gondwana.Input.Gamepad;
using Gondwana.Input.Keyboard;
using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Rendering.Direct;
using Gondwana.State;
using Gondwana.Timers;
using Microsoft.Extensions.Logging;
using Timer = Gondwana.Timers.Timer;

namespace Gondwana;

public sealed class Engine : IDisposable
{
    private static readonly Lazy<Engine> _instance = new(() => new Engine());
    public static Engine Instance => _instance.Value;

    public static ILogger<Engine> Logger => EngineLogger.GetLogger<Engine>();

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
    #endregion

    #region events
    /// <summary>
    /// Runs when Initialize() is called, prior to internal initialization.
    /// This event will only be raised the first time Initialize() is called.
    /// </summary>
    public event EventHandler PreInitialization;

    /// <summary>
    /// Runs when Initalize() is called, after all other internal initialization is complete.
    /// This event will only be raised the first time Initialize() is called.
    /// </summary>
    public event EventHandler PostInitialization;

    /// <summary>
    /// Runs when Initalize() is called, after all other internal initialization and
    /// PostInitialization is complete. This event will be raised each time Initialize() is called.
    /// </summary>
    public event EventHandler InitializationComplete;

    public delegate void BackgroundTaskExecuteHandler();

    public event BackgroundTaskExecuteHandler BeforeBackgroundTasksExecute;
    public event BackgroundTaskExecuteHandler AfterBackgroundTasksExecute;
    public event EngineCycleEventHandler BeforeEngineCycle;
    public event EngineCycleEventHandler AfterEngineCycle;
    public event CyclesPerSecondCalculatedHandler CPSCalculated;
    #endregion

    #region constructor
    private Engine() { }

    private bool _isInitialized = false;
    private bool _isInitializing = false;

    public void Initialize(
        string? configFileName = null,
        bool? autoSaveConfig = null,
        IKeyboardAdapter? keyboardAdapter = null,
        IGamepadManager<IGamepadAdapter>? gamepadManager = null)
    {
        if (_isInitialized || _isInitializing)
            return;

        _isInitializing = true;

        PreInitialization?.Invoke(this, EventArgs.Empty);

        Configuration = EngineConfigurationFile.Load(configFileName, autoSaveConfig).EngineConfig;

        if (Configuration.StateFiles?.Any() ?? false)
        {
            foreach (var stateFile in Configuration.StateFiles)
            {
                EngineState.LoadFromFile(stateFile);
            }
        }

        GamepadManager = gamepadManager;

        PostInitialization?.Invoke(this, EventArgs.Empty);

        _isInitializing = false;
        _isInitialized = true;

        InitializationComplete?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    #region public methods
    public void Start()
    {
        if (IsRunning)
            return;

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
    #endregion

    #region public properties
    public bool IsInitialized => _isInitialized;

    public bool IsInitializing => _isInitializing;

    public bool IsRunning { get; private set; }

    public int BufferWidth { get; set; } = 0;

    public int BufferHeight { get; set; } = 0;

    public double TotalSecondsEngineRunning
    {
        get { return (double)(HighResTimer.GetCurrentTick() - _startTick) / (double)HighResTimer.TicksPerSecond; }
    }

    public double CyclesPerSecond
    {
        get { return _grossCPS; }
    }

    public double FramesPerSecond
    {
        get { return _netFPS; }
    }

    public bool IsDisposed { get; private set; } = false;

    public EngineState State { get; } = new EngineState();

    private EngineConfiguration? _config = new();

    public EngineConfiguration Configuration
    {
        get => _config ??= new EngineConfiguration();
        set => _config = value;
    }

    public KeyboardEventPoller? KeyboardEventPoller { get; set; } = null;

    private IGamepadManager<IGamepadAdapter>? _gamepadManager = null;

    /// <summary>
    /// Gets or sets the gamepad manager responsible for handling gamepad input.
    /// </summary>
    /// <remarks>Setting this property attaches an update callback to the engine cycle, polling attached adapters</remarks>
    public IGamepadManager<IGamepadAdapter>? GamepadManager
    {
        get => _gamepadManager;
        set
        {
            GamepadManagerEventPoller.Initialize(value?.ConnectedAdapters);
            _gamepadManager = value;
        }
    }

    public GamepadManagerEventPoller? GamepadManagerEventPoller { get => GamepadManagerEventPoller.Instance; }
    #endregion

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
        if (BeforeBackgroundTasksExecute != null)
            BeforeBackgroundTasksExecute();

        // raise pre-cycle timer events
        Timer.RaiseTimerEvents(TimerType.PreCycle, tick);

        // check for keyboard events
        KeyboardEventPoller.Instance?.PollForEvents(tick);

        // check for gamepad events
        GamepadManagerEventPoller.Instance?.PollForEvents(tick);

        // perform any timed GridPointMatrix scrolling
        foreach (GridPointMatrix matrix in GridPointMatrix.GetAllGridPointMatrix())
            matrix.MoveNext(tick);

        // TODO: re-enable this... also, should be before or after other Tile animations? assuming before, since that worked before...

        // perform any timed DirectDrawing scrolling
        //foreach (DirectDrawing drawing in DirectDrawingManager._instances)
        //    drawing.MoveNext(tick);

        // cycle Animator frames
        CycleAnimations(tick);

        // advance Sprite Movement paths
        Sprites.MoveSprites(tick);

        // TODO: this has moved to CollisionManager
        // check for Tile collisions
        //RaiseCollisionEvent(tick);

        // refresh all VisibleSurface backbuffers
        DrawRefreshQueues<BitmapBackbuffer>();
        DrawRefreshQueues<GpuBackbuffer>();

        // all attached VisibleSurface backbuffers drawn; clear the refresh queues
        ClearRefreshQueues();

        if (AfterBackgroundTasksExecute != null)
            AfterBackgroundTasksExecute();
    }

    private void DoForegroundTasks(long tick)
    {
        // raise event
        if (BeforeEngineCycle != null)
            BeforeEngineCycle(new EngineCycleEventArgs(_grossCyclesThisMeasure, _grossCycles, _netCyclesThisMeasure, _netCycles, _grossCPS, _netFPS));

        // render each BitmapBackbuffer to RenderSurfaceHost adapter
        foreach (var surface in RenderSurfaceHost<BitmapBackbuffer>._allRenderSurfaceHosts)
            surface.RenderBackbuffer();

        // render each GpuBackbuffer to RenderSurfaceHost adapter
        foreach (var surface in RenderSurfaceHost<GpuBackbuffer>._allRenderSurfaceHosts)
            surface.RenderBackbuffer();

        // all RenderSurfaceHost backbuffers rendered; clear the dirty rectangles
        BackbufferBase._resetAllDirtyRectangles();

        // poll state of gamepad(s)
        GamepadManager?.Update();

        // save time of this last tick; increment CPS counter
        _lastTick = tick;
        _netCyclesThisMeasure++;
        _netCycles++;

        // raise event
        if (AfterEngineCycle != null)
            AfterEngineCycle(new EngineCycleEventArgs(_grossCyclesThisMeasure, _grossCycles, _netCyclesThisMeasure, _netCycles, _grossCPS, _netFPS));

        // raise post-cycle timer events
        Timer.RaiseTimerEvents(TimerType.PostCycle, tick);
    }

    private void DrawRefreshQueues<T>() where T : BackbufferBase
    {
        foreach (var surface in RenderSurfaceHost<T>._allRenderSurfaceHosts)
        {
            var backbuffer = surface.Backbuffer;
            GridPointMatrixes grids = surface.DrawSource;

            if (grids == null || grids.Count == 0)
            {
                // TODO: do we need to do this **every** time, really?
                // since there is no grid attached, clear the entire backbuffer
                backbuffer.Erase();

                // force refresh of all DirectDrawing objects
                foreach (DirectDrawingBase drawing in DirectDrawingManager._instances)
                    drawing.ForceRefresh();
            }
            else
            {
                switch (grids.RefreshNeeded)
                {
                    case MatrixesRefreshType.None:
                        // do nothing
                        return;

                    case MatrixesRefreshType.Queue:
                        // erase any DirectDrawing that intersects with a refresh area
                        foreach (DirectDrawingBase direct in DirectDrawingManager._instances)
                        {
                            if (grids.BackmostVisibleLayer.RefreshQueue.AreaIntersectsRefreshArea(direct.Bounds))
                                direct.ForceRefresh();
                        }

                        // TODO: do we need to erase?
                        // erase the existing areas in queue
                        //backbuffer.Erase(grids.BackmostVisibleLayer.RefreshQueue.GetDirtyRectangles());

                        // draw from back to front from visible layers array
                        for (int i = grids.CountOfVisibleLayers - 1; i >= 0; i--)
                            surface.Backbuffer.DrawTiles(grids.VisibleGridPointMatrixList[i].RefreshQueue.Tiles);

                        break;

                    case MatrixesRefreshType.All:
                        // TODO: do we need to erase?
                        // erase the entire backbuffer
                        //backbuffer.Erase();

                        // force refresh of all DirectDrawing objects
                        foreach (DirectDrawingBase drawing in DirectDrawingManager._instances)
                            drawing.ForceRefresh();

                        // draw from back to front from visible layers array
                        for (int i = grids.CountOfVisibleLayers - 1; i >= 0; i--)
                        {
                            // refreshing entire back buffer, so clear any partial queue...
                            grids.VisibleGridPointMatrixList[i].RefreshQueue.ClearRefreshQueue();

                            // find and add all Tile objects in range to queue
                            grids.VisibleGridPointMatrixList[i].RefreshQueue.AddPixelRangeToRefreshQueue(
                                new Rectangle(0, 0, surface.RenderSurfaceAdapter!.Width, surface.RenderSurfaceAdapter.Height), false);

                            // draw to backbuffer
                            surface.Backbuffer!.DrawTiles(grids.VisibleGridPointMatrixList[i].RefreshQueue.Tiles);
                        }

                        break;

                    default:
                        // shouldn't get here
                        continue;
                }
            }

            // draw all DirectDrawing objects that overlap with dirty rectangles and are "dirty"
            DirectDrawingManager.RenderAll();
        }
    }

    private void ClearRefreshQueues()
    {
        // step through all GridPointMatrixes objects
        foreach (GridPointMatrixes grids in GridPointMatrixes.GetAllGridPointMatrixes())
        {
            // clear each queue, mark as no refresh needed
            foreach (GridPointMatrix matrix in grids)
                matrix.RefreshQueue.ClearRefreshQueue();

            grids.RefreshNeeded = MatrixesRefreshType.None;
        }
    }

    private void CycleAnimations(long tick)
    {
        for (int i = 0; i < Tile.TilesAnimating.Count; i++)
            Tile.TilesAnimating[i].TileAnimator.CycleAnimation(tick);
    }

    private void CalculateCPS(long tick)
    {
        // check if CPS Sampling time has passed
        if (tick - _lastCPSSamplingTick >= Configuration.SamplingTimeForCPSTicks)
        {
            _grossCPS = (double)(_grossCyclesThisMeasure * HighResTimer.TicksPerSecond) / (double)(tick - _lastCPSSamplingTick);
            _netFPS = (double)(_netCyclesThisMeasure * HighResTimer.TicksPerSecond) / (double)(tick - _lastCPSSamplingTick);

            // raise the event
            if (CPSCalculated != null)
                CPSCalculated(new CyclesPerSecondCalculatedEventArgs(
                    _grossCyclesThisMeasure, _netCyclesThisMeasure, _grossCPS, _netFPS, Configuration.SamplingTimeForCPS));

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
                Timer.ClearAll();
                State.Clear();
            }

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
    #endregion
}
