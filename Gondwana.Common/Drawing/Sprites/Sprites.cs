using Gondwana.Common.Enums;
using Gondwana.Common.EventArgs;
using Gondwana.Common.Grid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;

namespace Gondwana.Common.Drawing.Sprites
{
    public static class Sprites
    {
        #region private / internal fields
        internal static List<Sprite> _spriteList = new List<Sprite>();
        #endregion

        #region events
        public static event SpriteMovedEventHandler SpriteNewGridPoint;
        public static event SpriteDisposingEventHandler SpriteDisposing;
        public static event SpriteMovementEventHandler SpriteMovementStarted;
        public static event SpriteMovePointFinishedHandler SpriteMovePointFinished;
        public static event SpriteMovementEventHandler SpriteMovementStopped;
        public static event AnimatorEventHandler SpriteAnimationStarted;
        public static event AnimatorEventHandler SpriteAnimationStopped;
        public static event AnimatorEventHandler SpriteAnimatorCycled;
        #endregion

        #region delegates
        private static SpriteMovedEventHandler newCoordinates;
        private static SpriteDisposingEventHandler disposing;
        private static SpriteMovementEventHandler moveStart;
        private static SpriteMovePointFinishedHandler moveFinish;
        private static SpriteMovementEventHandler moveStop;
        private static AnimatorEventHandler animStart;
        private static AnimatorEventHandler animStop;
        private static AnimatorEventHandler animCycle;
        #endregion

        static Sprites()
        {
            SetEventDelegates();
        }

        #region properties
        public static ReadOnlyCollection<Sprite> AllSprites
        {
            get { return _spriteList.AsReadOnly(); }
        }

        private static bool _sizeNewSpriteToParentGrid = true;
        public static bool SizeNewSpritesToParentGrid
        {
            get { return _sizeNewSpriteToParentGrid; }
            set { _sizeNewSpriteToParentGrid = value; }
        }
        #endregion

        #region public methods
        public static Sprite CreateSprite(GridPointMatrix matrix, Frame frame)
        {
            Sprite sprite = new Sprite(matrix, frame);
            SubscribeToSpriteEvents(sprite);
            return sprite;
        }

        public static Sprite CreateSprite(GridPointMatrix matrix, Frame frame, string ID)
        {
            Sprite sprite = CreateSprite(matrix, frame);
            if (sprite != null)
                sprite.ID = ID;

            return sprite;
        }

        public static Sprite CloneSprite(Sprite sprite, GridPointMatrix destMatrix)
        {
            Sprite newSprite = (Sprite)sprite.Clone();
            if (newSprite.ParentGrid != destMatrix)
            {
                newSprite.MoveSprite(destMatrix);
                newSprite.ParentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(newSprite.DrawLocation, true);
            }

            return newSprite;
        }

        public static Sprite CloneSprite(string ID, GridPointMatrix destMatrix)
        {
            Sprite sprite = GetSpriteByID(ID);
            if (sprite != null)
                return CloneSprite(sprite, destMatrix);

            return null;
        }

        public static void Remove(Sprite sprite)
        {
            // Dispose method of Sprite adds area to Ref Queue and removes from spriteList
            sprite.Dispose();
        }

        public static void Remove(string ID)
        {
            Sprite sprite = GetSpriteByID(ID);
            if (sprite != null)
                Remove(sprite);
        }

        public static void Clear()
        {
            List<Sprite> tempSprites = new List<Sprite>(_spriteList);
            foreach (Sprite sprite in tempSprites)
                Remove(sprite);
        }

        public static Sprite GetSpriteByID(string ID)
        {
            foreach (Sprite sprite in _spriteList)
            {
                if (sprite.ID == ID)
                    return sprite;
            }

            return null;
        }

        public static List<Sprite> GetSpritesInRange(Rectangle range)
        {
            return GetSpritesInRange(range, false);
        }

        public static List<Sprite> GetSpritesInRange(Rectangle range, bool fullEnclosures)
        {
            List<Sprite> retSprites = new List<Sprite>();

            foreach (Sprite sprite in _spriteList)
            {
                // check if any childTiles in range; if so, return ParentTile
                if (sprite.childTiles != null)
                {
                    foreach (Sprite child in sprite.childTiles)
                    {
                        if (fullEnclosures)
                        {
                            if (range.Contains(child.DrawLocation))
                                retSprites.Add(sprite);
                        }
                        else
                        {
                            if (child.DrawLocation.IntersectsWith(range))
                                retSprites.Add(sprite);
                        }
                    }
                }

                // check if sprite in range
                if (fullEnclosures)
                {
                    if (range.Contains(sprite.DrawLocation))
                        retSprites.Add(sprite);
                }
                else
                {
                    if (sprite.DrawLocation.IntersectsWith(range))
                        retSprites.Add(sprite);
                }
            }

            return retSprites;
        }

        public static List<Sprite> GetSpritesInRange(Rectangle range, GridPointMatrix grid)
        {
            return GetSpritesInRange(range, grid, false);
        }

        public static List<Sprite> GetSpritesInRange(Rectangle range, GridPointMatrix grid, bool fullEnclosures)
        {
            List<Sprite> retSprites = new List<Sprite>();

            foreach (Sprite sprite in _spriteList)
            {
                if (sprite.ParentGrid == grid)
                {
                    // check if any childTiles in range; if so, return ParentTile
                    if (sprite.childTiles != null)
                    {
                        foreach (Sprite child in sprite.childTiles)
                        {
                            if (fullEnclosures)
                            {
                                if (range.Contains(child.DrawLocation))
                                    retSprites.Add(sprite);
                            }
                            else
                            {
                                if (child.DrawLocation.IntersectsWith(range))
                                    retSprites.Add(sprite);
                            }
                        }
                    }

                    // check if sprite in range
                    if (fullEnclosures)
                    {
                        if (range.Contains(sprite.DrawLocation))
                            retSprites.Add(sprite);
                    }
                    else
                    {
                        if (sprite.DrawLocation.IntersectsWith(range))
                            retSprites.Add(sprite);
                    }
                }
            }

            return retSprites;
        }

        public static List<Sprite> GetSpritesAtPoint(Point pxlPt)
        {
            List<Sprite> retSprites = new List<Sprite>();

            foreach (Sprite sprite in _spriteList)
            {
                // check if any childTiles at Point; if so, return ParentTile
                if (sprite.childTiles != null)
                {
                    foreach (Sprite child in sprite.childTiles)
                    {
                        if (child.DrawLocation.Contains(pxlPt))
                            retSprites.Add(sprite);
                    }
                }

                // check if sprite at Point
                if (sprite.DrawLocation.Contains(pxlPt))
                    retSprites.Add(sprite);
            }

            return retSprites;
        }

        public static List<Sprite> GetSpritesAtPoint(Point pxlPt, GridPointMatrix grid)
        {
            List<Sprite> retSprites = new List<Sprite>();

            foreach (Sprite sprite in _spriteList)
            {
                // check if any childTiles at Point; if so, return ParentTile
                if (sprite.childTiles != null)
                {
                    foreach (Sprite child in sprite.childTiles)
                    {
                        if ((sprite.ParentGrid == grid) && (child.DrawLocation.Contains(pxlPt)))
                            retSprites.Add(sprite);
                    }
                }

                // check if sprite at Point
                if ((sprite.ParentGrid == grid) && (sprite.DrawLocation.Contains(pxlPt)))
                    retSprites.Add(sprite);
            }

            return retSprites;
        }

        public static void DisposeAllNonVisibleSprites()
        {
            List<Sprite> tempSprites = new List<Sprite>(_spriteList);
            foreach (Sprite sprite in tempSprites)
            {
                if (!sprite.visible)
                    sprite.Dispose();
            }
        }

        public static void PauseAllAnimation(bool pause)
        {
            foreach (Sprite sprite in _spriteList)
                sprite.PauseAnimation = pause;
        }

        public static void PauseAllMovement(bool pause)
        {
            foreach (Sprite sprite in _spriteList)
                sprite.PauseMovement = pause;
        }
        #endregion

        #region internal methods
        internal static void SubscribeToSpriteEvents(Sprite sprite)
        {
            sprite.SpriteMoved += newCoordinates;
            sprite.Disposing += disposing;
            sprite.movement.Started += moveStart;
            sprite.movement.MovePointFinished += moveFinish;
            sprite.movement.Stopped += moveStop;
            sprite.animator.Started += animStart;
            sprite.animator.Stopped += animStop;
            sprite.animator.Cycled += animCycle;
        }

        internal static void UnsubscribeFromSpriteEvents(Sprite sprite)
        {
            sprite.SpriteMoved -= newCoordinates;
            sprite.Disposing -= disposing;
            sprite.movement.Started -= moveStart;
            sprite.movement.MovePointFinished -= moveFinish;
            sprite.movement.Stopped -= moveStop;
            sprite.animator.Started -= animStart;
            sprite.animator.Stopped -= animStop;
            sprite.animator.Cycled -= animCycle;
        }

        internal static Rectangle DrawLocation(Sprite sprite, GridPointMatrix grid, PointF coord, Size size)
        {
            // if Sprite hasn't been placed on GridPointMatrix, this is moot
            if (grid == null)
                return new Rectangle();

            // get the "top left" of the Sprite gridCoordinates value
            Point pxlPt = grid.CoordinateSystem.GetSrcPxlAtGridPt(grid, coord);

            // adjust X coord
            switch (sprite.HorizAlign)
            {
                case HorizontalAlignment.Left:
                    // no adjustment necessary
                    break;
                case HorizontalAlignment.Center:
                    // shift right by half the difference between Tile Width values
                    // if Sprite Width > GridPt Width, Sprite will shift left
                    pxlPt.X += (grid.GridPointWidth - size.Width) / 2;
                    break;
                case HorizontalAlignment.Right:
                    // shift right by the entire difference between Tile Width values
                    // if Sprite Width > GridPt Width, Sprite will shift left
                    pxlPt.X += (grid.GridPointWidth - size.Width);
                    break;
                default:
                    // shouldn't get here...
                    break;
            }

            // adjust Y coord
            switch (sprite.VertAlign)
            {
                case VerticalAlignment.Top:
                    // no adjustment necessary
                    break;
                case VerticalAlignment.Middle:
                    // shift down by half the difference between Tile Height values
                    // if Sprite Height > GridPt Height, Sprite will shift up
                    pxlPt.Y += (grid.GridPointHeight - size.Height) / 2;
                    break;
                case VerticalAlignment.Bottom:
                    // shift down by the entire difference between Tile Height values
                    // if Sprite Height > GridPt Height, Sprite will shift up
                    pxlPt.Y += (grid.GridPointHeight - size.Height);
                    break;
                default:
                    // shouldn't get here...
                    break;
            }

            pxlPt.X += sprite.NudgeX;
            pxlPt.Y += sprite.NudgeY;

            return new Rectangle(pxlPt, size);
        }

        internal static PointF GridCoordinates(Sprite sprite, GridPointMatrix grid, Rectangle drawLocation)
        {
            // if Sprite hasn't been placed on GridPointMatrix, this is moot
            if (grid == null)
                return new PointF();

            // work the Sprites.DrawLocation method backwards...
            drawLocation.X -= sprite.NudgeX;
            drawLocation.Y -= sprite.NudgeY;

            // adjust X coord
            switch (sprite.HorizAlign)
            {
                case HorizontalAlignment.Left:
                    // no adjustment necessary
                    break;
                case HorizontalAlignment.Center:
                    // shift left by half the difference between Tile Width values
                    // if Sprite Width > GridPt Width, Sprite will shift right
                    drawLocation.X -= (grid.GridPointWidth - drawLocation.Width) / 2;
                    break;
                case HorizontalAlignment.Right:
                    // shift left by the entire difference between Tile Width values
                    // if Sprite Width > GridPt Width, Sprite will shift right
                    drawLocation.X -= (grid.GridPointWidth - drawLocation.Width);
                    break;
                default:
                    // shouldn't get here...
                    break;
            }

            // adjust Y coord
            switch (sprite.VertAlign)
            {
                case VerticalAlignment.Top:
                    // no adjustment necessary
                    break;
                case VerticalAlignment.Middle:
                    // shift up by half the difference between Tile Height values
                    // if Sprite Height > GridPt Height, Sprite will shift down
                    drawLocation.Y -= (grid.GridPointHeight - drawLocation.Height) / 2;
                    break;
                case VerticalAlignment.Bottom:
                    // shift up by the entire difference between Tile Height values
                    // if Sprite Height > GridPt Height, Sprite will shift down
                    drawLocation.Y -= (grid.GridPointHeight - drawLocation.Height);
                    break;
                default:
                    // shouldn't get here...
                    break;
            }

            // find and return the grid coordinates after the Sprite adjustments have been considered
            return grid.CoordinateSystem.GetGridPtAtPxl(grid, drawLocation.Location);
        }

        public static void MoveSprites(long tick)
        {
            // advance MovePoints
            for (int i = 0; i < Tile.TilesMoving.Count; i++)
            {
                Sprite sprite = Tile.TilesMoving[i] as Sprite;
                if (sprite != null)
                    sprite.movement.MoveNext(tick);
            }

            // move by velocity
            foreach (Sprite sprite in _spriteList)
            {
                if ((sprite.movement.VelocityX != 0) || (sprite.movement.VelocityY != 0) ||
                    (sprite.movement.AccelerationX != 0) || (sprite.movement.AccelerationY != 0))
                    sprite.movement.AdjustPositionByVelocity(tick);

                sprite.movement._lastTick = tick;
            }
        }

        internal static void CreateChildSprites()
        {
            foreach (Sprite sprite in _spriteList)
                sprite.CreateChildSprites();
        }

        internal static void CreateChildSprites(GridPointMatrix grid)
        {
            foreach (Sprite sprite in _spriteList)
            {
                if (sprite.ParentGrid == grid)
                    sprite.CreateChildSprites();
            }
        }
        #endregion

        #region private methods
        private static void sprite_NewGridPoint(SpriteMovedEventArgs e)
        {
            // pass the event up...
            if (SpriteNewGridPoint != null)
                SpriteNewGridPoint(e);
        }

        private static void sprite_Disposing(SpriteDisposingEventArgs e)
        {
            // pass the event up...
            if (SpriteDisposing != null)
                SpriteDisposing(e);
        }

        private static void movement_Started(SpriteMovementEventArgs e)
        {
#if DEBUG
            Console.WriteLine("SpriteMovementStarted");
#endif

            // pass the event up...
            if (SpriteMovementStarted != null)
                SpriteMovementStarted(e);
        }

        private static void movement_MovePointFinished(SpriteMovePointFinishedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("SpriteMovePointFinished");
#endif

            // pass the event up...
            if (SpriteMovePointFinished != null)
                SpriteMovePointFinished(e);
        }

        private static void movement_Stopped(SpriteMovementEventArgs e)
        {
#if DEBUG
            Console.WriteLine("SpriteMovementStopped");
#endif

            // pass the event up...
            if (SpriteMovementStopped != null)
                SpriteMovementStopped(e);
        }

        private static void animator_Started(AnimatorEventArgs e)
        {
            // pass the event up...
            if (SpriteAnimationStarted != null)
                SpriteAnimationStarted(e);
        }

        private static void animator_Stopped(AnimatorEventArgs e)
        {
            // pass the event up...
            if (SpriteAnimationStopped != null)
                SpriteAnimationStopped(e);
        }

        private static void animator_Cycled(AnimatorEventArgs e)
        {
            // pass the event up...
            if (SpriteAnimatorCycled != null)
                SpriteAnimatorCycled(e);
        }

        /// <summary>
        /// set delegates to be used to subscribe to Sprite events
        /// </summary>
        private static void SetEventDelegates()
        {
            newCoordinates = new SpriteMovedEventHandler(sprite_NewGridPoint);
            disposing = new SpriteDisposingEventHandler(sprite_Disposing);
            moveStart = new SpriteMovementEventHandler(movement_Started);
            moveFinish = new SpriteMovePointFinishedHandler(movement_MovePointFinished);
            moveStop = new SpriteMovementEventHandler(movement_Stopped);
            animStart = new AnimatorEventHandler(animator_Started);
            animStop = new AnimatorEventHandler(animator_Stopped);
            animCycle = new AnimatorEventHandler(animator_Cycled);
        }
        #endregion
    }
}
