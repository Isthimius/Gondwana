using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Timers;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Drawing.Direct.ImageLayer;

/// <summary>
/// Represents a lightweight image-instance layer that manages and renders a collection
/// of independently moving bitmap instances.
/// </summary>
/// <remarks>
/// <para>
/// This class is intended for effects composed of relatively few, larger visual elements,
/// such as drifting clouds, fog patches, leaves, embers, or other ambient visuals.
/// </para>
/// <para>
/// Unlike particle-style systems that are optimized for very large numbers of tiny,
/// short-lived particles, this layer is designed for persistent, individually managed
/// image instances.
/// </para>
/// <para>
/// Each instance tracks its own position, velocity, rotation, and tint. The layer updates
/// instance state over time and marks the affected regions as dirty for redraw.
/// </para>
/// <para>
/// This class may be used in two styles:
/// <list type="bullet">
/// <item><description>
/// Manual mode: callers add and manage <see cref="Instances"/> directly and optionally
/// provide recycle logic.
/// </description></item>
/// <item><description>
/// Delegate-assisted mode: callers provide optional hooks for initial population,
/// per-instance update customization, and recycle behavior.
/// </description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <para>
/// <b>Manual usage:</b>
/// </para>
/// <code>
/// var layer = new ImageInstanceLayer(renderHost, view, bounds);
///
/// layer.Instances.Add(new ImageInstance
/// {
///     Bitmap = bmp,
///     Bounds = new RectangleF(100, 50, 200, 120),
///     VelocityX = -10f,
///     Tint = new SKColor(255, 255, 255, 140)
/// });
///
/// layer.ShouldRecycle = (instance, area) => instance.Bounds.Right &lt; area.Left;
///
/// layer.RecycleInstance = (old, area, rng) =&gt; new ImageInstance
/// {
///     Bitmap = old.Bitmap,
///     Bounds = new RectangleF(area.Right + 40, old.Bounds.Y, old.Bounds.Width, old.Bounds.Height),
///     VelocityX = old.VelocityX,
///     Tint = old.Tint
/// };
/// </code>
///
/// <para>
/// <b>Delegate-assisted usage:</b>
/// </para>
/// <code>
/// var layer = new ImageInstanceLayer(
///     renderHost,
///     view,
///     bounds,
///     initializer: (area, rng) =&gt;
///     {
///         var list = new List&lt;ImageInstance&gt;();
///
///         for (int i = 0; i &lt; 8; i++)
///         {
///             list.Add(new ImageInstance
///             {
///                 Bitmap = variants[rng.Next(variants.Length)],
///                 Bounds = new RectangleF(
///                     area.Left + (float)rng.NextDouble() * area.Width,
///                     area.Top + (float)rng.NextDouble() * area.Height,
///                     180f,
///                     90f),
///                 VelocityX = -12f,
///                 Tint = new SKColor(255, 255, 255, (byte)rng.Next(70, 141))
///             });
///         }
///
///         return list;
///     },
///     shouldRecycle: (instance, area) =&gt; instance.Bounds.Right &lt; area.Left,
///     recycleInstance: (old, area, rng) =&gt;
///     {
///         old.Bounds = new RectangleF(area.Right + 40, old.Bounds.Y, old.Bounds.Width, old.Bounds.Height);
///         return old;
///     });
/// </code>
/// </example>
public sealed class ImageInstanceLayer : DirectDrawingMovableBase
{
    private readonly Random _rng = new();
    private readonly SKPaint _paint = new()
    {
        IsAntialias = true,
        BlendMode = SKBlendMode.SrcOver
    };

    private long _instanceLastTick;

    /// <summary>
    /// Gets the collection of image instances managed by this layer.
    /// </summary>
    public List<ImageInstance> Instances { get; } = new();

    /// <summary>
    /// Gets or sets an optional callback used to populate the initial set of instances.
    /// </summary>
    /// <remarks>
    /// If assigned before calling <see cref="InitializeInstances"/>, the callback will be invoked
    /// to create the starting set of image instances.
    /// </remarks>
    /// <example>
    /// <para>
    /// The following demonstrates how to populate the layer with an initial set of instances:
    /// </para>
    /// <code>
    /// layer.Initializer = (bounds, rng) =&gt;
    /// {
    ///     var instances = new List&lt;ImageInstance&gt;();
    ///
    ///     for (int i = 0; i &lt; 8; i++)
    ///     {
    ///         instances.Add(new ImageInstance
    ///         {
    ///             Bitmap = variants[rng.Next(variants.Length)],
    ///             Bounds = new RectangleF(
    ///                 bounds.Left + (float)rng.NextDouble() * bounds.Width,
    ///                 bounds.Top + (float)rng.NextDouble() * bounds.Height,
    ///                 180f,
    ///                 90f),
    ///             VelocityX = -12f,
    ///             Tint = new SKColor(255, 255, 255, (byte)rng.Next(70, 141))
    ///         });
    ///     }
    ///
    ///     return instances;
    /// };
    ///
    /// layer.InitializeInstances();
    /// </code>
    /// </example>
    public Func<Rectangle, Random, IEnumerable<ImageInstance>>? Initializer { get; set; }

    /// <summary>
    /// Gets or sets an optional callback used to determine whether an instance should be recycled.
    /// </summary>
    /// <example>
    /// <code>
    /// layer.ShouldRecycle = (instance, bounds) =&gt;
    ///     instance.Bounds.Right &lt; bounds.Left;
    /// </code>
    /// </example>
    public Func<ImageInstance, Rectangle, bool>? ShouldRecycle { get; set; }

    /// <summary>
    /// Gets or sets an optional callback used to create a replacement instance when recycling occurs.
    /// </summary>
    /// <example>
    /// <para>
    /// The following demonstrates how to recycle instances that move off-screen by repositioning
    /// them on the opposite side:
    /// </para>
    /// <code>
    /// layer.ShouldRecycle = (instance, bounds) =&gt;
    ///     instance.Bounds.Right &lt; bounds.Left;
    ///
    /// layer.RecycleInstance = (old, bounds, rng) =&gt;
    /// {
    ///     return new ImageInstance
    ///     {
    ///         Bitmap = old.Bitmap,
    ///         Bounds = new RectangleF(
    ///             bounds.Right + (float)rng.NextDouble() * 100f,
    ///             old.Bounds.Y,
    ///             old.Bounds.Width,
    ///             old.Bounds.Height),
    ///         VelocityX = old.VelocityX,
    ///         Tint = old.Tint
    ///     };
    /// };
    /// </code>
    /// </example>
    public Func<ImageInstance, Rectangle, Random, ImageInstance>? RecycleInstance { get; set; }

    /// <summary>
    /// Gets or sets an optional callback invoked after the built-in motion update for each instance.
    /// </summary>
    /// <example>
    /// <para>
    /// The following demonstrates how to apply a custom per-instance update, such as a gentle vertical
    /// oscillation:
    /// </para>
    /// <code>
    /// layer.UpdateInstance = (instance, dt) =&gt;
    /// {
    ///     float amplitude = 10f;
    ///     float speed = 2f;
    ///
    ///     instance.Bounds = new RectangleF(
    ///         instance.Bounds.X,
    ///         instance.Bounds.Y + MathF.Sin(instance.Bounds.X * 0.01f * speed) * amplitude * dt,
    ///         instance.Bounds.Width,
    ///         instance.Bounds.Height);
    /// };
    /// </code>
    /// </example>
    public Action<ImageInstance, float>? UpdateInstance { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageInstanceLayer"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host managing this layer.</param>
    /// <param name="view">The view to render into.</param>
    /// <param name="screenBounds">The screen-space bounds for this layer.</param>
    /// <param name="nickname">Optional friendly name for debugging.</param>
    public ImageInstanceLayer(RenderSurfaceHostBase renderSurfaceHost,
                              View view,
                              Rectangle screenBounds,
                              string? nickname = null)
        : base(renderSurfaceHost,
               DirectDrawingMode.View,
               sceneLayer: null,
               view: view,
               screenBounds: screenBounds,
               worldBounds: null,
               name: nickname)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageInstanceLayer"/> class for scene-layer rendering.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host managing this layer.</param>
    /// <param name="sceneLayer">The scene layer to render into.</param>
    /// <param name="worldBounds">The world-space bounds for this layer.</param>
    /// <param name="nickname">Optional friendly name for debugging.</param>
    public ImageInstanceLayer(RenderSurfaceHostBase renderSurfaceHost,
                              SceneLayer sceneLayer,
                              Rectangle worldBounds,
                              string? nickname = null)
        : base(renderSurfaceHost,
               DirectDrawingMode.SceneLayer,
               sceneLayer: sceneLayer,
               view: null,
               screenBounds: null,
               worldBounds: worldBounds,
               name: nickname)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageInstanceLayer"/> class and assigns optional hooks.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host managing this layer.</param>
    /// <param name="view">The view to render into.</param>
    /// <param name="screenBounds">The screen-space bounds for this layer.</param>
    /// <param name="initializer">Optional callback used to create the initial set of instances.</param>
    /// <param name="shouldRecycle">Optional callback used to determine whether an instance should be recycled.</param>
    /// <param name="recycleInstance">Optional callback used to create a replacement instance when recycling occurs.</param>
    /// <param name="updateInstance">Optional callback invoked after the built-in motion update for each instance.</param>
    /// <param name="nickname">Optional friendly name for debugging.</param>
    public ImageInstanceLayer(RenderSurfaceHostBase renderSurfaceHost,
                              View view,
                              Rectangle screenBounds,
                              Func<Rectangle, Random, IEnumerable<ImageInstance>>? initializer,
                              Func<ImageInstance, Rectangle, bool>? shouldRecycle = null,
                              Func<ImageInstance, Rectangle, Random, ImageInstance>? recycleInstance = null,
                              Action<ImageInstance, float>? updateInstance = null,
                              string? nickname = null)
        : this(renderSurfaceHost, view, screenBounds, nickname)
    {
        Initializer = initializer;
        ShouldRecycle = shouldRecycle;
        RecycleInstance = recycleInstance;
        UpdateInstance = updateInstance;

        InitializeInstances();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageInstanceLayer"/> class for scene-layer rendering
    /// and assigns optional hooks.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host managing this layer.</param>
    /// <param name="sceneLayer">The scene layer to render into.</param>
    /// <param name="worldBounds">The world-space bounds for this layer.</param>
    /// <param name="initializer">Optional callback used to create the initial set of instances.</param>
    /// <param name="shouldRecycle">Optional callback used to determine whether an instance should be recycled.</param>
    /// <param name="recycleInstance">Optional callback used to create a replacement instance when recycling occurs.</param>
    /// <param name="updateInstance">Optional callback invoked after the built-in motion update for each instance.</param>
    /// <param name="nickname">Optional friendly name for debugging.</param>
    public ImageInstanceLayer(RenderSurfaceHostBase renderSurfaceHost,
                              SceneLayer sceneLayer,
                              Rectangle worldBounds,
                              Func<Rectangle, Random, IEnumerable<ImageInstance>>? initializer,
                              Func<ImageInstance, Rectangle, bool>? shouldRecycle = null,
                              Func<ImageInstance, Rectangle, Random, ImageInstance>? recycleInstance = null,
                              Action<ImageInstance, float>? updateInstance = null,
                              string? nickname = null)
        : this(renderSurfaceHost, sceneLayer, worldBounds, nickname)
    {
        Initializer = initializer;
        ShouldRecycle = shouldRecycle;
        RecycleInstance = recycleInstance;
        UpdateInstance = updateInstance;

        InitializeInstances();
    }

    /// <summary>
    /// Clears the current instance collection and rebuilds it using <see cref="Initializer"/>, if one is assigned.
    /// </summary>
    public void InitializeInstances()
    {
        Instances.Clear();

        var bounds = Mode == DirectDrawingMode.SceneLayer
            ? WorldBounds
            : ScreenBounds;

        if (Initializer is not null)
            Instances.AddRange(Initializer(bounds, _rng));

        ForceRefresh();
    }

    /// <summary>
    /// Updates all instances, applying built-in motion, optional custom updates,
    /// and optional recycling behavior.
    /// </summary>
    /// <param name="tick">Current tick from <see cref="HighResTimer"/>.</param>
    public override void Update(long tick)
    {
        if (tick <= _lastTick)
            return;

        float dt = 0f;

        if (_instanceLastTick > 0)
            dt = HighResTimer.GetDuration(_instanceLastTick, tick);

        _instanceLastTick = tick;

        if (dt <= 0f)
        {
            base.Update(tick);
            return;
        }

        var bounds = Mode == DirectDrawingMode.SceneLayer
            ? WorldBounds
            : ScreenBounds;

        for (int i = 0; i < Instances.Count; i++)
        {
            var instance = Instances[i];
            var oldRefreshBounds = GetRefreshBounds(instance);

            instance.Bounds = new RectangleF(
                instance.Bounds.X + (instance.VelocityX * dt),
                instance.Bounds.Y + (instance.VelocityY * dt),
                instance.Bounds.Width,
                instance.Bounds.Height);

            instance.Rotation += instance.AngularVelocity * dt;

            UpdateInstance?.Invoke(instance, dt);

            if (ShouldRecycle is not null &&
                RecycleInstance is not null &&
                ShouldRecycle(instance, bounds))
            {
                var recycled = RecycleInstance(instance, bounds, _rng);
                Instances[i] = recycled;

                AddRefreshRect(oldRefreshBounds);
                AddRefreshRect(GetRefreshBounds(recycled));
            }
            else
            {
                AddRefreshRect(oldRefreshBounds);
                AddRefreshRect(GetRefreshBounds(instance));
            }
        }

        base.Update(tick);
    }

    /// <summary>
    /// Renders all image instances to the backbuffer.
    /// </summary>
    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        var canvas = backbuffer.Canvas;

        Rectangle srcBounds = Mode == DirectDrawingMode.SceneLayer
            ? WorldBounds
            : ScreenBounds;

        float sx = srcBounds.Width > 0
            ? destRectScreen.Width / srcBounds.Width
            : 1f;

        float sy = srcBounds.Height > 0
            ? destRectScreen.Height / srcBounds.Height
            : 1f;

        canvas.Save();
        canvas.ClipRect(destRectScreen.ToSKRect());

        foreach (var instance in Instances)
        {
            _paint.Color = instance.Tint;

            var dst = new SKRect(
                destRectScreen.Left + ((instance.Bounds.Left - srcBounds.Left) * sx),
                destRectScreen.Top + ((instance.Bounds.Top - srcBounds.Top) * sy),
                destRectScreen.Left + ((instance.Bounds.Right - srcBounds.Left) * sx),
                destRectScreen.Top + ((instance.Bounds.Bottom - srcBounds.Top) * sy));

            if (instance.Rotation != 0f)
            {
                float cx = (dst.Left + dst.Right) * 0.5f;
                float cy = (dst.Top + dst.Bottom) * 0.5f;

                canvas.Save();
                canvas.RotateDegrees(instance.Rotation, cx, cy);
                canvas.DrawBitmap(instance.Bitmap, dst, _paint);
                canvas.Restore();
            }
            else
            {
                canvas.DrawBitmap(instance.Bitmap, dst, _paint);
            }
        }

        canvas.Restore();
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _paint.Dispose();

        base.Dispose(disposing);
    }

    /// <summary>
    /// Adds a dirty rectangle for redraw, with a small safety margin.
    /// </summary>
    /// <param name="rect">The rectangle to mark dirty.</param>
    private void AddRefreshRect(RectangleF rect)
    {
        const float pad = 4f;

        var expanded = Rectangle.Ceiling(RectangleF.Inflate(rect, pad, pad));

        if (Mode == DirectDrawingMode.SceneLayer)
        {
            SceneLayer!.RefreshQueue.AddWorldRect(expanded);
            return;
        }

        foreach (var sceneLayer in RenderSurfaceHost.Scene.SceneLayers)
            sceneLayer.RefreshQueue.AddViewScreenRect(View!, sceneLayer, expanded);
    }

    /// <summary>
    /// Computes a conservative redraw bounds for the specified instance.
    /// </summary>
    /// <param name="instance">The instance to evaluate.</param>
    /// <returns>A rectangle large enough to cover the instance for refresh purposes.</returns>
    private static RectangleF GetRefreshBounds(ImageInstance instance)
    {
        if (instance.Rotation == 0f && instance.AngularVelocity == 0f)
            return instance.Bounds;

        float cx = instance.Bounds.Left + (instance.Bounds.Width * 0.5f);
        float cy = instance.Bounds.Top + (instance.Bounds.Height * 0.5f);

        float halfWidth = instance.Bounds.Width * 0.5f;
        float halfHeight = instance.Bounds.Height * 0.5f;
        float halfDiagonal = MathF.Sqrt((halfWidth * halfWidth) + (halfHeight * halfHeight));

        return new RectangleF(
            cx - halfDiagonal,
            cy - halfDiagonal,
            halfDiagonal * 2f,
            halfDiagonal * 2f);
    }
}