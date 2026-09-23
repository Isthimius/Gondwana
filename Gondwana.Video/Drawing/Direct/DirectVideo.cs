using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Video;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Draws video frames provided by IVideoPlayer onto the backbuffer.
/// Audio is handled by the IVideoPlayer implementation (e.g., VLC system output on desktop).
/// </summary>
public sealed class DirectVideo : DirectDrawingBase
{
    private readonly IVideoPlayer _player;
    private readonly VideoFrameMailbox _mailbox = new();
    private SKBitmap? _frame;
    private bool _videoDisposed;

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
    private StretchMode _stretch = StretchMode.Fill;
    public StretchMode Stretch
    {
        get => _stretch;
        set { if (_stretch != value) { _stretch = value; ForceRefresh(); } }
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
            _player.SetRate(value);
            _playbackRate = value;
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

    /// <summary>Creates a world-space video with required presentation bounds. Owns the player.</summary>
    public DirectVideo(IVideoPlayer player, Uri source, RenderSurfaceHostBase renderSurfaceHost,
        SceneLayer sceneLayer, Rectangle worldBounds, string? name = null)
        : this(player, VideoSource.FromUri(source), renderSurfaceHost, sceneLayer, worldBounds, name) { }

    /// <summary>Creates a screen-space video with required presentation bounds. Owns the player.</summary>
    public DirectVideo(IVideoPlayer player, Uri source, RenderSurfaceHostBase renderSurfaceHost,
        View view, Rectangle screenBounds, string? name = null)
        : this(player, VideoSource.FromUri(source), renderSurfaceHost, view, screenBounds, name) { }

    /// <summary>Creates a world-space video using an owned player from the factory.</summary>
    public DirectVideo(Func<IVideoPlayer> playerFactory, Uri source, RenderSurfaceHostBase renderSurfaceHost,
        SceneLayer sceneLayer, Rectangle worldBounds, string? name = null)
        : this(playerFactory, VideoSource.FromUri(source), renderSurfaceHost, sceneLayer, worldBounds, name) { }

    /// <summary>Creates a screen-space video using an owned player from the factory.</summary>
    public DirectVideo(Func<IVideoPlayer> playerFactory, Uri source, RenderSurfaceHostBase renderSurfaceHost,
        View view, Rectangle screenBounds, string? name = null)
        : this(playerFactory, VideoSource.FromUri(source), renderSurfaceHost, view, screenBounds, name) { }

    /// <summary>Creates a world-space URI/stream/GAF video and starts playback. Owns the player.</summary>
    public DirectVideo(IVideoPlayer player, VideoSource source, RenderSurfaceHostBase renderSurfaceHost,
        SceneLayer sceneLayer, Rectangle worldBounds, string? name = null)
        : this(() => player, source, renderSurfaceHost, DirectDrawingMode.SceneLayer, sceneLayer, null, null, worldBounds, name) { }

    /// <summary>Creates a screen-space URI/stream/GAF video and starts playback. Owns the player.</summary>
    public DirectVideo(IVideoPlayer player, VideoSource source, RenderSurfaceHostBase renderSurfaceHost,
        View view, Rectangle screenBounds, string? name = null)
        : this(() => player, source, renderSurfaceHost, DirectDrawingMode.View, null, view, screenBounds, null, name) { }

    /// <summary>Creates a world-space URI/stream/GAF video using an owned player from the factory.</summary>
    public DirectVideo(Func<IVideoPlayer> playerFactory, VideoSource source, RenderSurfaceHostBase renderSurfaceHost,
        SceneLayer sceneLayer, Rectangle worldBounds, string? name = null)
        : this(playerFactory, source, renderSurfaceHost, DirectDrawingMode.SceneLayer, sceneLayer, null, null, worldBounds, name) { }

    /// <summary>Creates a screen-space URI/stream/GAF video using an owned player from the factory.</summary>
    public DirectVideo(Func<IVideoPlayer> playerFactory, VideoSource source, RenderSurfaceHostBase renderSurfaceHost,
        View view, Rectangle screenBounds, string? name = null)
        : this(playerFactory, source, renderSurfaceHost, DirectDrawingMode.View, null, view, screenBounds, null, name) { }

    private DirectVideo(Func<IVideoPlayer> playerFactory, VideoSource source,
        RenderSurfaceHostBase renderSurfaceHost, DirectDrawingMode mode, SceneLayer? sceneLayer,
        View? view, Rectangle? screenBounds, Rectangle? worldBounds, string? name)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, name)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(playerFactory);
            ArgumentNullException.ThrowIfNull(source);
            _player = playerFactory() ?? throw new ArgumentException("The factory returned no video player.", nameof(playerFactory));
            HookPlayer();
            source.Open(_player);
            _player.Play();
        }
        catch
        {
            _mailbox.Dispose();
            if (_player is not null)
            {
                _player.FrameReady -= OnFrameReady;
                _player.StateChanged -= OnPlayerStateChanged;
                _player.Dispose();
            }
            base.Dispose(true); // Construction registered this drawing; undo on failure.
            throw;
        }
    }

    /// <summary>Replaces the source and starts playback. Call on the engine/UI thread.</summary>
    public void Open(VideoSource source)
    {
        ObjectDisposedException.ThrowIf(_videoDisposed, this);
        ArgumentNullException.ThrowIfNull(source);
        source.Open(_player);
        _mailbox.Consume(ref _frame); // Clear the prior source immediately on this engine thread.
        ForceRefresh();
        _player.Play();
    }

    /// <summary>Replaces the URI source and starts playback on the engine/UI thread.</summary>
    public void Open(Uri source) => Open(VideoSource.FromUri(source));

    /// <summary>Metadata snapshot for the active source; Pending values are not authoritative.</summary>
    public VideoMetadata Metadata => _player.Metadata;

    /// <summary>Decoded source dimensions, or (0,0) until known.</summary>
    public (int width, int height) NaturalSize => _player.NaturalSize;
    /// <summary>
    /// Subscribes to the player's events for frame delivery and playback lifecycle.
    /// </summary>
    private void HookPlayer()
    {
        _player.FrameReady += OnFrameReady;
        _player.StateChanged += OnPlayerStateChanged;
    }

    /// <summary>
    /// Renders the current video frame to the backbuffer at the specified destination rectangle.
    /// </summary>
    /// <param name="backbuffer">The backbuffer to draw onto.</param>
    /// <param name="destRectScreen">The destination rectangle in screen coordinates.</param>
    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        var bmp = _frame;
        if (bmp is null) return;

        var canvas = backbuffer.Canvas;
        var dest = ComputeDestRect(destRectScreen, bmp.Width, bmp.Height, Stretch);

        // DirectDrawingBase.Draw applies opacity and fades once.
        canvas.Save();
        canvas.ClipRect(new SKRect(destRectScreen.Left, destRectScreen.Top, destRectScreen.Right, destRectScreen.Bottom));
        canvas.DrawBitmap(bmp, dest);
        canvas.Restore();
    }

    /// <summary>
    /// Computes the destination rectangle for rendering based on the stretch mode and source dimensions.
    /// </summary>
    /// <param name="bounds">The target bounds in screen coordinates.</param>
    /// <param name="srcW">The source video width in pixels.</param>
    /// <param name="srcH">The source video height in pixels.</param>
    /// <param name="mode">The stretch mode to apply.</param>
    /// <returns>The computed destination rectangle.</returns>
    internal static SKRect ComputeDestRect(RectangleF bounds, int srcW, int srcH, StretchMode mode)
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

    // The event pointer is valid only during the callback; copy before returning.
    private void OnFrameReady(object? sender, VideoFrameReadyEventArgs e) => _mailbox.Publish(e);

    private void OnPlayerStateChanged(object? sender, VideoStateChangedEventArgs e)
    {
        if (e.State is "MediaOpening" or "MediaOpened") _mailbox.Reset();
    }

    /// <summary>Consumes the latest decoded frame on the engine thread, then advances fades.</summary>
    public override void Update(long tick)
    {
        if (_videoDisposed) return;
        if (_mailbox.Consume(ref _frame)) ForceRefresh();
        base.Update(tick);
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
    /// The last consumed frame remains visible until another frame arrives or the source changes.
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

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="DirectVideo"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (_videoDisposed) return;
        _videoDisposed = true;
        _mailbox.Dispose();
        _player.FrameReady -= OnFrameReady;
        _player.StateChanged -= OnPlayerStateChanged;
        try { _player.Dispose(); }
        finally
        {
            _frame?.Dispose();
            _frame = null;
            base.Dispose(true);
        }
    }
}
