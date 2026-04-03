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

    // --- Optional return leg (for one-shot pulse-style resize) ---
    private bool _resizeReturnToStart;
    private float _resizeReturnDurationSeconds;
    private Size _resizeOriginalStart;
    private Size _resizePeak;

    // --- Explicit pulse loop state ---
    private bool _isPulseMode;
    private bool _pulseLoop;
    private bool _pulseForward; // true = original -> peak, false = peak -> original
    private float _pulseGrowDurationSeconds;
    private float _pulseShrinkDurationSeconds;

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
            CompleteResizeLeg();
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
            RenderSize = next;                // invalidation happens in setter
            ApplyScaledCollisionAdjust(next); // physical pulse: collision scales too
        }

        if (t >= 1f)
        {
            CompleteResizeLeg();
        }
    }

    private void CompleteResizeLeg()
    {
        // Snap exactly to the target at the end of the leg
        RenderSize = _resizeTarget;
        ApplyScaledCollisionAdjust(RenderSize);

        if (_isPulseMode)
        {
            if (_pulseForward)
            {
                // Original -> Peak finished; now go Peak -> Original
                _pulseForward = false;
                StartResizeLeg(
                    start: _resizePeak,
                    target: _resizeOriginalStart,
                    durationSeconds: _pulseShrinkDurationSeconds,
                    returnToStart: false,
                    returnDurationSeconds: 0f);
                return;
            }

            // Peak -> Original finished
            if (_pulseLoop)
            {
                // Start next cycle
                _pulseForward = true;
                StartResizeLeg(
                    start: _resizeOriginalStart,
                    target: _resizePeak,
                    durationSeconds: _pulseGrowDurationSeconds,
                    returnToStart: false,
                    returnDurationSeconds: 0f);
                return;
            }

            _isPulseMode = false;
            _isResizing = false;
            return;
        }

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
        // stays correct in both directions.
        _resizeStartCollisionAdjust = AdjustCollisionArea;
        _resizeStartSizeForCollision = start;
    }

    private void ApplyScaledCollisionAdjust(Size currentSize)
    {
        if (_resizeStartSizeForCollision.Width <= 0 || _resizeStartSizeForCollision.Height <= 0)
            return;

        float sx = (float)currentSize.Width / _resizeStartSizeForCollision.Width;
        float sy = (float)currentSize.Height / _resizeStartSizeForCollision.Height;

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

        _isPulseMode = false;
        _pulseLoop = false;

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
    /// factor greater than 1 grows; factor less than 1 shrinks.
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
    /// Perform a full pulse: resize to the specified absolute target size, then return to the original size.
    /// If loop is true, this continues indefinitely until stopped.
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

        _isPulseMode = true;
        _pulseLoop = loop;
        _pulseForward = true;
        _pulseGrowDurationSeconds = Math.Max(0f, growDurationSeconds);
        _pulseShrinkDurationSeconds = Math.Max(0f, shrinkDurationSeconds);

        StartResizeLeg(
            start: _resizeOriginalStart,
            target: _resizePeak,
            durationSeconds: _pulseGrowDurationSeconds,
            returnToStart: false,
            returnDurationSeconds: 0f);
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

        PulseTo(new Size(Math.Max(1, w), Math.Max(1, h)),
            growDurationSeconds,
            shrinkDurationSeconds,
            loop);
    }

    /// <summary>
    /// Stops the current pulse animation, optionally snapping the sprite back to its original size.
    /// </summary>
    /// <param name="snapBack">Whether to return the sprite to its original size.</param>
    /// <param name="returnDuration">The duration in seconds for the return animation. If 0, the sprite snaps back instantly.</param>
    public void StopPulse(bool snapBack = true, float returnDuration = 0f)
    {
        Size originalSize = _resizeOriginalStart;
        var originalCollisionAdjust = _resizeStartCollisionAdjust;

        CancelResize();

        if (snapBack)
        {
            if (returnDuration <= 0f)
            {
                RenderSize = originalSize;
                AdjustCollisionArea = originalCollisionAdjust;
            }
            else
            {
                ResizeTo(originalSize, returnDuration);
            }
        }
    }

    /// <summary>
    /// Cancel any in-progress resize or pulse.
    /// </summary>
    public void CancelResize()
    {
        _isResizing = false;
        _resizeReturnToStart = false;

        _isPulseMode = false;
        _pulseLoop = false;
        _pulseForward = false;
    }
}