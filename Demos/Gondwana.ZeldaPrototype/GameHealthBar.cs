using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Rendering;

namespace Gondwana.ZeldaPrototype;

/// <summary>
/// A small world-space health bar built only from Gondwana's stable direct-drawing API.
/// </summary>
internal sealed class GameHealthBar
{
    private const int InnerPadding = 2;

    private readonly Sprite _target;
    private readonly DirectRectangle _track;
    private readonly DirectRectangle _fill;
    private readonly float _maximum;
    private readonly Size _size;
    private float _value;

    internal GameHealthBar(
        RenderSurfaceHostBase renderSurfaceHost,
        Sprite target,
        float maximum,
        Size size,
        string nickname)
    {
        if (maximum <= 0f)
            throw new ArgumentOutOfRangeException(nameof(maximum));

        _target = target;
        _maximum = maximum;
        _value = maximum;
        _size = size;

        _track = new DirectRectangle(
                Color.FromArgb(220, 20, 24, 31),
                renderSurfaceHost,
                target.SceneLayer,
                new Rectangle(Point.Empty, size),
                $"{nickname}-track")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(235, 235, 241, 247))
            .SetStrokeWidth(1f)
            .SetStrokeAlign(DirectRectangle.StrokeAlign.Inside)
            .SetCornerRadius(2f);

        _fill = new DirectRectangle(
                Color.FromArgb(245, 55, 210, 105),
                renderSurfaceHost,
                target.SceneLayer,
                Rectangle.Empty,
                $"{nickname}-fill")
            .SetFilled(true)
            .SetStrokeWidth(0f)
            .SetCornerRadius(1f);

        _target.SpriteMoved += OnTargetMoved;
        RefreshPosition();
        UpdateFillBounds();
    }

    internal float Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0f, _maximum);
            UpdateFillBounds();
        }
    }

    internal void SetFillColor(Color color) => _fill.SetColor(color);

    internal void SetZOrder(int zOrder)
    {
        _track.ZOrder = zOrder;
        _fill.ZOrder = zOrder + 1;
    }

    internal void Show()
    {
        _track.Visible = true;
        _fill.Visible = _value > 0f;
    }

    internal void Hide()
    {
        _track.Visible = false;
        _fill.Visible = false;
    }

    internal void RefreshPosition()
    {
        Rectangle targetBounds = _target.DrawLocationWorld;
        int x = targetBounds.Left + (targetBounds.Width - _size.Width) / 2;
        int y = targetBounds.Top - _size.Height - 6;

        _track.WorldBounds = new Rectangle(x, y, _size.Width, _size.Height);
        UpdateFillBounds();
    }

    private void OnTargetMoved(SpriteMovedEventArgs args) => RefreshPosition();

    private void UpdateFillBounds()
    {
        int availableWidth = Math.Max(0, _size.Width - InnerPadding * 2);
        int availableHeight = Math.Max(1, _size.Height - InnerPadding * 2);
        int fillWidth = (int)MathF.Round(availableWidth * (_value / _maximum));

        _fill.WorldBounds = new Rectangle(
            _track.WorldBounds.Left + InnerPadding,
            _track.WorldBounds.Top + InnerPadding,
            fillWidth,
            availableHeight);
        _fill.Visible = _value > 0f && _track.Visible;
    }
}
