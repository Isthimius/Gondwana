using System.Drawing;
using Gondwana.Drawing.Collisions;

namespace Gondwana.Drawing.Sprites;

public partial class Sprite
{
    // --- RenderSize tween state ---
    private bool _isResizing;
    private float _resizeElapsedSeconds;
    private float _resizeDurationSeconds;
    private Size _resizeStart;
    private Size _resizeTarget;

    // --- Collision adjustment tween baseline ---
    private CollisionDetectionAdjustment _resizeStartCollisionAdjust;
    private Size _resizeStartSizeForCollision;

    internal void AdvanceResize(float deltaSeconds)
    {
        if (!_isResizing)
            return;

        if (_resizeDurationSeconds <= 0f)
        {
            RenderSize = _resizeTarget;
            ApplyScaledCollisionAdjust(RenderSize);
            _isResizing = false;
            return;
        }

        _resizeElapsedSeconds += deltaSeconds;

        float t = _resizeElapsedSeconds / _resizeDurationSeconds;
        if (t >= 1f) t = 1f;

        int w = (int)MathF.Round(_resizeStart.Width + ((_resizeTarget.Width - _resizeStart.Width) * t));
        int h = (int)MathF.Round(_resizeStart.Height + ((_resizeTarget.Height - _resizeStart.Height) * t));

        var next = new Size(Math.Max(1, w), Math.Max(1, h));

        if (next != renderSize)
        {
            RenderSize = next;                 // invalidation happens here (your setter)
            ApplyScaledCollisionAdjust(next);   // keep collision proportional
        }

        if (t >= 1f)
            _isResizing = false;
    }

    private void ApplyScaledCollisionAdjust(Size currentSize)
    {
        // Guard: avoid divide-by-zero and nonsense
        if (_resizeStartSizeForCollision.Width <= 0 || _resizeStartSizeForCollision.Height <= 0)
            return;

        float sx = (float)currentSize.Width / _resizeStartSizeForCollision.Width;
        float sy = (float)currentSize.Height / _resizeStartSizeForCollision.Height;

        // Scale horizontal adjustments by sx, vertical by sy
        AdjustCollisionArea = new CollisionDetectionAdjustment(
            top: (int)MathF.Round(_resizeStartCollisionAdjust.Top * sy),
            bottom: (int)MathF.Round(_resizeStartCollisionAdjust.Bottom * sy),
            left: (int)MathF.Round(_resizeStartCollisionAdjust.Left * sx),
            right: (int)MathF.Round(_resizeStartCollisionAdjust.Right * sx)
        );
    }

    /// <summary>
    /// Smoothly resize the sprite to an absolute pixel size over the given duration (seconds).
    /// </summary>
    public void ResizeTo(Size targetSize, float durationSeconds)
    {
        _resizeStart = RenderSize;
        _resizeTarget = targetSize;
        _resizeDurationSeconds = durationSeconds;
        _resizeElapsedSeconds = 0f;
        _isResizing = true;

        // collision baseline
        _resizeStartCollisionAdjust = AdjustCollisionArea;
        _resizeStartSizeForCollision = RenderSize;

        // Make sure we repaint right away if something changes on first tick
        QueueRefreshArea(Rectangle.Union(this.DrawLocation, SpriteManager.GetDrawLocation(this, _sceneLayer, sceneLayerCoordinates, targetSize)));
    }

    /// <summary>
    /// Scale to a factor relative to current RenderSize over the given duration (seconds).
    /// factor > 1 grows; factor < 1 shrinks.
    /// </summary>
    public void ScaleBy(float factor, float durationSeconds)
    {
        factor = MathF.Max(0.01f, factor);

        int w = (int)MathF.Round(RenderSize.Width * factor);
        int h = (int)MathF.Round(RenderSize.Height * factor);

        ResizeTo(new Size(Math.Max(1, w), Math.Max(1, h)), durationSeconds);
    }

    /// <summary>
    /// Cancel any in-progress resize.
    /// </summary>
    public void CancelResize()
    {
        _isResizing = false;
    }
}
