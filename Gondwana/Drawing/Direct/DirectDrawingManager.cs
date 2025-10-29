using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Gondwana.Movement;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Direct;

public sealed class DirectDrawingManager
{
    // ---- Singleton ----
    private static readonly Lazy<DirectDrawingManager> _instance =
        new(() => new DirectDrawingManager());

    internal static DirectDrawingManager Instance => _instance.Value;

    // ---- Storage (instance-level) ----
    private readonly ConcurrentDictionary<string, IDirectDrawable> _directDrawings =
        new(StringComparer.Ordinal);

    private DirectDrawingManager() { }

    // Expose a read-only snapshot to callers.
    public ReadOnlyCollection<IDirectDrawable> DirectDrawings =>
        new ReadOnlyCollection<IDirectDrawable>([.. _directDrawings.Values]);

    public int Count => _directDrawings.Count;

    public IDirectDrawable? GetDirectDrawing(string name)
    {
        return name is null ? null : _directDrawings.TryGetValue(name, out IDirectDrawable? d) ? d : null;
    }

    public void ClearAll()
    {
        var toDispose = _directDrawings.Values.ToArray();
        foreach (var d in toDispose)
            d.Dispose(); // removal happens via event handler
    }

    public void Clear(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (_directDrawings.TryGetValue(name, out var d))
            d.Dispose();
    }

    /// <summary>
    /// Updates all registered drawables using the current engine tick.
    /// </summary>
    /// <param name="tick">Current tick from <see cref="HighResTimer"/>.</param>
    internal void UpdateAll(long tick)
    {
        // Snapshot to avoid races while iterating (consistent with RenderAll).
        var snapshot = _directDrawings.Values.ToArray();

        foreach (var drawing in snapshot)
            drawing.Update(tick);
    }

    /// <summary>
    /// Draws all registered drawables in Z-order to their associated Backbuffer.
    /// </summary>
    internal void RenderAll()
    {
        // Snapshot to avoid races while iterating.
        var snapshot = _directDrawings.Values.OfType<DirectDrawingBase>().ToArray();
        Array.Sort(snapshot, _defaultComparer);

        foreach (var drawing in snapshot)
        {
            // if the drawing's RenderSurfaceHost's Backbuffer's DirtyRectangle intersects with the drawing's Bounds, mark as dirty
            if ((drawing.RenderSurfaceHost?.Backbuffer?.DirtyRectangle.IntersectsWith(drawing.Bounds) ?? false) ||
                drawing.RenderSurfaceHost?.Scene?.RefreshNeeded == SceneRefreshType.All)
            {
                drawing._dirty = true;
            }

            if (drawing._dirty)
            {
                if (drawing.IsVisible)
                    drawing.Render();

                drawing._dirty = false;
            }
        }
    }

    /// <summary>
    /// Adds a drawing by its Name. If a drawing with the same Name already exists,
    /// it is disposed and replaced by the new one. Automatically removes on Dispose.
    /// </summary>
    internal void AddOrReplace(IDirectDrawable drawing)
    {
        if (drawing is null)
            throw new ArgumentNullException(nameof(drawing));

        var name = drawing.Name;

        _directDrawings.AddOrUpdate(
            name,
            // Add case
            key =>
            {
                drawing.Disposing += OnDrawingDisposing;
                return drawing;
            },
            // Update case
            (key, existing) =>
            {
                // Unsubscribe old instance
                existing.Disposing -= OnDrawingDisposing;

                // Dispose it
                existing.Dispose();

                // Wire up new one
                drawing.Disposing += OnDrawingDisposing;
                return drawing;
            });
    }

    private static readonly IComparer<DirectDrawingBase> _defaultComparer =
        Comparer<DirectDrawingBase>.Create((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            // First compare ZOrder
            int z = a.ZOrder.CompareTo(b.ZOrder);
            if (z != 0) return z;

            // If ZOrder equal, fall back to Name
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

    private void OnDrawingDisposing(object? sender, IDirectDrawable drawing)
    {
        _directDrawings.TryRemove(drawing.Name, out _);
        drawing.Disposing -= OnDrawingDisposing;
    }
}
