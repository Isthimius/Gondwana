using System.Drawing;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Drawing;

// A render instance, never a clone of the tile, sprite, drawing, or widget.
internal sealed class WrappedDrawable(IDrawable owner, SceneLayer layer, PointF offset) : IDrawable
{
    internal IDrawable Owner { get; } = owner;
    internal SceneLayer Layer { get; } = layer;
    internal PointF Offset { get; } = offset;
    public Guid Id => Owner.Id;
    public string? Nickname => Owner.Nickname;
    public bool Visible => Owner.Visible;
    public int ZOrder => Owner.ZOrder;
    public RectangleF GetDrawLocationScreen(View view)
    {
        using var scope = Enter(view);
        return Owner.GetDrawLocationScreen(view);
    }
    public void Draw(BackbufferBase backbuffer, RectangleF destRectScreen) => Owner.Draw(backbuffer, destRectScreen);

    [ThreadStatic] private static WrappedDrawable? _current;
    [ThreadStatic] private static View? _view;
    internal static PointF ScreenOffset(View view, SceneLayer layer)
    {
        if (_current is null || !ReferenceEquals(_view, view) || !ReferenceEquals(_current.Layer, layer))
            return PointF.Empty;
        float zoom = Gondwana.Rendering.RenderContext.Current?.ViewportZoom ?? view.Viewport.Zoom;
        return new(_current.Offset.X * zoom, _current.Offset.Y * zoom);
    }
    internal IDisposable Enter(View view) => new Scope(this, view);
    private sealed class Scope : IDisposable
    {
        private readonly WrappedDrawable? _prior = _current;
        private readonly View? _priorView = _view;
        internal Scope(WrappedDrawable instance, View view) { _current = instance; _view = view; }
        public void Dispose() { _current = _prior; _view = _priorView; }
    }
}
