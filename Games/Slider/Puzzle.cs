using Gondwana;
using Gondwana.Common.Drawing;
using Gondwana.Common.Drawing.Sprites;
using Gondwana.Common.Enums;
using Gondwana.Common.EventArgs;
using Gondwana.Common.Grid;
using Gondwana.Coordinates;
using Gondwana.Media;
using Gondwana.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Slider
{
    public class Puzzle : IDisposable
    {
        #region private / internal fields
        internal bool _spriteMoving = false;
        internal bool _isShuffling = false;

        private SpriteMovementEventHandler delMoveStart;
        private SpriteMovementEventHandler delMoveStop;

        private int numColumns;
        private int numRows;
        private Size originalSize;
        private Size adjustedSize;
        private Point openSpace;

        private VisibleSurface surface;
        private Tilesheet tilesheet;
        private GridPointMatrixes matrixes;
        #endregion

        #region constructors / destructor
        public Puzzle(Graphics dc, string imgFile, int columns, int rows, Size size)
        {
            tilesheet = new Tilesheet("picture", imgFile);

            int tileWidth = (int)((float)tilesheet.Bmp.Width / (float)columns);
            int tileHeight = (int)((float)tilesheet.Bmp.Height / (float)rows);
            int adjWidth = tileWidth * columns;
            int adjHeight = tileHeight * rows;

            tilesheet.TileSize = new Size(tileWidth, tileHeight);

            originalSize = tilesheet.Bmp.Size;
            numColumns = columns;
            numRows = rows;
            adjustedSize = new Size(adjWidth, adjHeight);

            GridPointMatrix matrix = new GridPointMatrix(numColumns, numRows, tileWidth, tileHeight);
            matrix.CoordinateSystem = new SquareIsoCoordinates();
            matrixes = new GridPointMatrixes(matrix);

            surface = new VisibleSurface(dc, size.Width, size.Height, matrixes);
            surface.Erase();

            InitializeSprites(tileWidth, tileHeight);
            //Gondwana.Scripting.Parser.WriteToFile("bmpProp_file.gond", System.IO.FileMode.Create, tilesheet);
            //Engine.ScriptEngineState("file.gond", true);

            delMoveStart = new SpriteMovementEventHandler(Sprites_SpriteMovementStarted);
            delMoveStop = new SpriteMovementEventHandler(Sprites_SpriteMovementStopped);

            Sprites.SpriteMovementStarted += delMoveStart;
            Sprites.SpriteMovementStopped += delMoveStop;
        }

        ~Puzzle()
        {
            Dispose();
        }
        #endregion

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
            get { return Sprites.AllSprites.Count; }
        }

        public int TotalPiecesCorrect
        {
            get
            {
                int totalCorrect = 0;

                foreach (Sprite sprite in Sprites.AllSprites)
                {
                    Point spriteLoc = new Point((int)sprite.GridCoordinates.X, (int)sprite.GridCoordinates.Y);

                    if (spriteLoc == ParseSpriteCoordID(sprite.ID))
                        totalCorrect++;
                }

                return totalCorrect;
            }
        }
        #endregion

        #region public methods
        public bool SlidePiece(Sprite sprite, double slideTime)
        {
            if (FindSpritesAdjToOpenSpace().IndexOf(sprite) == -1)
                // sprite not eligible to move
                return false;
            else
            {
                // capture the starting point of the sprite being moved
                Point startPt = new Point((int)sprite.GridCoordinates.X, (int)sprite.GridCoordinates.Y);

                // move the sprite to the open space
                sprite.SpriteMovement.Start(slideTime, new PointF((float)openSpace.X, (float)openSpace.Y));

                // make the openSpace value equal to the original sprite starting point
                openSpace = startPt;

                // move was successful
                return true;
            }
        }

        private int _totalMoves;
        private double _slideTime;
        private int _moveNumber;
        private Sprite _lastMoved;

        public void Shuffle(int totalMoves, double slideTime)
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
            return matrixes[0].CoordinateSystem.GetGridPtAtPxl(matrixes[0], new Point(pxlX, pxlY));
        }
        #endregion

        #region private methods
        private void InitializeSprites(int tileWidth, int tileHeight)
        {
            Sprites.Clear();

            for (int x = 0; x < numColumns; x++)
			{
                for (int y = 0; y < numRows; y++)
                {
                    Sprite sprite = Sprites.CreateSprite(matrixes[0], new Frame(tilesheet, x, y),
                        x.ToString() + "-" + y.ToString());
                    sprite.MoveSprite((float)x, (float)y);
                    sprite.Visible = true;
                }
			}

            // remove the bottom-right tile; this will be the space for sliding
            int maxX = numColumns - 1;
            int maxY = numRows - 1;
            Sprites.GetSpriteByID(maxX.ToString() + "-" + maxY.ToString()).Dispose();
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
            List<GridPoint> adjGridPts = new List<GridPoint>();

            adjGridPts.Add(
                matrixes[0].CoordinateSystem.GetAdjGridPt(matrixes[0][openSpace], CardinalDirections.N));
            adjGridPts.Add(
                matrixes[0].CoordinateSystem.GetAdjGridPt(matrixes[0][openSpace], CardinalDirections.S));
            adjGridPts.Add(
                matrixes[0].CoordinateSystem.GetAdjGridPt(matrixes[0][openSpace], CardinalDirections.E));
            adjGridPts.Add(
                matrixes[0].CoordinateSystem.GetAdjGridPt(matrixes[0][openSpace], CardinalDirections.W));

            foreach (GridPoint gPt in adjGridPts)
            {
                if (gPt != null)
                    adjSprites.AddRange(Sprites.GetSpritesInRange(gPt.DrawLocation));
            }

            return adjSprites;
        }
        #endregion

        #region event handlers
        private void Sprites_SpriteMovementStarted(SpriteMovementEventArgs e)
        {
            _spriteMoving = true;
        }

        private void Sprites_SpriteMovementStopped(SpriteMovementEventArgs e)
        {
            _spriteMoving = false;

            if (_isShuffling)
                ShuffleNext();
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            tilesheet.Dispose();
            matrixes.Dispose();
            surface.Dispose();
            Sprites.Clear();
            Sprites.SpriteMovementStarted -= delMoveStart;
            Sprites.SpriteMovementStopped -= delMoveStop;
        }
        #endregion
    }
}
