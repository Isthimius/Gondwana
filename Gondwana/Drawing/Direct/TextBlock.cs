using Gondwana.Rendering;
using Gondwana.Skia;
using Gondwana.Timers;
using SkiaSharp;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

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
public class TextBlock : DirectDrawingMovableBase
{
    public enum VerticalAlign
    {
        Top,
        Center,
        Bottom
    }

    private string _text = string.Empty;
    private readonly List<string> _lines = new();
    private float _lineHeight;
    private bool _layoutDirty = true;

    private SKColor _foreColor = SKColors.White;
    private SKColor _backColor = SKColors.Transparent;

    private float _fontSize = 16f;
    private float? _minFontSize = null;
    private SKTypeface? _typeface = null;

    private bool _useOutline = false;
    private bool _wrapText = true;
    private int? _maxLines = null;

    private bool _useShadow = false;
    private float _shadowDx = 2f, _shadowDy = 2f;
    private byte _shadowAlpha = 128;
    private float _shadowBlurSigma = 1.5f; // 0 = no blur

    private SKTextAlign _hAlign = SKTextAlign.Left;
    private VerticalAlign _vAlign = VerticalAlign.Top;

    // --- Pulse (text color) ---
    private bool _pulseTextEnabled;
    private SKColor _pulseFrom, _pulseTo;
    private float _pulsePeriodSec = 1f;
    private enum PulseWave { Sine, Triangle }
    private PulseWave _pulseWave = PulseWave.Sine;

    // timing
    private long? _pulseLastTick;
    private float _timeSec;

    // --- Text reveal animation ---
    private long _revealLastTick;
    private TextRevealMode _textRevealMode = TextRevealMode.None;
    private int _revealCharCount;        // how many chars currently visible
    private int _revealTargetCharCount;  // cap (usually Text.Length)
    private float _revealRate;             // cps or wps (depending on mode)
    private float _revealAccum;            // accumulator for dt-based stepping

    // precomputed for word-based reveal
    private int[]? _wordEndCharIndexes;    // end indexes (exclusive) per word
    private int _wordIndex;             // words shown so far

    // Punctuation pause tuning
    private bool _pauseEnabled = true;
    private float _pauseLongSec = 0.25f;
    private float _pauseShortSec = 0.10f;

    // current resolved color used for drawing (defaults to _foreColor)
    private SKColor _resolvedForeColor;

    public TextBlock(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds)
        : base(renderSurfaceHost, bounds)
    {
        _resolvedForeColor = _foreColor;
    }

    public float LineSpacingMultiplier { get; set; } = 1.0f;
    public float HorizontalPadding { get; set; } = 0f;
    public float VerticalPadding { get; set; } = 0f;

    public TextBlock SetText(string text)
    {
        _text = text ?? string.Empty;
        _layoutDirty = true;

        if (_textRevealMode == TextRevealMode.None)
        {
            // when not revealing, default to fully visible
            _revealCharCount = _text.Length;
        }
        else
        {
            // keep the animation’s target in sync with the new text
            _revealTargetCharCount = _text.Length;

            // if we had already revealed more than the new length, clamp it
            if (_revealCharCount > _revealTargetCharCount)
                _revealCharCount = _revealTargetCharCount;

            // optional: smooth start after text swap
            _revealAccum = 0f;
            // if you added a dedicated reveal timer, warm it so first frame doesn't "time-warp"
            _revealLastTick = 0;   // keep this if you’re using the separate reveal timer
        }

        ForceRefresh();
        return this;
    }

    public TextBlock SetFont(SKTypeface typeface, float size, float? minSize = null)
    {
        _typeface = typeface;
        _fontSize = size;
        _minFontSize = minSize;
        _layoutDirty = true;
        ForceRefresh();
        return this;
    }

    public TextBlock SetColors(SKColor fg, SKColor bg)
    {
        _foreColor = fg;
        _backColor = bg;
        _resolvedForeColor = fg; // keep resolved in sync
        ForceRefresh();
        return this;
    }

    public TextBlock SetColors(Color fg, Color bg) => SetColors(fg.ToSKColor(), bg.ToSKColor());

    public TextBlock SetAlignment(SKTextAlign h, VerticalAlign v)
    {
        _hAlign = h;
        _vAlign = v;
        ForceRefresh();
        return this;
    }

    public TextBlock UseShadow(bool enable = true)
    {
        _useShadow = enable;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Configures the drop shadow effect for text rendering.
    /// </summary>
    /// <param name="dx">
    /// Horizontal offset in pixels for the shadow. Positive values move the shadow right, negative left.
    /// </param>
    /// <param name="dy">
    /// Vertical offset in pixels for the shadow. Positive values move the shadow down, negative up.
    /// </param>
    /// <param name="alpha">
    /// Opacity of the shadow (0–255). Higher values make the shadow darker and more opaque.
    /// </param>
    /// <param name="blurSigma">
    /// Blur radius in pixels for the shadow’s softness. Set to 0 for a hard shadow. Typical range: 1.0–3.0.
    /// </param>
    /// <remarks>
    /// <para>
    /// Call this after <see cref="UseShadow(bool)"/> to fine-tune the offset, opacity, and blur strength
    /// of the text shadow. This method sets the internal paint values and marks the text block as dirty
    /// so it will be redrawn on the next render pass.
    /// </para>
    /// </remarks>
    /// <returns>The current <see cref="TextBlock"/> instance for method chaining.</returns>
    public TextBlock SetShadow(float dx, float dy, byte alpha = 128, float blurSigma = 1.5f)
    {
        _shadowDx = dx;
        _shadowDy = dy;
        _shadowAlpha = alpha;
        _shadowBlurSigma = MathF.Max(0f, blurSigma);
        ForceRefresh();
        return this;
    }

    public TextBlock UseOutline(bool enable = true)
    {
        _useOutline = enable;
        ForceRefresh();
        return this;
    }

    public TextBlock EnableWrapping(bool enable = true)
    {
        _wrapText = enable;
        _layoutDirty = true;
        ForceRefresh();
        return this;
    }

    public TextBlock SetMaxLines(int? maxLines)
    {
        _maxLines = maxLines;
        ForceRefresh();
        return this;
    }

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
        ForceRefresh();
        return this;
    }

    /// <summary>Stops text color pulsing and restores the base text color.</summary>
    public TextBlock StopColorPulse()
    {
        _pulseTextEnabled = false;
        _resolvedForeColor = _foreColor; // restore base
        ForceRefresh();
        return this;
    }

    public TextBlock StartTypewriter(float charsPerSecond,
                                     int? maxChars = null,
                                     bool enablePauses = true,
                                     float longPauseSec = 0.25f,
                                     float shortPauseSec = 0.10f)
    {
        _textRevealMode = TextRevealMode.CharactersPerSecond;
        _revealRate = MathF.Max(0f, charsPerSecond);
        _revealCharCount = 0;
        _revealTargetCharCount = maxChars.HasValue ? Math.Min(maxChars.Value, _text.Length) : _text.Length;
        _revealAccum = 0f;
        _wordEndCharIndexes = null;

        _revealLastTick = 0;

        // punctuation tuning
        _pauseEnabled = enablePauses;
        _pauseLongSec = longPauseSec;
        _pauseShortSec = shortPauseSec;

        ForceRefresh();
        return this;
    }

    public TextBlock StartWordReveal(float wordsPerSecond,
                                     bool enablePauses = true,
                                     float longPauseSec = 0.25f,
                                     float shortPauseSec = 0.10f)
    {
        _textRevealMode = TextRevealMode.WordsPerSecond;
        _revealRate = MathF.Max(0f, wordsPerSecond);
        _revealCharCount = 0;
        _revealTargetCharCount = _text.Length;
        _revealAccum = 0f;
        _wordIndex = 0;

        // split words once; keep trailing punctuation with the word so it reveals naturally
        // You can refine this regex to match your localization needs.
        _wordEndCharIndexes = Regex.Matches(_text, @"\S+\s*")
                                   .Select(m => m.Index + m.Length)
                                   .ToArray();

        _revealLastTick = 0;

        _pauseEnabled = enablePauses;
        _pauseLongSec = longPauseSec;
        _pauseShortSec = shortPauseSec;

        ForceRefresh();
        return this;
    }

    public TextBlock SetPunctuationPauses(bool enabled, float longPauseSec = 0.25f, float shortPauseSec = 0.10f)
    {
        _pauseEnabled = enabled;
        _pauseLongSec = longPauseSec;
        _pauseShortSec = shortPauseSec;
        return this;
    }

    public TextBlock RevealSetCount(int charCount)
    {
        _textRevealMode = TextRevealMode.ManualCount;
        _revealCharCount = Math.Clamp(charCount, 0, _text.Length);
        _revealTargetCharCount = _text.Length;
        ForceRefresh();
        return this;
    }

    public TextBlock RevealStop()
    {
        _textRevealMode = TextRevealMode.None;
        _revealCharCount = _text.Length;
        ForceRefresh();
        return this;
    }

    private float PauseFor(char c)
    {
        if (!_pauseEnabled) return 0f;

        return c switch
        {
            '.' or '!' or '?' => _pauseLongSec,
            ',' or ';' or ':' => _pauseShortSec,
            _ => 0f
        };
    }

    public override void Update(long tick)
    {
        if (tick == _lastTick)
            return;

        // --- PULSE TIMING (independent) ---
        float dtPulse = 0f;
        if (_pulseLastTick.HasValue)
            dtPulse = HighResTimer.GetDuration(_pulseLastTick.Value, tick);

        _pulseLastTick = tick;

        if (dtPulse > 0f && dtPulse < 1f)
            _timeSec += dtPulse;

        if (_pulseTextEnabled)
        {
            float t = PulseT(_timeSec, _pulsePeriodSec, _pulseWave);
            var c = LerpColor(_pulseFrom, _pulseTo, t);
            if (c != _resolvedForeColor)
            {
                _resolvedForeColor = c;
                ForceRefresh();
            }
        }
        else
        {
            // keep resolved color at base if not pulsing
            if (_resolvedForeColor != _foreColor)
            {
                _resolvedForeColor = _foreColor;
                ForceRefresh();
            }
        }

        // --- REVEAL TIMING (independent and clamped) ---
        float dtReveal = 0f;
        if (_revealLastTick == 0)
        {
            _revealLastTick = tick;           // warm up: first frame uses 0 dt
        }
        else
        {
            dtReveal = HighResTimer.GetDuration(_revealLastTick, tick);
            _revealLastTick = tick;

            // Clamp to avoid first-frame "time warp" completing the whole text
            if (dtReveal > 0.1f) dtReveal = 0.1f;   // ~100ms/frame cap
            if (dtReveal < 0f) dtReveal = 0f;
        }

        // --- Typewriter / word reveal ---
        if (_textRevealMode == TextRevealMode.CharactersPerSecond)
        {
            _revealAccum += dtReveal;
            if (_revealRate > 0f)
            {
                int step = (int)(_revealAccum * _revealRate);
                if (step > 0)
                {
                    _revealAccum -= step / _revealRate;

                    int old = _revealCharCount;
                    _revealCharCount = Math.Min(_revealCharCount + step, _revealTargetCharCount);

                    // Punctuation pauses for newly revealed chars
                    for (int i = old; i < _revealCharCount; i++)
                        _revealAccum -= PauseFor(_text[i]);

                    _dirty = true;
                    ForceRefresh();

                    if (_revealCharCount >= _revealTargetCharCount)
                        _textRevealMode = TextRevealMode.None;
                }
            }
        }
        else if (_textRevealMode == TextRevealMode.WordsPerSecond && _wordEndCharIndexes is not null)
        {
            _revealAccum += dtReveal;
            if (_revealRate > 0f)
            {
                int step = (int)(_revealAccum * _revealRate);
                if (step > 0)
                {
                    _revealAccum -= step / _revealRate;

                    int oldCount = _revealCharCount;

                    _wordIndex = Math.Min(_wordIndex + step, _wordEndCharIndexes.Length);
                    _revealCharCount = _wordIndex == 0 ? 0 : _wordEndCharIndexes[_wordIndex - 1];

                    // Simple frontier pause (cheap). Loop oldCount.._revealCharCount-1 if you want per-char.
                    if (_revealCharCount > 0)
                        _revealAccum -= PauseFor(_text[_revealCharCount - 1]);

                    _dirty = true;
                    ForceRefresh();

                    if (_wordIndex >= _wordEndCharIndexes.Length)
                        _textRevealMode = TextRevealMode.None;
                }
            }
        }

        // Let the base advance fade tween and set _lastTick
        base.Update(tick);
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
            Color = _resolvedForeColor,   // resolved (pulsed) color
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

        // Determine how many characters are currently visible (content-driven reveal)
        int visibleChars = _textRevealMode != TextRevealMode.None
            ? Math.Clamp(_revealCharCount, 0, _text.Length)
            : _text.Length;

        // Build the set of lines to draw from the already-laid-out _lines,
        // truncating at 'visibleChars' so wrapping and alignment still work.
        List<string> drawLines = new List<string>(_lines.Count);
        if (visibleChars <= 0)
        {
            // nothing to show
        }
        else if (visibleChars >= _text.Length)
        {
            drawLines.AddRange(_lines);
        }
        else
        {
            int remaining = visibleChars;
            foreach (var ln in _lines)
            {
                if (remaining <= 0) break;
                if (ln.Length <= remaining)
                {
                    drawLines.Add(ln);
                    remaining -= ln.Length;
                }
                else
                {
                    drawLines.Add(ln.Substring(0, remaining));
                    remaining = 0;
                }
            }
        }

        // Apply max-lines cap at draw time
        int linesToDraw = _maxLines.HasValue ? Math.Min(drawLines.Count, _maxLines.Value) : drawLines.Count;

        // Vertical start (Skia draws at baseline, so apply ascent shift)
        var fm = paint.FontMetrics;
        float baselineShift = -fm.Ascent;

        float contentH = linesToDraw * _lineHeight;
        float yStart = _vAlign switch
        {
            VerticalAlign.Center => rect.Top + VerticalPadding + Math.Max(0, (innerH - contentH) * 0.5f),
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
            var line = drawLines[i];
            float x = _hAlign switch
            {
                SKTextAlign.Center => xAnchorCenter,
                SKTextAlign.Right => xAnchorRight,
                _ => xAnchorLeft
            };

            if (_useShadow)
            {
                using var shadow = paint.Clone();
                shadow.IsStroke = false;
                shadow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, _shadowBlurSigma);
                shadow.Color = new SKColor(0, 0, 0, _shadowAlpha);

                // manually offset
                canvas.DrawText(line, x + _shadowDx, y + baselineShift + _shadowDy, shadow);
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

            // if wrapping is disabled, keep the paragraph as a single line
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

    public enum TextRevealMode
    {
        None,
        CharactersPerSecond,
        WordsPerSecond,
        ManualCount
    }
}
