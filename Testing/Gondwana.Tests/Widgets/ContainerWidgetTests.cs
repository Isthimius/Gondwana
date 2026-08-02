using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Widgets;
using System.Drawing;
using System.Numerics;
using Gondwana.Tests;

namespace Gondwana.Tests.Widgets;

public sealed class ContainerWidgetTests
{
    [Fact]
    public void SetPosition_MovesChildWidget()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        using var parent = new TestContainerWidget(
            host,
            DirectDrawingMode.View,
            new PointF(
                10,
                20));

        var child = new TestLeafWidget(
            host,
            view,
            new Rectangle(
                15,
                25,
                20,
                20));

        parent.Attach(
            child,
            new Vector2(
                5,
                5));

        parent.SetPosition(
            new Vector2(
                100,
                200));

        Assert.Equal(
            new Vector2(
                105,
                205),
            child.GetPosition());
    }

    [Fact]
    public void ShowHideActivate_PropagateToChildWidget()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        using var parent = new TestContainerWidget(
            host,
            DirectDrawingMode.View);

        var child = new TestLeafWidget(
            host,
            view,
            new Rectangle(
                0,
                0,
                20,
                20));

        bool shown = false;
        bool hidden = false;
        bool activated = false;

        child.Shown +=
            () => shown = true;

        child.Hidden +=
            () => hidden = true;

        child.Activated +=
            () => activated = true;

        parent.Attach(
            child,
            Vector2.Zero);

        parent.Show();
        parent.Activate();
        parent.Hide();

        Assert.True(shown);
        Assert.True(activated);
        Assert.True(hidden);
    }

    [Fact]
    public void Dispose_DisposesChildWidget()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        var parent = new TestContainerWidget(
            host,
            DirectDrawingMode.View);

        var child = new TestLeafWidget(
            host,
            view,
            new Rectangle(
                0,
                0,
                20,
                20));

        bool disposed = false;

        child.Disposing +=
            (_, _) => disposed = true;

        parent.Attach(
            child,
            Vector2.Zero);

        parent.Dispose();

        Assert.True(disposed);
    }

    [Fact]
    public void UnhandledChildKeyboardInput_BubblesToContainer()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        using var parent = new TestContainerWidget(
            host,
            DirectDrawingMode.View);

        var child = new TestLeafWidget(
            host,
            view,
            new Rectangle(
                0,
                0,
                20,
                20));

        parent.Attach(
            child,
            Vector2.Zero);

        child.RaiseKeyboard(
            key: 27);

        Assert.Equal(
            27,
            parent.LastKeyboardKey);
    }

    [Fact]
    public void SceneLayerWidget_IsHittableThroughEachNonOverlappingView()
    {
        using var host = new TestRenderSurfaceHost();

        SceneLayer layer =
            AddLayer(host);

        View leftView = AddView(
            host,
            new Rectangle(
                0,
                0,
                320,
                240));

        View rightView = AddView(
            host,
            new Rectangle(
                320,
                0,
                320,
                240));

        using var widget = new TestLeafWidget(
            host,
            layer,
            new Rectangle(
                10,
                10,
                40,
                40));

        PointF leftScreen =
            leftView.WorldPxToScreenPx(
                layer,
                new PointF(
                    20,
                    20));

        PointF rightScreen =
            rightView.WorldPxToScreenPx(
                layer,
                new PointF(
                    20,
                    20));

        Assert.True(
            widget.HitTest(
                leftView,
                Point.Round(leftScreen)));

        Assert.True(
            widget.HitTest(
                rightView,
                Point.Round(rightScreen)));
    }

    private sealed class TestContainerWidget :
        ContainerWidget
    {
        internal int? LastKeyboardKey { get; private set; }

        internal TestContainerWidget(
            RenderSurfaceHostBase host,
            DirectDrawingMode mode,
            PointF anchor = default)
            : base(
                host,
                mode,
                anchor)
        {
        }

        internal void Attach(
            WidgetBase widget,
            Vector2 offset)
        {
            Add(
                widget,
                offset);
        }

        protected override void OnKeyboardInput(
            WidgetKeyboardEventArgs args)
        {
            base.OnKeyboardInput(args);
            LastKeyboardKey = args.Key;
            args.Handled = true;
        }
    }

    private sealed class TestLeafWidget :
        WidgetBase
    {
        internal TestLeafWidget(
            RenderSurfaceHostBase host,
            View view,
            Rectangle bounds)
            : base(
                host,
                DirectDrawingMode.View,
                bounds.Location)
        {
            Add(
                new DirectRectangle(
                    Color.White,
                    host,
                    view,
                    bounds));
        }

        internal TestLeafWidget(
            RenderSurfaceHostBase host,
            SceneLayer layer,
            Rectangle bounds)
            : base(
                host,
                DirectDrawingMode.SceneLayer,
                bounds.Location)
        {
            Add(
                new DirectRectangle(
                    Color.White,
                    host,
                    layer,
                    bounds));
        }

        internal void RaiseKeyboard(int key)
        {
            DispatchKeyboardInput(
                new WidgetKeyboardEventArgs(
                    this,
                    key,
                    KeyAction.Pressed,
                    KeyboardModifierState.None));
        }
    }

    private static View AddView(
        TestRenderSurfaceHost host,
        Rectangle? bounds = null)
    {
        Rectangle targetBounds =
            bounds ??
            new Rectangle(
                0,
                0,
                640,
                480);

        int zOrder =
            host.ViewManager.Views.Count;

        host.ViewManager.AddView(
            targetBounds,
            zOrder: zOrder);

        return host.ViewManager.Views
            .Single(view =>
                view.ZOrder == zOrder &&
                view.Viewport.TargetRectPx ==
                    targetBounds);
    }

    private static SceneLayer AddLayer(
        TestRenderSurfaceHost host)
    {
        return host.Scene.AddLayer(
            columnCount: 1,
            rowCount: 1,
            width: 512,
            height: 512,
            zOrder: 0,
            parallax: 1f,
            coordinateSystem:
                CoordinateSystemTypes.Orthogonal);
    }
}
