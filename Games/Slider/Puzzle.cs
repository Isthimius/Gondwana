using Gondwana;
using Gondwana.Audio;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Movement.Scripted;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace Slider
{
    public class Puzzle : IDisposable
    {
        #region private / internal fields

        internal bool _spriteMoving = false;
        internal bool _isShuffling = false;

        private readonly Action<ScriptedMovement> delMoveStart;
        private readonly Action<ScriptedMovement> delMoveStop;

        private int numColumns;
        private int numRows;
        private Size originalSize;
        private Size adjustedSize;
        private Point openSpace;

        private RenderSurfaceHost<BitmapBackbuffer> _renderSurfaceHost;
        private Tilesheet tilesheet;
        private Scene matrixes;

        private AudioResource slideSound;
        private AudioResource tadaSound;

        #endregion private / internal fields

        #region constructors / destructor

        public Puzzle(RenderSurfaceHost<BitmapBackbuffer> renderSurfaceHost, string imgFile, int columns, int rows, Size size)
        {
            tilesheet = new Tilesheet("picture", imgFile);
            tilesheet.ApplyPremultiplyAlpha();

            int tileWidth = (int)((float)tilesheet.SkBitmap.Width / (float)columns);
            int tileHeight = (int)((float)tilesheet.SkBitmap.Height / (float)rows);
            int adjWidth = tileWidth * columns;
            int adjHeight = tileHeight * rows;

            tilesheet.TileSize = new Size(tileWidth, tileHeight);

            originalSize = new Size(tilesheet.SkBitmap.Width, tilesheet.SkBitmap.Height);
            numColumns = columns;
            numRows = rows;
            adjustedSize = new Size(adjWidth, adjHeight);

            matrixes = new Scene();
            matrixes.AddLayer(numColumns, numRows, tileWidth, tileHeight, 0, 1, CoordinateSystemTypes.Orthogonal);

            //surface = new VisibleSurface(size.Width, size.Height, matrixes);
            //surface = new VisibleSurface(size.Width, size.Height);
            //surface.Backbuffer.Erase();

            Engine.Instance.InitializationComplete += OnEngineInitializationComplete;

            _renderSurfaceHost = renderSurfaceHost;
            _renderSurfaceHost.RedrawDirtyRectangleOnly = true;
            _renderSurfaceHost.Backbuffer.ClearColor = SkiaSharp.SKColors.Black;
            _renderSurfaceHost.Bind(matrixes);

            delMoveStart = Sprites_SpriteMovementStarted;
            delMoveStop = Sprites_SpriteMovementStopped;

            InitializeSprites(tileWidth, tileHeight);
            slideSound = AudioResourceManager.Instance.LoadFromFile("move", "assets/75143__willc2-45220__slide-cup-16b-44k-0-747s.wav");
            tadaSound = AudioResourceManager.Instance.LoadFromFile("tada", "assets/177120__rdholder__2dogsound-tadaa1-3s-2013jan31-cc-by-30-us.wav");

            //Engine.Instance.InitializeWinFormsAudioFormats();
            //Engine.Instance.InitializeXInputGamepadManager();
        }

        private void OnEngineInitializationComplete()
        {
            Engine.Instance.Configuration.TargetFPS = 120;
        }

        ~Puzzle()
        {
            Dispose();
        }

        #endregion constructors / destructor

        #region public properties

        public int Columns
        {
            get { return numColumns; }
        }

        public int Rows
        {
            get { return numRows; }
        }

        public Size OriginalBitmapSize
        {
            get { return originalSize; }
        }

        public Size AdjustedBitmapSize
        {
            get { return adjustedSize; }
        }

        public Point OpenSpace
        {
            get { return openSpace; }
        }

        public bool ShowGridLines
        {
            get { return matrixes[0].ShowGridLines; }
            set { matrixes[0].ShowGridLines = value; }
        }

        public int TotalPieces
        {
            get { return SpriteManager.AllSprites.Count; }
        }

        public int TotalPiecesCorrect
        {
            get
            {
                int totalCorrect = 0;

                foreach (Sprite sprite in SpriteManager.AllSprites)
                {
                    Point spriteLoc = new Point((int)sprite.SceneLayerCoordinates.X, (int)sprite.SceneLayerCoordinates.Y);

                    if (spriteLoc == ParseSpriteCoordID(sprite.Nickname))
                        totalCorrect++;
                }

                return totalCorrect;
            }
        }

        #endregion public properties

        #region public methods

        public bool SlidePiece(Sprite sprite, float slideTime)
        {
            if (FindSpritesAdjToOpenSpace().IndexOf(sprite) == -1)
                // sprite not eligible to move
                return false;
            else
            {
                // capture the starting point of the sprite being moved
                Point startPt = new Point((int)sprite.SceneLayerCoordinates.X, (int)sprite.SceneLayerCoordinates.Y);

                // move the sprite to the open space
                sprite.Movement.MoveTo(new Vector2(openSpace.X, openSpace.Y), slideTime, null , 0.01f);

                // make the openSpace value equal to the original sprite starting point
                openSpace = startPt;

                // move was successful
                return true;
            }
        }

        private int _totalMoves;
        private float _slideTime;
        private int _moveNumber;
        private Sprite _lastMoved;

        public void Shuffle(int totalMoves, float slideTime)
        {
            _isShuffling = true;
            _totalMoves = totalMoves;
            _slideTime = slideTime;
            _moveNumber = 0;
            _lastMoved = null;

            ShuffleNext();
        }

        private void ShuffleNext()
        {
            Random random = new Random();

            // find all pieces next to open space
            List<Sprite> sprites = FindSpritesAdjToOpenSpace();

            // pick one of the pieces at random
            Sprite sprite = sprites[random.Next(0, sprites.Count)];

            // don't move the same sprite 2 times in a row
            while (sprite == _lastMoved)
                sprite = sprites[random.Next(0, sprites.Count)];

            // move the piece
            SlidePiece(sprite, _slideTime);
            _lastMoved = sprite;

            if (++_moveNumber >= _totalMoves)
                _isShuffling = false;
        }

        public PointF GetGridCoordinates(int pxlX, int pxlY)
        {
            var view = _renderSurfaceHost.ViewManager.Views[0];
            var worldPx = view.ScreenPxToWorldPx(matrixes[0], new PointF(pxlX, pxlY));
            return matrixes[0].WorldPxToGrid(worldPx);
        }

        #endregion public methods

        #region private methods

        private void InitializeSprites(int tileWidth, int tileHeight)
        {
            SpriteManager.Clear();

            for (int x = 0; x < numColumns; x++)
            {
                for (int y = 0; y < numRows; y++)
                {
                    Sprite sprite = SpriteManager.CreateSprite(matrixes[0], new Frame(tilesheet, x, y),
                        x.ToString() + "-" + y.ToString());
                    sprite.SetPosition(new System.Numerics.Vector2((float)x, (float)y));
                    sprite.Visible = true;

                    sprite.Movement.ScriptedMovementStarted += delMoveStart;
                    sprite.Movement.ScriptedMovementStopped += delMoveStop;
                }
            }

            // remove the bottom-right tile; this will be the space for sliding
            int maxX = numColumns - 1;
            int maxY = numRows - 1;
            SpriteManager.GetSpriteByID(maxX.ToString() + "-" + maxY.ToString()).Dispose();
            openSpace = new Point(maxX, maxY);
        }

        private Point ParseSpriteCoordID(string ID)
        {
            string[] coords = ID.Split('-');
            int x = int.Parse(coords[0]);
            int y = int.Parse(coords[1]);
            return new Point(x, y);
        }

        private List<Sprite> FindSpritesAdjToOpenSpace()
        {
            List<Sprite> adjSprites = new List<Sprite>();
            List<SceneLayerTile> adjGridPts = new List<SceneLayerTile>();

            var layer = matrixes[0];
            var centerTile = layer[openSpace];

            adjGridPts.Add(layer.GetAdjacentTile(centerTile, CardinalDirections.N));
            adjGridPts.Add(layer.GetAdjacentTile(centerTile, CardinalDirections.S));
            adjGridPts.Add(layer.GetAdjacentTile(centerTile, CardinalDirections.E));
            adjGridPts.Add(layer.GetAdjacentTile(centerTile, CardinalDirections.W));

            foreach (SceneLayerTile gPt in adjGridPts)
            {
                if (gPt != null)
                    adjSprites.AddRange(SpriteManager.GetSpritesInWorldRectRange(gPt.DrawLocationWorld));
            }

            return adjSprites;
        }

        #endregion private methods

        #region event handlers

        private void Sprites_SpriteMovementStarted(ScriptedMovement scriptedMovement)
        {
            _spriteMoving = true;
            slideSound.Play();
        }

        private void Sprites_SpriteMovementStopped(ScriptedMovement scriptedMovement)
        {
            _spriteMoving = false;
            slideSound.Stop();

            if (_isShuffling)
                ShuffleNext();
        }

        #endregion event handlers

        #region IDisposable Members

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            tilesheet.Dispose();
            matrixes.Dispose();
            SpriteManager.Clear();
        }

        #endregion IDisposable Members
    }
}