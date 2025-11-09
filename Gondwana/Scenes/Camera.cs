using System;
using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

/// <summary>
/// Parallax-aware camera that controls how SceneLayers map world->screen.
/// - Set ViewportPx to your render surface size.
/// - Set WorldBoundsPx to your total world rect in pixels (optional).
/// - Call Update(dt) each frame; call Follow(...) or SnapTo(...) to move.
/// It writes RenderSurfaceOriginPx on each visible SceneLayer.
/// </summary>
public sealed class Camera
{
    private readonly Scene _scene;

    // Upper-left of the camera in WORLD PIXELS (what the camera is looking at)
    public PointF PositionPx { get; private set; } = new PointF(0, 0);

    // Size of the render target in pixels (used for clamping)
    public Size ViewportPx { get; set; } = new Size(1280, 720);

    // Optional world bounds in pixels. If empty, no clamping.
    public RectangleF WorldBoundsPx { get; set; } = RectangleF.Empty;

    // Dead-zone (relative to viewport) to reduce camera jitter while following
    // Example: new Rectangle(400, 240, 480, 240) for a 1280x720 viewport
    public Rectangle DeadZonePx { get; set; } = Rectangle.Empty;

    // Smooth follow
    public float FollowLerpPerSecond { get; set; } = 8f; // 0 = snap

    // Target in world pixels (null = free camera)
    private Func<PointF>? _followWorldPx;
    private bool _hardFollow;

    public Camera(Scene scene)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    /// <summary>Snap the camera so that PositionPx becomes the given world pixel.</summary>
    public void SnapTo(PointF worldUpperLeftPx)
    {
        PositionPx = ClampToWorld(worldUpperLeftPx);
        PushToLayers();
    }

    /// <summary>Center the camera on a world pixel position.</summary>
    public void CenterOn(PointF worldCenterPx)
    {
        var ul = new PointF(worldCenterPx.X - ViewportPx.Width * 0.5f,
                            worldCenterPx.Y - ViewportPx.Height * 0.5f);
        SnapTo(ul);
    }

    /// <summary>Pan by a pixel delta in world space.</summary>
    public void PanBy(PointF deltaPx)
    {
        SnapTo(new PointF(PositionPx.X + deltaPx.X, PositionPx.Y + deltaPx.Y));
    }

    /// <summary>Follow a moving world-space pixel point (e.g., player sprite center).</summary>
    public void Follow(Func<PointF> getWorldPixel, bool hardFollow = false)
    {
        _followWorldPx = getWorldPixel ?? throw new ArgumentNullException(nameof(getWorldPixel));
        _hardFollow = hardFollow;
    }

    /// <summary>Stop following.</summary>
    public void ClearFollow() => _followWorldPx = null;

    /// <summary>Advance camera one frame. dtSeconds = time step.</summary>
    public void Update(float dtSeconds)
    {
        if (_followWorldPx is null)
        {
            // Free camera: still push parallax origins in case viewport changed.
            PushToLayers();
            return;
        }

        var target = _followWorldPx();

        // Compute the desired UL so target lies within the dead-zone (or centered if none)
        var desiredUL = DesiredUpperLeftToContainTarget(target);

        if (_hardFollow || FollowLerpPerSecond <= 0f)
        {
            PositionPx = ClampToWorld(desiredUL);
        }
        else
        {
            // Critically-damped-ish simple lerp
            float t = 1f - (float)Math.Exp(-FollowLerpPerSecond * Math.Max(0f, dtSeconds));
            var cur = PositionPx;
            var clamped = ClampToWorld(desiredUL);
            PositionPx = new PointF(cur.X + (clamped.X - cur.X) * t,
                                    cur.Y + (clamped.Y - cur.Y) * t);
        }

        PushToLayers();
    }

    // --- Helpers ---------------------------------------------------------

    private PointF DesiredUpperLeftToContainTarget(PointF targetWorldPx)
    {
        if (DeadZonePx == Rectangle.Empty)
        {
            // Center the target if no dead-zone is defined
            return new PointF(targetWorldPx.X - ViewportPx.Width * 0.5f,
                              targetWorldPx.Y - ViewportPx.Height * 0.5f);
        }

        // World-space rect currently visible
        var viewWorld = new RectangleF(PositionPx.X, PositionPx.Y, ViewportPx.Width, ViewportPx.Height);
        // World-space dead-zone rect
        var dzWorld = new RectangleF(viewWorld.X + DeadZonePx.X,
                                     viewWorld.Y + DeadZonePx.Y,
                                     DeadZonePx.Width, DeadZonePx.Height);

        // If target is inside dead-zone, no change
        if (dzWorld.Contains(targetWorldPx)) return PositionPx;

        float newX = PositionPx.X;
        float newY = PositionPx.Y;

        if (targetWorldPx.X < dzWorld.Left) newX -= (dzWorld.Left - targetWorldPx.X);
        if (targetWorldPx.X > dzWorld.Right) newX += (targetWorldPx.X - dzWorld.Right);
        if (targetWorldPx.Y < dzWorld.Top) newY -= (dzWorld.Top - targetWorldPx.Y);
        if (targetWorldPx.Y > dzWorld.Bottom) newY += (targetWorldPx.Y - dzWorld.Bottom);

        return new PointF(newX, newY);
    }

    private PointF ClampToWorld(PointF ul)
    {
        if (WorldBoundsPx == RectangleF.Empty) return ul;

        float minX = WorldBoundsPx.Left;
        float minY = WorldBoundsPx.Top;
        float maxX = WorldBoundsPx.Right - ViewportPx.Width;
        float maxY = WorldBoundsPx.Bottom - ViewportPx.Height;

        // If the world is smaller than the viewport, lock to min
        if (maxX < minX) maxX = minX;
        if (maxY < minY) maxY = minY;

        return new PointF(
            Math.Clamp(ul.X, minX, maxX),
            Math.Clamp(ul.Y, minY, maxY)
        );
    }

    private void PushToLayers()
    {
        // For each visible layer, compute originPx = -cameraUL * Parallax
        // (Parallax of 1.0 tracks camera exactly; <1.0 moves slower; >1.0 faster.)
        foreach (var layer in _scene.VisibleSceneLayers)
        {
            float p = layer.Parallax; // already exposed on SceneLayer
            int ox = (int)Math.Floor(-PositionPx.X * p);
            int oy = (int)Math.Floor(-PositionPx.Y * p);

            // Only push if changed to avoid excess invalidation
            if (layer.RenderSurfaceOriginPx.X != ox || layer.RenderSurfaceOriginPx.Y != oy)
                layer.RenderSurfaceOriginPx = new Point(ox, oy);
        }
    }
}
