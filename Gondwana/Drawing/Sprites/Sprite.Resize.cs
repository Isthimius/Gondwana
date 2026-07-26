using System.Drawing;
using Gondwana.Physics.Collisions;

namespace Gondwana.Drawing.Sprites;

/// <summary>
/// Partial class containing resize and pulse animation members for <see cref="Sprite"/>.
/// </summary>
public partial class Sprite
{
    /// <summary>
    /// Raised when a resize or pulse animation completes its current leg, or when cancelled.
    /// </summary>
    public event Action? ResizeComplete;

    private bool _isResizing;
    private float _resizeElapsedSeconds;
    private float _resizeDurationSeconds;
    private Size _resizeStart;
    private Size _resizeTarget;

    private bool _resizeReturnToStart;
    private float _resizeReturnDurationSeconds;
    private Size _resizeOriginalStart;
    private Size _resizePeak;

    private bool _isPulseMode;
    private bool _pulseLoop;
    private bool _pulseForward;
    private float _pulseGrowDurationSeconds;
    private float _pulseShrinkDurationSeconds;

    private CollisionAdjust _resizeStartCollisionAdjust;
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

        int w = (int)MathF.Round(
            _resizeStart.Width + ((_resizeTarget.Width - _resizeStart.Width) * t));
        int h = (int)MathF.Round(
            _resizeStart.Height + ((_resizeTarget.Height - _resizeStart.Height) * t));

        var next = new Size(Math.Max(1, w), Math.Max(1, h));

        if (next != _renderSize)
        {
            RenderSize = next;
            ApplyScaledCollisionAdjust(next);
        }

        if (t >= 1f)
            CompleteResizeLeg();
    }

    private void CompleteResizeLeg()
    {
        RenderSize = _resizeTarget;
        ApplyScaledCollisionAdjust(RenderSize);

        if (_isPulseMode)
        {
            if (_pulseForward)
            {
                _pulseForward = false;
                StartResizeLeg(
                    start: _resizePeak,
                    target: _resizeOriginalStart,
                    durationSeconds: _pulseShrinkDurationSeconds,
                    returnToStart: false,
                    returnDurationSeconds: 0f);
                return;
            }

            if (_pulseLoop)
            {
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
            ResizeComplete?.Invoke();
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
            ResizeComplete?.Invoke();
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

        _resizeStartCollisionAdjust = AdjustCollisionArea;
        _resizeStartSizeForCollision = start;
    }

    private void ApplyScaledCollisionAdjust(Size currentSize)
    {
        if (_resizeStartSizeForCollision.Width <= 0 ||
            _resizeStartSizeForCollision.Height <= 0)
        {
            return;
        }

        float sx = (float)currentSize.Width / _resizeStartSizeForCollision.Width;
        float sy = (float)currentSize.Height / _resizeStartSizeForCollision.Height;

        AdjustCollisionArea = new CollisionAdjust(
            top: (int)MathF.Round(_resizeStartCollisionAdjust.Top * sy),
            bottom: (int)MathF.Round(_resizeStartCollisionAdjust.Bottom * sy),
            left: (int)MathF.Round(_resizeStartCollisionAdjust.Left * sx),
            right: (int)MathF.Round(_resizeStartCollisionAdjust.Right * sx));
    }

    /// <summary>
    /// Smoothly resizes the sprite to an absolute pixel size.
    /// </summary>
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
    /// Scales the sprite by a factor relative to its current render size.
    /// </summary>
    public void ScaleBy(float factor, float durationSeconds)
    {
        factor = MathF.Max(0.01f, factor);

        int w = (int)MathF.Round(RenderSize.Width * factor);
        int h = (int)MathF.Round(RenderSize.Height * factor);

        ResizeTo(
            new Size(Math.Max(1, w), Math.Max(1, h)),
            durationSeconds);
    }

    /// <summary>
    /// Resizes to an absolute target size and then returns to the original size.
    /// </summary>
    public void PulseTo(
        Size targetSize,
        float growDurationSeconds,
        float shrinkDurationSeconds,
        bool loop = false)
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
    /// Pulses by a factor relative to the current render size.
    /// </summary>
    public void PulseBy(
        float factor,
        float growDurationSeconds,
        float shrinkDurationSeconds,
        bool loop = false)
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
    /// Stops the current pulse, optionally returning to the original size.
    /// </summary>
    public void StopPulse(bool snapBack = true, float returnDuration = 0f)
    {
        Size originalSize = _resizeOriginalStart;
        var originalCollisionAdjust = _resizeStartCollisionAdjust;

        CancelResize();

        if (!snapBack)
            return;

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

    /// <summary>
    /// Cancels any in-progress resize or pulse.
    /// </summary>
    public void CancelResize()
    {
        _isResizing = false;
        _resizeReturnToStart = false;
        _isPulseMode = false;
        _pulseLoop = false;
        _pulseForward = false;

        ResizeComplete?.Invoke();
    }
}
