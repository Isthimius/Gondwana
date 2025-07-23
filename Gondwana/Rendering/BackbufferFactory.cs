using Gondwana.Grid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gondwana.Rendering;

public static class BackbufferFactory
{
    public static BackbufferBase Create(int width, int height, GridPointMatrixes drawSource)
    {
        if (Engine.Instance.Configuration.UseGpuBackbuffer)
        {
            try
            {
                return new GpuBackbuffer(width, height, drawSource);
            }
            catch (Exception ex)
            {
                Engine.Logger.LogWarning(ex, "Failed to create GPU backbuffer, falling back to bitmap backbuffer.");
            }
        }

        return new BitmapBackbuffer(width, height, drawSource);
    }
}
