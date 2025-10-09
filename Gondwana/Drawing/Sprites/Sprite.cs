using System.Drawing;
using System.Runtime.Serialization;
using Gondwana.Drawing.Animation;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites;

[JsonObject(IsReference = true)]
public class Sprite : Tile, IDisposable, ICloneable
{
    #region events

    public event SpriteMovedEventHandler SpriteMoved;

    public event SpriteDisposingEventHandler Disposing;

    #endregion events

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

    #endregion private / internal fields

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

    #endregion constructors / finalizer

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

    #endregion ICloneable Members

    #region public properties

    [JsonProperty]
    public string ID
    {
        get { return id; }
        set { id = value; }
    }

    [JsonIgnore]
    public Movement SpriteMovement
    {
        get { return movement; }
    }

    [JsonIgnore]
    public bool PauseMovement
    {
        get { return pauseMovement; }
        set { pauseMovement = value; }
    }

    [JsonProperty]
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

    [JsonProperty]
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

    [JsonProperty]
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

    [JsonProperty]
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

    [JsonProperty]
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

    [JsonIgnore]
    public override Rectangle DrawLocation
    {
        get { return SpriteManager.GetDrawLocation(this, parentGrid, gridCoordinates, renderSize); }
    }

    [JsonIgnore]
    public override bool IsPositionFixed
    {
        get { return false; }
    }

    [JsonIgnore]
    public override PointF GridCoordinates
    {
        get { return gridCoordinates; }
    }

    [JsonIgnore]
    public override SceneLayer ParentGrid
    {
        get { return parentGrid; }
    }

    [JsonProperty]
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

    #endregion public properties

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

    #endregion public methods

    #region private methods

    private void WrapSpriteLocation()
    {
        // find the "wrapped" equivalent point of gridCoordinates
        PointF wrappedPt = parentGrid.CoordinateSystem.FindEquivalentLayerPoint(gridCoordinates,
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

    #endregion private methods

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

            // just added Sprite and overhanging Tile objects to queue,
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

    #endregion IDisposable Members
}