using Gondwana.Common;
using Gondwana.Common.Collisions;
using Gondwana.Common.Configuration;
using Gondwana.Common.Drawing.Direct;
using Gondwana.Common.Drawing.Sprites;
using Gondwana.Common.Enums;
using Gondwana.Common.EventArgs;
using Gondwana.Common.Grid;
using Gondwana.Common.Timers;
using Gondwana.Common.Win32;
using Gondwana.Input.Keyboard;
using Gondwana.Media;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Gondwana
{
    public static class Engine
    {
        #region private fields
        private static long _startTick;
        private static long _lastCPSSamplingTick;
        private static long _lastTick = HighResTimer.GetCurrentTickCount();

        private static long _grossCycles = 0;
        private static long _grossCyclesThisMeasure = 0;
        private static long _netCycles = 0;
        private static long _netCyclesThisMeasure = 0;
        private static double _grossCPS = 0;
        private static double _netFPS = 0;

        private static bool _backgroundRun = false;
        #endregion

        #region events
        public delegate void BackgroundTaskExecuteHandler();

        public static event BackgroundTaskExecuteHandler BeforeBackgroundTasksExecute;
        public static event BackgroundTaskExecuteHandler AfterBackgroundTasksExecute;
        public static event EngineCycleEventHandler BeforeEngineCycle;
        public static event EngineCycleEventHandler AfterEngineCycle;
        public static event CyclesPerSecondCalculatedHandler CPSCalculated;
        public static event CollisionEventHandler TileCollisions;
        #endregion

        #region constructor
        static Engine()
        {
            Configuration = EngineConfiguration.Open();
            //MediaFile.MCIErrorsThrowExceptions = Configuration.Settings.MCIErrorsThrowExceptions;
            _startTick = HighResTimer.GetCurrentTickCount();
            _lastCPSSamplingTick = _startTick;
            Keyboard.DefaultTicksBetweenKeyEvents = (long)(Configuration.Settings.TimeBetweenKeyboardEvents * (double)HighResTimer.TicksPerSecond);

            Directory.SetCurrentDirectory(Path.GetDirectoryName(Application.ExecutablePath));

            // deserialize all EngineState files listed in configuration
            if (Configuration.StateFiles != null && Configuration.StateFiles.LoadAtStartup == true)
            {
                foreach (var stateFile in Configuration.StateFiles)
                {
                    if (stateFile.LoadAtStartup == true)
                        LoadEngineStateFile(stateFile.Path, stateFile.IsBinary);
                }
            }

            VisibleSurfaces.ForcedRefreshRate = Configuration.Settings.VisibleSurfaceRefreshTimer;
            Application.Idle += Application_Idle;
        }
        #endregion

        #region public methods
        public static void Start()
        {
            IsRunning = true;
        }

        public static void Stop()
        {
            IsRunning = false;
        }

        public static void ResetTotalTimeRunning()
        {
            _startTick = HighResTimer.GetCurrentTickCount();
        }

        public static void ClearEngineState()
        {
            State.Clear();
        }

        public static void LoadEngineStateFile(string file, bool isBinary)
        {
            EngineState.GetEngineState(file, isBinary);
        }
        #endregion

        #region public properties
        public static double TotalSecondsEngineRunning
        {
            get { return (double)(HighResTimer.GetCurrentTickCount() - _startTick) / (double)HighResTimer.TicksPerSecond; }
        }

        public static double CyclesPerSecond
        {
            get { return _grossCPS; }
        }

        public static double FramesPerSecond
        {
            get { return _netFPS; }
        }

        private static EngineState _state = null;
        public static EngineState State
        {
            get
            {
                if (_state == null)
                    _state = EngineState.GetEngineState();

                return _state;
            }
        }

        public static EngineConfiguration Configuration { get; set; }

        public static bool IsRunning { get; private set; }
        #endregion

        #region private methods
        private static void Application_Idle(object sender, EventArgs e)
        {
            while (IsApplicationIdle())
            {
                if (IsRunning)
                    Cycle();
                else
                    return;
            }
        }

        private static bool IsApplicationIdle()
        {
            NativeMessage result;
            return pInvoke.PeekMessage(out result, IntPtr.Zero, (uint)0, (uint)0, (uint)0) == 0;
        }

        private static void Cycle()
        {
            long tick = HighResTimer.GetCurrentTickCount();

            // throttle time hasn't passed; do background tasks
            if ((Configuration.Settings.TargetFPS > 0) && ((double)(tick - _lastTick) < (((double)1 / (double)Configuration.Settings.TargetFPS)) * (double)HighResTimer.TicksPerSecond))
            {
                DoBackgroundTasks(tick);

                // flag that the background tasks have been run this "tick"
                _backgroundRun = true;
            }
            else        // Settings.Throttle time has passed since last tick...
            {
                // make sure background rendering done at least once
                if (!_backgroundRun)
                    DoBackgroundTasks(tick);

                DoForegroundTasks(tick);

                // this "tick" complete, reset flag for next tick
                _backgroundRun = false;
            }

            // increment CPS counter
            _grossCyclesThisMeasure++;
            _grossCycles++;

            // if 0 or negative, sampling is turned off
            if (Configuration.Settings.SamplingTimeForCPS > 0)
                CalculateCPS(tick);
        }


        private static void DoBackgroundTasks(long tick)
        {
            if (BeforeBackgroundTasksExecute != null)
                BeforeBackgroundTasksExecute();

            // raise pre-cycle timer events
            Timers.RaiseTimerEvents(TimerType.PreCycle, tick);

            // check for keyboard events
            Keyboard.RaiseKeyEvents(tick);

            // perform any timed GridPointMatrix scrolling
            foreach (GridPointMatrix matrix in GridPointMatrix.GetAllGridPointMatrix())
                matrix.MoveNext(tick);

            // perform any timed DirectDrawing scrolling
            foreach (DirectDrawing drawing in DirectDrawing.AllDirectDrawings)
                drawing.MoveNext(tick);

            // cycle Animator frames
            CycleAnimations(tick);

            // advance Sprite Movement paths
            Sprites.MoveSprites(tick);

            // check for Tile collisions
            RaiseCollisionEvent(tick);

            // refresh all VisibleSurface backbuffers
            DrawRefreshQueues();

            // all attached VisibleSurface backbuffers drawn; clear the refresh queues
            ClearRefreshQueues();

            if (AfterBackgroundTasksExecute != null)
                AfterBackgroundTasksExecute();
        }

        private static void DoForegroundTasks(long tick)
        {
            // raise event
            if (BeforeEngineCycle != null)
                BeforeEngineCycle(new EngineCycleEventArgs(_grossCyclesThisMeasure, _grossCycles, _netCyclesThisMeasure, _netCycles, _grossCPS, _netFPS));

            // render to each VisibleSurface
            foreach (VisibleSurfaceBase surface in VisibleSurfaces.AllVisibleSurfaces)
                surface.RenderBackbuffer(surface.RedrawDirtyRectangleOnly);

            // save time of this last tick; increment CPS counter
            _lastTick = tick;
            _netCyclesThisMeasure++;
            _netCycles++;

            // raise event
            if (AfterEngineCycle != null)
                AfterEngineCycle(new EngineCycleEventArgs(_grossCyclesThisMeasure, _grossCycles, _netCyclesThisMeasure, _netCycles, _grossCPS, _netFPS));

            // raise post-cycle timer events
            Timers.RaiseTimerEvents(TimerType.PostCycle, tick);
        }

        private static void DrawRefreshQueues()
        {
            foreach (VisibleSurfaceBase surface in VisibleSurfaces.AllVisibleSurfaces)
            {
                IBackbuffer backbuffer = surface.Buffer;
                GridPointMatrixes grids = backbuffer.DrawSource;

                if (grids == null || grids.Count == 0)
                {
                    // clear the entire backbuffer each pass
                    backbuffer.Erase();

                    // force refresh of all DirectDrawing objects
                    foreach (DirectDrawing drawing in DirectDrawing.AllDirectDrawings)
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
                            foreach (DirectDrawing direct in DirectDrawing.AllDirectDrawings)
                            {
                                if (grids.BackmostVisibleLayer.RefreshQueue.AreaIntersectsRefreshArea(direct.Bounds))
                                    direct.ForceRefresh();
                            }

                            // erase the existing areas in queue
                            backbuffer.Erase(grids.BackmostVisibleLayer.RefreshQueue.GetDirtyRectangles());

                            // draw from back to front from visible layers array
                            for (int i = grids.CountOfVisibleLayers - 1; i >= 0; i--)
                                surface.Buffer.DrawTiles(grids.VisibleGridPointMatrixList[i].RefreshQueue.Tiles);

                            break;

                        case MatrixesRefreshType.All:
                            // erase the entire backbuffer
                            backbuffer.Erase();

                            // force refresh of all DirectDrawing objects
                            foreach (DirectDrawing drawing in DirectDrawing.AllDirectDrawings)
                                drawing.ForceRefresh();

                            // draw from back to front from visible layers array
                            for (int i = grids.CountOfVisibleLayers - 1; i >= 0; i--)
                            {
                                // refreshing entire back buffer, so clear any partial queue...
                                grids.VisibleGridPointMatrixList[i].RefreshQueue.ClearRefreshQueue();

                                // find and add all Tile objects in range to queue
                                grids.VisibleGridPointMatrixList[i].RefreshQueue.AddPixelRangeToRefreshQueue(
                                    new Rectangle(0, 0, surface.Width, surface.Height), false);

                                // draw to backbuffer
                                surface.Buffer.DrawTiles(grids.VisibleGridPointMatrixList[i].RefreshQueue.Tiles);
                            }

                            break;

                        default:
                            // shouldn't get here
                            continue;
                    }
                }

                // draw all DirectDrawing objects that overlap with dirty rectangles and are "dirty"
                DirectDrawing.RenderAll();
            }
        }

        private static void ClearRefreshQueues()
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

        private static void CycleAnimations(long tick)
        {
            for (int i = 0; i < Tile.TilesAnimating.Count; i++)
                Tile.TilesAnimating[i].TileAnimator.CycleAnimation(tick);
        }

        private static void RaiseCollisionEvent(long tick)
        {
            // TODO: refactor this

            // only check for collisions if something is subscribed to see it
            if (TileCollisions != null)
            {
                List<Collision> collisions = new List<Collision>();

                // get a list of all Sprites and GridPoints that have collision detection turned on
                List<Tile> allSpritesAndCollisionTiles = new List<Tile>(Tile.TileCollisions);

                // add *all* Sprites if they are not already in list;
                // GridPoints don't move, so there's no need to do the same for them
                foreach (Tile tile in Sprites.AllSprites)
                {
                    if (allSpritesAndCollisionTiles.IndexOf(tile) == -1)
                        allSpritesAndCollisionTiles.Add(tile);
                }

                // now cycle through each Tile with collision detection turned on and check for collisions
                foreach (Tile tilePrimary in Tile.TileCollisions)
                {
                    List<Tile> secondaryList;

                    // find the list of Tiles that need to be checked for collisions against tilePrimary
                    switch (tilePrimary.DetectCollision)
                    {
                        case CollisionDetection.All:
                            // cycle through all Tile objects marked for collisions, and all Sprite objects
                            secondaryList = allSpritesAndCollisionTiles;
                            break;
                        case CollisionDetection.OthersWithColDetect:
                            // only cycle through other Tile objects with detection turned on
                            secondaryList = Tile.TileCollisions;
                            break;
                        default:
                            // shouldn't ever get here
                            secondaryList = new List<Tile>();
                            break;
                    }

                    // add any collisions detected for the tilePrimary to the list
                    collisions.AddRange(CheckForCollisions(tilePrimary, secondaryList));
                }

                // if collisions were detected...
                if (collisions.Count > 0)
                {
                    // ...raise the event
                    TileCollisions(new CollisionEventArgs(collisions));
                }
            }
        }

        private static List<Collision> CheckForCollisions(Tile primary, List<Tile> secondaryList)
        {
            List<Collision> collisions = new List<Collision>();
            Rectangle primaryLoc = primary.CollisionArea;

            foreach (Tile tile in secondaryList)
            {
                // only check for collisions if on same layer
                if (tile.ParentGrid == primary.ParentGrid)
                {
                    // Sprite can't collide with itself
                    if (tile != primary)
                    {
                        Rectangle secondaryLoc = tile.CollisionArea;

                        if (primaryLoc.IntersectsWith(secondaryLoc))
                        {
                            bool isNorth;   // is in top 25% of primaryLoc
                            bool isSouth;   // is in bottom 25% of primaryLoc
                            bool isWest;    // is in left 25% of primaryLoc
                            bool isEast;    // is in right 25% of primaryLoc

                            isNorth = secondaryLoc.IntersectsWith(new Rectangle(
                                primaryLoc.X, primaryLoc.Y,
                                primaryLoc.Width, (int)((float)primaryLoc.Height * (float)0.25)));

                            isSouth = secondaryLoc.IntersectsWith(new Rectangle(
                                primaryLoc.X, primaryLoc.Y + (int)((float)primaryLoc.Height * (float)0.75),
                                primaryLoc.Width, (int)((float)primaryLoc.Height * (float)0.25)));

                            isWest = secondaryLoc.IntersectsWith(new Rectangle(
                                primaryLoc.X, primaryLoc.Y,
                                (int)((float)primaryLoc.Width * (float)0.25), primaryLoc.Height));

                            isEast = secondaryLoc.IntersectsWith(new Rectangle(
                                primaryLoc.X + (int)((float)primaryLoc.Width * (float)0.75), primaryLoc.Y,
                                (int)((float)primaryLoc.Width * (float)0.25), primaryLoc.Height));

                            bool isNSCenter = !(isNorth ^ isSouth);
                            bool isWECenter = !(isWest ^ isEast);

                            if (isNSCenter && isWECenter)
                            {
                                collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.Center));
                            }
                            else if (isWECenter && isNorth)
                            {
                                collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.N));
                            }
                            else if (!isWECenter && isNorth && isEast)
                            {
                                collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.NE));
                            }
                            else if (isNSCenter && isEast)
                            {
                                collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.E));
                            }
                            else if (!isNSCenter && isEast && isSouth)
                            {
                                collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.SE));
                            }
                            else if (isWECenter && isSouth)
                            {
                                collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.S));
                            }
                            else if (!isWECenter && isSouth && isWest)
                            {
                                collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.SW));
                            }
                            else if (isNSCenter && isWest)
                            {
                                collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.W));
                            }
                            else if (!isNSCenter && isWest && isNorth)
                            {
                                collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.NW));
                            }
                        }
                    }
                }
            }

            return collisions;
        }

        private static void CalculateCPS(long tick)
        {
            // check if CPS Sampling time has passed
            if (tick - _lastCPSSamplingTick >= Configuration.Settings.SamplingTimeForCPSTicks)
            {
                _grossCPS = (double)(_grossCyclesThisMeasure * HighResTimer.TicksPerSecond) / (double)(tick - _lastCPSSamplingTick);
                _netFPS = (double)(_netCyclesThisMeasure * HighResTimer.TicksPerSecond) / (double)(tick - _lastCPSSamplingTick);

                // raise the event
                if (CPSCalculated != null)
                    CPSCalculated(new CyclesPerSecondCalculatedEventArgs(
                        _grossCyclesThisMeasure, _netCyclesThisMeasure, _grossCPS, _netFPS, Configuration.Settings.SamplingTimeForCPS));

                // reset values for next calculation
                _lastCPSSamplingTick = tick;
                _grossCyclesThisMeasure = 0;
                _netCyclesThisMeasure = 0;
            }
        }
        #endregion
    }
}
