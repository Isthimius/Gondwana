using Gondwana.Drawing.Animation;
using Gondwana.Common.Enums;
using Gondwana.EventArgs;
using Gondwana.Grid;
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
    private GridPointMatrix parentGrid;

    private bool pauseMovement;
    private Common.Enums.HorizontalAlignment horizAlign;
    private VerticalAlignment vertAlign;
    private int nudgeX;
    private int nudgeY;
    private Size renderSize;
    private PointF gridCoordinates;
    #endregion

    #region constructors / finalizer
    protected internal Sprite(GridPointMatrix matrix, Frame frame)
    {
        id = Guid.NewGuid().ToString();
        parentGrid = matrix;
        animator = new Animator(this);
        movement = new Movement(this);
        pauseAnimation = false;
        pauseMovement = false;
        horizAlign = Common.Enums.HorizontalAlignment.Center;
        vertAlign = VerticalAlignment.Bottom;
        nudgeX = 0;
        nudgeY = 0;
        CurrentFrame = frame;

        if ((Sprites.SizeNewSpritesToParentGrid) && (parentGrid != null))
            renderSize = new Size(parentGrid.GridPointWidth, parentGrid.GridPointHeight);
        else
            renderSize = CurrentFrame.Tilesheet.TileSize;

        zOrder = 1;

        if (parentGrid != null)
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

        Sprites._spriteList.Add(this);
        CreateChildSprites();
    }

    /// <summary>
    /// Private constructor used when calling the Clone() method on a Sprite.
    /// </summary>
    private Sprite(Sprite sprite)
    {
        id = Guid.NewGuid().ToString();
        animator = new Animator(this);
        movement = new Movement(this);
        Sprites._spriteList.Add(this);

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

        if (parentGrid != null)
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

        Sprites.SubscribeToSpriteEvents(this);

        CreateChildSprites();
    }

    /// <summary>
    /// private constructor used when generating "child" Sprite objects.  Adds the new Sprite
    /// to the argument Sprite's childTiles List.  Does not add "child" Sprite to Engine-level
    /// Sprite List.  Does not register "child" Sprite events with static Sprites class.
    /// </summary>
    /// <param name="sprite"></param>
    /// <param name="gridCoord"></param>
    private Sprite(Sprite sprite, PointF gridCoord)
    {
        id = Guid.NewGuid().ToString();
        parentGrid = sprite.parentGrid;
        //animator = new Animator(this);
        //movement = new Movement(this);
        frame = sprite.frame;
        collisionDetection = sprite.collisionDetection;
        horizAlign = sprite.horizAlign;
        vertAlign = sprite.vertAlign;
        nudgeX = sprite.nudgeX;
        nudgeY = sprite.nudgeY;
        renderSize = sprite.renderSize;
        zOrder = sprite.zOrder;
        visible = sprite.visible;
        gridCoordinates = gridCoord;
        AdjustCollisionArea = sprite.AdjustCollisionArea;

        if (parentGrid != null)
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

        // add new Sprite to passed-in sprite's childTiles list
        sprite.AddChild(this);
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

        Sprites._spriteList.Add(this);
        Sprites.SubscribeToSpriteEvents(this);

        CreateChildSprites();
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
    public Common.Enums.HorizontalAlignment HorizAlign
    {
        get { return horizAlign; }
        set
        {
            // make same change in child sprites
            if (childTiles != null)
            {
                foreach (Sprite sprite in childTiles)
                    sprite.HorizAlign = value;
            }

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
            // make same change in child sprites
            if (childTiles != null)
            {
                foreach (Sprite sprite in childTiles)
                    sprite.VertAlign = value;
            }

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
            // make same change in child sprites
            if (childTiles != null)
            {
                foreach (Sprite sprite in childTiles)
                    sprite.NudgeX = value;
            }

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
            // make same change in child sprites
            if (childTiles != null)
            {
                foreach (Sprite sprite in childTiles)
                    sprite.NudgeY = value;
            }

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
            // make same change in child sprites
            if (childTiles != null)
            {
                foreach (Sprite sprite in childTiles)
                    sprite.RenderSize = value;
            }

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
        get { return Sprites.DrawLocation(this, parentGrid, gridCoordinates, renderSize); }
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
    public override GridPointMatrix ParentGrid
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
        // make same change in child sprites
        if (childTiles != null)
        {
            // find the net change in coordinates
            float adjX = newGridCoordinates.X - gridCoordinates.X;
            float adjY = newGridCoordinates.Y - gridCoordinates.Y;

            // apply net change to all child Sprites
            foreach (Sprite sprite in childTiles)
                sprite.MoveSprite(sprite.GridCoordinates.X + adjX, sprite.GridCoordinates.Y + adjY);
        }

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

        if ((parentGrid.WrapHorizontally || parentGrid.WrapVertically) && !IsChildTile)
            WrapSpriteLocation();
    }

    public void MoveSprite(Rectangle newDrawLocation)
    {
        RenderSize = new Size(newDrawLocation.Size.Width, newDrawLocation.Size.Height);
        MoveSprite(Sprites.GridCoordinates(this, parentGrid, newDrawLocation));
    }

    public void MoveSprite(GridPointMatrix newLayer)
    {
        Rectangle drawLoc = DrawLocation;

        if (parentGrid != null)
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

        parentGrid = newLayer;
        MoveSprite(drawLoc);

        // create new child Sprites on new grid
        CreateChildSprites();
    }

    public void MoveSprite(GridPointMatrix newLayer, Size newSize)
    {
        MoveSprite(newLayer);
        RenderSize = newSize;
    }
    #endregion

    #region internal methods
    protected internal void CreateChildSprites()
    {
        int xMin;
        int xMax;
        int yMin;
        int yMax;

        // remove previous child tiles
        DisposeChildTiles();

        // find number of times a sprite might be repeated within a single VisibleSurface
        int horizRepeats = (int)((float)VisibleSurfaces.MaxSurfaceSize.Width /
            (float)(parentGrid.GridPointWidth * parentGrid.GridColumnCount)) + 1;

        int vertiRepeats = (int)((float)VisibleSurfaces.MaxSurfaceSize.Height /
            (float)(parentGrid.GridPointHeight * parentGrid.GridRowCount)) + 1;

        // find the range in which to create child Sprites
        if (parentGrid.WrapHorizontally)
        {
            xMin = horizRepeats * -1;
            xMax = horizRepeats;
        }
        else
        {
            xMin = 0;
            xMax = 0;
        }

        if (parentGrid.WrapVertically)
        {
            yMin = vertiRepeats * -1;
            yMax = vertiRepeats;
        }
        else
        {
            yMin = 0;
            yMax = 0;
        }

        // step through range and create child Sprites
        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                // 0, 0 is the "parent" Sprite's coordinate; no child necessary here
                if ((x != 0) || (y != 0))
                {
                    // get the coordinates of the child Sprite
                    PointF gridCoord = new PointF(
                        this.gridCoordinates.X + (float)(x * parentGrid.GridColumnCount),
                        this.gridCoordinates.Y + (float)(y * parentGrid.GridRowCount));

                    // create a new Sprite and add to childTiles list
                    this.AddChild(new Sprite(this, gridCoord));
                }
            }
        }
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

        // remove all references from static Sprites to this Sprite
        if (!IsChildTile)
        {
            Sprites.UnsubscribeFromSpriteEvents(this);
            movement.Dispose();
        }
        else
            ParentTile.childTiles.Remove(this);

        if (parentGrid != null)
        {
            parentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

            // just added Sprite and overlapping Tile objects to queue,
            // remove the actual Sprite from the queue since it will
            // no longer be available
            parentGrid.RefreshQueue.Tiles.Remove(this);
        }

        if (Sprites._spriteList.IndexOf(this) != -1)
            Sprites._spriteList.Remove(this);

        // clear the events
        SpriteMoved = null;
        Disposing = null;
    }
    #endregion
}
