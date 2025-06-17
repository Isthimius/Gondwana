using Gondwana.Common;
using Gondwana.Common.Configuration;
using Gondwana.Common.Drawing;
using Gondwana.Common.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Gondwana.Rendering
{
    internal class ResizedFrame : IDisposable
    {
        #region static properties / methods
        internal static Dictionary<string, ResizedFrame> ResizedFrameCache = new Dictionary<string, ResizedFrame>();

        internal static ResizedFrame GetResizedFrame(Frame frame, Size renderSize)
        {
            var settings = EngineConfiguration.Open().Settings;
            ResizedFrame resizedFrame;

            if (ResizedFrameCache.TryGetValue(GetId(frame, renderSize), out resizedFrame) == false)
            {
                resizedFrame = new ResizedFrame(frame, renderSize);

                // check cache limit; if over, trim cache
                while (ResizedFrameCache.Count > settings.ResizedFrameCacheLimit)
                {
                    var oldestInCache = ResizedFrameCache.OrderBy(kvp => kvp.Value.CreationTick).First();
                    var oldResizedFrame = oldestInCache.Value;
                    ResizedFrameCache.Remove(oldestInCache.Key);
                    oldResizedFrame.Dispose();
                }
            }

            return resizedFrame;
        }

        internal static string GetId(Frame frame, Size renderSize)
        {
            return string.Format("{0}_{1}_{2}_{3}_{4}", frame.Tilesheet.Name, frame.XTile.ToString(), frame.YTile.ToString(), renderSize.Width.ToString(), renderSize.Height.ToString());
        }
        #endregion

        internal Frame OriginalFrame;
        internal Size RenderSize;
        private long CreationTick;

        private Bitmap NewBmp;
        private Graphics ResizedGraphics;
        internal IntPtr hDC;
        private IntPtr hBmp;

        private Bitmap NewBmpMask;
        private Graphics ResizedGraphicsMask;
        internal IntPtr hDCMask;
        private IntPtr hBmpMask;

        private ResizedFrame(Frame orig, Size render)
        {
            OriginalFrame = orig;
            RenderSize = render;
            CreationTick = HighResTimer.GetCurrentTickCount();

            // resize original Bitmap
            CreateResizedGDIBitmap(orig.Tilesheet, ref NewBmp, ref ResizedGraphics, ref hDC, ref hBmp);

            // resize Mask of original Bitmap
            if (OriginalFrame.Tilesheet.Mask != null)
                CreateResizedGDIBitmap(orig.Tilesheet.Mask, ref NewBmpMask, ref ResizedGraphicsMask, ref hDCMask, ref hBmpMask);

            // hook into Tilesheet Disposed event
            orig.Tilesheet.Disposed += Tilesheet_Disposed;

            ResizedFrameCache.Add(this.Id, this);
        }

        ~ResizedFrame()
        {
            Dispose();
        }

        private string Id
        {
            get { return ResizedFrame.GetId(OriginalFrame, RenderSize); }
        }
        
        private void CreateResizedGDIBitmap(Tilesheet origTilesheet, ref Bitmap newBmp, ref Graphics newGraphics, ref IntPtr hdc, ref IntPtr hbmp)
        {
            // create the new Bitmap object with the new stretched Size
            newBmp = new Bitmap(RenderSize.Width, RenderSize.Height);

            // get Graphics object from new Bitmap with new size
            newGraphics = Graphics.FromImage(newBmp);

            // get pointer to Graphics object for new Bitmap
            hdc = ResizedGraphics.GetHdc();

            // create and get handle to a GDI bitmap object compatible with the GDI+ Bitmap
            hbmp = NewBmp.GetHbitmap();

            // associate the new bitmap handle with the new Graphics handle
            pInvoke.SelectObject(hdc, hbmp);

            // draw (i.e., StretchBlt) to new Graphics object from original Frame Bitmap
            Win32Support.DrawBitmap(hdc,
                                    new Rectangle(0, 0, NewBmp.Width, NewBmp.Height),
                                    origTilesheet.hDC,
                                    origTilesheet.GetSourceRange(OriginalFrame.XTile, OriginalFrame.YTile),
                                    TernaryRasterOperations.SRCCOPY);

            // release the pointer to the Graphics Hdc
            ResizedGraphics.ReleaseHdc(hdc);
        }

        private void Tilesheet_Disposed(Common.EventArgs.TilesheetDisposedEventArgs e)
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            // if Tilesheet still exists (has not been Disposed), unhook from event
            if (OriginalFrame.Tilesheet != null)
                OriginalFrame.Tilesheet.Disposed -= Tilesheet_Disposed;

            // remove from static collection of ResizedFram instances
            ResizedFrameCache.Remove(this.Id);

            pInvoke.DeleteObject(hBmp);
            ResizedGraphics.Dispose();
            NewBmp.Dispose();

            if (ResizedGraphicsMask != null)
            {
                pInvoke.DeleteObject(hBmpMask);
                ResizedGraphicsMask.Dispose();
                NewBmpMask.Dispose();
            }
        }
    }
}