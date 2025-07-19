using System.Collections.ObjectModel;

namespace Gondwana.Rendering.Direct;

public static class DirectDrawingManager
{
    internal static readonly List<DirectDrawing> _instances = new();

    public static ReadOnlyCollection<DirectDrawing> Instances => _instances.AsReadOnly();

    public static int Count => _instances.Count;

    public static DirectDrawing? GetDirectDrawing(string name) =>
        _instances.FirstOrDefault(d => d.Name == name);

    public static void RenderAll()
    {
        _instances.Sort();

        foreach (var drawing in _instances)
        {
            if (!drawing.Bounds.IntersectsWith(drawing.Surface.Buffer.DirtyRectangle))
                continue;

            if (drawing._dirty)
            {
                drawing.Render();
                drawing._dirty = false;
            }
        }
    }

    public static void Add(DirectDrawing drawing)
    {
        if (!_instances.Contains(drawing))
        {
            _instances.Add(drawing);
            drawing.Disposing += (sender, directDrawing) => _instances.Remove(directDrawing);
        }
    }

    public static void ClearAll()
    {
        foreach (var drawing in _instances)
            drawing.Dispose();
    }

    public static void Clear(string name) => GetDirectDrawing(name)?.Dispose();
}
