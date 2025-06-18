using Gondwana.EventArgs;
using Gondwana.Timers;

namespace Gondwana.Drawing.Sprites;

public class Movement : IDisposable
{
    #region events
    public event SpriteMovementEventHandler Started;
    public event SpriteMovePointFinishedHandler MovePointFinished;
    public event SpriteMovementEventHandler Stopped;
    #endregion

    #region private / internal fields
    private Sprite _parent;
    internal long _lastTick;
    internal MovePoint _movePoint;
    #endregion

    #region constructors / finalizer
    protected internal Movement(Sprite sprite)
    {
        _parent = sprite;
        _lastTick = HighResTimer.GetCurrentTickCount();
        _velocityX = 0;
        _velocityY = 0;
        _accelerationX = 0;
        _accelerationY = 0;
    }

    ~Movement()
    {
        Dispose();
    }
    #endregion

    #region public properties
    public Sprite Parent
    {
        get { return _parent; }
    }

    public bool HasMovePoint
    {
        get
        {
            if (Tile.TilesMoving.IndexOf(_parent) == -1)
                return false;
            else
                return true;
        }
    }

    /// <summary>
    /// Returns total number of seconds left in current list of MovePoint objects for the
    /// parent Sprite.  If there are no MovePoint objects, will return 0.  If the list of
    /// MovePoint objects is recursive, will return -1.  Note that non 0 values in 
    /// VelocityX or VelocityY will not affect this property.
    /// <para>For <see cref="Sprite"/> instances moving with Velocity instead of a MovePoint, this value will be 0</para>
    /// </summary>
    public double TimeRemaining
    {
        get
        {
            if (_movePoint == null)
                return 0;
            else
            {
                // keep a list of all MovePoint objects accumulated; if the same
                // MovePoint instance is encountered twice, return -1
                List<MovePoint> breadcrumb = new List<MovePoint>();
                double timeRemaining = 0;
                MovePoint movePt = _movePoint;

                while (movePt != null)
                {
                    // if we've hit this MovePoint previously, we're moving recursively (looping)
                    if (breadcrumb.Contains(movePt) == true)
                        return -1;
                    else
                    {
                        breadcrumb.Add(movePt);
                        timeRemaining += movePt.SecondsUntilCompletion;
                        movePt = movePt.nextMovePoint;
                    }
                }

                return timeRemaining;
            }
        }
    }

    public MovePoint CurrentMovePoint
    {
        get { return _movePoint; }
    }

    private double _velocityX;          // tiles per second
    public double VelocityX
    {
        get { return _velocityX; }
        set
        {
            if (value == _velocityX)
                return;

            if (HasMovePoint)
                Stop();

            //_lastTick = HighResTimer.GetCurrentTickCount();
            _velocityX = value;

            LimitVelocityXByTerminal();
        }
    }

    private double _velocityY;          // tiles per second
    public double VelocityY
    {
        get { return _velocityY; }
        set
        {
            if (value == _velocityY)
                return;

            if (HasMovePoint)
                Stop();

            //_lastTick = HighResTimer.GetCurrentTickCount();
            _velocityY = value;

            LimitVelocityYByTerminal();
        }
    }

    private double _accelerationX;
    public double AccelerationX
    {
        get { return _accelerationX; }
        set
        {
            if (value == _accelerationX)
                return;

            if (_movePoint != null)
                Stop();

            //_lastTick = HighResTimer.GetCurrentTickCount();
            _accelerationX = value;
        }
    }

    private double _accelerationY;
    public double AccelerationY
    {
        get { return _accelerationY; }
        set
        {
            if (value == _accelerationY)
                return;

            if (_movePoint != null)
                Stop();

            //_lastTick = HighResTimer.GetCurrentTickCount();
            _accelerationY = value;
        }
    }

    private double _terminalVelocityXMin = double.MinValue;
    public double TerminalVelocityXMin
    {
        get { return _terminalVelocityXMin; }
        set
        {
            _terminalVelocityXMin = value;
            LimitVelocityXByTerminal();
        }
    }

    private double _terminalVelocityXMax = double.MaxValue;
    public double TerminalVelocityXMax
    {
        get { return _terminalVelocityXMax; }
        set
        {
            _terminalVelocityXMax = value;
            LimitVelocityXByTerminal();
        }
    }

    private double _terminalVelocityYMin = double.MinValue;
    public double TerminalVelocityYMin
    {
        get { return _terminalVelocityYMin; }
        set
        {
            _terminalVelocityYMin = value;
            LimitVelocityYByTerminal();
        }
    }

    private double _terminalVelocityYMax = double.MaxValue;
    public double TerminalVelocityYMax
    {
        get { return _terminalVelocityYMax; }
        set
        {
            _terminalVelocityYMax = value;
            LimitVelocityYByTerminal();
        }
    }
    #endregion

    #region public methods
    public void AddMovePoint(double totalTime, PointF destCoord)
    {
        AddMovePoint(totalTime, destCoord, _parent.RenderSize);
    }

    public void AddMovePoint(double totalTime, PointF destCoord, Size destSize)
    {
        AddMovePoint(new MovePoint(_parent, totalTime, destCoord, destSize));
    }

    public void AddMovePoint(double totalTime, Rectangle destLoc)
    {
        PointF destCoord = Sprites.GridCoordinates(_parent, _parent.ParentGrid, destLoc);
        Size destSize = new Size(destLoc.Width, destLoc.Height);
        AddMovePoint(totalTime, destCoord, destSize);
    }

    public void AddMovePoint(MovePoint movePt)
    {
        // if no existing MovePoint, or in a "looping" chain of MovePoints...
        if ((_movePoint == null) || (this.TimeRemaining == -1))
            _movePoint = movePt;
        else
        {
            // find the last MovePoints in the stack
            MovePoint lastMove = _movePoint;
            while (lastMove.nextMovePoint != null)
                lastMove = lastMove.nextMovePoint;

            // add the new MovePoints to the end of the stack
            lastMove.nextMovePoint = movePt;
        }
    }

    public void Start()
    {
        // don't do anything if there is not a current movePoint
        if (_movePoint == null)
            return;

        // ignore if Sprite already moving
        if (Tile.TilesMoving.IndexOf(_parent) != -1)
            return;

        // reset velocities since using MovePoints
        _velocityX = 0;
        _velocityY = 0;

        // add Sprite to moving list
        Tile.TilesMoving.Add(_parent);

        _lastTick = HighResTimer.GetCurrentTickCount();

        // initialize MovePoint values based on Sprite's current state
        _movePoint.InitializeMovePoint();

        // raise the event
        if (Started != null)
            Started(new SpriteMovementEventArgs(_parent, this));
    }

    public void Start(double totalTime, PointF destCoord)
    {
        // Start with the new coordinates, keeping the current RenderSize
        Start(totalTime, destCoord, _parent.RenderSize);
    }

    public void Start(double totalTime, PointF destCoord, Size destSize)
    {
        // passing in a new MovePoint, so stop the other if it exists
        //Stop();

        // set the new MovePoint
        _movePoint = new MovePoint(_parent, totalTime, destCoord, destSize);

        Start();
    }

    public void Start(double totalTime, Rectangle destLoc)
    {
        // passing in a new MovePoint, so stop the other if it exists
        //Stop();

        // set the new MovePoint
        _movePoint = new MovePoint(_parent, totalTime, destLoc);

        Start();
    }

    public void Finish()
    {
        Finish(true);
    }

    public void Finish(bool startNextMovePoint)
    {
        // don't do anything if there is not a current movePoint
        if (_movePoint == null)
            return;

        _parent.RenderSize = _movePoint.DestSize;
        _parent.MoveSprite(_movePoint.DestCoord);

        if (MovePointFinished != null)
            MovePointFinished(new SpriteMovePointFinishedEventArgs(_parent, this, _movePoint));

        if (_movePoint.nextMovePoint == null)
            Stop();
        else
        {
            _movePoint = _movePoint.nextMovePoint;
            _movePoint.InitializeMovePoint();
        }

        // remove from moving list if startNextMovePoint is false
        // this will stop the Sprite movement until Start() is called again
        if (!startNextMovePoint)
            Tile.TilesMoving.Remove(_parent);
    }

    public void FinishAll()
    {
        while (_movePoint != null)
            Finish(true);
    }

    public void Stop()
    {
        // reset velocities and acceleration
        _velocityX = 0;
        _velocityY = 0;
        _accelerationX = 0;
        _accelerationY = 0;

        // ignore if Sprite not moving via MovePoint
        if (Tile.TilesMoving.IndexOf(_parent) == -1)
            return;

        // remove from moving list
        Tile.TilesMoving.Remove(_parent);

        // raise the event
        if (Stopped != null)
            Stopped(new SpriteMovementEventArgs(_parent, this));

        // clear the current MovePoint
        _movePoint = null;
    }

    public void AdjustCurrentMovementSpeed(double byFactorOf)
    {
        // adjust speed of MovePoint chain
        MovePoint movePt = _movePoint;
        while (movePt != null)
        {
            movePt.TotalTicks = (long)((double)movePt.TotalTicks * byFactorOf);
            movePt.TotalTicksRunning = (long)((double)movePt.TotalTicksRunning * byFactorOf);
            movePt = movePt.nextMovePoint;
        }

        // adjust velocities
        _velocityX *= byFactorOf;
        _velocityY *= byFactorOf;

        LimitVelocityXByTerminal();
        LimitVelocityYByTerminal();
    }
    #endregion

    #region internal methods
    internal void MoveNext(long currentTick)
    {
        // if still on the same tick, no point in proceeding
        if (_lastTick == currentTick)
            return;

        // do not move if Movement is paused
        if (_parent.PauseMovement)
            return;

        // accumulate TotalTimeRunning
        _movePoint.TotalTicksRunning += (currentTick - _lastTick);
        //_lastTick = currentTick;

        if (_movePoint.TicksUntilCompletion <= 0)
            Finish(true);
        else
            CalcNextLocation();
    }

    internal void AdjustPositionByVelocity(long currentTick)
    {
        long tickDif = currentTick - _lastTick;

        // if still on the same tick, no point in proceeding
        if (tickDif == 0)
            return;

        //_lastTick = currentTick;
        double secondsElapsed = (double)tickDif / (double)HighResTimer.TicksPerSecond;

        // do not move if Movement is paused
        if (_parent.PauseMovement)
            return;

        // adjust velocity if acceleration is not 0
        if (AccelerationX != 0)
        {
            _velocityX += (float)(AccelerationX * secondsElapsed);
            LimitVelocityXByTerminal();
        }

        if (AccelerationY != 0)
        {
            _velocityY += (float)(AccelerationY * secondsElapsed);
            LimitVelocityYByTerminal();
        }

        // adjust the new DrawLocation
        PointF nextLoc = _parent.GridCoordinates;
        nextLoc.X += (float)(_velocityX * secondsElapsed);
        nextLoc.Y += (float)(_velocityY * secondsElapsed);

        // move the sprite to the new DrawLocation
        _parent.MoveSprite(nextLoc);
    }
    #endregion

    #region private methods
    private void CalcNextLocation()
    {
        Rectangle origLoc = Sprites.DrawLocation(_parent, _parent.ParentGrid,
            _movePoint.origCoord, _movePoint.origSize);
        Rectangle destLoc = _movePoint.DestDrawLocation;

        // calculate the percentage complete, based on time running vs total time originally
        double percentComplete = (double)_movePoint.TotalTicksRunning / (double)_movePoint.TotalTicks;

        // calculate the new DrawLocation
        Rectangle nextDrawLoc = origLoc;
        nextDrawLoc.X += (int)((double)(destLoc.X - origLoc.X) * percentComplete);
        nextDrawLoc.Y += (int)((double)(destLoc.Y - origLoc.Y) * percentComplete);
        nextDrawLoc.Width += (int)((double)(destLoc.Width - _movePoint.origSize.Width) * percentComplete);
        nextDrawLoc.Height += (int)((double)(destLoc.Height - _movePoint.origSize.Height) * percentComplete);

        // move the sprite to the new DrawLocation
        _parent.MoveSprite(nextDrawLoc);
    }

    private void LimitVelocityXByTerminal()
    {
        if (_velocityX < TerminalVelocityXMin)
            _velocityX = TerminalVelocityXMin;

        if (_velocityX > TerminalVelocityXMax)
            _velocityX = TerminalVelocityXMax;
    }

    private void LimitVelocityYByTerminal()
    {
        if (_velocityY < TerminalVelocityYMin)
            _velocityY = TerminalVelocityYMin;

        if (_velocityY > TerminalVelocityYMax)
            _velocityY = TerminalVelocityYMax;
    }
    #endregion

    #region IDisposable Members
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Started = null;
        MovePointFinished = null;
        Stopped = null;
    }
    #endregion
}
