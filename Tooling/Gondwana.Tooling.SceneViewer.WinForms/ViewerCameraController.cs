using Gondwana.Scenes;
using View = Gondwana.Rendering.Views.View;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal sealed class ViewerCameraController
{
    internal const float NormalSpeed = 320f;
    internal const float FastSpeed = 960f;
    internal const float MovementLerpPerSecond = 12f;
    internal const float MinimumZoom = .125f;
    internal const float MaximumZoom = 8f;
    internal const float ZoomDurationSeconds = .25f;

    private readonly View _view;
    private readonly SceneLayer? _zoomLayer;
    private readonly PointF _initialPosition;
    private readonly float _initialZoom;

    private PointF _velocityPxPerSecond;
    private float _targetZoom;

    internal ViewerCameraController(View view, SceneLayer? zoomLayer = null)
    {
        _view = view;
        _zoomLayer = zoomLayer;
        _initialPosition = view.Camera.PositionPx;
        _initialZoom = view.Viewport.Zoom;
        _targetZoom = _initialZoom;
    }

    internal void Move(bool north, bool south, bool west, bool east, bool fast, double seconds)
    {
        float dt = (float)Math.Clamp(seconds, 0, .1);
        if (dt <= 0)
            return;

        float x = (east ? 1 : 0) - (west ? 1 : 0);
        float y = (south ? 1 : 0) - (north ? 1 : 0);

        float targetX = 0f;
        float targetY = 0f;

        if (x != 0 || y != 0)
        {
            float speed = fast ? FastSpeed : NormalSpeed;
            float length = MathF.Sqrt(x * x + y * y);
            targetX = x / length * speed;
            targetY = y / length * speed;
        }

        float lerp = 1f - MathF.Exp(-MovementLerpPerSecond * dt);
        _velocityPxPerSecond = new PointF(
            _velocityPxPerSecond.X + (targetX - _velocityPxPerSecond.X) * lerp,
            _velocityPxPerSecond.Y + (targetY - _velocityPxPerSecond.Y) * lerp);

        if (targetX == 0f && targetY == 0f &&
            Math.Abs(_velocityPxPerSecond.X) < .01f &&
            Math.Abs(_velocityPxPerSecond.Y) < .01f)
        {
            _velocityPxPerSecond = PointF.Empty;
            return;
        }

        _view.Camera.PanBy(new PointF(
            _velocityPxPerSecond.X * dt,
            _velocityPxPerSecond.Y * dt));
    }

    internal void Zoom(int wheelDelta)
    {
        var bounds = _view.Viewport.TargetRectPx;
        Zoom(
            new Point(
                bounds.Left + bounds.Width / 2,
                bounds.Top + bounds.Height / 2),
            wheelDelta);
    }

    internal void Zoom(Point screenPosition, int wheelDelta)
    {
        if (wheelDelta == 0)
            return;

        _targetZoom = Math.Clamp(
            _targetZoom * MathF.Pow(1.2f, wheelDelta / 120f),
            MinimumZoom,
            MaximumZoom);

        if (_zoomLayer is not null)
        {
            _view.ZoomAroundScreenPoint(
                _zoomLayer,
                screenPosition,
                _targetZoom,
                ZoomDurationSeconds);
            return;
        }

        _view.Viewport.ZoomToOverDuration(_targetZoom, ZoomDurationSeconds);
    }

    internal void Reset()
    {
        _velocityPxPerSecond = PointF.Empty;
        _targetZoom = _initialZoom;

        if (_zoomLayer is not null)
        {
            var bounds = _view.Viewport.TargetRectPx;
            _view.ZoomAroundScreenPoint(
                _zoomLayer,
                new PointF(
                    bounds.Left + bounds.Width / 2f,
                    bounds.Top + bounds.Height / 2f),
                _initialZoom,
                durationSeconds: 0f);
        }
        else
        {
            _view.Viewport.SnapZoom(_initialZoom);
        }

        _view.Camera.SnapTo(_initialPosition);
    }
}
