using System.Drawing;

namespace Gondwana.Collision;

/// <summary>
/// Represents an axis-aligned bounding box (AABB) defined by its minimum and maximum coordinates along the X and Y
/// axes. This is the collision area of a <see cref="Gondwana.Drawing.Tile"/>."
/// </summary>
public readonly struct Aabb
{
    public float MinX { get; }
    public float MinY { get; }
    public float MaxX { get; }
    public float MaxY { get; }

    public float Width => MaxX - MinX;
    public float Height => MaxY - MinY;

    public PointF Center => new(
        MinX + Width * 0.5f,
        MinY + Height * 0.5f);

    public Aabb(float minX, float minY, float maxX, float maxY)
    {
        if (maxX < minX) throw new ArgumentException("maxX < minX");
        if (maxY < minY) throw new ArgumentException("maxY < minY");
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public static Aabb FromRectangle(Rectangle r) =>
        new(r.Left, r.Top, r.Right, r.Bottom);

    public static Aabb FromRectangleF(RectangleF r) =>
        new(r.Left, r.Top, r.Right, r.Bottom);

    public bool Intersects(in Aabb other) =>
        !(other.MinX >= MaxX || other.MaxX <= MinX ||
          other.MinY >= MaxY || other.MaxY <= MinY);

    public Rectangle ToRectangle() =>
        Rectangle.FromLTRB(
            (int)MinX, (int)MinY,
            (int)MaxX, (int)MaxY);
}
