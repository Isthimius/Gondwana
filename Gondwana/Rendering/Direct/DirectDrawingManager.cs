using System.Collections.ObjectModel;

namespace Gondwana.Rendering.Direct;

public static class DirectDrawingManager
{
    internal static readonly List<DirectDrawingBase> _instances = new();

    public static ReadOnlyCollection<DirectDrawingBase> Instances => _instances.AsReadOnly();

    public static int Count => _instances.Count;

    public static DirectDrawingBase? GetDirectDrawing(string name) =>
        _instances.FirstOrDefault(d => d.Name == name);

    internal static void RenderAll()
    {
        _instances.Sort();

        foreach (var drawing in _instances)
        {
            if (!drawing.Bounds.IntersectsWith(drawing.RenderSurfaceHost.Backbuffer.DirtyRectangle))
                continue;

            if (drawing._dirty)
            {
                drawing.Render();
                drawing._dirty = false;
            }
        }
    }

    public static void Add(DirectDrawingBase drawing)
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
