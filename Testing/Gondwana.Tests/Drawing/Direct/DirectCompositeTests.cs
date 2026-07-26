using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Physics.Movement;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
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
    public void CompositeAndMovableDrawing_ImplementCompositeChildContract()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        using var composite = new DirectComposite(
            host,
            DirectDrawingMode.View);

        using var rectangle = new DirectRectangle(
            Color.White,
            host,
            view,
            new Rectangle(
                0,
                0,
                20,
                20));

        Assert.IsAssignableFrom<IDirectCompositeChild>(
            composite);

        Assert.IsAssignableFrom<IDirectCompositeChild>(
            rectangle);
    }

    [Fact]
    public void Add_CustomCompositeChild_UsesInterfaceOperations()
    {
        using var host = new TestRenderSurfaceHost();

        View view = AddView(host);

        using var composite = new DirectComposite(
            host,
            DirectDrawingMode.View);

        var child = new TestCompositeChild(
            host,
            view,
            new Rectangle(
                10,
                20,
                30,
                40));

        composite.Add(child);
        composite.SetPosition(
            new Vector2(
                100,
                200));

        composite.SetIsVisible(false);
        composite.SetZOrder(17);
        composite.SetOpacity(0.4f);
        composite.FadeTo(
            0.75f,
            0.25f);

        Assert.Equal(
            new Vector2(
                110,
                220),
            child.GetPosition());

        Assert.False(child.Visible);
        Assert.Equal(
            17,
            child.AppliedZOrder);

        Assert.Equal(
            0.4f,
            child.AppliedOpacity);

        Assert.Equal(
            0.75f,
            child.FadeTargetOpacity);

        Assert.Equal(
            0.25f,
            child.FadeDurationSec);
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

    private sealed class TestCompositeChild :
        IDirectCompositeChild
    {
        private Rectangle _screenBounds;
        private Vector2 _position;
        private bool _disposed;

        internal TestCompositeChild(
            RenderSurfaceHostBase renderSurfaceHost,
            View view,
            Rectangle screenBounds)
        {
            RenderSurfaceHost = renderSurfaceHost;
            View = view;
            _screenBounds = screenBounds;
            _position = new Vector2(
                screenBounds.X,
                screenBounds.Y);
        }

        public event EventHandler<IDirectDrawable>? Disposing;

        public Guid Id { get; } =
            Guid.NewGuid();

        public string? Nickname =>
            nameof(TestCompositeChild);

        public bool Visible { get; private set; } =
            true;

        public int ZOrder =>
            AppliedZOrder;

        public RenderSurfaceHostBase RenderSurfaceHost { get; }

        public DirectDrawingMode Mode =>
            DirectDrawingMode.View;

        public Rectangle ScreenBounds =>
            _screenBounds;

        public Rectangle WorldBounds =>
            Rectangle.Empty;

        public SceneLayer? SceneLayer =>
            null;

        public View? View { get; }

        public MovementSpace PositionSpace =>
            MovementSpace.Pixel;

        internal int AppliedZOrder { get; private set; }

        internal float AppliedOpacity { get; private set; } =
            1f;

        internal float FadeTargetOpacity { get; private set; } =
            1f;

        internal float FadeDurationSec { get; private set; }

        public Vector2 GetPosition() =>
            _position;

        public void SetPosition(Vector2 position)
        {
            _position = position;

            _screenBounds = new Rectangle(
                (int)MathF.Round(position.X),
                (int)MathF.Round(position.Y),
                _screenBounds.Width,
                _screenBounds.Height);
        }

        public void SetIsVisible(bool visible)
        {
            Visible = visible;
        }

        public void SetZOrder(int zOrder)
        {
            AppliedZOrder = zOrder;
        }

        public void SetOpacity(float opacity)
        {
            AppliedOpacity = opacity;
        }

        public void FadeTo(
            float targetOpacity,
            float durationSec)
        {
            FadeTargetOpacity =
                targetOpacity;

            FadeDurationSec =
                durationSec;
        }

        public RectangleF GetDrawLocationScreen(
            View view)
        {
            return ReferenceEquals(
                    View,
                    view)
                ? _screenBounds
                : RectangleF.Empty;
        }

        public void Draw(
            BackbufferBase backbuffer,
            RectangleF destRectScreen)
        {
        }

        public void Update(long tick)
        {
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Disposing?.Invoke(
                this,
                this);
        }
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
