using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using System.Drawing;
using System.Numerics;
using Gondwana.Tests;

namespace Gondwana.Tests.Drawing.Direct;

public sealed class DirectCompositeTests
{
    [Fact]
    public void SetPosition_MovesNestedCompositeAndDrawing()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        using var parent = new DirectComposite(
            host,
            DirectDrawingMode.View,
            new PointF(10, 20));

        var child = new DirectComposite(
            host,
            DirectDrawingMode.View,
            new PointF(20, 30));

        var rectangle = new DirectRectangle(
                Color.White,
                host,
                view,
                new Rectangle(
                    25,
                    35,
                    20,
                    20))
            .SetFilled(true);

        child.Add(rectangle);
        parent.Add(child);

        parent.SetPosition(
            new Vector2(
                100,
                200));

        Assert.Equal(
            new Vector2(
                110,
                210),
            child.GetPosition());

        Assert.Equal(
            new Vector2(
                115,
                215),
            rectangle.GetPosition());
    }

    [Fact]
    public void Add_WhenChildAlreadyHasParent_Throws()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        using var firstParent = new DirectComposite(
            host,
            DirectDrawingMode.View);

        using var secondParent = new DirectComposite(
            host,
            DirectDrawingMode.View);

        var child = CreateViewComposite(
            host,
            view);

        firstParent.Add(child);

        Assert.Throws<InvalidOperationException>(
            () => secondParent.Add(child));
    }

    [Fact]
    public void Remove_AllowsChildToBeReparented()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        using var firstParent = new DirectComposite(
            host,
            DirectDrawingMode.View);

        using var secondParent = new DirectComposite(
            host,
            DirectDrawingMode.View);

        var child = CreateViewComposite(
            host,
            view);

        firstParent.Add(child);
        firstParent.Remove(child);
        secondParent.Add(child);

        Assert.Contains(
            child,
            secondParent.Children);
    }

    [Fact]
    public void Add_WhenRelationshipWouldCreateCycle_Throws()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        using var parent = new DirectComposite(
            host,
            DirectDrawingMode.View);

        var child = CreateViewComposite(
            host,
            view);

        parent.Add(child);

        Assert.Throws<InvalidOperationException>(
            () => child.Add(parent));
    }

    [Fact]
    public void Add_WhenViewDiffers_Throws()
    {
        using var host = new TestRenderSurfaceHost();

        View firstView = AddView(
            host,
            new Rectangle(
                0,
                0,
                320,
                240));

        View secondView = AddView(
            host,
            new Rectangle(
                320,
                0,
                320,
                240));

        using var composite = new DirectComposite(
            host,
            DirectDrawingMode.View);

        var first = new DirectRectangle(
            Color.White,
            host,
            firstView,
            new Rectangle(
                0,
                0,
                20,
                20));

        using var second = new DirectRectangle(
            Color.White,
            host,
            secondView,
            new Rectangle(
                320,
                0,
                20,
                20));

        composite.Add(first);

        Assert.Throws<ArgumentException>(
            () => composite.Add(second));
    }

    [Fact]
    public void Add_WhenSceneLayerDiffers_Throws()
    {
        using var host = new TestRenderSurfaceHost();

        SceneLayer firstLayer =
            AddLayer(host);

        SceneLayer secondLayer =
            AddLayer(host);

        using var composite = new DirectComposite(
            host,
            DirectDrawingMode.SceneLayer);

        var first = new DirectRectangle(
            Color.White,
            host,
            firstLayer,
            new Rectangle(
                0,
                0,
                20,
                20));

        using var second = new DirectRectangle(
            Color.White,
            host,
            secondLayer,
            new Rectangle(
                0,
                0,
                20,
                20));

        composite.Add(first);

        Assert.Throws<ArgumentException>(
            () => composite.Add(second));
    }

    [Fact]
    public void Dispose_DisposesNestedChildren()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        var parent = new DirectComposite(
            host,
            DirectDrawingMode.View);

        var child = CreateViewComposite(
            host,
            view);

        bool childDisposed = false;

        child.Disposing +=
            (_, _) => childDisposed = true;

        parent.Add(child);
        parent.Dispose();

        Assert.True(childDisposed);
    }

    private static DirectComposite CreateViewComposite(
        TestRenderSurfaceHost host,
        View view)
    {
        var composite = new DirectComposite(
            host,
            DirectDrawingMode.View);

        composite.Add(
            new DirectRectangle(
                Color.White,
                host,
                view,
                new Rectangle(
                    0,
                    0,
                    20,
                    20)));

        return composite;
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
            zOrder: host.Scene.SceneLayers.Count,
            parallax: 1f,
            coordinateSystem:
                CoordinateSystemTypes.Orthogonal);
    }
}
