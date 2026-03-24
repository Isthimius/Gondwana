using System.Drawing;
using Gondwana.Collisions;

namespace Gondwana.Drawing.Sprites;

public partial class Sprite
{
    // --- RenderSize tween state ---
    private bool _isResizing;
    private float _resizeElapsedSeconds;
    private float _resizeDurationSeconds;
    private Size _resizeStart;
    private Size _resizeTarget;

    // --- Optional return leg (pulse back to original) ---
    private bool _resizeReturnToStart;
    private float _resizeReturnDurationSeconds;
    private Size _resizeOriginalStart;
    private bool _resizeLoop;
    private Size _resizePeak;

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

            if (_resizeReturnToStart)
            {
                StartResizeLeg(
                    start: RenderSize,
                    target: _resizeOriginalStart,
                    durationSeconds: _resizeReturnDurationSeconds,
                    returnToStart: false,
                    returnDurationSeconds: 0f);
            }
            else
            {
                _isResizing = false;
            }

            return;
        }

        _resizeElapsedSeconds += deltaSeconds;

        float t = _resizeElapsedSeconds / _resizeDurationSeconds;
        if (t >= 1f)
            t = 1f;

        int w = (int)MathF.Round(_resizeStart.Width + ((_resizeTarget.Width - _resizeStart.Width) * t));
        int h = (int)MathF.Round(_resizeStart.Height + ((_resizeTarget.Height - _resizeStart.Height) * t));

        var next = new Size(Math.Max(1, w), Math.Max(1, h));

        if (next != _renderSize)
        {
            RenderSize = next;               // invalidation happens in setter
            ApplyScaledCollisionAdjust(next); // physical pulse: collision scales too
        }

        if (t >= 1f)
        {
            if (_resizeReturnToStart)
            {
                // grow → shrink
                StartResizeLeg(
                    start: RenderSize,
                    target: _resizeOriginalStart,
                    durationSeconds: _resizeReturnDurationSeconds,
                    returnToStart: false,
                    returnDurationSeconds: 0f);
            }
            else if (_resizeLoop)
            {
                // shrink → grow again
                StartResizeLeg(
                    start: RenderSize,
                    target: _resizePeak,
                    durationSeconds: _resizeDurationSeconds,
                    returnToStart: true,
                    returnDurationSeconds: _resizeReturnDurationSeconds);
            }
            else
            {
                _isResizing = false;
            }
        }
    }

    private void StartResizeLeg(
        Size start,
        Size target,
        float durationSeconds,
        bool returnToStart,
        float returnDurationSeconds)
    {
        _resizeStart = start;
        _resizeTarget = target;
        _resizeDurationSeconds = Math.Max(0f, durationSeconds);
        _resizeElapsedSeconds = 0f;
        _isResizing = true;

        _resizeReturnToStart = returnToStart;
        _resizeReturnDurationSeconds = Math.Max(0f, returnDurationSeconds);

        // Re-baseline collision scaling for this leg so proportional adjustment
        // stays correct in both the outward and return trip.
        _resizeStartCollisionAdjust = AdjustCollisionArea;
        _resizeStartSizeForCollision = start;
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
    /// This is a one-way resize only.
    /// </summary>
    /// <param name="targetSize">The target size in pixels to resize to.</param>
    /// <param name="durationSeconds">The duration of the resize animation in seconds.</param>
    public void ResizeTo(Size targetSize, float durationSeconds)
    {
        targetSize = new Size(
            Math.Max(1, targetSize.Width),
            Math.Max(1, targetSize.Height));

        _resizeOriginalStart = RenderSize;

        StartResizeLeg(
            start: RenderSize,
            target: targetSize,
            durationSeconds: durationSeconds,
            returnToStart: false,
            returnDurationSeconds: 0f);
    }

    /// <summary>
    /// Scale to a factor relative to current RenderSize over the given duration (seconds).
    /// factor > 1 grows; factor < 1 shrinks.
    /// This is a one-way resize only.
    /// </summary>
    /// <param name="factor">The scaling factor. Values greater than 1 grow the sprite, values less than 1 shrink it.</param>
    /// <param name="durationSeconds">The duration of the scaling animation in seconds.</param>
    public void ScaleBy(float factor, float durationSeconds)
    {
        factor = MathF.Max(0.01f, factor);

        int w = (int)MathF.Round(RenderSize.Width * factor);
        int h = (int)MathF.Round(RenderSize.Height * factor);

        ResizeTo(new Size(Math.Max(1, w), Math.Max(1, h)), durationSeconds);
    }

    /// <summary>
    /// Perform a full pulse: grow/shrink to the specified absolute target size,
    /// then return to the original size.
    /// Collision adjustment scales physically during both legs.
    /// </summary>
    /// <param name="targetSize">The peak size in pixels.</param>
    /// <param name="growDurationSeconds">Seconds to reach the peak size.</param>
    /// <param name="shrinkDurationSeconds">Seconds to return to the original size.</param>
    /// <param name="loop">Whether to continuously loop the pulse animation.</param>
    public void PulseTo(Size targetSize, float growDurationSeconds, float shrinkDurationSeconds, bool loop = false)
    {
        targetSize = new Size(
            Math.Max(1, targetSize.Width),
            Math.Max(1, targetSize.Height));

        _resizeOriginalStart = RenderSize;
        _resizePeak = targetSize;
        _resizeLoop = loop;

        StartResizeLeg(
            start: RenderSize,
            target: targetSize,
            durationSeconds: growDurationSeconds,
            returnToStart: true,
            returnDurationSeconds: shrinkDurationSeconds);
    }

    /// <summary>
    /// Perform a pulse animation by scaling the sprite by a factor relative to its current size,
    /// then returning to the original size. The sprite can optionally loop this animation continuously.
    /// </summary>
    /// <param name="factor">The scaling factor for the pulse. Values greater than 1 grow the sprite, values less than 1 shrink it.</param>
    /// <param name="growDurationSeconds">The duration in seconds to reach the peak size.</param>
    /// <param name="shrinkDurationSeconds">The duration in seconds to return to the original size.</param>
    /// <param name="loop">Whether to continuously loop the pulse animation.</param>
    public void PulseBy(float factor, float growDurationSeconds, float shrinkDurationSeconds, bool loop = false)
    {
        factor = MathF.Max(0.01f, factor);

        int w = (int)MathF.Round(RenderSize.Width * factor);
        int h = (int)MathF.Round(RenderSize.Height * factor);

        PulseTo(
            new Size(Math.Max(1, w), Math.Max(1, h)),
            growDurationSeconds,
            shrinkDurationSeconds,
            loop);
    }

    /// <summary>
    /// Stops the current pulse animation, optionally snapping the sprite back to its original size.
    /// </summary>
    /// <param name="snapBack">Whether to animate the sprite back to its original size.</param>
    /// <param name="returnDuration">The duration in seconds for the return animation. If 0, the sprite snaps back instantly.</param>
    public void StopPulse(bool snapBack = true, float returnDuration = 0f)
    {
        CancelResize();

        if (snapBack)
        {
            ResizeTo(_resizeOriginalStart, returnDuration);
        }
    }

    /// <summary>
    /// Cancel any in-progress resize or pulse.
    /// </summary>
    public void CancelResize()
    {
        _isResizing = false;
        _resizeReturnToStart = false;
    }
}