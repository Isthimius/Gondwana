using View = Gondwana.Rendering.Views.View;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal sealed class ViewerCameraController
{
    internal const float NormalSpeed = 320f;
    internal const float FastSpeed = 960f;
    internal const float MinimumZoom = .125f;
    internal const float MaximumZoom = 8f;
    private readonly View _view;
    private readonly PointF _initialPosition;
    private readonly float _initialZoom;

    internal ViewerCameraController(View view)
    {
        _view = view;
        _initialPosition = view.Camera.PositionPx;
        _initialZoom = view.Viewport.Zoom;
    }

    internal void Move(bool north, bool south, bool west, bool east, bool fast, double seconds)
    {
        float x = (east ? 1 : 0) - (west ? 1 : 0);
        float y = (south ? 1 : 0) - (north ? 1 : 0);
        if (x == 0 && y == 0)
            return;
        float distance = (fast ? FastSpeed : NormalSpeed) * (float)Math.Clamp(seconds, 0, .1);
        float length = MathF.Sqrt(x * x + y * y);
        _view.Camera.PanBy(new PointF(x * distance / length, y * distance / length));
    }

    internal void Zoom(int wheelDelta) =>
        _view.Viewport.SnapZoom(Math.Clamp(
            _view.Viewport.Zoom * MathF.Pow(1.2f, wheelDelta / 120f), MinimumZoom, MaximumZoom));

    internal void Reset()
    {
        _view.Camera.SnapTo(_initialPosition);
        _view.Viewport.SnapZoom(_initialZoom);
    }
}
