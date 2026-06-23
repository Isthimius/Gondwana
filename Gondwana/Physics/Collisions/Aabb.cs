using System.Drawing;

namespace Gondwana.Physics.Collisions;

/// <summary>
/// Represents an axis-aligned bounding box (AABB) defined by its minimum and maximum coordinates along the X and Y
/// axes. This is the collision area of a <see cref="Drawing.Tile"/>."
/// </summary>
public readonly struct Aabb
{
    /// <summary>
    /// Gets the minimum X coordinate of the bounding box.
    /// </summary>
    public float MinX { get; }
    
    /// <summary>
    /// Gets the minimum Y coordinate of the bounding box.
    /// </summary>
    public float MinY { get; }
    
    /// <summary>
    /// Gets the maximum X coordinate of the bounding box.
    /// </summary>
    public float MaxX { get; }
    
    /// <summary>
    /// Gets the maximum Y coordinate of the bounding box.
    /// </summary>
    public float MaxY { get; }

    /// <summary>
    /// Gets the width of the bounding box.
    /// </summary>
    public float Width => MaxX - MinX;
    
    /// <summary>
    /// Gets the height of the bounding box.
    /// </summary>
    public float Height => MaxY - MinY;

    /// <summary>
    /// Gets the center point of the bounding box.
    /// </summary>
    public PointF Center => new(
        MinX + Width * 0.5f,
        MinY + Height * 0.5f);

    /// <summary>
    /// Initializes a new instance of the <see cref="Aabb"/> struct with the specified boundaries.
    /// </summary>
    /// <param name="minX">The minimum X coordinate.</param>
    /// <param name="minY">The minimum Y coordinate.</param>
    /// <param name="maxX">The maximum X coordinate.</param>
    /// <param name="maxY">The maximum Y coordinate.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="maxX"/> is less than <paramref name="minX"/>, or <paramref name="maxY"/> is less than <paramref name="minY"/>.</exception>
    public Aabb(float minX, float minY, float maxX, float maxY)
    {
        if (maxX < minX)
            throw new ArgumentException("maxX < minX");

        if (maxY < minY)
            throw new ArgumentException("maxY < minY");

        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    /// <summary>
    /// Creates an <see cref="Aabb"/> from a <see cref="Rectangle"/>.
    /// </summary>
    /// <param name="r">The rectangle to convert.</param>
    /// <returns>An <see cref="Aabb"/> with boundaries matching the rectangle.</returns>
    public static Aabb FromRectangle(Rectangle r) =>
        new(r.Left, r.Top, r.Right, r.Bottom);

    /// <summary>
    /// Creates an <see cref="Aabb"/> from a <see cref="RectangleF"/>.
    /// </summary>
    /// <param name="r">The rectangle to convert.</param>
    /// <returns>An <see cref="Aabb"/> with boundaries matching the rectangle.</returns>
    public static Aabb FromRectangleF(RectangleF r) =>
        new(r.Left, r.Top, r.Right, r.Bottom);

    /// <summary>
    /// Determines whether this bounding box intersects with another bounding box.
    /// </summary>
    /// <param name="other">The other bounding box to test for intersection.</param>
    /// <returns><c>true</c> if the bounding boxes intersect; otherwise, <c>false</c>.</returns>
    public bool Intersects(in Aabb other) =>
        !(other.MinX >= MaxX || other.MaxX <= MinX ||
          other.MinY >= MaxY || other.MaxY <= MinY);

    /// <summary>
    /// Converts this bounding box to a <see cref="Rectangle"/> by casting the floating-point coordinates to integers.
    /// </summary>
    /// <returns>A <see cref="Rectangle"/> representing this bounding box.</returns>
    public Rectangle ToRectangle() =>
        Rectangle.FromLTRB(
            (int)MinX, (int)MinY,
            (int)MaxX, (int)MaxY);
}
