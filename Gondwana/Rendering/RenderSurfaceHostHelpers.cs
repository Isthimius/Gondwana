using System;
using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

// Small helper intended to be unit-testable. Computes the adapter/view dirty rect
// produced by projecting a scene's per-tile world dirty rectangles into screen space.
internal static class RenderSurfaceHostHelpers
{
    public static Rectangle ComputeViewDirtyRectangle(View view, Scene scene)
    {
        if (view is null)
            throw new ArgumentNullException(nameof(view));

        if (scene is null)
            throw new ArgumentNullException(nameof(scene));

        var camera = view.Camera;
        var viewport = view.Viewport;
        float zoom = (viewport.Zoom <= 0f) ? 1e-6f : viewport.Zoom;
        float inverseZoom = 1f / zoom;

        Rectangle viewDirty = Rectangle.Empty;

        for (int i = 0; i < scene.CountOfVisibleLayers; i++)
        {
            var layer = scene.VisibleSceneLayers[i];
            var tiles = layer.RefreshQueue.Tiles;
            float parallax = layer.Parallax;

            for (int t = 0; t < tiles.Count; t++)
            {
                var tile = tiles[t];

                if (tile.DrawLocationRefresh is not null && tile.DrawLocationRefresh.Count > 0)
                {
                    for (int r = 0; r < tile.DrawLocationRefresh.Count; r++)
                    {
                        AccumulateDirtyRect(tile.DrawLocationRefresh[r], parallax, camera, viewport, inverseZoom, ref viewDirty);
                    }
                }
                else
                {
                    AccumulateDirtyRect(tile.DrawLocation, parallax, camera, viewport, inverseZoom, ref viewDirty);
                }
            }
        }

        // Clip to this viewport's target rectangle (same behavior as before)
        if (!viewDirty.IsEmpty)
            viewDirty.Intersect(viewport.TargetRectPx);

        return viewDirty;
    }

    private static void AccumulateDirtyRect(Rectangle rr, float parallax, Camera camera, Viewport viewport, float inverseZoom, ref Rectangle viewDirty)
    {
        // Render path per layer is:
        // screen = viewportOffset + (world - camera * parallax) / zoom

        float localX = rr.Left - camera.PositionPx.X * parallax;
        float localY = rr.Top - camera.PositionPx.Y * parallax;

        float screenX = viewport.TargetRectPx.Left + viewport.ScreenOffsetPx.X + localX * inverseZoom;
        float screenY = viewport.TargetRectPx.Top + viewport.ScreenOffsetPx.Y + localY * inverseZoom;

        int sx = (int)Math.Floor(screenX);
        int sy = (int)Math.Floor(screenY);

        int sw = (int)Math.Ceiling(rr.Width * inverseZoom);
        int sh = (int)Math.Ceiling(rr.Height * inverseZoom);

        var scr = new Rectangle(sx, sy, sw, sh);

        // Safety: inflate by 1px to eat rounding seams (preserve previous behavior)
        scr.Inflate(1, 1);

        if (!scr.IsEmpty)
            viewDirty = viewDirty.IsEmpty ? scr : Rectangle.Union(viewDirty, scr);
    }
}
