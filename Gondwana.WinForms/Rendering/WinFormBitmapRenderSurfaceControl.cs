using Gondwana.Rendering;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public partial class WinFormBitmapRenderSurfaceControl : UserControl
{
    private readonly SKControl _skControl;
    public RenderSurfaceHost RenderSurfaceHost { get; }

    public WinFormBitmapRenderSurfaceControl()
    {
        _skControl = new SKControl { Dock = DockStyle.Fill };
        Controls.Add(_skControl);

        // Create the render adapter for the SKControl
        var renderAdapter = new WinFormBitmapRenderSurfaceAdapter(_skControl);

        // Create the surface and hook the adapter
        RenderSurfaceHost = new RenderSurfaceHost(renderAdapter);

        // Create the Backbuffer
        //var screenBounds = Screen.FromControl(this).Bounds;
        var buffer = new BitmapBackbuffer(Width, Height);

        // Bind the buffer to the surface
        RenderSurfaceHost.Bind(buffer);
    }

    /// <summary> 
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
            RenderSurfaceHost?.Dispose();
        }

        base.Dispose(disposing);
    }
}
