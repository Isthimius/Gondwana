using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Video;
using SkiaSharp;

#if BROWSER || NO_UNSAFE
    using System.Runtime.InteropServices;
#endif

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Draws video frames provided by IVideoPlayer onto the backbuffer.
/// Audio is handled by the IVideoPlayer implementation (e.g., VLC system output on desktop).
/// </summary>
/// <example>
///
/// *** Basic Playback (fixed size in a HUD corner):
/// var clip = new DirectVideo(surface, new Rectangle(64, 64, 640, 360),
///                            playerFactory: () => videoPlayerFactory.Create(),
///                            source: new Uri("file:///C:/media/intro.mp4"))
/// {
///     ZOrder = 100,                     // above tiles/UI as needed
///     Stretch = StretchMode.Uniform,    // preserve aspect, fit inside box
///     Loop = false
/// };
///
/// *** Full-bleed Cutscene (cover viewport, crop overflow):
/// var cutscene = new DirectVideo(surface, viewportBounds,
///                                () => videoPlayerFactory.Create(),
///                                new Uri("file:///C:/media/cutscene.mkv"))
/// {
///     ZOrder = 1000,
///     Stretch = StretchMode.UniformToFill, // cover the whole bounds
///     Opacity = 1.0f,
///     Loop = false
/// };
///
/// *** Faded Picture-in-Picture (overlay, semi-transparent):
/// var pip = new DirectVideo(surface, new Rectangle(20, 20, 320, 180),
///                           () => videoPlayerFactory.Create(),
///                           new Uri("file:///C:/media/camfeed.webm"))
/// {
///     ZOrder = 500,
///     Stretch = StretchMode.Fill,  // ignore AR, fill the box
///     Opacity = 0.75f,
///     Loop = true
/// };
///
/// *** Playback Control (pause/seek/rate) during your update tick:
/// // Pause on a modal
/// if (ui.ShowingModal) pip.Pause();
///
/// // Resume when modal closes
/// if (!ui.ShowingModal && !pipIsPlaying) pip.Play();
///
/// // Jump to 10s (for scrubbing thumbnails or replays)
/// pip.Seek(TimeSpan.FromSeconds(10));
///
/// // Slow-mo or fast-forward
/// pip.PlaybackRate = 0.5; // half-speed
/// // pip.PlaybackRate = 1.25; // 25% faster
///
/// *** Swap Source (re-use the same player instance path):
/// var trailer = new DirectVideo(surface, new Rectangle(80, 80, 960, 540),
///                               myExistingIVideoPlayerInstance,
///                               new Uri("file:///C:/media/trailer.mp4"))
/// {
///     Stretch = StretchMode.Uniform,
///     Opacity = 1.0f
/// };
///
/// *** Animated Slide-in (using your engine's movement/scrolling):
/// // Start off-screen, then slide into position over 0.4s
/// cutscene.ScrollToSourceGridPoint(0.4, new Rectangle(0, 0, viewportBounds.Width, viewportBounds.Height));
///
/// </example>
public sealed class DirectVideo : DirectDrawingBase
{
    private readonly IVideoPlayer _player;
    private readonly object _frameLock = new();

    private SKBitmap? _frame;                 // RGBA target
    private int _srcW, _srcH, _srcStride;     // last frame metadata

    // ---- knobs ----
    
    /// <summary>
    /// Gets or sets the stretch mode that determines how the video frame is scaled 
    /// to fit within the destination bounds.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><see cref="StretchMode.None"/> - Video displays at its native resolution.</description></item>
    /// <item><description><see cref="StretchMode.Fill"/> - Video fills the entire bounds, ignoring aspect ratio.</description></item>
    /// <item><description><see cref="StretchMode.Uniform"/> - Video scales to fit inside bounds while preserving aspect ratio (letterboxing/pillarboxing as needed).</description></item>
    /// <item><description><see cref="StretchMode.UniformToFill"/> - Video scales to fill the entire bounds while preserving aspect ratio (cropping as needed).</description></item>
    /// </list>
    /// </remarks>
    /// <value>The default value is <see cref="StretchMode.Fill"/>.</value>
    public StretchMode Stretch { get; set; } = StretchMode.Fill;

    private float _opacity = 1f;              // 0..1

    /// <summary>
    /// Gets or sets the opacity level of the video overlay.
    /// </summary>
    /// <remarks>
    /// This value controls the alpha transparency applied to the entire video frame during rendering.
    /// Values are automatically clamped to the valid range.
    /// </remarks>
    /// <value>
    /// A floating-point value between 0.0 (fully transparent) and 1.0 (fully opaque).
    /// The default value is 1.0.
    /// </value>
    public float Opacity
    {
        get => _opacity;
        set => _opacity = Math.Clamp(value, 0f, 1f);
    }

    private double _playbackRate = 1.0;

    /// <summary>
    /// Gets or sets the playback speed multiplier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property controls the rate at which the video plays relative to its normal speed.
    /// Setting this value updates the underlying <see cref="IVideoPlayer"/> immediately.
    /// </para>
    /// <para>
    /// Common values:
    /// <list type="bullet">
    /// <item><description>0.5 - Half speed (slow motion)</description></item>
    /// <item><description>1.0 - Normal speed</description></item>
    /// <item><description>1.5 - 50% faster</description></item>
    /// <item><description>2.0 - Double speed</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <value>A multiplier for playback speed. The default value is 1.0 (normal speed).</value>
    public double PlaybackRate
    {
        get => _playbackRate;
        set
        {
            _playbackRate = value;
            _player.SetRate(value);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the video should automatically restart 
    /// from the beginning when it reaches the end.
    /// </summary>
    /// <remarks>
    /// When set to <c>true</c>, the video will continuously play in a loop. 
    /// When set to <c>false</c>, playback stops when the video reaches its end.
    /// </remarks>
    /// <value><c>true</c> to enable looping; otherwise, <c>false</c>.</value>
    public bool Loop
    {
        get => _player.Loop;
        set => _player.Loop = value;
    }

    // --- ctors ---

    /// <summary>
    /// Use this when you already have an IVideoPlayer instance (e.g., resolved via DI).
    /// </summary>
    private DirectVideo(IVideoPlayer player,
                       Uri source,
                       RenderSurfaceHostBase renderSurfaceHost,
                       DirectDrawingMode mode,
                       SceneLayer? sceneLayer,
                       View? view,
                       Rectangle? screenBounds,
                       Rectangle? worldBounds,
                       string? name = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, name)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        HookPlayer();
        _player.Open(source);
        _player.Play();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectVideo"/> class that renders 
    /// to a specific <see cref="SceneLayer"/> using world coordinates.
    /// </summary>
    /// <param name="player">The video player instance responsible for decoding and providing frames.</param>
    /// <param name="source">The URI of the video source to play.</param>
    /// <param name="renderSurfaceHost">The render surface host that will display the video.</param>
    /// <param name="sceneLayer">The scene layer to which this video will be attached.</param>
    /// <param name="worldBounds">
    /// The rectangular bounds in world coordinates where the video will be rendered. 
    /// If <c>null</c>, the video's natural size will be used.
    /// </param>
    /// <param name="name">An optional name for this video instance, useful for debugging or identification.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The video begins playing automatically after initialization.
    /// </remarks>
    public DirectVideo(IVideoPlayer player,
                       Uri source,
                       RenderSurfaceHostBase renderSurfaceHost,
                       SceneLayer sceneLayer,
                       Rectangle? worldBounds,
                       string? name = null)
        : this(player, source, renderSurfaceHost,
               DirectDrawingMode.SceneLayer,
               sceneLayer, null,
               null, worldBounds, name) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectVideo"/> class that renders 
    /// to a specific <see cref="View"/> using screen coordinates.
    /// </summary>
    /// <param name="player">The video player instance responsible for decoding and providing frames.</param>
    /// <param name="source">The URI of the video source to play.</param>
    /// <param name="renderSurfaceHost">The render surface host that will display the video.</param>
    /// <param name="view">The view to which this video will be attached.</param>
    /// <param name="screenBounds">
    /// The rectangular bounds in screen coordinates where the video will be rendered. 
    /// If <c>null</c>, the video's natural size will be used.
    /// </param>
    /// <param name="name">An optional name for this video instance, useful for debugging or identification.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is <c>null</c>.</exception>
    /// <remarks>
    /// This constructor is useful for HUD overlays, menus, or other UI-aligned video elements.
    /// The video begins playing automatically after initialization.
    /// </remarks>
    public DirectVideo(IVideoPlayer player,
                      Uri source,
                      RenderSurfaceHostBase renderSurfaceHost,
                      View view,
                      Rectangle? screenBounds,
                      string? name = null)
        : this(player, source, renderSurfaceHost,
                DirectDrawingMode.View,
                null, view,
                screenBounds, null, name) { }

    /// <summary>
    /// Use this when you have a factory that abstracts platform differences.
    /// e.g., desktop/mobile -> VLC impl, web -> HTML5/WebCodecs impl.
    /// </summary>
    private DirectVideo(Func<IVideoPlayer> playerFactory,
                       Uri source,
                       RenderSurfaceHostBase renderSurfaceHost,
                       DirectDrawingMode mode,
                       SceneLayer? sceneLayer,
                       View? view,
                       Rectangle? screenBounds,
                       Rectangle? worldBounds,
                       string? name = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, name)
    {
        if (playerFactory is null) throw new ArgumentNullException(nameof(playerFactory));
        _player = playerFactory();
        HookPlayer();
        _player.Open(source);
        _player.Play();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectVideo"/> class using a factory 
    /// to create the player, rendering to a specific <see cref="SceneLayer"/> using world coordinates.
    /// </summary>
    /// <param name="playerFactory">
    /// A factory function that creates and returns an <see cref="IVideoPlayer"/> instance.
    /// This allows for platform-specific player implementations or dependency injection patterns.
    /// </param>
    /// <param name="source">The URI of the video source to play.</param>
    /// <param name="renderSurfaceHost">The render surface host that will display the video.</param>
    /// <param name="sceneLayer">The scene layer to which this video will be attached.</param>
    /// <param name="worldBounds">
    /// The rectangular bounds in world coordinates where the video will be rendered. 
    /// If <c>null</c>, the video's natural size will be used.
    /// </param>
    /// <param name="name">An optional name for this video instance, useful for debugging or identification.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="playerFactory"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The factory pattern is useful when the player implementation varies by platform 
    /// (e.g., VLC on desktop, HTML5 on web). The video begins playing automatically after initialization.
    /// </remarks>
    public DirectVideo(Func<IVideoPlayer> playerFactory,
                       Uri source,
                       RenderSurfaceHostBase renderSurfaceHost,
                       SceneLayer sceneLayer,
                       Rectangle? worldBounds,
                       string? name = null)
        : this(playerFactory, source, renderSurfaceHost,
               DirectDrawingMode.SceneLayer,
               sceneLayer, null,
               null, worldBounds, name) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectVideo"/> class using a factory 
    /// to create the player, rendering to a specific <see cref="View"/> using screen coordinates.
    /// </summary>
    /// <param name="playerFactory">
    /// A factory function that creates and returns an <see cref="IVideoPlayer"/> instance.
    /// This allows for platform-specific player implementations or dependency injection patterns.
    /// </param>
    /// <param name="source">The URI of the video source to play.</param>
    /// <param name="renderSurfaceHost">The render surface host that will display the video.</param>
    /// <param name="view">The view to which this video will be attached.</param>
    /// <param name="screenBounds">
    /// The rectangular bounds in screen coordinates where the video will be rendered. 
    /// If <c>null</c>, the video's natural size will be used.
    /// </param>
    /// <param name="name">An optional name for this video instance, useful for debugging or identification.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="playerFactory"/> is <c>null</c>.</exception>
    /// <remarks>
    /// This constructor is useful for HUD overlays, menus, or other UI-aligned video elements.
    /// The factory pattern allows for platform-specific implementations. 
    /// The video begins playing automatically after initialization.
    /// </remarks>
    public DirectVideo(Func<IVideoPlayer> playerFactory,
                       Uri source,
                       RenderSurfaceHostBase renderSurfaceHost,
                       View view,
                       Rectangle? screenBounds,
                       string? name = null)
        : this(playerFactory, source, renderSurfaceHost,
                DirectDrawingMode.View,
                null, view,
                screenBounds, null, name) { }

    private void HookPlayer()
    {
        _player.FrameReady += OnFrameReady;
        _player.Ended += (_, __) => { /* engine could fire a scene event here if needed */ };
    }

    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        SKBitmap? bmp;
        lock (_frameLock) bmp = _frame;
        if (bmp is null) return;

        var canvas = backbuffer.Canvas;
        var dest = ComputeDestRect(destRectScreen, bmp.Width, bmp.Height, Stretch);

        using var paint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(Opacity * 255)) };
        canvas.DrawBitmap(bmp, dest, paint);
    }

    private static SKRect ComputeDestRect(RectangleF bounds, int srcW, int srcH, StretchMode mode)
    {
        var b = bounds;
        var dst = new SKRect(b.Left, b.Top, b.Right, b.Bottom);
        if (mode == StretchMode.Fill || srcW <= 0 || srcH <= 0) return dst;

        float bw = b.Width, bh = b.Height;
        float arSrc = (float)srcW / srcH;
        float arDst = bw / bh;

        if (mode == StretchMode.None)
            return new SKRect(b.Left, b.Top, b.Left + srcW, b.Top + srcH);

        if (mode == StretchMode.Uniform)
        {
            if (arSrc > arDst)
            {
                float w = bw, h = w / arSrc, y = b.Top + (bh - h) / 2f;
                return new SKRect(b.Left, y, b.Left + w, y + h);
            }
            else
            {
                float h = bh, w = h * arSrc, x = b.Left + (bw - w) / 2f;
                return new SKRect(x, b.Top, x + w, b.Top + h);
            }
        }

        // UniformToFill
        if (arSrc < arDst)
        {
            float w = bw, h = w / arSrc, y = b.Top + (bh - h) / 2f;
            return new SKRect(b.Left, y, b.Left + w, y + h);
        }
        else
        {
            float h = bh, w = h * arSrc, x = b.Left + (bw - w) / 2f;
            return new SKRect(x, b.Top, x + w, b.Top + h);
        }
    }

    // --- frame ingestion ---

    private void OnFrameReady(object? sender, VideoFrameReadyEventArgs e)
    {
        // Copy the RGBA buffer into a reusable SKBitmap.
        // NOTE: do not hold the lock while touching Skia; just protect shared refs/copies.
        SKBitmap? local;
        lock (_frameLock)
        {
            if (_frame is null || _srcW != e.Width || _srcH != e.Height)
            {
                _frame?.Dispose();
                _frame = new SKBitmap(new SKImageInfo(e.Width, e.Height, SKColorType.Rgba8888));
                _srcW = e.Width;
                _srcH = e.Height;
                _srcStride = e.Stride;
            }
            local = _frame;
        }

        if (local is null) return;

        CopyFrameToBitmap(e.Pixels, e.Stride, local, _srcW, _srcH);

        // Mark dirty so the direct-draw manager repaints our area.
        ForceRefresh();
    }

    private static void CopyFrameToBitmap(IntPtr srcPixels, int srcStride, SKBitmap dst, int width, int height)
    {
#if !BROWSER && !NO_UNSAFE
        // Fast path: single memcpy using pointers
        unsafe
        {
            using var pix = dst.PeekPixels();
            var dstPtr = (byte*)pix.GetPixels().ToPointer();
            Buffer.MemoryCopy(
                source: (void*)srcPixels,
                destination: dstPtr,
                destinationSizeInBytes: pix.RowBytes * height,
                sourceBytesToCopy: (long)srcStride * height);
        }
#else
        // Safe path: row-by-row Marshal.Copy (no unsafe)
        using var pix = dst.PeekPixels();
        IntPtr dstBase = pix.GetPixels();
        int dstRB = (int)pix.RowBytes;
        int srcRB = srcStride;
        int rowBytes = Math.Min(dstRB, srcRB);

        var rowBuf = System.Buffers.ArrayPool<byte>.Shared.Rent(rowBytes);
        try
        {
            for (int y = 0; y < height; y++)
            {
                var srcRow = IntPtr.Add(srcPixels, y * srcRB);
                var dstRow = IntPtr.Add(dstBase, y * dstRB);
                Marshal.Copy(srcRow, rowBuf, 0, rowBytes);
                Marshal.Copy(rowBuf, 0, dstRow, rowBytes);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rowBuf, clearArray: false);
        }
#endif
    }

    // --- control surface (thin passthrough to player) ---

    /// <summary>
    /// Starts or resumes video playback.
    /// </summary>
    /// <remarks>
    /// If the video is paused, this method resumes playback from the current position.
    /// If the video has not yet started, this method begins playback from the beginning.
    /// This method is called automatically during initialization.
    /// </remarks>
    public void Play() => _player.Play();

    /// <summary>
    /// Pauses video playback at the current position.
    /// </summary>
    /// <remarks>
    /// While paused, the current frame remains visible on screen. 
    /// Call <see cref="Play"/> to resume playback from the paused position.
    /// </remarks>
    public void Pause() => _player.Pause();

    /// <summary>
    /// Stops video playback and resets the playback position to the beginning.
    /// </summary>
    /// <remarks>
    /// After calling this method, the video will return to its first frame.
    /// Call <see cref="Play"/> to start playback again from the beginning.
    /// </remarks>
    public void Stop() => _player.Stop();

    /// <summary>
    /// Seeks to a specific position in the video timeline.
    /// </summary>
    /// <param name="position">
    /// The target position to seek to, specified as a <see cref="TimeSpan"/> 
    /// from the beginning of the video.
    /// </param>
    /// <remarks>
    /// <para>
    /// Seeking may not be frame-accurate depending on the video codec and player implementation.
    /// Most players will seek to the nearest keyframe before or at the specified position.
    /// </para>
    /// <para>
    /// This method can be called while the video is playing or paused.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Seek to 30 seconds into the video
    /// video.Seek(TimeSpan.FromSeconds(30));
    /// 
    /// // Seek to 2 minutes and 15 seconds
    /// video.Seek(new TimeSpan(0, 2, 15));
    /// </code>
    /// </example>
    public void Seek(TimeSpan position) => _player.Seek(position);

    // --- cleanup ---

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        _player.FrameReady -= OnFrameReady;
        _player.Dispose();

        lock (_frameLock)
        {
            _frame?.Dispose();
            _frame = null;
        }
    }
}