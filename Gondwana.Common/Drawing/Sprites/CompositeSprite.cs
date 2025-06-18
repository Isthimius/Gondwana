using Gondwana.EventArgs;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;

namespace Gondwana.Drawing.Sprites;

[DataContract]
public class CompositeSprite
{
    [DataMember]
    private List<Sprite> _children = new List<Sprite>();

    #region ctor
    public CompositeSprite() { }

    public CompositeSprite(List<Sprite> sprites)
    {
        foreach (var sprite in sprites)
            Add(sprite);
    }

    public CompositeSprite(params Sprite[] sprites) : this(sprites.ToList<Sprite>()) { }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        foreach (var sprite in _children)
            sprite.Disposing += sprite_Disposing;
    }
    #endregion

    #region methods
    public void Add(Sprite sprite)
    {
        if (_children.Contains(sprite))
            return;

        sprite.Disposing += sprite_Disposing;
        _children.Add(sprite);
    }

    public void Remove(Sprite sprite)
    {
        if (!_children.Contains(sprite))
            return;

        sprite.Disposing -= sprite_Disposing;
        _children.Remove(sprite);
    }

    private void sprite_Disposing(SpriteDisposingEventArgs e)
    {
        _children.Remove(e.sprite);
    }

    public Rectangle SetRangeLocation(Point newLocation)
    {
        Rectangle oldRange = Range;

        int difX = newLocation.X - oldRange.Location.X;
        int difY = newLocation.Y - oldRange.Location.Y;

        foreach (var sprite in _children)
        {
            var drawRange = sprite.DrawLocation;
            drawRange.Location = new Point(drawRange.X + difX, drawRange.Y + difY);
            sprite.MoveSprite(drawRange);
        }

        return Range;
    }
    #endregion

    #region properties
    [IgnoreDataMember]
    public ReadOnlyCollection<Sprite> Children
    {
        get { return _children.AsReadOnly(); }
    }

    [IgnoreDataMember]
    public Rectangle Range
    {
        get
        {
            int minX = 0;
            int maxX = 0;
            int minY = 0;
            int maxY = 0;

            foreach (var sprite in _children)
            {
                var drawLoc = sprite.DrawLocation;

                if (drawLoc.Left < minX)
                    minX = drawLoc.Left;

                if (drawLoc.Right > maxX)
                    maxX = drawLoc.Right;

                if (drawLoc.Top < minY)
                    minY = drawLoc.Top;

                if (drawLoc.Bottom > maxY)
                    maxY = drawLoc.Bottom;
            }

            return new Rectangle(minX, minY, (maxX - minX), (maxY - minY));
        }
    }
    #endregion
}
