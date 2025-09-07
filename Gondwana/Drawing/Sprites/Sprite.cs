using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Sprites;
using Gondwana.Rendering;
using Gondwana.Scenes;
using System.Drawing;
using System.Runtime.Serialization;

namespace Gondwana.Drawing.Sprites;

[DataContract(IsReference = true)]
public class Sprite : Tile, IDisposable, ICloneable
{
    #region events
    public event SpriteMovedEventHandler SpriteMoved;
    public event SpriteDisposingEventHandler Disposing;
    #endregion

    #region private / internal fields
    protected internal Movement movement;
    private string id;

    [DataMember(Name = "ParentGrid")]
    private SceneLayer parentGrid;

    private bool pauseMovement;
    private HorizontalAlignment horizAlign;
    private VerticalAlignment vertAlign;
    private int nudgeX;
    private int nudgeY;
    private Size renderSize;
    private PointF gridCoordinates;
    #endregion

    #region constructors / finalizer
    protected internal Sprite(SceneLayer matrix, Frame frame)
    {
        id = Guid.NewGuid().ToString();
        parentGrid = matrix;
        animator = new Animator(this);
        movement = new Movement(this);
        pauseAnimation = false;
        pauseMovement = false;
        horizAlign = HorizontalAlignment.Center;
        vertAlign = VerticalAlignment.Bottom;
        nudgeX = 0;
        nudgeY = 0;
        CurrentFrame = frame;

        if ((SpriteManager.SizeNewSpritesToParentGrid) && (parentGrid != null))
            renderSize = new Size(parentGrid.GridPointWidth, parentGrid.GridPointHeight);
        else
            renderSize = CurrentFrame.Tilesheet.TileSize;

        zOrder = 1;

        if (parentGrid != null)
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

        SpriteManager._spriteList.Add(this);
    }

    /// <summary>
    /// Private constructor used when calling the Clone() method on a Sprite.
    /// </summary>
    private Sprite(Sprite sprite)
    {
        id = Guid.NewGuid().ToString();
        animator = new Animator(this);
        movement = new Movement(this);
        SpriteManager._spriteList.Add(this);

        parentGrid = sprite.parentGrid;
        frame = sprite.frame;
        DetectCollision = sprite.collisionDetection;
        horizAlign = sprite.horizAlign;
        vertAlign = sprite.vertAlign;
        nudgeX = sprite.nudgeX;
        nudgeY = sprite.nudgeY;
        renderSize = sprite.renderSize;
        ZOrder = sprite.zOrder;
        visible = sprite.visible;
        gridCoordinates = sprite.gridCoordinates;
        AdjustCollisionArea = sprite.AdjustCollisionArea;

        parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
    }

    ~Sprite()
    {
        Dispose();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        animator = new Animator(this);
        movement = new Movement(this);
        pauseAnimation = false;
        pauseMovement = false;

        if (parentGrid != null)
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

        SpriteManager._spriteList.Add(this);
    }
    #endregion

    #region ICloneable Members
    /// <summary>
    /// does not copy the value of the Tag property
    /// </summary>
    /// <returns></returns>
    public object Clone()
    {
        Sprite newSprite = new Sprite(this);
        return newSprite;
    }
    #endregion

    #region public properties
    [DataMember]
    public string ID
    {
        get { return id; }
        set { id = value; }
    }

    [IgnoreDataMember]
    public Movement SpriteMovement
    {
        get { return movement; }
    }

    [IgnoreDataMember]
    public bool PauseMovement
    {
        get { return pauseMovement; }
        set { pauseMovement = value; }
    }

    [DataMember]
    public HorizontalAlignment HorizAlign
    {
        get { return horizAlign; }
        set
        {
            // add to refresh queue before and after property change
            if (parentGrid != null)
            {
                parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
                horizAlign = value;
                parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
            }
            else
                horizAlign = value;
        }
    }

    [DataMember]
    public VerticalAlignment VertAlign
    {
        get { return vertAlign; }
        set
        {
            // add to refresh queue before and after property change
            if (parentGrid != null)
            {
                parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
                vertAlign = value;
                parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
            }
            else
                vertAlign = value;
        }
    }

    [DataMember]
    public int NudgeX
    {
        get { return nudgeX; }
        set
        {
            // add to refresh queue before and after property change
            if (parentGrid != null)
            {
                parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
                nudgeX = value;
                parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
            }
            else
                nudgeX = value;
        }
    }

    [DataMember]
    public int NudgeY
    {
        get { return nudgeY; }
        set
        {
            // add to refresh queue before and after property change
            if (parentGrid != null)
            {
                parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
                nudgeY = value;
                parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
            }
            else
                nudgeY = value;
        }
    }

    [DataMember]
    public Size RenderSize
    {
        get { return renderSize; }
        set
        {
            // add to refresh queue before and after property change
            if (parentGrid != null)
            {
                parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
                renderSize = value;
                parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
            }
            else
                renderSize = value;
        }
    }

    [IgnoreDataMember]
    public override Rectangle DrawLocation
    {
        get
        {
            // if Sprite hasn't been placed on SceneLayer, this is moot
            if (parentGrid == null)
                return new Rectangle();

            // get the "top left" of the Sprite gridCoordinates value
            Point pxlPt = parentGrid.CoordinateSystem.GetSrcPxlAtGridPt(parentGrid, gridCoordinates);

            // adjust X coord
            switch (this.HorizAlign)
            {
                case HorizontalAlignment.Left:
                    // no adjustment necessary
                    break;
                case HorizontalAlignment.Center:
                    // shift right by half the difference between Tile Width values
                    // if Sprite Width > GridPt Width, Sprite will shift left
                    pxlPt.X += (parentGrid.GridPointWidth - renderSize.Width) / 2;
                    break;
                case HorizontalAlignment.Right:
                    // shift right by the entire difference between Tile Width values
                    // if Sprite Width > GridPt Width, Sprite will shift left
                    pxlPt.X += (parentGrid.GridPointWidth - renderSize.Width);
                    break;
                default:
                    // shouldn't get here...
                    break;
            }

            // adjust Y coord
            switch (this.VertAlign)
            {
                case VerticalAlignment.Top:
                    // no adjustment necessary
                    break;
                case VerticalAlignment.Middle:
                    // shift down by half the difference between Tile Height values
                    // if Sprite Height > GridPt Height, Sprite will shift up
                    pxlPt.Y += (parentGrid.GridPointHeight - renderSize.Height) / 2;
                    break;
                case VerticalAlignment.Bottom:
                    // shift down by the entire difference between Tile Height values
                    // if Sprite Height > GridPt Height, Sprite will shift up
                    pxlPt.Y += (parentGrid.GridPointHeight - renderSize.Height);
                    break;
                default:
                    // shouldn't get here...
                    break;
            }

            pxlPt.X += this.NudgeX;
            pxlPt.Y += this.NudgeY;

            return new Rectangle(pxlPt, renderSize);
        }
    }

    [IgnoreDataMember]
    public override bool IsPositionFixed
    {
        get { return false; }
    }

    [IgnoreDataMember]
    public override PointF GridCoordinates
    {
        get { return gridCoordinates; }
    }

    [IgnoreDataMember]
    public override SceneLayer ParentGrid
    {
        get { return parentGrid; }
    }

    [IgnoreDataMember]
    public virtual new int OverlappingPixels
    {
        get { return 0; }
    }

    [DataMember]
    public virtual new int ZOrder
    {
        get { return zOrder; }
        set
        {
            if (value < 1)
                base.ZOrder = 1;
            else
                base.ZOrder = value;
        }
    }
    #endregion

    #region public methods
    public void MoveSprite(float X, float Y)
    {
        MoveSprite(new PointF(X, Y));
    }

    public void MoveSprite(double X, double Y)
    {
        MoveSprite(new PointF((float)X, (float)Y));
    }

    public void MoveSprite(PointF newGridCoordinates)
    {
        // capture the Sprite coordinates before the move
        PointF oldCoord = gridCoordinates;

        // add to refresh queue before move, then move, then add to queue after move
        if (parentGrid != null)
        {
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
            gridCoordinates = newGridCoordinates;
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
        }
        else
            gridCoordinates = newGridCoordinates;

        // raise the SpriteMoved event
        if (SpriteMoved != null)
            SpriteMoved(new SpriteMovedEventArgs(this, oldCoord, newGridCoordinates));

        // TODO: how to handle this now that Parent / ghost children removed?
        if ((parentGrid.WrapHorizontally || parentGrid.WrapVertically))
            WrapSpriteLocation();
    }

    public void MoveSprite(Rectangle newDrawLocation)
    {
        RenderSize = new Size(newDrawLocation.Size.Width, newDrawLocation.Size.Height);
        MoveSprite(SpriteManager.GridCoordinates(this, parentGrid, newDrawLocation));
    }

    public void MoveSprite(SceneLayer newLayer)
    {
        Rectangle drawLoc = DrawLocation;

        if (parentGrid != null)
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

        parentGrid = newLayer;
        MoveSprite(drawLoc);
    }

    public void MoveSprite(SceneLayer newLayer, Size newSize)
    {
        MoveSprite(newLayer);
        RenderSize = newSize;
    }
    #endregion

    #region private methods
    private void WrapSpriteLocation()
    {
        // find the "wrapped" equivalent point of gridCoordinates
        PointF wrappedPt = parentGrid.CoordinateSystem.FindEquivGridCoord(gridCoordinates,
            parentGrid.GridColumnCount - 1, parentGrid.GridRowCount - 1);

        PointF moveTo = gridCoordinates;
        bool wrapped = false;

        // if horizontal wrapping is turned on and X is outside of X range, wrap it
        if (parentGrid.WrapHorizontally &&
            ((gridCoordinates.X >= parentGrid.GridColumnCount) || (gridCoordinates.X < 0)))
        {
            moveTo.X = wrappedPt.X;
            wrapped = true;
        }

        // if horizontal wrapping is turned on and Y is outside of Y range, wrap it
        if (parentGrid.WrapVertically &&
            ((gridCoordinates.Y >= parentGrid.GridRowCount) || (gridCoordinates.Y < 0)))
        {
            moveTo.Y = wrappedPt.Y;
            wrapped = true;
        }

        // if we wrapped, move the Sprite
        if (wrapped)
            MoveSprite(moveTo);
    }
    #endregion

    #region IDisposable Members
    public override void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Disposing != null)
            Disposing(new SpriteDisposingEventArgs(this));

        base.Dispose();

        if (parentGrid != null)
        {
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

            // just added Sprite and overlapping Tile objects to queue,
            // remove the actual Sprite from the queue since it will
            // no longer be available
            parentGrid.RefreshQueue.Tiles.Remove(this);
        }

        if (SpriteManager._spriteList.IndexOf(this) != -1)
            SpriteManager._spriteList.Remove(this);

        // clear the events
        SpriteMoved = null;
        Disposing = null;
    }
    #endregion
}
