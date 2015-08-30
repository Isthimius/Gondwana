using Gondwana.Common;
using Gondwana.Common.Enums;
using Gondwana.Common.EventArgs;
using Gondwana.Common.Grid;
using Gondwana.Common.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Gondwana.Rendering
{
    public class Backbuffer : IBackbuffer
    {
        #region private / internal field declarations
        protected internal Graphics dc;
        private Rectangle range;
        private Rectangle dirtyRectangle = new Rectangle();
        private GridPointMatrixes source;
        private SolidBrush fogBrush;
        private Pen gridPen;
        //private bool showGrid;
        #endregion

        #region delegates
        private GridPointMatrixesDisposingEventHandler matrixDisposeDel;
        #endregion

        #region constructors / finalizer
        protected internal Backbuffer(VisibleSurface visSurface)
        {
            // make the dimensions equal to the VisibleSurface dimensions
            range = new Rectangle(0, 0, visSurface.Width, visSurface.Height);

            // create a new temp GDI+ Bitmap to get old GDI32 bitmap handle
            Bitmap memBmp = new Bitmap(range.Width, range.Height, visSurface.DC);

            // obtain handle to VisibleSurface Graphics object for Backbuffer instantiation
            IntPtr visDC = visSurface.DC.GetHdc();

            // create a DC compatible with the visible surface DC
            IntPtr compatDC = pInvoke.CreateCompatibleDC(visDC);

            // associate the new bitmap handle with the Backbuffer DC
            pInvoke.SelectObject(compatDC, memBmp.GetHbitmap());

            //create a GDI+ Graphics object for the Backbuffer
            dc = Graphics.FromHdc(compatDC);

            // release handle to VisibleSurface Graphics object
            visSurface.DC.ReleaseHdc(visDC);

            // Dispose of temp GDI+ Bitmap
            memBmp.Dispose();

            // set default FogPen value to black with 128 alpha blending
            fogBrush = new SolidBrush(Color.FromArgb(128, Color.Black));

            // set default GridPen value to white
            gridPen = new Pen(Color.White);

            //showGrid = false;

            matrixDisposeDel = new GridPointMatrixesDisposingEventHandler(source_Disposing);
        }

        ~Backbuffer()
        {
            Dispose();
        }
        #endregion

        #region properties
        public Rectangle DirtyRectangle
        {
            get { return dirtyRectangle; }
            internal set { dirtyRectangle = value; }
        }

        public int Width
        {
            get { return range.Width; }
        }

        public int Height
        {
            get { return range.Height; }
        }

        public Graphics DC
        {
            get { return dc; }
        }

        public GridPointMatrixes DrawSource
        {
            get { return source; }
            internal set
            {
                // remove the subscription to the old source
                if (source != null)
                    source.Disposing -= matrixDisposeDel;

                // subscribe to the new source
                source = value;
                if (source != null)
                {
                    source.Disposing += matrixDisposeDel;
                    source.RefreshNeeded = MatrixesRefreshType.All;
                }
            }
        }

        public SolidBrush FogBrush
        {
            get { return fogBrush; }
            set
            {
                fogBrush = value;
                if (source != null)
                    source.RefreshNeeded = MatrixesRefreshType.All;
            }
        }

        public Pen GridPen
        {
            get { return gridPen; }
            set
            {
                gridPen = value;
                if (source != null)
                    source.RefreshNeeded = MatrixesRefreshType.All;
            }
        }
        #endregion

        #region public / internal methods
        public void SaveToFile(string file)
        {
            Bitmap toSave = new Bitmap(range.Width, range.Height, dc);
            Graphics graphics = Graphics.FromImage(toSave);

            IntPtr graphicsDC = graphics.GetHdc();
            IntPtr hDC = dc.GetHdc();
            Win32Support.DrawBitmap(graphicsDC, range, hDC, range, TernaryRasterOperations.SRCCOPY);
            dc.ReleaseHdc(hDC);
            graphics.ReleaseHdc(graphicsDC);

            toSave.Save(file);
            graphics.Dispose();
            toSave.Dispose();
        }

        public void Erase()
        {
            Erase(range);
        }

        public void Erase(Rectangle pxlRange)
        {
            IntPtr hDC = dc.GetHdc();
            pxlRange = Rectangle.Intersect(pxlRange, range);
            Win32Support.DrawBitmap(hDC, pxlRange, hDC, pxlRange, TernaryRasterOperations.BLACKNESS);
            AddToDirtyRectangle(pxlRange);
            dc.ReleaseHdc(hDC);
        }

        public void Erase(IList<Rectangle> areas)
        {
            if (areas.Count == 0)
                return;

            IntPtr hDC = dc.GetHdc();
            foreach (Rectangle area in areas)
            {
                Rectangle pxlRange = Rectangle.Intersect(area, range);
                Win32Support.DrawBitmap(hDC, pxlRange, hDC, pxlRange,
                    TernaryRasterOperations.BLACKNESS);
                AddToDirtyRectangle(pxlRange);
            }

            dc.ReleaseHdc(hDC);
        }

        public void DrawTiles(IList<Tile> tiles)
        {
            IntPtr hDC = dc.GetHdc();

            foreach (Tile tile in tiles)
                DrawTile(tile, ref hDC);

            dc.ReleaseHdc(hDC);

            // draw grid lines if turned on
            foreach (Tile tile in tiles)
            {
                if (tile.ParentGrid.ShowGridLines)
                {
                    if (tile.IsPositionFixed)
                        DrawGridLines(tile);
                }
            }

            // draw fog for GridPoint objects where turned on
            foreach (Tile tile in tiles)
            {
                if (tile.EnableFog)
                    DrawFog(tile);
            }
        }
        #endregion

        #region private methods
        private void DrawTile(Tile tile, ref IntPtr hDC)
        {
            Rectangle tileLoc = tile.DrawLocation;

            // redraw each "refresh area"
            foreach (Rectangle refreshRect in tile.DrawLocationRefresh)
            {
                // clip the DrawLocationRefresh to Backbuffer if necessary
                Rectangle tempRefreshLoc = Rectangle.Intersect(refreshRect, range);

                // if the Tile is outside the range, has no Tilesheet, or is not visible, return without blitting
                if ((tempRefreshLoc.IsEmpty) || (tile.CurrentFrame.Tilesheet == null) || (tile.Visible == false))
                    return;

                // check if source Frame needs to be resized
                ResizedFrame resizedFrame = null;
                if (tileLoc.Size != tile.CurrentFrame.Tilesheet.TileSize)
                    resizedFrame = ResizedFrame.GetResizedFrame(tile.CurrentFrame, tileLoc.Size);

                // get source refresh range, hDC, and related mask hdc
                Rectangle _srcRefreshRange;
                IntPtr _srcHDCMask;
                IntPtr _srcHDC;

                if (resizedFrame == null)
                {
                    // get the source Rectangle being rendered and Graphics / DC handles from the Tilesheet object
                    _srcRefreshRange = tile.CurrentFrame.Tilesheet.GetSourceRange(tile.CurrentFrame.XTile, tile.CurrentFrame.YTile);
                    _srcHDC = tile.CurrentFrame.Tilesheet.hDC;

                    if (tile.CurrentFrame.Tilesheet.Mask != null)
                        _srcHDCMask = tile.CurrentFrame.Tilesheet.Mask.hDC;
                    else
                        _srcHDCMask = default(IntPtr);
                }
                else
                {
                    // get the source Rectangle being rendered and Graphics / DC handles from the ResizedFrame object
                    _srcRefreshRange = new Rectangle(new Point(0, 0), resizedFrame.RenderSize);
                    _srcHDCMask = resizedFrame.hDCMask;
                    _srcHDC = resizedFrame.hDC;
                }

                // if only refreshing part of the Tile...
                if (tileLoc.Size != tempRefreshLoc.Size)
                    // capture just the section of the source Tilesheet that needs to be refreshed on Backbuffer
                    _srcRefreshRange = GetSourceBmpRangeForRefresh(_srcRefreshRange, tempRefreshLoc, tileLoc);

                // if the Tilesheet has a mask...
                if (tile.CurrentFrame.Tilesheet.Mask != null)
                {
                    // AND the mask
                    Win32Support.DrawBitmap(hDC, tempRefreshLoc,
                        _srcHDCMask, _srcRefreshRange,
                        TernaryRasterOperations.SRCAND);

                    // PAINT the primary
                    Win32Support.DrawBitmap(hDC, tempRefreshLoc,
                        _srcHDC, _srcRefreshRange,
                        TernaryRasterOperations.SRCPAINT);
                }
                else
                {
                    // use the specified Tile.RasterOp
                    Win32Support.DrawBitmap(hDC, tempRefreshLoc,
                        _srcHDC, _srcRefreshRange, tile.RasterOp);
                }
            }
        }

        private void DrawFog(Tile tile)
        {
            dc.FillPolygon(fogBrush, tile.OutlinePoints);
        }

        private void DrawGridLines(Tile tile)
        {
            dc.DrawPolygon(gridPen, tile.OutlinePoints);
        }

        private Rectangle GetSourceBmpRangeForRefresh(Rectangle srcBmpRefreshRange, Rectangle tempRefreshLoc, Rectangle tileLoc)
        {
            float shiftLeftRatio = (float)(tempRefreshLoc.Left - tileLoc.Left) / (float)tileLoc.Width;
            float shiftTopRatio = (float)(tempRefreshLoc.Top - tileLoc.Top) / (float)tileLoc.Height;
            float ratioWidth = (float)tempRefreshLoc.Width / (float)tileLoc.Width;
            float ratioHeight = (float)tempRefreshLoc.Height / (float)tileLoc.Height;

            Rectangle tmpSrcRange = srcBmpRefreshRange;
            tmpSrcRange.X += (int)Math.Floor((float)srcBmpRefreshRange.Width * shiftLeftRatio);
            tmpSrcRange.Y += (int)Math.Floor((float)srcBmpRefreshRange.Height * shiftTopRatio);
            tmpSrcRange.Width = (int)((float)srcBmpRefreshRange.Width * ratioWidth);
            tmpSrcRange.Height = (int)((float)srcBmpRefreshRange.Height * ratioHeight);

            return Rectangle.Intersect(srcBmpRefreshRange, tmpSrcRange);
            //return tmpSrcRange;
        }

        private void AddToDirtyRectangle(Rectangle area)
        {
            // update dirtyRectangle to include area drawn
            if (!area.IsEmpty)
            {
                if (dirtyRectangle.IsEmpty)
                    dirtyRectangle = area;
                else
                    dirtyRectangle = Rectangle.Union(dirtyRectangle, area);
            }
        }

        private void source_Disposing(GridPointMatrixesDisposingEventArgs e)
        {
            source = null;
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            dc.Dispose();
            fogBrush.Dispose();
            gridPen.Dispose();
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
