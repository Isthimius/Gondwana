using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Draws a bitmap/image into a rectangular region with optional scaling mode,
/// source sub-rect, tint/opacity, blend mode, and rotation about an anchor.
/// </summary>
/// <remarks>
/// The image object is not owned/disposed by this drawable; manage its lifetime externally.
/// Setters mark the drawable dirty so the manager re-renders next frame.
/// </remarks>
public sealed class DirectImage : DirectDrawingMovableBase
{
    public enum ScaleMode
    {
        Stretch,     // fill destination rectangle, ignore aspect
        Fit,         // letterbox inside bounds, preserve aspect
        Fill,        // cover bounds, preserve aspect (may crop)
        Center,      // no scaling, centered
        PixelPerfect // scale by integer factors only (min integer that fits)
    }

    private SKImage? _image;        // primary storage
    private SKBitmap? _bitmap;      // optional if provided instead of SKImage
    private SKRect? _src;           // source sprite region (in image pixels)
    private ScaleMode _scale = ScaleMode.Stretch;

    private readonly SKPaint _paint = new()
    {
        IsAntialias = true,
        FilterQuality = SKFilterQuality.Medium,
        BlendMode = SKBlendMode.SrcOver,
        Color = SKColors.White // used for tint/alpha via color filter
    };

    // visual modifiers
    private SKColor? _tint;         // multiplied over image
    private byte _opacity = 255;    // 0..255

    // rotation
    private float _rotationDeg = 0f;

    // normalized anchor (0..1) where 0,0 = top-left of dest rect
    private float _anchorX = 0.5f, _anchorY = 0.5f;

    private DirectImage(SKBitmap bitmap,
                       RenderSurfaceHostBase renderSurfaceHost,
                       DirectDrawingMode mode,
                       SceneLayer? sceneLayer,
                       View? view,
                       Rectangle? screenBounds,
                       Rectangle? worldBounds,
                       string? name = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, name)
    {
        SetBitmap(bitmap);
    }

    private DirectImage(SKImage image,
                        RenderSurfaceHostBase renderSurfaceHost,
                        DirectDrawingMode mode,
                        SceneLayer? sceneLayer,
                        View? view,
                        Rectangle? screenBounds,
                        Rectangle? worldBounds,
                        string? nickname = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, nickname)
    {
        SetImage(image);
    }

    public DirectImage (SKBitmap bitmap,
                        RenderSurfaceHostBase renderSurfaceHost,
                        SceneLayer sceneLayer,
                        Rectangle worldBounds,
                        string? nickname = null) : this(bitmap, renderSurfaceHost, DirectDrawingMode.SceneLayer, sceneLayer, null, null, worldBounds, nickname) { }

    public DirectImage (SKBitmap bitmap,
                        RenderSurfaceHostBase renderSurfaceHost,
                        View view,
                        Rectangle screenBounds,
                        string? nickname = null) : this(bitmap, renderSurfaceHost, DirectDrawingMode.View, null, view, screenBounds, null, nickname) { }

    public DirectImage(SKImage image,
                       RenderSurfaceHostBase renderSurfaceHost,
                       SceneLayer sceneLayer,
                       Rectangle worldBounds,
                       string? nickname = null) : this(image, renderSurfaceHost, DirectDrawingMode.SceneLayer, sceneLayer, null, null, worldBounds, nickname) { }

    public DirectImage(SKImage image,
                        RenderSurfaceHostBase renderSurfaceHost,
                        View view,
                        Rectangle screenBounds,
                        string? nickname = null) : this(image, renderSurfaceHost, DirectDrawingMode.View, null, view, screenBounds, null, nickname) { }

    /// <summary>Replace the backing bitmap (converts to SKImage on draw if needed).</summary>
    public DirectImage SetBitmap(SKBitmap bitmap)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        _image = null;
        ForceRefresh();
        return this;
    }

    /// <summary>Replace the backing image.</summary>
    public DirectImage SetImage(SKImage image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _bitmap = null;
        ForceRefresh();
        return this;
    }

    /// <summary>Optional source rectangle (in image pixels). Pass null to draw the whole image.</summary>
    public DirectImage SetSourceRect(SKRect? srcPixels)
    {
        _src = srcPixels;
        ForceRefresh();
        return this;
    }

    /// <summary>Choose how the image fits the destination bounds.</summary>
    public DirectImage SetScaleMode(ScaleMode mode)
    {
        _scale = mode;
        ForceRefresh();
        return this;
    }

    /// <summary>Set a color tint (multiplicative). Use <see cref="ClearTint"/> to remove.</summary>
    public DirectImage SetTint(SKColor color)
    {
        _tint = color;
        ForceRefresh();
        return this;
    }

    /// <summary>Remove any color tint.</summary>
    public DirectImage ClearTint()
    {
        _tint = null;
        ForceRefresh();
        return this;
    }

    /// <summary>Set overall image opacity (0–255).</summary>
    public DirectImage SetOpacity(byte alpha)
    {
        _opacity = alpha;
        ForceRefresh();
        return this;
    }

    /// <summary>Set the Skia blend mode (e.g., SrcOver, Screen, Plus).</summary>
    public DirectImage SetBlendMode(SKBlendMode mode)
    {
        _paint.BlendMode = mode;
        ForceRefresh();
        return this;
    }

    /// <summary>Set filter quality for scaling.</summary>
    public DirectImage SetFilterQuality(SKFilterQuality q)
    {
        _paint.FilterQuality = q;
        ForceRefresh();
        return this;
    }

    /// <summary>Rotate in degrees around the anchor.</summary>
    public DirectImage SetRotation(float degrees)
    {
        _rotationDeg = degrees;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Set the rotation/scaling anchor inside the destination rect (normalized 0..1).
    /// (0,0)=top-left, (0.5,0.5)=center, (1,1)=bottom-right.
    /// </summary>
    public DirectImage SetAnchor(float ax, float ay)
    {
        _anchorX = Math.Clamp(ax, 0f, 1f);
        _anchorY = Math.Clamp(ay, 0f, 1f);
        ForceRefresh();
        return this;
    }

    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        var canvas = backbuffer.Canvas;

        // get SKImage to draw
        var img = _image ?? (_bitmap != null ? SKImage.FromBitmap(_bitmap) : null);
        if (img is null)
            return;

        // source rect (pixels in image space)
        SKRect src = _src ?? new SKRect(0, 0, img.Width, img.Height);

        // destination rect (screen space), computed by scale mode
        SKRect dst = ComputeDestRect(destRectScreen.ToPixelAlignedRect(), src, _scale);

        // apply tint/opacity via color filter
        // combine tint with opacity (multiply in linear-ish sRGB)
        var tint = _tint ?? SKColors.White;
        var withAlpha = new SKColor(tint.Red, tint.Green, tint.Blue, _opacity);
        using var cf = SKColorFilter.CreateBlendMode(withAlpha, SKBlendMode.Modulate);
        _paint.ColorFilter = cf;

        // rotation about anchor inside dst
        if (_rotationDeg != 0f)
        {
            float ax = dst.Left + _anchorX * dst.Width;
            float ay = dst.Top + _anchorY * dst.Height;
            canvas.Save();
            canvas.RotateDegrees(_rotationDeg, ax, ay);
        }

        // draw
        canvas.DrawImage(img, src, dst, _paint);

        if (_rotationDeg != 0f)
            canvas.Restore();

        // if we created a transient SKImage from a bitmap, dispose it now
        if (_image is null && img is not null)
            img.Dispose();
    }

    private static SKRect ComputeDestRect(Rectangle bounds, SKRect src, ScaleMode mode)
    {
        var dst = bounds.ToSKRect();

        if (mode == ScaleMode.Stretch)
            return dst;

        if (mode == ScaleMode.Center)  // center, no scale
        {
            float w = MathF.Min(dst.Width, src.Width);
            float h = MathF.Min(dst.Height, src.Height);
            float dx = dst.MidX - w * 0.5f, dy = dst.MidY - h * 0.5f;

            return new SKRect(dx, dy, dx + w, dy + h);
        }

        float srcW = src.Width, srcH = src.Height;
        float dstW = dst.Width, dstH = dst.Height;

        float scaleX = dstW / srcW;
        float scaleY = dstH / srcH;

        float scale = mode switch
        {
            ScaleMode.Fit => MathF.Min(scaleX, scaleY),
            ScaleMode.Fill => MathF.Max(scaleX, scaleY),
            ScaleMode.PixelPerfect => MathF.Max(1f, MathF.Floor(MathF.Min(scaleX, scaleY))), _ => 1f
        };

        float w2 = srcW * scale, h2 = srcH * scale;
        float x = dst.MidX - w2 * 0.5f;
        float y = dst.MidY - h2 * 0.5f;

        return new SKRect(x, y, x + w2, y + h2);
    }
}
