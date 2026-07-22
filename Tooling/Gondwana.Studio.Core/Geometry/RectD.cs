namespace Gondwana.Studio.Core.Geometry;

/// <summary>
/// A platform-neutral rectangle with double-precision coordinates.
/// Replaces <c>Avalonia.Rect</c> in framework-agnostic ViewModels.
/// </summary>
public readonly struct RectD
{
    /// <summary>Gets the X coordinate of the left edge.</summary>
    public double X { get; init; }

    /// <summary>Gets the Y coordinate of the top edge.</summary>
    public double Y { get; init; }

    /// <summary>Gets the width.</summary>
    public double Width { get; init; }

    /// <summary>Gets the height.</summary>
    public double Height { get; init; }

    /// <summary>
    /// Initializes a new <see cref="RectD"/>.
    /// </summary>
    public RectD(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the right edge.</summary>
    public double Right => X + Width;

    /// <summary>Gets the bottom edge.</summary>
    public double Bottom => Y + Height;

    /// <summary>Returns true if the rectangle has no area.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <inheritdoc/>
    public override string ToString() => $"({X},{Y},{Width},{Height})";
}
