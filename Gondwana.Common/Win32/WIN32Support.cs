using Gondwana.Common.Exceptions;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Gondwana.Common.Win32
{
    /// <summary>
    /// Static class encapsulating wrapper methods used for calling the Win32 API
    /// </summary>
    public static class Win32Support
    {
        public static int DrawBitmap(IntPtr destHdc, int destX, int destY, int destWidth, int destHeight,
            IntPtr srcHdc, int srcX, int srcY, int srcWidth, int srcHeight,
            TernaryRasterOperations dwRop)
        {
            if ((destWidth == srcWidth)
                && (destHeight == srcHeight))
            {
                return pInvoke.BitBlt(destHdc, destX, destY, destWidth, destHeight,
                    srcHdc, srcX, srcY, dwRop);
            }
            else
            {
                return pInvoke.StretchBlt(destHdc, destX, destY, destWidth, destHeight,
                    srcHdc, srcX, srcY, srcWidth, srcHeight, dwRop);
            }
        }

        public static int DrawBitmap(IntPtr destHdc, Rectangle destRect,
            IntPtr srcHdc, Rectangle srcRect, TernaryRasterOperations dwRop)
        {
            int destX = destRect.X;
            int destY = destRect.Y;
            int destWidth = destRect.Width;
            int destHeight = destRect.Height;

            int srcX = srcRect.X;
            int srcY = srcRect.Y;
            int srcWidth = srcRect.Width;
            int srcHeight = srcRect.Height;

            return DrawBitmap(destHdc, destX, destY, destWidth, destHeight,
                srcHdc, srcX, srcY, srcWidth, srcHeight, dwRop);
        }

        /// <summary>
        /// Set the screen resolution to specified size.  Will throw a <see cref="ResolutionChangeException" /> if unable to set to desired resolution.
        /// </summary>
        /// <exception cref="ResolutionChangeException" />
        /// <param name="size">Desired screen resolution</param>
        public static void SetScreenResolution(Size size)
        {
            DEVMODE1 dm = new DEVMODE1();
            dm.dmDeviceName = new String(new char[32]);
            dm.dmFormName = new String(new char[32]);
            dm.dmSize = (short)Marshal.SizeOf(dm);

            if (pInvoke.EnumDisplaySettings(null, DisplaySettingsConstants.ENUM_CURRENT_SETTINGS, ref dm) != 0)
            {
                dm.dmPelsWidth = size.Width;
                dm.dmPelsHeight = size.Height;

                int iRet = pInvoke.ChangeDisplaySettings(ref dm, DisplaySettingsConstants.CDS_TEST);
                if (iRet == DisplaySettingsConstants.DISP_CHANGE_FAILED)
                    throw new ResolutionChangeException(
                        "Call to ChangeDisplaySettings with CDS_TEST failed");
                else
                {
                    iRet = pInvoke.ChangeDisplaySettings(ref dm, DisplaySettingsConstants.CDS_UPDATEREGISTRY);
                    switch (iRet)
                    {
                        case DisplaySettingsConstants.DISP_CHANGE_SUCCESSFUL:
                            return;         // change successful
                        case DisplaySettingsConstants.DISP_CHANGE_RESTART:
                            throw new ResolutionChangeException(
                                "A reboot must take place for the changes to take affect");
                        default:
                            throw new ResolutionChangeException(
                                "Call to ChangeDisplaySettings with CDS_UPDATEREGISTRY failed");
                    }
                }
            }

            throw new ResolutionChangeException("Error on call to Win32Support.EnumDisplaySettings");
        }
    }
}
