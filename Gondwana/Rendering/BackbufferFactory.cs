using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gondwana.Rendering;

public static class BackbufferFactory
{
    public static BackbufferBase Create(int width, int height)
    {
        if (Engine.Instance.Configuration.UseGpuBackbuffer)
        {
            try
            {
                return new GpuBackbuffer(width, height);
            }
            catch (Exception ex)
            {
                Engine.Logger.LogWarning(ex, "Failed to create GPU backbuffer, falling back to bitmap backbuffer.");
            }
        }

        return new BitmapBackbuffer(width, height);
    }
}
