using System.Drawing;
using Avalonia.Controls;
using Gondwana.Avalonia.Rendering;

namespace Gondwana.Avalonia.Input;

internal static class PointerCoordinates
{
    internal static Point ToScreenPx(Control? control, PointF point)
    {
        if (control is AvaloniaGpuRenderSurfaceControl gpu)
        {
            // Events are DIPs; the GPU adapter presents in device pixels.
            var dpi = TopLevel.GetTopLevel(gpu)?.RenderScaling ?? 1d;
            return gpu.Adapter.AdapterPxToScreenPx(new PointF((float)(point.X * dpi), (float)(point.Y * dpi)));
        }
        if (control is AvaloniaBitmapRenderSurfaceControl bitmap)
            return bitmap.Adapter.AdapterPxToScreenPx(point);
        return new Point((int)Math.Floor(point.X), (int)Math.Floor(point.Y));
    }
}
