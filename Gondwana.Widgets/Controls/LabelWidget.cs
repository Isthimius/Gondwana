using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Displays retained-mode text as a reusable Gondwana widget.
/// </summary>
public sealed class LabelWidget : WidgetBase
{
    private SKColor _foregroundColor = SKColors.White;
    private SKColor _backgroundColor = SKColors.Transparent;
    private string _text;

    /// <summary>
    /// Creates a view-level label.
    /// </summary>
    public LabelWidget(RenderSurfaceHostBase renderSurfaceHost,
                       View view,
                       Rectangle bounds,
                       string text = "",
                       string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        _text = text ?? string.Empty;
        TextBlock = new TextBlock(renderSurfaceHost, view, bounds, $"{Nickname}.text")
            .SetText(_text)
            .SetColors(_foregroundColor, _backgroundColor);

        Add(TextBlock, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        DisableInput();
    }

    /// <summary>
    /// Creates a scene-layer label.
    /// </summary>
    public LabelWidget(RenderSurfaceHostBase renderSurfaceHost,
                       SceneLayer sceneLayer,
                       Rectangle bounds,
                       string text = "",
                       string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        _text = text ?? string.Empty;
        TextBlock = new TextBlock(renderSurfaceHost, sceneLayer, view: null, worldBounds: bounds, nickname: $"{Nickname}.text")
            .SetText(_text)
            .SetColors(_foregroundColor, _backgroundColor);

        Add(TextBlock, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        DisableInput();
    }

    /// <summary>
    /// Gets the underlying text drawing.
    /// </summary>
    public TextBlock TextBlock { get; }

    /// <summary>
    /// Gets the current label text.
    /// </summary>
    public string Text => _text;

    /// <summary>
    /// Gets the label bounds in its native coordinate space.
    /// </summary>
    public Rectangle Bounds => Mode == DirectDrawingMode.View
        ? TextBlock.ScreenBounds
        : TextBlock.WorldBounds;

    /// <summary>
    /// Gets or sets the label size while preserving its current position.
    /// </summary>
    public Size Size
    {
        get => Bounds.Size;
        set
        {
            ValidateSize(value);
            TextBlock.SetSize(value);
        }
    }

    /// <summary>
    /// Changes the displayed text.
    /// </summary>
    public LabelWidget SetText(string text)
    {
        _text = text ?? string.Empty;
        TextBlock.SetText(_text);
        return this;
    }

    /// <summary>
    /// Configures the label typeface and size.
    /// </summary>
    public LabelWidget SetFont(SKTypeface typeface, float size, float? minSize = null)
    {
        ArgumentNullException.ThrowIfNull(typeface);
        TextBlock.SetFont(typeface, size, minSize);
        return this;
    }

    /// <summary>
    /// Sets foreground and background colors.
    /// </summary>
    public LabelWidget SetColors(SKColor foreground, SKColor background)
    {
        _foregroundColor = foreground;
        _backgroundColor = background;
        TextBlock.SetColors(_foregroundColor, _backgroundColor);
        return this;
    }

    /// <summary>
    /// Sets the foreground text color while preserving the background color.
    /// </summary>
    public LabelWidget SetTextColor(SKColor color)
    {
        _foregroundColor = color;
        TextBlock.SetColors(_foregroundColor, _backgroundColor);
        return this;
    }

    /// <summary>
    /// Sets the text-block background color while preserving the foreground color.
    /// </summary>
    public LabelWidget SetBackgroundColor(SKColor color)
    {
        _backgroundColor = color;
        TextBlock.SetColors(_foregroundColor, _backgroundColor);
        return this;
    }

    /// <summary>
    /// Configures horizontal and vertical text alignment.
    /// </summary>
    public LabelWidget SetAlignment(SKTextAlign horizontal, TextBlock.VerticalAlign vertical)
    {
        TextBlock.SetAlignment(horizontal, vertical);
        return this;
    }

    /// <summary>
    /// Enables or disables wrapping.
    /// </summary>
    public LabelWidget EnableWrapping(bool enabled = true)
    {
        TextBlock.EnableWrapping(enabled);
        return this;
    }

    /// <summary>
    /// Sets symmetric horizontal and vertical padding.
    /// </summary>
    public LabelWidget SetPadding(float horizontal, float vertical)
    {
        if (!float.IsFinite(horizontal) || horizontal < 0f)
            throw new ArgumentOutOfRangeException(nameof(horizontal));
        if (!float.IsFinite(vertical) || vertical < 0f)
            throw new ArgumentOutOfRangeException(nameof(vertical));

        TextBlock.SetPadding(horizontal, vertical);
        return this;
    }

    private void DisableInput()
    {
        IsInputEnabled = false;
        IsPointerInputEnabled = false;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        ValidateSize(bounds.Size);
        return bounds;
    }

    private static void ValidateSize(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "Label size must be positive.");
    }
}
