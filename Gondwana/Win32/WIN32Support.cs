using System.Drawing;

namespace Gondwana.Common.Win32;

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
}
