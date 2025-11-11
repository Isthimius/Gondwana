using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

/// <summary>
/// Parallax-aware camera: updates each visible SceneLayer.RenderSurfaceOriginPx.
/// It does NOT scale or place on screen; Viewport handles that.
/// </summary>
public sealed class Camera
{
    private readonly Scene _scene;

    public PointF PositionPx { get; private set; } = new(0, 0); // world UL
    public RectangleF WorldBoundsPx { get; set; } = RectangleF.Empty;

    public Rectangle DeadZonePx { get; set; } = Rectangle.Empty;
    public float FollowLerpPerSecond { get; set; } = 8f;

    private Func<PointF>? _followWorldPx;
    private bool _hardFollow;

    public Camera(Scene scene) => _scene = scene ?? throw new ArgumentNullException(nameof(scene));

    /// <summary>
    /// Returns the current visible world size (in pixels).
    /// Typically assigned by <see cref="View"/> to point at its <see cref="Viewport.VisibleWorldSizePx"/>.
    /// </summary>
    public Func<SizeF> GetVisibleWorldSizePx { get; set; } = () => new SizeF(1280, 720);

    public void SnapTo(PointF worldUpperLeftPx)
    {
        PositionPx = ClampToWorld(worldUpperLeftPx);
        PushToLayers();
    }

    public void CenterOn(PointF worldCenterPx)
    {
        var vis = GetVisibleWorldSizePx();
        SnapTo(new PointF(worldCenterPx.X - vis.Width * 0.5f,
                          worldCenterPx.Y - vis.Height * 0.5f));
    }

    public void PanBy(PointF deltaPx) => SnapTo(new PointF(PositionPx.X + deltaPx.X, PositionPx.Y + deltaPx.Y));

    public void Follow(Func<PointF> getWorldPixel, bool hardFollow = false)
    {
        _followWorldPx = getWorldPixel ?? throw new ArgumentNullException(nameof(getWorldPixel));
        _hardFollow = hardFollow;
    }

    public void ClearFollow() => _followWorldPx = null;

    internal void Update(float dtSeconds)
    {
        if (_followWorldPx is null) { PushToLayers(); return; }

        var desiredUL = DesiredUpperLeftToContainTarget(_followWorldPx());
        if (_hardFollow || FollowLerpPerSecond <= 0f)
        {
            PositionPx = ClampToWorld(desiredUL);
        }
        else
        {
            float t = 1f - (float)Math.Exp(-FollowLerpPerSecond * Math.Max(0f, dtSeconds));
            var clamped = ClampToWorld(desiredUL);
            PositionPx = new PointF(PositionPx.X + (clamped.X - PositionPx.X) * t,
                                    PositionPx.Y + (clamped.Y - PositionPx.Y) * t);
        }

        PushToLayers();
    }

    #region private methods
    private PointF DesiredUpperLeftToContainTarget(PointF targetWorldPx)
    {
        var vis = GetVisibleWorldSizePx();
        if (DeadZonePx == Rectangle.Empty)
            return new PointF(targetWorldPx.X - vis.Width * 0.5f,
                              targetWorldPx.Y - vis.Height * 0.5f);

        var viewWorld = new RectangleF(PositionPx.X, PositionPx.Y, vis.Width, vis.Height);
        var dzWorld = new RectangleF(viewWorld.X + DeadZonePx.X,
                                     viewWorld.Y + DeadZonePx.Y,
                                     DeadZonePx.Width, DeadZonePx.Height);

        if (dzWorld.Contains(targetWorldPx))
            return PositionPx;

        float newX = PositionPx.X;
        float newY = PositionPx.Y;

        if (targetWorldPx.X < dzWorld.Left)
            newX -= (dzWorld.Left - targetWorldPx.X);

        if (targetWorldPx.X > dzWorld.Right)
            newX += (targetWorldPx.X - dzWorld.Right);

        if (targetWorldPx.Y < dzWorld.Top)
            newY -= (dzWorld.Top - targetWorldPx.Y);

        if (targetWorldPx.Y > dzWorld.Bottom)
            newY += (targetWorldPx.Y - dzWorld.Bottom);

        return new PointF(newX, newY);
    }

    private PointF ClampToWorld(PointF ul)
    {
        if (WorldBoundsPx == RectangleF.Empty) return ul;

        var vis = GetVisibleWorldSizePx();
        float minX = WorldBoundsPx.Left;
        float minY = WorldBoundsPx.Top;
        float maxX = WorldBoundsPx.Right - vis.Width;
        float maxY = WorldBoundsPx.Bottom - vis.Height;

        if (maxX < minX)
            maxX = minX;

        if (maxY < minY)
            maxY = minY;

        return new PointF(Math.Clamp(ul.X, minX, maxX),
                          Math.Clamp(ul.Y, minY, maxY));
    }

    private void PushToLayers()
    {
        foreach (var layer in _scene.VisibleSceneLayers)
        {
            float p = layer.Parallax;
            int ox = (int)Math.Floor(-PositionPx.X * p);
            int oy = (int)Math.Floor(-PositionPx.Y * p);

            if (layer.RenderSurfaceOriginPx.X != ox || layer.RenderSurfaceOriginPx.Y != oy)
                layer.RenderSurfaceOriginPx = new Point(ox, oy);
        }
    }
    #endregion private methods
}
