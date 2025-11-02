using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites;

public class CompositeSprite
{
    [JsonProperty]
    private List<Sprite> _children = new List<Sprite>();

    #region ctor

    public CompositeSprite()
    { }

    public CompositeSprite(List<Sprite> sprites)
    {
        foreach (var sprite in sprites)
            Add(sprite);
    }

    public CompositeSprite(params Sprite[] sprites) : this(sprites.ToList<Sprite>())
    {
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        foreach (var sprite in _children)
            sprite.Disposing += sprite_Disposing;
    }

    #endregion ctor

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

    private void sprite_Disposing(Sprite sprite)
    {
        _children.Remove(sprite);
    }

    #endregion methods

    #region properties

    [JsonIgnore]
    public ReadOnlyCollection<Sprite> Children => _children.AsReadOnly();

    [JsonIgnore]
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

    #endregion properties
}