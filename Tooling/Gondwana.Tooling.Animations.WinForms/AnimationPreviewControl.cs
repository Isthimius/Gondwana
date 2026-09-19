using System.Diagnostics;
using System.Drawing.Drawing2D;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Tooling.Animations.Editing;

namespace Gondwana.Tooling.Animations.WinForms;

internal sealed class AnimationPreviewControl : Control
{
    private readonly System.Windows.Forms.Timer _timer = new()
    {
        Interval = 15
    };

    private readonly Stopwatch _clock = new();
    private Func<AnimationFrameDefinition, FramePreview?>? _resolver;
    private AnimationDefinition? _definition;
    private int _index;
    private int _direction = 1;
    private double _lastAdvanceSeconds;
    private bool _hiddenAtEnd;
    private float? _zoom;

    public bool IsPlaying => _timer.Enabled;
    public int CurrentFrameIndex => _index;

    public event EventHandler? CurrentFrameChanged;

    public AnimationPreviewControl()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = DarkTheme.Background;
        ForeColor = DarkTheme.Foreground;
        _timer.Tick += (_, _) => TickPreview();
    }

    public void Configure(
        AnimationDefinition definition,
        Func<AnimationFrameDefinition, FramePreview?> resolver)
    {
        _definition = definition;
        _resolver = resolver;
        if (_index >= definition.Frames.Count)
            _index = Math.Max(0, definition.Frames.Count - 1);

        Invalidate();
    }

    public void TogglePlay()
    {
        if (IsPlaying)
            Pause();
        else
            Play();
    }

    public void Play()
    {
        if (_definition is null ||
            _definition.Frames.Count == 0 ||
            _definition.ThrottleTime <= 0 ||
            !double.IsFinite(_definition.ThrottleTime))
        {
            Pause();
            Invalidate();
            return;
        }

        _hiddenAtEnd = false;
        _clock.Restart();
        _lastAdvanceSeconds = 0;
        _timer.Start();
        Invalidate();
    }

    public void Pause()
    {
        _timer.Stop();
        _clock.Stop();
        Invalidate();
    }

    public void Restart()
    {
        _index = 0;
        _direction = 1;
        _hiddenAtEnd = false;
        _lastAdvanceSeconds = 0;
        CurrentFrameChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();

        if (IsPlaying)
        {
            _clock.Restart();
            _lastAdvanceSeconds = 0;
        }
    }

    public void Step()
    {
        Pause();
        Advance();
        Invalidate();
    }

    public void SetZoom(float? zoom)
    {
        _zoom = zoom;
        Invalidate();
    }

    private void TickPreview()
    {
        if (_definition is null ||
            _definition.Frames.Count == 0)
        {
            Pause();
            return;
        }

        var throttle = _definition.ThrottleTime;
        if (throttle <= 0 || !double.IsFinite(throttle))
        {
            Pause();
            return;
        }

        var elapsed = _clock.Elapsed.TotalSeconds;
        int guard = 0;

        while (elapsed - _lastAdvanceSeconds >= throttle &&
               guard++ < 100)
        {
            _lastAdvanceSeconds += throttle;
            if (!Advance())
                break;
        }

        Invalidate();
    }

    private bool Advance()
    {
        if (_definition is null ||
            _definition.Frames.Count == 0)
        {
            return false;
        }

        int count = _definition.Frames.Count;

        switch (_definition.CycleType)
        {
            case CycleType.Simple:
                if (_index < count - 1)
                {
                    _index++;
                }
                else
                {
                    _hiddenAtEnd = _definition.HideTileOnCycleEnd;
                    Pause();
                    return false;
                }
                break;

            case CycleType.Repeating:
                _index = (_index + 1) % count;
                break;

            case CycleType.PingPong:
                if (count == 1)
                {
                    _index = 0;
                }
                else
                {
                    _index += _direction;

                    if (_index >= count - 1)
                    {
                        _index = count - 1;
                        _direction = -1;
                    }
                    else if (_index <= 0)
                    {
                        _index = 0;
                        _direction = 1;
                    }
                }
                break;

            default:
                Pause();
                return false;
        }

        _hiddenAtEnd = false;
        CurrentFrameChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        using (var checker = new HatchBrush(
                   HatchStyle.LargeCheckerBoard,
                   Color.FromArgb(52, 52, 52),
                   Color.FromArgb(40, 40, 40)))
        {
            graphics.FillRectangle(checker, ClientRectangle);
        }

        if (_definition is null ||
            _definition.Frames.Count == 0)
        {
            DrawMessage(graphics, "Add frames to preview the animation.");
            return;
        }

        if (_hiddenAtEnd)
        {
            DrawMessage(graphics, "Tile hidden at end of Simple cycle.");
            return;
        }

        var frame = _definition.Frames[
            Math.Clamp(_index, 0, _definition.Frames.Count - 1)];

        var preview = _resolver?.Invoke(frame);
        if (preview is null)
        {
            DrawMessage(
                graphics,
                $"No preview source for {frame.Tilesheet} / {frame.RegionName} / ({frame.XTile}, {frame.YTile}).");
            return;
        }

        var source = preview.Value.SourceBounds;
        if (source.Width <= 0 || source.Height <= 0)
        {
            DrawMessage(graphics, "Selected frame has invalid dimensions.");
            return;
        }

        float scale = _zoom ?? Math.Min(
            Math.Max(0.01f, (ClientSize.Width - 40f) / source.Width),
            Math.Max(0.01f, (ClientSize.Height - 60f) / source.Height));

        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var destination = new Rectangle(
            (ClientSize.Width - width) / 2,
            (ClientSize.Height - height) / 2,
            width,
            height);

        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.DrawImage(
            preview.Value.Image,
            destination,
            source,
            GraphicsUnit.Pixel);

        var caption =
            $"Frame {_index + 1}/{_definition.Frames.Count}  " +
            $"{frame.Tilesheet}:{frame.RegionName} ({frame.XTile},{frame.YTile})";

        TextRenderer.DrawText(
            graphics,
            caption,
            Font,
            new Rectangle(6, ClientSize.Height - 28, ClientSize.Width - 12, 22),
            ForeColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
    }

    private void DrawMessage(Graphics graphics, string message)
    {
        TextRenderer.DrawText(
            graphics,
            message,
            Font,
            ClientRectangle,
            Color.Silver,
            TextFormatFlags.WordBreak |
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _clock.Stop();
        }

        base.Dispose(disposing);
    }
}
