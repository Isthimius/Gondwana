using Gondwana.Physics.Collisions;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// Represents a single tile cell within a tilesheet editor grid.
/// </summary>
public sealed class TileCellViewModel : ViewModelBase
{
    /// <summary>
    /// Gets the zero-based sequential index of this tile.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets the column position of this tile in the grid.
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Gets the row position of this tile in the grid.
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// Gets the left pixel offset of this tile within the tilesheet image.
    /// </summary>
    public double Left { get; init; }

    /// <summary>
    /// Gets the top pixel offset of this tile within the tilesheet image.
    /// </summary>
    public double Top { get; init; }

    /// <summary>
    /// Gets the pixel width of this tile.
    /// </summary>
    public double Width { get; init; }

    /// <summary>
    /// Gets the pixel height of this tile.
    /// </summary>
    public double Height { get; init; }

    private string _name = string.Empty;
    private int _collisionAdjustTop;
    private int _collisionAdjustBottom;
    private int _collisionAdjustLeft;
    private int _collisionAdjustRight;

    /// <summary>
    /// Gets or sets the tile name.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Gets or sets the frame-specific top collision adjustment.
    /// </summary>
    public int CollisionAdjustTop
    {
        get => _collisionAdjustTop;
        set
        {
            if (SetProperty(ref _collisionAdjustTop, value))
                NotifyCollisionAreaChanged();
        }
    }

    /// <summary>
    /// Gets or sets the frame-specific bottom collision adjustment.
    /// </summary>
    public int CollisionAdjustBottom
    {
        get => _collisionAdjustBottom;
        set
        {
            if (SetProperty(ref _collisionAdjustBottom, value))
                NotifyCollisionAreaChanged();
        }
    }

    /// <summary>
    /// Gets or sets the frame-specific left collision adjustment.
    /// </summary>
    public int CollisionAdjustLeft
    {
        get => _collisionAdjustLeft;
        set
        {
            if (SetProperty(ref _collisionAdjustLeft, value))
                NotifyCollisionAreaChanged();
        }
    }

    /// <summary>
    /// Gets or sets the frame-specific right collision adjustment.
    /// </summary>
    public int CollisionAdjustRight
    {
        get => _collisionAdjustRight;
        set
        {
            if (SetProperty(ref _collisionAdjustRight, value))
                NotifyCollisionAreaChanged();
        }
    }

    /// <summary>
    /// Gets or sets the frame-specific collision adjustment as one value object.
    /// </summary>
    public CollisionAdjust CollisionAdjust
    {
        get => new(
            CollisionAdjustTop,
            CollisionAdjustBottom,
            CollisionAdjustLeft,
            CollisionAdjustRight);
        set
        {
            if (_collisionAdjustTop == value.Top &&
                _collisionAdjustBottom == value.Bottom &&
                _collisionAdjustLeft == value.Left &&
                _collisionAdjustRight == value.Right)
            {
                return;
            }

            _collisionAdjustTop = value.Top;
            _collisionAdjustBottom = value.Bottom;
            _collisionAdjustLeft = value.Left;
            _collisionAdjustRight = value.Right;

            OnPropertyChanged(nameof(CollisionAdjustTop));
            OnPropertyChanged(nameof(CollisionAdjustBottom));
            OnPropertyChanged(nameof(CollisionAdjustLeft));
            OnPropertyChanged(nameof(CollisionAdjustRight));
            NotifyCollisionAreaChanged();
        }
    }

    /// <summary>
    /// Gets the adjusted collision rectangle's absolute left position in the source image.
    /// </summary>
    public double CollisionLeft => Left + CollisionAdjustLeft;

    /// <summary>
    /// Gets the adjusted collision rectangle's absolute top position in the source image.
    /// </summary>
    public double CollisionTop => Top + CollisionAdjustTop;

    /// <summary>
    /// Gets the adjusted collision rectangle width.
    /// </summary>
    public double CollisionWidth =>
        Math.Max(0d, Width + CollisionAdjustRight - CollisionAdjustLeft);

    /// <summary>
    /// Gets the adjusted collision rectangle height.
    /// </summary>
    public double CollisionHeight =>
        Math.Max(0d, Height + CollisionAdjustBottom - CollisionAdjustTop);

    private void NotifyCollisionAreaChanged()
    {
        OnPropertyChanged(nameof(CollisionAdjust));
        OnPropertyChanged(nameof(CollisionLeft));
        OnPropertyChanged(nameof(CollisionTop));
        OnPropertyChanged(nameof(CollisionWidth));
        OnPropertyChanged(nameof(CollisionHeight));
    }
}
