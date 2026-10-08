using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Physics.Movement;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Tests.Drawing.Direct;

/// <summary>
/// Contains regression tests for direct composite.
/// </summary>
public sealed class DirectCompositeTests
{
    /// <summary>
    /// Verifies set position moves nested composite and drawing.
    /// </summary>
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

    /// <summary>
    /// Verifies add when child already has parent throws.
    /// </summary>
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

    /// <summary>
    /// Verifies remove allows child to be reparented.
    /// </summary>
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

    /// <summary>
    /// Verifies add when relationship would create cycle throws.
    /// </summary>
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

    /// <summary>
    /// Verifies add when view differs throws.
    /// </summary>
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

    /// <summary>
    /// Verifies add when scene layer differs throws.
    /// </summary>
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

    /// <summary>
    /// Verifies composite and movable drawing implement composite child contract.
    /// </summary>
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

    /// <summary>
    /// Verifies add custom composite child uses interface operations.
    /// </summary>
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

    /// <summary>
    /// Verifies dispose disposes nested children.
    /// </summary>
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

        /// <summary>
        /// Occurs when disposal begins.
        /// </summary>
        public event EventHandler<IDirectDrawable>? Disposing;

        /// <summary>
        /// Gets the unique identifier.
        /// </summary>
        public Guid Id { get; } =
            Guid.NewGuid();

        /// <summary>
        /// Gets the optional lookup name.
        /// </summary>
        public string? Nickname =>
            nameof(TestCompositeChild);

        /// <summary>
        /// Gets whether this drawable is visible.
        /// </summary>
        public bool Visible { get; private set; } =
            true;

        /// <summary>
        /// Gets the drawing order within the layer.
        /// </summary>
        public int ZOrder =>
            AppliedZOrder;

        /// <summary>
        /// Gets the render surface host.
        /// </summary>
        public RenderSurfaceHostBase RenderSurfaceHost { get; }

        /// <summary>
        /// Gets the mode.
        /// </summary>
        public DirectDrawingMode Mode =>
            DirectDrawingMode.View;

        /// <summary>
        /// Gets the screen bounds.
        /// </summary>
        public Rectangle ScreenBounds =>
            _screenBounds;

        /// <summary>
        /// Gets the world bounds.
        /// </summary>
        public Rectangle WorldBounds =>
            Rectangle.Empty;

        /// <inheritdoc/>
        public SceneLayer? SceneLayer =>
            null;

        /// <inheritdoc/>
        public View? View { get; }

        /// <summary>
        /// Gets the coordinate space used by the position API.
        /// </summary>
        public MovementSpace PositionSpace =>
            MovementSpace.Pixel;

        internal int AppliedZOrder { get; private set; }

        internal float AppliedOpacity { get; private set; } =
            1f;

        internal float FadeTargetOpacity { get; private set; } =
            1f;

        internal float FadeDurationSec { get; private set; }

        /// <summary>
        /// Gets the sprite position in scene-layer grid coordinates.
        /// </summary>
        /// <returns>The sprite position in scene-layer grid coordinates.</returns>
        public Vector2 GetPosition() =>
            _position;

        /// <summary>
        /// Sets the sprite position in scene-layer grid coordinates and invalidates its old and new bounds.
        /// </summary>
        /// <param name="position">The position value for this test case.</param>
        public void SetPosition(Vector2 position)
        {
            _position = position;

            _screenBounds = new Rectangle(
                (int)MathF.Round(position.X),
                (int)MathF.Round(position.Y),
                _screenBounds.Width,
                _screenBounds.Height);
        }

        /// <inheritdoc/>
        public void SetIsVisible(bool visible)
        {
            Visible = visible;
        }

        /// <inheritdoc/>
        public void SetZOrder(int zOrder)
        {
            AppliedZOrder = zOrder;
        }

        /// <inheritdoc/>
        public void SetOpacity(float opacity)
        {
            AppliedOpacity = opacity;
        }

        /// <inheritdoc/>
        public void FadeTo(
            float targetOpacity,
            float durationSec)
        {
            FadeTargetOpacity =
                targetOpacity;

            FadeDurationSec =
                durationSec;
        }

        /// <summary>
        /// Transforms the drawable bounds into screen pixels for the specified view.
        /// </summary>
        /// <param name="view">The view value for this test case.</param>
        /// <returns>The bounds in screen pixels for the supplied view.</returns>
        public RectangleF GetDrawLocationScreen(
            View view)
        {
            return ReferenceEquals(
                    View,
                    view)
                ? _screenBounds
                : RectangleF.Empty;
        }

        /// <summary>
        /// Draws the current content into the backbuffer at the supplied screen-pixel bounds.
        /// </summary>
        /// <param name="backbuffer">The backbuffer value for this test case.</param>
        /// <param name="destRectScreen">The dest rect screen value for this test case.</param>
        public void Draw(
            BackbufferBase backbuffer,
            RectangleF destRectScreen)
        {
        }

        /// <summary>
        /// Updates the test adapter state.
        /// </summary>
        /// <param name="tick">The tick value for this test case.</param>
        public void Update(long tick)
        {
        }

        /// <summary>
        /// Releases the resources owned by this instance.
        /// </summary>
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
