using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Manages the lifecycle, registration, and retrieval of all direct drawing objects in the Gondwana engine.
/// </summary>
/// <remarks>
/// <para>
/// DirectDrawingManager is a thread-safe singleton that serves as the central registry for all
/// <see cref="IDirectDrawable"/> instances. It handles automatic registration during construction,
/// removal on disposal, and provides efficient lookup and filtering capabilities.
/// </para>
/// <para>
/// Direct drawings are automatically registered when constructed and removed when disposed. The manager
/// maintains a concurrent dictionary keyed by each drawing's unique <see cref="IDirectDrawable.Id"/>,
/// ensuring thread-safe access during rendering and updates.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item><description>Automatic registration and cleanup of direct drawings.</description></item>
/// <item><description>Per-frame update coordination via <see cref="UpdateAll"/>.</description></item>
/// <item><description>Filtering by scene layer or view for targeted rendering.</description></item>
/// <item><description>Safe concurrent access from rendering and game logic threads.</description></item>
/// </list>
/// </para>
/// <para>
/// Access the singleton instance via <see cref="Instance"/>. Direct drawings register themselves
/// automatically during construction by calling <see cref="AddOrReplace"/> internally.
/// </para>
/// <para>
/// Thread safety: All public methods are thread-safe. The manager uses <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// internally and creates snapshots for iteration to avoid race conditions during enumeration.
/// </para>
/// </remarks>
public sealed class DirectDrawingManager
{
    // ---- Singleton ----
    private static readonly Lazy<DirectDrawingManager> _instance =
        new(() => new DirectDrawingManager());

    public static DirectDrawingManager Instance => _instance.Value;

    // ---- Storage (instance-level) ----
    private readonly ConcurrentDictionary<string, IDirectDrawable> _directDrawings =
        new(StringComparer.Ordinal);

    private DirectDrawingManager() { }

    /// <summary>
    /// Gets a read-only snapshot of all currently registered direct drawings.
    /// </summary>
    /// <value>
    /// A <see cref="ReadOnlyCollection{T}"/> containing all direct drawings managed by this instance.
    /// The collection is a snapshot taken at the time of access and will not reflect subsequent
    /// additions or removals.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property creates a new snapshot each time it is accessed, so avoid calling it repeatedly
    /// in performance-critical loops. Cache the result if you need to iterate multiple times.
    /// </para>
    /// <para>
    /// The returned collection is unordered. For ordered collections filtered by layer or view,
    /// use <see cref="GetDrawingsForLayer"/> or <see cref="GetDrawingsForView"/>.
    /// </para>
    /// </remarks>
    public ReadOnlyCollection<IDirectDrawable> DirectDrawings =>
        new ReadOnlyCollection<IDirectDrawable>([.. _directDrawings.Values]);

    /// <summary>
    /// Gets the current number of registered direct drawings.
    /// </summary>
    /// <value>
    /// An integer count of all direct drawings currently managed by this instance.
    /// </value>
    /// <remarks>
    /// This property provides a thread-safe snapshot of the count at the moment of access.
    /// The count may change immediately after being read if drawings are added or removed
    /// from other threads.
    /// </remarks>
    public int Count => _directDrawings.Count;

    /// <summary>
    /// Retrieves a direct drawing by its nickname.
    /// </summary>
    /// <param name="name">The nickname of the direct drawing to retrieve. May be <see langword="null"/>.</param>
    /// <returns>
    /// The <see cref="IDirectDrawable"/> instance with the specified nickname, or <see langword="null"/>
    /// if no matching drawing is found or <paramref name="name"/> is <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs a case-sensitive ordinal comparison using the drawing's
    /// <see cref="IDirectDrawable.Nickname"/>. If multiple drawings share the same nickname
    /// (which should generally be avoided), only one will be returned.
    /// </para>
    /// <para>
    /// For reliable lookup, consider using the drawing's <see cref="IDirectDrawable.Id"/> instead,
    /// which is guaranteed to be unique.
    /// </para>
    /// </remarks>
    public IDirectDrawable? GetDirectDrawing(string name)
    {
        return name is null ? null : _directDrawings.TryGetValue(name, out IDirectDrawable? d) ? d : null;
    }

    /// <summary>
    /// Disposes and removes all registered direct drawings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method creates a snapshot of all current drawings and disposes each one in sequence.
    /// Disposal automatically triggers removal from the manager via the <see cref="IDirectDrawable.Disposing"/>
    /// event handler.
    /// </para>
    /// <para>
    /// Use this method to perform a full cleanup, such as when changing scenes or shutting down
    /// the engine. After calling this method, <see cref="Count"/> will be zero.
    /// </para>
    /// <para>
    /// This operation is safe to call even if drawings are being added from other threads, though
    /// newly added drawings after the snapshot is taken will not be affected.
    /// </para>
    /// </remarks>
    public void ClearAll()
    {
        var toDispose = _directDrawings.Values.ToArray();
        foreach (var d in toDispose)
            d.Dispose(); // removal happens via event handler
    }

    /// <summary>
    /// Disposes and removes a specific direct drawing by its nickname.
    /// </summary>
    /// <param name="name">The nickname of the direct drawing to remove. May be <see langword="null"/> or empty.</param>
    /// <remarks>
    /// <para>
    /// If a drawing with the specified nickname exists, it is disposed, which automatically triggers
    /// its removal from the manager. If no matching drawing is found, or if <paramref name="name"/>
    /// is <see langword="null"/> or empty, this method does nothing.
    /// </para>
    /// <para>
    /// This method uses case-sensitive ordinal comparison. If multiple drawings share the same nickname,
    /// only one will be removed.
    /// </para>
    /// </remarks>
    public void Clear(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (_directDrawings.TryGetValue(name, out var d))
            d.Dispose();
    }

    /// <summary>
    /// Updates all registered direct drawings using the current engine tick.
    /// </summary>
    /// <param name="tick">The current tick value from <see cref="HighResTimer"/>.</param>
    /// <remarks>
    /// <para>
    /// This method is called internally by the engine's main loop each frame. It creates a snapshot
    /// of all registered drawings at the time of the call and invokes <see cref="IDirectDrawable.Update"/>
    /// on each one in sequence.
    /// </para>
    /// <para>
    /// The snapshot approach ensures that drawings added or removed during the update pass do not
    /// cause enumeration errors or skipped/duplicate updates.
    /// </para>
    /// <para>
    /// Do not call this method from game code; the engine handles update coordination automatically.
    /// </para>
    /// </remarks>
    internal void UpdateAll(long tick)
    {
        // Snapshot to avoid races while iterating (consistent with RenderAll).
        var snapshot = _directDrawings.Values.ToArray();

        foreach (var drawing in snapshot)
            drawing.Update(tick);
    }

    /// <summary>
    /// Adds a new direct drawing or replaces an existing one with the same ID.
    /// </summary>
    /// <param name="drawing">The direct drawing to add. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="drawing"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This method is called internally by direct drawing constructors to register themselves with the manager.
    /// If a drawing with the same <see cref="IDirectDrawable.Id"/> already exists, the old instance is
    /// disposed and replaced by the new one.
    /// </para>
    /// <para>
    /// The method automatically subscribes to the drawing's <see cref="IDirectDrawable.Disposing"/> event
    /// to ensure automatic cleanup when the drawing is disposed. Do not call this method directly from
    /// game code; registration happens automatically during construction.
    /// </para>
    /// <para>
    /// Thread safety: This method is thread-safe and uses atomic add-or-update semantics to prevent
    /// race conditions when multiple threads attempt to register drawings simultaneously.
    /// </para>
    /// </remarks>
    internal void AddOrReplace(IDirectDrawable drawing)
    {
        if (drawing is null)
            throw new ArgumentNullException(nameof(drawing));

        var name = drawing.Id.ToString();

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
        Comparer<DirectDrawingBase>.Create((Comparison<DirectDrawingBase>)((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            // First compare ZOrder
            int z = a.ZOrder.CompareTo(b.ZOrder);
            if (z != 0) return z;

            // If ZOrder equal, fall back to Name
            return string.Compare(a.Nickname, b.Nickname, StringComparison.Ordinal);
        }));

    private void OnDrawingDisposing(object? sender, IDirectDrawable drawing)
    {
        _directDrawings.TryRemove(drawing.Nickname, out _);
        drawing.Disposing -= OnDrawingDisposing;
    }

    #region helper methods

    /// <summary>
    /// Returns all direct drawings associated with a specific scene layer, ordered by Z-order then nickname.
    /// </summary>
    /// <param name="layer">The scene layer to query. May be <see langword="null"/>.</param>
    /// <returns>
    /// A read-only list of <see cref="DirectDrawingBase"/> instances attached to the specified layer,
    /// sorted by <see cref="DirectDrawingBase.ZOrder"/> (ascending) and then by <see cref="DirectDrawingBase.Nickname"/>
    /// (ordinal comparison). Returns an empty list if <paramref name="layer"/> is <see langword="null"/>
    /// or if no drawings are attached to the layer.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method filters direct drawings by <see cref="DirectDrawingMode.SceneLayer"/> mode and uses
    /// reference equality to match the specified layer instance. Only drawings positioned in world
    /// coordinates relative to the given layer are included.
    /// </para>
    /// <para>
    /// The returned list is a snapshot taken at the time of the call and will not reflect subsequent
    /// additions or removals. The sorting ensures consistent draw order when rendering the layer.
    /// </para>
    /// <para>
    /// Use this method during rendering to retrieve all drawings that should be rendered as part of
    /// a specific scene layer, respecting Z-order for correct layering.
    /// </para>
    /// </remarks>
    public IReadOnlyList<DirectDrawingBase> GetDrawingsForLayer(SceneLayer layer)
    {
        if (layer is null)
            return Array.Empty<DirectDrawingBase>();

        // Snapshot to avoid races while iterating (consistent with UpdateAll / Render patterns).
        var snapshot = _directDrawings.Values.ToArray();

        var result = snapshot
            .OfType<DirectDrawingBase>()
            .Where(d =>
                d.Mode == DirectDrawingMode.SceneLayer &&
                ReferenceEquals(d.SceneLayer, layer))
            .ToList();

        result.Sort(_defaultComparer);
        return result;
    }

    /// <summary>
    /// Returns all direct drawings associated with a specific view, ordered by Z-order then nickname.
    /// </summary>
    /// <param name="view">The view to query. May be <see langword="null"/>.</param>
    /// <returns>
    /// A read-only list of <see cref="DirectDrawingBase"/> instances attached to the specified view,
    /// sorted by <see cref="DirectDrawingBase.ZOrder"/> (ascending) and then by <see cref="DirectDrawingBase.Nickname"/>
    /// (ordinal comparison). Returns an empty list if <paramref name="view"/> is <see langword="null"/>
    /// or if no drawings are attached to the view.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method filters direct drawings by <see cref="DirectDrawingMode.View"/> mode and uses
    /// reference equality to match the specified view instance. Only drawings positioned in screen
    /// coordinates relative to the given view are included (e.g., UI overlays, HUD elements).
    /// </para>
    /// <para>
    /// The returned list is a snapshot taken at the time of the call and will not reflect subsequent
    /// additions or removals. The sorting ensures consistent draw order when rendering view overlays.
    /// </para>
    /// <para>
    /// Use this method during rendering to retrieve all drawings that should be rendered on top of
    /// a specific view, unaffected by camera movement. Common use cases include UI elements, debug
    /// overlays, and screen-space effects.
    /// </para>
    /// </remarks>
    public IReadOnlyList<DirectDrawingBase> GetDrawingsForView(View view)
    {
        if (view is null)
            return Array.Empty<DirectDrawingBase>();

        var snapshot = _directDrawings.Values.ToArray();

        var result = snapshot
            .OfType<DirectDrawingBase>()
            .Where(d =>
                d.Mode == DirectDrawingMode.View &&
                ReferenceEquals(d.View, view))
            .ToList();

        result.Sort(_defaultComparer);
        return result;
    }

    #endregion helper methods
}
