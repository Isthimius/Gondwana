using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Timers;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// A retained-mode text drawable with wrapping, multi-line (\n) support, horizontal/vertical
/// alignment, padding, optional shadow/outline effects, and auto-shrink to fit.
/// </summary>
/// <remarks>
/// <para>
/// <c>TextBlock</c> caches a line layout derived from the current text, font, bounds, and
/// wrapping settings, and only rebuilds when any of those change. Newlines (<c>\n</c>) are
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
///     .SetText("Gondwana welcomes you\n- render boldly.")
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
    /// <summary>
    /// Vertical alignment options for multi-line text within the control's bounds.
    /// </summary>
    public enum VerticalAlign
    {
        /// <summary>Align text to the top of the inner content area.</summary>
        Top,
        /// <summary>Center text vertically within the inner content area.</summary>
        Center,
        /// <summary>Align text to the bottom of the inner content area.</summary>
        Bottom
    }

    #region events

    /// <summary>
    /// Raised whenever the revealed text portion advances. The argument is the cumulative
    /// text currently revealed (substring from start to visible character count).
    /// </summary>
    public event Action<string>? TextRevealed;

    /// <summary>
    /// Raised once when the reveal completes. Argument is the full text content.
    /// Note: when the final chunk is revealed this class will raise TextRevealed first,
    /// then TextRevealComplete.
    /// </summary>
    public event Action<string>? TextRevealComplete;

    #endregion events

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

    /// <summary>
    /// Creates a new <see cref="TextBlock"/> bound to a render surface and rectangle.
    /// </summary>
    /// <param name="renderSurfaceHost">The target render surface host responsible for drawing.</param>
    private TextBlock(RenderSurfaceHostBase renderSurfaceHost,
                     DirectDrawingMode mode,
                     SceneLayer? sceneLayer,
                     View? view,
                     Rectangle? screenBounds,
                     Rectangle? worldBounds,
                     string? nickname = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, nickname)
    {
        _resolvedForeColor = _foreColor;
    }

    /// <summary>
    /// Creates a new <see cref="TextBlock"/> that draws in world space on the specified scene layer.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns this direct drawing.</param>
    /// <param name="sceneLayer">The scene layer used for world-space positioning and refresh.</param>
    /// <param name="view">The optional view associated with the text block.</param>
    /// <param name="worldBounds">The text block bounds in world coordinates.</param>
    /// <param name="nickname">An optional nickname used to identify the text block.</param>
    public TextBlock(RenderSurfaceHostBase renderSurfaceHost,
                     SceneLayer sceneLayer,
                     View? view,
                     Rectangle worldBounds,
                     string? nickname = null)
        : this(renderSurfaceHost,
               DirectDrawingMode.SceneLayer,
               sceneLayer,
               view,
               screenBounds: null,
               worldBounds: worldBounds,
               nickname: nickname) { }

    /// <summary>
    /// Creates a new <see cref="TextBlock"/> that draws in screen space relative to a view.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns this direct drawing.</param>
    /// <param name="view">The view the text block is associated with.</param>
    /// <param name="screenBounds">The text block bounds in screen coordinates.</param>
    /// <param name="nickname">An optional nickname used to identify the text block.</param>
    public TextBlock(RenderSurfaceHostBase renderSurfaceHost,
                     View view,
                     Rectangle screenBounds,
                     string? nickname = null)
        : this(renderSurfaceHost,
               DirectDrawingMode.View,
               sceneLayer: null,
               view: view,
               screenBounds: screenBounds,
               worldBounds: null,
               nickname: nickname) { }

    /// <summary>
    /// Gets or sets a scale factor applied to the computed line height (1.0 = natural spacing).
    /// </summary>
    public float LineSpacingMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the horizontal padding (in pixels) on both left and right sides of the text.
    /// </summary>
    public float HorizontalPadding { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the vertical padding (in pixels) on both top and bottom sides of the text.
    /// </summary>
    public float VerticalPadding { get; set; } = 0f;

    /// <summary>
    /// Sets the current text content and rebuilds layout as needed.
    /// If no reveal animation is active, the text is shown fully.
    /// If a reveal animation is active, the target length is synced to the new text.
    /// </summary>
    /// <param name="text">The new text to display (null is treated as an empty string).</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
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
            // keep the animation's target in sync with the new text
            _revealTargetCharCount = _text.Length;

            // if we had already revealed more than the new length, clamp it
            if (_revealCharCount > _revealTargetCharCount)
                _revealCharCount = _revealTargetCharCount;

            // optional: smooth start after text swap
            _revealAccum = 0f;
            // if you added a dedicated reveal timer, warm it so first frame doesn't "time-warp"
            _revealLastTick = 0;   // keep this if you're using the separate reveal timer
        }

        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the font face and size (and optional minimum size for auto-shrink).
    /// </summary>
    /// <param name="typeface">Typeface to use; defaults to <see cref="SKTypeface.Default"/> if null.</param>
    /// <param name="size">Requested font size in pixels.</param>
    /// <param name="minSize">Optional minimum size for auto-shrink; if set, layout will step down to fit height.</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock SetFont(SKTypeface typeface, float size, float? minSize = null)
    {
        _typeface = typeface;
        _fontSize = size;
        _minFontSize = minSize;
        _layoutDirty = true;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the foreground (text) and background colors using Skia colors.
    /// </summary>
    /// <param name="fg">Text color.</param>
    /// <param name="bg">Background color (drawn as a solid rect if alpha &gt; 0).</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock SetColors(SKColor fg, SKColor bg)
    {
        _foreColor = fg;
        _backColor = bg;
        _resolvedForeColor = fg; // keep resolved in sync
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the foreground (text) and background colors using System.Drawing colors.
    /// </summary>
    /// <param name="fg">Text color.</param>
    /// <param name="bg">Background color (drawn as a solid rect if alpha &gt; 0).</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock SetColors(Color fg, Color bg) => SetColors(fg.ToSKColor(), bg.ToSKColor());

    /// <summary>
    /// Sets the horizontal and vertical alignment used when drawing the laid-out lines within the bounds.
    /// </summary>
    /// <param name="h">Horizontal alignment (Left, Center, Right).</param>
    /// <param name="v">Vertical alignment (Top, Center, Bottom).</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock SetAlignment(SKTextAlign h, VerticalAlign v)
    {
        _hAlign = h;
        _vAlign = v;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Enables or disables drawing a drop shadow behind the text (parameters are configured via <see cref="SetShadow(float, float, byte, float)"/>).
    /// </summary>
    /// <param name="enable">True to enable the shadow; false to disable.</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
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
    /// Opacity of the shadow (0-255). Higher values make the shadow darker and more opaque.
    /// </param>
    /// <param name="blurSigma">
    /// Blur radius in pixels for the shadow's softness. Set to 0 for a hard shadow. Typical range: 1.0-3.0.
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

    /// <summary>
    /// Enables or disables a 1-pixel outline stroke around the text glyphs.
    /// </summary>
    /// <param name="enable">True to enable an outline; false to disable.</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock UseOutline(bool enable = true)
    {
        _useOutline = enable;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Enables or disables word wrapping when laying out paragraphs.
    /// </summary>
    /// <param name="enable">True to wrap text to the available width; false to keep each paragraph on one line.</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock EnableWrapping(bool enable = true)
    {
        _wrapText = enable;
        _layoutDirty = true;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets a maximum number of lines to draw. If null, all laid-out lines are drawn.
    /// </summary>
    /// <param name="maxLines">Maximum visible lines, or null for no limit.</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock SetMaxLines(int? maxLines)
    {
        _maxLines = maxLines;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Animates the text color between <paramref name="from"/> and <paramref name="to"/> over <paramref name="periodSec"/> seconds.
    /// </summary>
    /// <param name="from">Starting text color.</param>
    /// <param name="to">Ending text color.</param>
    /// <param name="periodSec">Time in seconds for one full pulse cycle.</param>
    /// <param name="enabled">True to enable pulsing; false to disable.</param>
    /// <param name="triangle">True to use a triangle waveform; otherwise a sine wave is used.</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
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

    /// <summary>
    /// Stops text color pulsing and restores the base foreground color.
    /// </summary>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock StopColorPulse()
    {
        _pulseTextEnabled = false;
        _resolvedForeColor = _foreColor; // restore base
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Begins a character-by-character reveal (typewriter effect).
    /// </summary>
    /// <param name="charsPerSecond">Characters per second to reveal.</param>
    /// <param name="maxChars">Optional cap on total characters to reveal; defaults to the full text length.</param>
    /// <param name="enablePauses">True to add punctuation pauses during reveal; false for uniform pacing.</param>
    /// <param name="longPauseSec">Pause added after '.', '!', or '?' characters (seconds).</param>
    /// <param name="shortPauseSec">Pause added after ',', ';', or ':' characters (seconds).</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
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

    /// <summary>
    /// Begins a word-by-word reveal. Words are detected by <c>\S+\s*</c> and include their trailing whitespace/punctuation.
    /// </summary>
    /// <param name="wordsPerSecond">Words per second to reveal.</param>
    /// <param name="enablePauses">True to add punctuation pauses during reveal; false for uniform pacing.</param>
    /// <param name="longPauseSec">Pause added after '.', '!', or '?' characters (seconds).</param>
    /// <param name="shortPauseSec">Pause added after ',', ';', or ':' characters (seconds).</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
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

    /// <summary>
    /// Configures punctuation pause behavior used by the current or next reveal animation.
    /// </summary>
    /// <param name="enabled">True to enable punctuation pauses; false to disable.</param>
    /// <param name="longPauseSec">Pause after '.', '!', or '?' (seconds).</param>
    /// <param name="shortPauseSec">Pause after ',', ';', or ':' (seconds).</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock SetPunctuationPauses(bool enabled, float longPauseSec = 0.25f, float shortPauseSec = 0.10f)
    {
        _pauseEnabled = enabled;
        _pauseLongSec = longPauseSec;
        _pauseShortSec = shortPauseSec;
        return this;
    }

    /// <summary>
    /// Sets the currently revealed character count directly (manual mode).
    /// </summary>
    /// <param name="charCount">The number of characters (from the start of the text) to show.</param>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock RevealSetCount(int charCount)
    {
        _textRevealMode = TextRevealMode.ManualCount;
        _revealCharCount = Math.Clamp(charCount, 0, _text.Length);
        _revealTargetCharCount = _text.Length;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Stops any active reveal animation and shows the full text immediately.
    /// </summary>
    /// <returns>The current <see cref="TextBlock"/> for chaining.</returns>
    public TextBlock RevealStop()
    {
        _textRevealMode = TextRevealMode.None;
        _revealCharCount = _text.Length;

        // Fire final events: first the revealed text (full), then the completion event
        TextRevealed?.Invoke(_text);
        TextRevealComplete?.Invoke(_text);

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

    /// <summary>
    /// Advances internal animations (pulse, typewriter/word reveal) based on the current tick, and
    /// then allows the base class to progress fade and bookkeeping. Called once per frame.
    /// </summary>
    /// <param name="tick">High-resolution tick value for this frame.</param>
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

                    // Mark dirty + redraw
                    ForceRefresh();

                    // Fire TextRevealed with the cumulative revealed text
                    if (_revealCharCount > old)
                    {
                        var revealed = _text.Substring(0, _revealCharCount);
                        TextRevealed?.Invoke(revealed);
                    }

                    // If we've reached the target, fire completion (TextRevealed already sent above)
                    if (_revealCharCount >= _revealTargetCharCount)
                    {
                        _textRevealMode = TextRevealMode.None;
                        // Ensure ordering: revealed then complete
                        TextRevealComplete?.Invoke(_text);
                    }
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

                    ForceRefresh();

                    // Fire TextRevealed with the cumulative revealed text
                    if (_revealCharCount > oldCount)
                    {
                        var revealed = _text.Substring(0, _revealCharCount);
                        TextRevealed?.Invoke(revealed);
                    }

                    if (_wordIndex >= _wordEndCharIndexes.Length)
                    {
                        _textRevealMode = TextRevealMode.None;
                        // Ensure ordering: revealed then complete
                        TextRevealComplete?.Invoke(_text);
                    }
                }
            }
        }

        // Let the base advance fade tween and set _lastTick
        base.Update(tick);
    }

    /// <summary>
    /// Draws the laid-out text block contents into the destination rectangle.
    /// </summary>
    /// <param name="backbuffer">The backbuffer that provides the target drawing surface.</param>
    /// <param name="destRectScreen">The destination rectangle in screen space.</param>
    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        var canvas = backbuffer.Canvas;
        var rect = destRectScreen.ToSKRect();

        float zoom = ResolveTextScale(Mode, RenderContext.Current?.ViewportZoom ?? 1f);

        // Scale "pixel-like" adornments with zoom so text behaves like other world-space drawables.
        float hPad = HorizontalPadding * zoom;
        float vPad = VerticalPadding * zoom;
        float shadowDx = _shadowDx * zoom;
        float shadowDy = _shadowDy * zoom;
        float shadowBlur = _shadowBlurSigma * zoom;
        float outlineWidth = 1.5f * zoom;

        // Background
        if (_backColor.Alpha != 0)
        {
            using var bg = new SKPaint { Color = _backColor };
            canvas.DrawRect(rect, bg);
        }

        _typeface ??= SKTypeface.Default;

        // Build a paint we can reuse for layout + draw (TEXT SIZE IS IN SCREEN PIXELS)
        using var paint = new SKPaint
        {
            Typeface = _typeface ?? SKTypeface.Default,
            TextSize = _fontSize * zoom,
            Color = _resolvedForeColor,
            IsAntialias = true,
            IsStroke = false,
            TextAlign = _hAlign
        };

        // Auto-shrink: reflow until it fits height (if min size provided)
        float fontSize = _fontSize * zoom;
        float minFontSize = _minFontSize.HasValue ? _minFontSize.Value * zoom : 0f;

        float innerW = Math.Max(0, rect.Width - hPad * 2f);
        float innerH = Math.Max(0, rect.Height - vPad * 2f);

        while (true)
        {
            paint.TextSize = fontSize;

            // Rebuild when flagged, and also whenever zoom is in play so wrap/line-height stay correct.
            if (_layoutDirty || zoom != 1f)
                RebuildLayout(paint, innerW);

            int drawableLines = _maxLines.HasValue ? Math.Min(_lines.Count, _maxLines.Value) : _lines.Count;
            float totalH = drawableLines * _lineHeight;

            bool exceedsHeight = totalH > innerH;
            bool exceedsWidth = _lines.Any(line => paint.MeasureText(line) > innerW);

            if (_minFontSize.HasValue &&
                (exceedsHeight || exceedsWidth) &&
                fontSize > minFontSize)
            {
                fontSize = Math.Max(minFontSize, fontSize - 1f);
                _layoutDirty = true;

                if (fontSize <= minFontSize)
                    break;

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
        List<string> drawLines = new(_lines.Count);
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
                if (remaining <= 0)
                    break;

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

        bool wasTruncatedByMaxLines = _maxLines.HasValue && drawLines.Count > _maxLines.Value;

        // Apply max-lines cap at draw time
        int linesToDraw = _maxLines.HasValue ? Math.Min(drawLines.Count, _maxLines.Value) : drawLines.Count;

        if (linesToDraw > 0 && wasTruncatedByMaxLines)
        {
            drawLines[linesToDraw - 1] = FitLineWithEllipsis(paint, drawLines[linesToDraw - 1], innerW);
        }

        // Vertical start (Skia draws at baseline, so apply ascent shift)
        var fm = paint.FontMetrics;
        float baselineShift = -fm.Ascent;

        float contentH = linesToDraw * _lineHeight;
        float yStart = _vAlign switch
        {
            VerticalAlign.Center => rect.Top + vPad + Math.Max(0, (innerH - contentH) * 0.5f),
            VerticalAlign.Bottom => rect.Bottom - vPad - contentH,
            _ => rect.Top + vPad
        };

        // Horizontal anchor per line
        float xAnchorLeft = rect.Left + hPad;
        float xAnchorCenter = rect.MidX;
        float xAnchorRight = rect.Right - hPad;

        canvas.Save();
        canvas.ClipRect(rect);

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
                shadow.MaskFilter = shadowBlur > 0f
                    ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, shadowBlur)
                    : null;
                shadow.Color = new SKColor(0, 0, 0, _shadowAlpha);

                // manually offset
                canvas.DrawText(line, x + shadowDx, y + baselineShift + shadowDy, shadow);
            }

            if (_useOutline)
            {
                using var outline = paint.Clone();
                outline.IsStroke = true;
                outline.StrokeWidth = outlineWidth;
                outline.Color = SKColors.Black;
                canvas.DrawText(line, x, y + baselineShift, outline);
            }

            canvas.DrawText(line, x, y + baselineShift, paint);
            y += _lineHeight;

            // safety clip
            if (y > rect.Bottom)
                break;
        }

        canvas.Restore();
    }

    internal static float ResolveTextScale(DirectDrawingMode mode, float viewportZoom)
    {
        if (mode != DirectDrawingMode.SceneLayer)
            return 1f;

        return viewportZoom > 0f ? viewportZoom : 1f;
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

            if (!_wrapText)
            {
                _lines.Add(para);
                continue;
            }

            var words = para.Split(' ', StringSplitOptions.None);
            var current = string.Empty;

            foreach (var word in words)
            {
                string candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                float candidateWidth = paint.MeasureText(candidate);

                if (candidateWidth <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                // If current already has content, commit it first and retry this word on a new line.
                if (!string.IsNullOrEmpty(current))
                {
                    _lines.Add(current);
                    current = string.Empty;
                }

                // If the single word fits on its own line, use it.
                if (paint.MeasureText(word) <= maxWidth)
                {
                    current = word;
                    continue;
                }

                // Fallback: hard-break an overlong word by character.
                var chunk = string.Empty;
                foreach (char ch in word)
                {
                    string next = chunk + ch;
                    if (paint.MeasureText(next) <= maxWidth || string.IsNullOrEmpty(chunk))
                    {
                        chunk = next;
                    }
                    else
                    {
                        _lines.Add(chunk);
                        chunk = ch.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(chunk))
                    current = chunk;
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

    private static string FitLineWithEllipsis(SKPaint paint, string text, float maxWidth)
    {
        const string ellipsis = "...";

        if (string.IsNullOrEmpty(text))
            return text;

        if (paint.MeasureText(text) <= maxWidth)
            return text;

        float ellipsisWidth = paint.MeasureText(ellipsis);
        if (ellipsisWidth > maxWidth)
            return string.Empty;

        int length = text.Length;
        while (length > 0)
        {
            string candidate = text.Substring(0, length).TrimEnd() + ellipsis;
            if (paint.MeasureText(candidate) <= maxWidth)
                return candidate;

            length--;
        }

        return ellipsisWidth <= maxWidth ? ellipsis : string.Empty;
    }

    #region public readonly properties

    /// <summary>Gets the current raw text content.</summary>
    public string Text => _text;

    /// <summary>Gets the laid-out lines (after wrapping and paragraph processing).</summary>
    public List<string> Lines => _lines;

    /// <summary>Gets the measured line height (in pixels) used for drawing.</summary>
    public float LineHeight => _lineHeight;

    /// <summary>Gets the configured foreground (text) color.</summary>
    public SKColor ForeColor => _foreColor;

    /// <summary>Gets the configured background color.</summary>
    public SKColor BackColor => _backColor;

    /// <summary>Gets the requested font size (before any auto-shrink adjustments).</summary>
    public float FontSize => _fontSize;

    /// <summary>Gets the optional minimum font size used by auto-shrink (or null if disabled).</summary>
    public float? MinFontSize => _minFontSize;

    /// <summary>Gets the configured typeface (or null to use <see cref="SKTypeface.Default"/>).</summary>
    public SKTypeface? TypeFace => _typeface;

    /// <summary>Gets a value indicating whether an outline stroke is enabled.</summary>
    public bool OutlineEnabled => _useOutline;

    /// <summary>Gets a value indicating whether word wrapping is enabled.</summary>
    public bool WrapText => _wrapText;

    /// <summary>Gets the optional maximum number of lines to draw (or null for no limit).</summary>
    public int? MaxLines => _maxLines;

    /// <summary>Gets a value indicating whether a drop shadow is enabled.</summary>
    public bool ShadowEnabled => _useShadow;

    /// <summary>Gets the horizontal shadow offset (in pixels).</summary>
    public float ShadowDx => _shadowDx;

    /// <summary>Gets the vertical shadow offset (in pixels).</summary>
    public float ShadowDy => _shadowDy;

    /// <summary>Gets the shadow opacity (0-255).</summary>
    public byte ShadowAlpha => _shadowAlpha;

    /// <summary>Gets the blur radius for the shadow (sigma, in pixels).</summary>
    public float ShadowBlurSigma => _shadowBlurSigma;

    /// <summary>Gets the horizontal alignment used when drawing lines.</summary>
    public SKTextAlign AlignHoriz => _hAlign;

    /// <summary>Gets the vertical alignment used when drawing lines.</summary>
    public VerticalAlign AlignVert => _vAlign;

    /// <summary>Gets a value indicating whether color pulsing is enabled.</summary>
    public bool PulseTextEnabled => _pulseTextEnabled;

    /// <summary>Gets the starting color for the pulse effect.</summary>
    public SKColor PulseFrom => _pulseFrom;

    /// <summary>Gets the ending color for the pulse effect.</summary>
    public SKColor PulseTo => _pulseTo;

    /// <summary>Gets the pulse period in seconds.</summary>
    public float PulsePeriodSec => _pulsePeriodSec;

    /// <summary>Gets the pulse waveform currently in use.</summary>
    public PulseWave PulseWaveValue => _pulseWave;

    /// <summary>Gets the active text reveal mode.</summary>
    public TextRevealMode TextRevealModeValue => _textRevealMode;

    /// <summary>Gets the configured reveal rate (characters/sec or words/sec depending on mode).</summary>
    public float TextRevealRate => _revealRate;

    /// <summary>Gets a value indicating whether punctuation pauses are enabled.</summary>
    public bool PuctuationPauseEnabled => _pauseEnabled;

    /// <summary>Gets the long punctuation pause duration (seconds) used for '.', '!', '?'.</summary>
    public float PunctuationPauseLongSec => _pauseLongSec;

    /// <summary>Gets the short punctuation pause duration (seconds) used for ',', ';', ':'.</summary>
    public float PunctuationPauseShortSec => _pauseShortSec;

    /// <summary>Gets the currently resolved (effective) foreground color used for drawing.</summary>
    public SKColor ResolvedForeColor => _resolvedForeColor;

    #endregion public readonly properties

    /// <summary>
    /// Selects the waveform used by <see cref="PulseColor(Color, Color, float, bool, bool)"/>.
    /// </summary>
    public enum PulseWave
    {
        /// <summary>Sine wave: smooth continuous pulse between colors.</summary>
        Sine,
        /// <summary>Triangle wave: linear fade-in/out between colors.</summary>
        Triangle
    }

    /// <summary>
    /// Describes the mode used for revealing text over time.
    /// </summary>
    public enum TextRevealMode
    {
        /// <summary>No reveal is active; all text is shown.</summary>
        None,
        /// <summary>Reveal advances by characters per second.</summary>
        CharactersPerSecond,
        /// <summary>Reveal advances by words per second.</summary>
        WordsPerSecond,
        /// <summary>Reveal amount is controlled directly via <see cref="RevealSetCount(int)"/>.</summary>
        ManualCount
    }

    private readonly object _eventLock = new();

    /// <summary>
    /// Releases managed resources used by the text block.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> when disposing managed resources; otherwise, <see langword="false"/>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_eventLock)
            {
                TextRevealed = null;
                TextRevealComplete = null;
            }
        }

        base.Dispose(disposing);
    }
}
