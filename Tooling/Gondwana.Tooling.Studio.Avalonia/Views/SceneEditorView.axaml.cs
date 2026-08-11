using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Gondwana.Tooling.Studio.Core.Geometry;
using Gondwana.Tooling.Studio.Avalonia.ViewModels;
using Gondwana.Tooling.Studio.ViewModels;

namespace Gondwana.Tooling.Studio.Avalonia.Views;

/// <summary>
/// SceneEditorView.
/// </summary>
public partial class SceneEditorView : UserControl
{
    private const double ZoomSensitivity = 0.1;
    private const double MinZoom = 0.2;
    private const double MaxZoom = 6.0;

    private bool _panning;
    private bool _drawingCollider;
    private Point _lastPointer;
    private Point _colliderStart;

    /// <summary>
    /// SceneEditorView.
    /// </summary>
    public SceneEditorView()
    {
        InitializeComponent();
    }

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not SceneEditorViewModel vm)
            return;

        var p = e.GetPosition(SceneCanvas);
        _lastPointer = p;

        if (e.GetCurrentPoint(SceneCanvas).Properties.IsMiddleButtonPressed)
        {
            _panning = true;
            return;
        }

        var world = ScreenToWorld(vm, p);
        if (vm.ActiveTool == "Collider")
        {
            _drawingCollider = true;
            _colliderStart = world;
            return;
        }

        vm.ApplyToolAt(world.X, world.Y);
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not SceneEditorViewModel vm)
            return;

        var p = e.GetPosition(SceneCanvas);
        if (_panning)
        {
            vm.CameraX += p.X - _lastPointer.X;
            vm.CameraY += p.Y - _lastPointer.Y;
            ApplyTransform(vm);
        }

        _lastPointer = p;
    }

    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not SceneEditorViewModel vm)
            return;

        if (_drawingCollider)
        {
            var end = ScreenToWorld(vm, e.GetPosition(SceneCanvas));
            var rect = new RectD(
                Math.Min(_colliderStart.X, end.X),
                Math.Min(_colliderStart.Y, end.Y),
                Math.Abs(end.X - _colliderStart.X),
                Math.Abs(end.Y - _colliderStart.Y));
            vm.AddCollider(rect);
        }

        _panning = false;
        _drawingCollider = false;
    }

    private void OnCanvasWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not SceneEditorViewModel vm)
            return;

        vm.Zoom = Math.Clamp(vm.Zoom + (e.Delta.Y * ZoomSensitivity), MinZoom, MaxZoom);
        ApplyTransform(vm);
    }

    private static Point ScreenToWorld(SceneEditorViewModel vm, Point p) =>
        new((p.X - vm.CameraX) / vm.Zoom, (p.Y - vm.CameraY) / vm.Zoom);

    private void ApplyTransform(SceneEditorViewModel vm)
    {
        SceneCanvas.RenderTransform = new TransformGroup
        {
            Children =
            [
                new ScaleTransform(vm.Zoom, vm.Zoom),
                new TranslateTransform(vm.CameraX, vm.CameraY)
            ]
        };
    }
}
