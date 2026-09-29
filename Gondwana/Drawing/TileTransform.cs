using System.Drawing;
using Gondwana.Physics.Collisions;

namespace Gondwana.Drawing;

/// <summary>The eight orthogonal orientations of artwork placed in a fixed scene cell.</summary>
public enum TileTransform
{
    /// <summary>Original orientation.</summary>
    Identity,
    /// <summary>Clockwise quarter turn.</summary>
    Rotate90,
    /// <summary>Half turn.</summary>
    Rotate180,
    /// <summary>Counterclockwise quarter turn.</summary>
    Rotate270,
    /// <summary>Reflection across the vertical axis.</summary>
    FlipHorizontal,
    /// <summary>Reflection across the horizontal axis.</summary>
    FlipVertical,
    /// <summary>Reflection across the top-left to bottom-right diagonal (axis swap).</summary>
    FlipDiagonal,
    /// <summary>Reflection across the top-right to bottom-left diagonal.</summary>
    FlipAntiDiagonal
}

/// <summary>Shared placement geometry; source atlas metadata is never modified.</summary>
public static class TileTransformGeometry
{
    /// <summary>Transforms a vector in screen coordinates (positive Y points down).</summary>
    public static PointF TransformPoint(PointF point, TileTransform transform) => transform switch
    {
        TileTransform.Identity => point,
        TileTransform.Rotate90 => new(-point.Y, point.X),
        TileTransform.Rotate180 => new(-point.X, -point.Y),
        TileTransform.Rotate270 => new(point.Y, -point.X),
        TileTransform.FlipHorizontal => new(-point.X, point.Y),
        TileTransform.FlipVertical => new(point.X, -point.Y),
        TileTransform.FlipDiagonal => new(point.Y, point.X),
        TileTransform.FlipAntiDiagonal => new(-point.Y, -point.X),
        _ => throw new ArgumentOutOfRangeException(nameof(transform))
    };

    /// <summary>Gets the dimensions after orientation, swapping axes for quarter turns and diagonal reflections.</summary>
    public static Size TransformSize(Size size, TileTransform transform)
    {
        var point = TransformPoint(new(size.Width, size.Height), transform);
        return new((int)Math.Abs(point.X), (int)Math.Abs(point.Y));
    }

    /// <summary>Moves directional edge values with the artwork.</summary>
    public static Spacing TransformSpacing(Spacing value, TileTransform transform) => transform switch
    {
        TileTransform.Identity => value,
        TileTransform.Rotate90 => new(value.Bottom, value.Left, value.Top, value.Right),
        TileTransform.Rotate180 => new(value.Right, value.Bottom, value.Left, value.Top),
        TileTransform.Rotate270 => new(value.Top, value.Right, value.Bottom, value.Left),
        TileTransform.FlipHorizontal => new(value.Right, value.Top, value.Left, value.Bottom),
        TileTransform.FlipVertical => new(value.Left, value.Bottom, value.Right, value.Top),
        TileTransform.FlipDiagonal => new(value.Top, value.Left, value.Bottom, value.Right),
        TileTransform.FlipAntiDiagonal => new(value.Bottom, value.Right, value.Top, value.Left),
        _ => throw new ArgumentOutOfRangeException(nameof(transform))
    };

    /// <summary>Moves signed collision insets with the artwork.</summary>
    public static CollisionAdjust TransformCollisionAdjust(CollisionAdjust value, TileTransform transform)
    {
        var edges = TransformSpacing(new(value.Left, value.Top, value.Right, value.Bottom), transform);
        return new(edges.Top, edges.Bottom, edges.Left, edges.Right);
    }

    /// <summary>Applies an operation after the current orientation in placement space.</summary>
    public static TileTransform Compose(TileTransform current, TileTransform operation)
    {
        var x = TransformPoint(TransformPoint(new(1, 0), current), operation);
        var y = TransformPoint(TransformPoint(new(0, 1), current), operation);
        foreach (var candidate in Enum.GetValues<TileTransform>())
            if (TransformPoint(new(1, 0), candidate) == x && TransformPoint(new(0, 1), candidate) == y)
                return candidate;
        throw new InvalidOperationException("Orthogonal transforms must be closed under composition.");
    }

    /// <summary>Gets visual bounds around the fixed cell center, with oriented overhang. Half-pixel anchors round down.</summary>
    public static Rectangle GetVisualBounds(Rectangle cell, Spacing sourceOverhang, TileTransform transform)
    {
        var size = TransformSize(cell.Size, transform);
        int x = cell.X + (int)Math.Floor((cell.Width - size.Width) / 2d);
        int y = cell.Y + (int)Math.Floor((cell.Height - size.Height) / 2d);
        var edges = TransformSpacing(sourceOverhang, transform);
        return Rectangle.FromLTRB(x - edges.Left, y - edges.Top, x + size.Width + edges.Right, y + size.Height + edges.Bottom);
    }

    /// <summary>Maps a normalized source point into an oriented destination rectangle.</summary>
    public static PointF MapToDestination(PointF normalizedPoint, RectangleF destination, TileTransform transform)
    {
        var point = TransformPoint(new(normalizedPoint.X - .5f, normalizedPoint.Y - .5f), transform);
        return new(destination.X + (point.X + .5f) * destination.Width,
            destination.Y + (point.Y + .5f) * destination.Height);
    }
}
