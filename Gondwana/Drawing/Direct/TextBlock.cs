using System.Drawing;
using System.Runtime.CompilerServices;
using Gondwana.Rendering;
using Gondwana.Skia;
using Gondwana.Timers;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// A retained-mode text drawable with wrapping, multi-line (\\n) support, horizontal/vertical
/// alignment, padding, optional shadow/outline effects, and auto-shrink to fit.
/// </summary>
/// <remarks>
/// <para>
/// <c>TextBlock</c> caches a line layout derived from the current text, font, bounds, and
/// wrapping settings, and only rebuilds when any of those change. Newlines (<c>\\n</c>) are
/// respected as hard breaks; long lines wrap to the available width when wrapping is enabled.
/// </para>
/// <para>
/// Horizontal alignment (left/center/right) and vertical alignment (top/center/bottom) are
/// applied per frame. Optional background fill, shadow, and outline can be enabled for
/// readability. When a minimum font size is specified, the control will step down from the
/// requested size until the text fits within the height or the minimum is reached.
/// </para>
/// </remarks>
/// <example>
/// // Centered, wrapped headline with shadow and outline
/// var headline = new TextBlock(surface, new Rectangle(0, 0, 640, 140))
///     .SetText("Gondwana welcomes you\\n— render boldly.")
///     .SetFont(SKTypeface.Default, 28f, minSize: 16f)
///     .SetColors(SKColors.White, SKColors.Transparent)
///     .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
///     .EnableWrapping(true)
///     .SetMaxLines(3)
///     .UseShadow(true)
///     .UseOutline(true);
/// </example>
public class TextBlock : DirectDrawingBase
{
    public enum VerticalAlign
    {
        Top,
        Center,
        Bottom
    }

    private string _text = string.Empty;
    private List<string> _lines = new();
    private float _lineHeight;
    private bool _layoutDirty = true;

    private SKColor _foreColor = SKColors.White;
    private SKColor _backColor = SKColors.Transparent;

    private float _fontSize = 16f;
    private float? _minFontSize = null;
    private SKTypeface? _typeface = null;

    private bool _useShadow = false;
    private bool _useOutline = false;
    private bool _wrapText = true;
    private int? _maxLines = null;

    private SKTextAlign _hAlign = SKTextAlign.Left;
    private VerticalAlign _vAlign = VerticalAlign.Top;

    // --- Pulse (text color) ---
    private bool _pulseTextEnabled;
    private SKColor _pulseFrom, _pulseTo;
    private float _pulsePeriodSec = 1f;
    private enum PulseWave { Sine, Triangle }
    private PulseWave _pulseWave = PulseWave.Sine;

    // timing
    private long? _lastTick;
    private float _timeSec;

    // current resolved color used for drawing (defaults to _foreColor)
    private SKColor _resolvedForeColor;

    public TextBlock(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds)
        : base(renderSurfaceHost, bounds)
    {
    }

    public float LineSpacingMultiplier { get; set; } = 1.0f;
    public float HorizontalPadding { get; set; } = 0f;
    public float VerticalPadding { get; set; } = 0f;

    public TextBlock SetText(string text)
    {
        _text = text ?? string.Empty;
        _layoutDirty = true;
        _dirty = true; // if your base uses this to request redraw
        return this;
    }

    public TextBlock SetFont(SKTypeface typeface, float size, float? minSize = null)
    { _typeface = typeface; _fontSize = size; _minFontSize = minSize; return this; }

    public TextBlock SetColors(SKColor fg, SKColor bg)
    { _foreColor = fg; _backColor = bg; return this; }

    public TextBlock SetColors(Color fg, Color bg) => SetColors(fg.ToSKColor(), bg.ToSKColor());

    public TextBlock SetAlignment(SKTextAlign h, VerticalAlign v)
    {
        _hAlign = h;
        _vAlign = v;
        return this;
    }

    public TextBlock UseShadow(bool enable = true)
    { _useShadow = enable; return this; }

    public TextBlock UseOutline(bool enable = true)
    { _useOutline = enable; return this; }

    public TextBlock EnableWrapping(bool enable = true)
    { _wrapText = enable; return this; }

    public TextBlock SetMaxLines(int? maxLines)
    { _maxLines = maxLines; return this; }

    /// <summary>
    /// Animates the text color between <paramref name="from"/> and <paramref name="to"/> over <paramref name="periodSec"/> seconds.
    /// </summary>
    public TextBlock PulseColor(Color from, Color to, float periodSec, bool enabled = true, bool triangle = false)
    {
        _pulseTextEnabled = enabled;
        _pulseFrom = from.ToSKColor();
        _pulseTo = to.ToSKColor();
        _pulsePeriodSec = MathF.Max(0.0001f, periodSec);
        _pulseWave = triangle ? PulseWave.Triangle : PulseWave.Sine;

        // start from base time; force a redraw
        _resolvedForeColor = _foreColor;
        _dirty = true;
        return this;
    }

    /// <summary>Stops text color pulsing and restores the base text color.</summary>
    public TextBlock StopColorPulse()
    {
        _pulseTextEnabled = false;
        _resolvedForeColor = _foreColor; // restore base
        _dirty = true;
        return this;
    }

    protected internal override void Update(long tick)
    {
        // time accumulation (same timer model as your particles)
        if (_lastTick is { } last)
        {
            long deltaTicks = tick - last;
            if (deltaTicks < 0) deltaTicks = 0;
            float dt = (float)(deltaTicks / (double)HighResTimer.TicksPerSecond);
            if (dt > 0f && dt < 1f) _timeSec += dt;
        }
        _lastTick = tick;

        if (_pulseTextEnabled)
        {
            float t = PulseT(_timeSec, _pulsePeriodSec, _pulseWave);
            var c = LerpColor(_pulseFrom, _pulseTo, t);
            if (c != _resolvedForeColor)
            {
                _resolvedForeColor = c;
                _dirty = true; // request redraw
            }
        }
        else
        {
            // keep resolved color at base if not pulsing
            if (_resolvedForeColor != _foreColor)
            {
                _resolvedForeColor = _foreColor;
                _dirty = true;
            }
        }
    }

    protected internal override void Draw()
    {
        var canvas = RenderSurfaceHost.Backbuffer.Canvas;
        var rect = Bounds.ToSKRect();

        // Background
        if (_backColor.Alpha != 0)
        {
            using var bg = new SKPaint { Color = _backColor };
            canvas.DrawRect(rect, bg);
        }

        // Ensure typeface
        _typeface ??= SKTypeface.Default;

        // Build a paint we can reuse for layout + draw
        using var paint = new SKPaint
        {
            Typeface = _typeface ?? SKTypeface.Default,
            TextSize = _fontSize,
            Color = _resolvedForeColor,   // <<< use resolved color
            IsAntialias = true,
            IsStroke = false,
            TextAlign = _hAlign
        };

        // Auto-shrink: reflow until it fits height (if min size provided)
        float fontSize = _fontSize;
        float innerW = Math.Max(0, rect.Width - HorizontalPadding * 2f);
        float innerH = Math.Max(0, rect.Height - VerticalPadding * 2f);

        while (true)
        {
            paint.TextSize = fontSize;
            if (_layoutDirty) RebuildLayout(paint, innerW);

            int drawableLines = _maxLines.HasValue ? Math.Min(_lines.Count, _maxLines.Value) : _lines.Count;
            float totalH = drawableLines * _lineHeight;

            if (_minFontSize.HasValue && totalH > innerH && fontSize > _minFontSize.Value)
            {
                fontSize -= 1f;                 // step down and retry
                _layoutDirty = true;
                continue;
            }
            break;
        }

        // Vertical start (remember: Skia draws at baseline, so apply ascent shift)
        var fm = paint.FontMetrics;
        float baselineShift = -fm.Ascent;

        int linesToDraw = _maxLines.HasValue ? Math.Min(_lines.Count, _maxLines.Value) : _lines.Count;
        float contentH = linesToDraw * _lineHeight;

        float yStart = _vAlign switch
        {
            VerticalAlign.Center => rect.Top + VerticalPadding + (innerH - contentH) * 0.5f,
            VerticalAlign.Bottom => rect.Bottom - VerticalPadding - contentH,
            _ => rect.Top + VerticalPadding
        };

        // Horizontal anchor per line
        float xAnchorLeft = rect.Left + HorizontalPadding;
        float xAnchorCenter = rect.MidX;
        float xAnchorRight = rect.Right - HorizontalPadding;

        float y = yStart;
        for (int i = 0; i < linesToDraw; i++)
        {
            var line = _lines[i];
            float x = _hAlign switch
            {
                SKTextAlign.Center => xAnchorCenter,
                SKTextAlign.Right => xAnchorRight,
                _ => xAnchorLeft
            };

            if (_useShadow)
            {
                using var shadow = paint.Clone();
                shadow.Color = SKColors.Black.WithAlpha(100);
                canvas.DrawText(line, x + 2, y + baselineShift + 2, shadow);
            }

            if (_useOutline)
            {
                using var outline = paint.Clone();
                outline.IsStroke = true;
                outline.StrokeWidth = 1.5f;
                outline.Color = SKColors.Black;
                canvas.DrawText(line, x, y + baselineShift, outline);
            }

            canvas.DrawText(line, x, y + baselineShift, paint);
            y += _lineHeight;
            if (y > rect.Bottom) break; // safety clip
        }
    }

    private void RebuildLayout(SKPaint paint, float maxWidth)
    {
        _lines.Clear();

        var fm = paint.FontMetrics;
        _lineHeight = ((fm.Descent - fm.Ascent) + fm.Leading) * LineSpacingMultiplier;

        var paragraphs = _text.Replace("\r\n", "\n").Split('\n');

        foreach (var para in paragraphs)
        {
            if (string.IsNullOrEmpty(para))
            {
                _lines.Add(string.Empty);
                continue;
            }

            // NEW: if wrapping is disabled, keep the paragraph as a single line
            if (!_wrapText)
            {
                _lines.Add(para);
                continue;
            }

            // word wrap
            var words = para.Split(' ');
            var current = string.Empty;

            foreach (var word in words)
            {
                string candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                float w = paint.MeasureText(candidate);

                if (w <= maxWidth || string.IsNullOrEmpty(current))
                    current = candidate;
                else
                {
                    _lines.Add(current);
                    current = word;
                }
            }

            if (!string.IsNullOrEmpty(current))
                _lines.Add(current);
        }

        _layoutDirty = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PulseT(float timeSec, float periodSec, PulseWave wave)
    {
        float phase = (timeSec / periodSec) % 1f;
        return wave == PulseWave.Sine
            ? 0.5f * (1f + MathF.Sin(phase * MathF.PI * 2f))             // [0..1]
            : (phase < 0.5f ? phase * 2f : 1f - (phase - 0.5f) * 2f);    // triangle
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKColor LerpColor(SKColor a, SKColor b, float t01)
    {
        t01 = Math.Clamp(t01, 0f, 1f);
        byte r = (byte)(a.Red + (b.Red - a.Red) * t01);
        byte g = (byte)(a.Green + (b.Green - a.Green) * t01);
        byte bl = (byte)(a.Blue + (b.Blue - a.Blue) * t01);
        byte al = (byte)(a.Alpha + (b.Alpha - a.Alpha) * t01);
        return new SKColor(r, g, bl, al);
    }
}
