using Microsoft.Extensions.Logging;

namespace Gondwana.Rendering;

public static class BackbufferFactory
{
    public static IBackbuffer Create(int width, int height)
    {
        try
        {
            return new GpuBackbuffer(width, height);
        }
        catch (Exception ex)
        {
            Engine.Logger.LogWarning(ex, "Failed to create GPU backbuffer, falling back to bitmap backbuffer.");
            return new BitmapBackbuffer(width, height);
        }
    }
}
