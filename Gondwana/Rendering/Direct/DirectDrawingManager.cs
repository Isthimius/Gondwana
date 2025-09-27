using System.Collections.ObjectModel;

namespace Gondwana.Rendering.Direct;

public sealed class DirectDrawingManager
{
    internal static readonly Lazy<List<DirectDrawingBase>> _instances = new(() => new List<DirectDrawingBase>());

    public ReadOnlyCollection<DirectDrawingBase> Instances => _instances.Value.AsReadOnly();

    public int Count => _instances.Value.Count;

    public DirectDrawingBase? GetDirectDrawing(string name) =>
        _instances.Value.FirstOrDefault(d => d.Name == name);

    internal void RenderAll()
    {
        _instances.Value.Sort();

        foreach (var drawing in _instances.Value)
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

    internal static void Add(DirectDrawingBase drawing)
    {
        if (!_instances.Value.Contains(drawing))
        {
            _instances.Value.Add(drawing);
            drawing.Disposing += (sender, directDrawing) => _instances.Value.Remove(directDrawing);
        }
    }

    public void ClearAll()
    {
        foreach (var drawing in _instances.Value)
            drawing.Dispose();
    }

    public void Clear(string name) => GetDirectDrawing(name)?.Dispose();
}