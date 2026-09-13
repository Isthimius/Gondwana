using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Widgets.Hud;

/// <summary>
/// Displays a world-space name label that automatically follows a sprite.
/// </summary>
public sealed class NameTagWidget : WidgetBase
{
    private bool _disposed;
    private bool _backgroundVisible = true;
    private Size _size;
    private Point _offsetPx;
    private string _text;

    /// <summary>
    /// Creates a name tag that follows <paramref name="target"/>.
    /// </summary>
    public NameTagWidget(RenderSurfaceHostBase renderSurfaceHost,
                         Sprite target,
                         string text,
                         Size? size = null,
                         Point? offsetPx = null,
                         string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, nickname: nickname)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        _text = text ?? string.Empty;
        _size = ValidateSize(size ?? new Size(128, 26));
        _offsetPx = offsetPx ?? Point.Empty;

        Background = new DirectRectangle(
                Color.FromArgb(185, 20, 22, 28),
                renderSurfaceHost,
                Target.SceneLayer,
                new Rectangle(Point.Empty, _size),
                $"{Nickname}-name-tag-background")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(200, 220, 220, 230))
            .SetStrokeWidth(1f)
            .SetCornerRadius(4f);

        TextBlock = new TextBlock(
                renderSurfaceHost,
                Target.SceneLayer,
                view: null,
                worldBounds: new Rectangle(Point.Empty, _size),
                nickname: $"{Nickname}-name-tag-text")
            .SetText(_text)
            .SetFont(SKTypeface.Default, 15f, minSize: 9f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetPadding(5f, 2f)
            .EnableWrapping(false);

        Add(Background, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        Add(TextBlock, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);

        IsInputEnabled = false;
        IsPointerInputEnabled = false;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;

        SetNameTagZOrder(0);

        Target.SpriteMoved += OnTargetMoved;
        Target.VisualBoundsChanged += OnTargetVisualBoundsChanged;
        Target.Disposing += OnTargetDisposing;

        RefreshPosition();
    }

    /// <summary>
    /// Gets the sprite followed by the tag.
    /// </summary>
    public Sprite Target { get; }

    /// <summary>
    /// Gets the background drawing.
    /// </summary>
    public DirectRectangle Background { get; }

    /// <summary>
    /// Gets the text drawing.
    /// </summary>
    public TextBlock TextBlock { get; }

    /// <summary>
    /// Gets the displayed name text.
    /// </summary>
    public string Text => _text;

    /// <summary>
    /// Gets or sets the name-tag size in world pixels.
    /// </summary>
    public Size Size
    {
        get => _size;
        set
        {
            Size validated = ValidateSize(value);
            if (_size == validated)
                return;

            _size = validated;
            Background.WorldBounds = new Rectangle(Background.WorldBounds.Location, _size);
            TextBlock.SetSize(_size);
            RefreshPosition();
        }
    }

    /// <summary>
    /// Gets or sets an additional world-pixel offset from the centered position above the sprite.
    /// </summary>
    public Point OffsetPx
    {
        get => _offsetPx;
        set
        {
            _offsetPx = value;
            RefreshPosition();
        }
    }

    /// <summary>
    /// Gets the current world bounds of the name tag.
    /// </summary>
    public Rectangle BoundsWorld => Background.WorldBounds;

    /// <summary>
    /// Changes the displayed name.
    /// </summary>
    public NameTagWidget SetText(string text)
    {
        _text = text ?? string.Empty;
        TextBlock.SetText(_text);
        return this;
    }

    /// <summary>
    /// Sets the name-tag text color.
    /// </summary>
    public NameTagWidget SetTextColor(SKColor color)
    {
        TextBlock.SetColors(color, SKColors.Transparent);
        return this;
    }

    /// <summary>
    /// Sets the background color.
    /// </summary>
    public NameTagWidget SetBackgroundColor(Color color)
    {
        Background.SetColor(color);
        Background.SetPosition(Background.GetPosition());
        return this;
    }

    /// <summary>
    /// Shows or hides the background while preserving the text.
    /// </summary>
    public NameTagWidget ShowBackground(bool visible = true)
    {
        _backgroundVisible = visible;
        Background.Visible = visible;
        return this;
    }

    /// <summary>
    /// Sets the base Z-order used by the name-tag visuals.
    /// </summary>
    public NameTagWidget SetNameTagZOrder(int zOrder)
    {
        Background.ZOrder = zOrder;
        TextBlock.ZOrder = zOrder + 1;
        return this;
    }

    /// <summary>
    /// Repositions the tag from the target's current render bounds.
    /// </summary>
    public void RefreshPosition()
    {
        Rectangle targetBounds = Target.DrawLocationWorld;
        int x = targetBounds.Left + (targetBounds.Width - Size.Width) / 2 + OffsetPx.X;
        int y = targetBounds.Top - Size.Height - 6 + OffsetPx.Y;
        SetPosition(x, y);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Target.SpriteMoved -= OnTargetMoved;
        Target.VisualBoundsChanged -= OnTargetVisualBoundsChanged;
        Target.Disposing -= OnTargetDisposing;
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();
        Background.Visible = _backgroundVisible;
    }

    private void OnTargetMoved(SpriteMovedEventArgs args)
    {
        RefreshPosition();
    }

    private void OnTargetVisualBoundsChanged(Sprite sprite)
    {
        RefreshPosition();
    }

    private void OnTargetDisposing(Sprite sprite)
    {
        Dispose();
    }

    private static Size ValidateSize(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "Name-tag size must be positive.");

        return size;
    }
}
