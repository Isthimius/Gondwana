using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Widgets;

namespace Gondwana.Video.Widgets;

/// <summary>An interactive video drawing with opt-in Widget dragging.</summary>
/// <remarks>
/// The composite owns its DirectVideo, which owns the supplied or factory-created player.
/// Construct, control and dispose on the engine/UI thread. Playback events retain the player's
/// sender and threading contract; marshal UI work to the engine thread. Show/Hide affect only
/// Widget visibility and interaction, not playback. Construction starts playback like DirectVideo.
/// </remarks>
public sealed class VideoWidget : DraggableWidgetBase
{
    private readonly IVideoPlayer _player;

    /// <summary>Creates a view/screen-space widget. The default factory creates VlcVideoPlayer.</summary>
    public VideoWidget(RenderSurfaceHostBase renderSurfaceHost, View view, Rectangle bounds,
        VideoSource source, Func<IVideoPlayer>? playerFactory = null, string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(source);
        IVideoPlayer? player = null;
        Video = new DirectVideo(() => player = (playerFactory ?? DefaultPlayerFactory)(),
            source, renderSurfaceHost, view, bounds, $"{Nickname}.video");
        _player = player!;
        InitializeVideo();
    }

    /// <summary>Creates a scene-layer/world-space widget. The default factory creates VlcVideoPlayer.</summary>
    public VideoWidget(RenderSurfaceHostBase renderSurfaceHost, SceneLayer sceneLayer, Rectangle bounds,
        VideoSource source, Func<IVideoPlayer>? playerFactory = null, string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);
        ArgumentNullException.ThrowIfNull(source);
        IVideoPlayer? player = null;
        Video = new DirectVideo(() => player = (playerFactory ?? DefaultPlayerFactory)(),
            source, renderSurfaceHost, sceneLayer, bounds, $"{Nickname}.video");
        _player = player!;
        InitializeVideo();
    }

    /// <summary>Creates a view widget transferring player ownership to DirectVideo.</summary>
    public VideoWidget(RenderSurfaceHostBase renderSurfaceHost, View view, Rectangle bounds,
        VideoSource source, IVideoPlayer player, string? nickname = null)
        : this(renderSurfaceHost, view, bounds, source, OwnedPlayerFactory(player), nickname) { }

    /// <summary>Creates a scene-layer widget transferring player ownership to DirectVideo.</summary>
    public VideoWidget(RenderSurfaceHostBase renderSurfaceHost, SceneLayer sceneLayer, Rectangle bounds,
        VideoSource source, IVideoPlayer player, string? nickname = null)
        : this(renderSurfaceHost, sceneLayer, bounds, source, OwnedPlayerFactory(player), nickname) { }

    /// <summary>Gets the owned drawing for advanced configuration. Do not dispose it separately.</summary>
    public DirectVideo Video { get; }

    /// <summary>Gets presentation bounds in screen pixels (View) or world pixels (SceneLayer).</summary>
    public Rectangle Bounds => Mode == DirectDrawingMode.View ? Video.ScreenBounds : Video.WorldBounds;

    /// <summary>Moves the composite and resizes its video using existing drawing bounds.</summary>
    public VideoWidget SetBounds(Rectangle bounds)
    {
        ValidateBounds(bounds);
        SetPosition(bounds.X, bounds.Y);
        SetLocalOffset(Video, Vector2.Zero);
        if (Mode == DirectDrawingMode.View) Video.ScreenBounds = bounds;
        else Video.WorldBounds = bounds;
        return this;
    }

    /// <summary>Starts or resumes playback.</summary>
    public void Play() => Video.Play();
    /// <summary>Pauses playback without hiding the widget.</summary>
    public void Pause() => Video.Pause();
    /// <summary>Stops playback; DirectVideo retains its last consumed frame.</summary>
    public void Stop() => Video.Stop();
    /// <summary>Seeks to a media position.</summary>
    public void Seek(TimeSpan position) => Video.Seek(position);
    /// <summary>Replaces the source and starts playback through DirectVideo.</summary>
    public void Open(VideoSource source) => Video.Open(source);
    /// <summary>Replaces a URI/file source and starts playback through DirectVideo.</summary>
    public void Open(Uri source) => Video.Open(source);
    /// <summary>Gets or sets playback speed.</summary>
    public double PlaybackRate { get => Video.PlaybackRate; set => Video.PlaybackRate = value; }
    /// <summary>Gets or sets looping.</summary>
    public bool Loop { get => Video.Loop; set => Video.Loop = value; }
    /// <summary>Gets or sets scaling within the presentation bounds.</summary>
    public StretchMode Stretch { get => Video.Stretch; set => Video.Stretch = value; }
    /// <summary>Gets the player's current playing state.</summary>
    public bool IsPlaying => _player.IsPlaying;
    /// <summary>Gets media duration; check metadata readiness before interpreting unknown values.</summary>
    public TimeSpan Duration => _player.Duration;
    /// <summary>Gets the current media position.</summary>
    public TimeSpan Position => _player.Position;
    /// <summary>Gets actual decoded dimensions, or (0, 0) before discovery.</summary>
    public (int width, int height) NaturalSize => Video.NaturalSize;
    /// <summary>Gets audio-track presence; false is inconclusive until metadata is ready.</summary>
    public bool HasAudio => _player.HasAudio;
    /// <summary>Gets whether metadata is available for this source.</summary>
    public bool IsMetadataReady => _player.IsMetadataReady;
    /// <summary>Gets the authoritative metadata snapshot.</summary>
    public VideoMetadata Metadata => Video.Metadata;

    /// <summary>Forwards playback start notifications on the player's event thread.</summary>
    public event EventHandler Started { add => _player.Started += value; remove => _player.Started -= value; }
    /// <summary>Forwards pause notifications on the player's event thread.</summary>
    public event EventHandler Paused { add => _player.Paused += value; remove => _player.Paused -= value; }
    /// <summary>Forwards stop notifications on the player's event thread.</summary>
    public event EventHandler Stopped { add => _player.Stopped += value; remove => _player.Stopped -= value; }
    /// <summary>Forwards end notifications on the player's event thread.</summary>
    public event EventHandler Ended { add => _player.Ended += value; remove => _player.Ended -= value; }
    /// <summary>Forwards state/metadata notifications, retaining the player's sender and threading.</summary>
    public event EventHandler<VideoStateChangedEventArgs> StateChanged
    {
        add => _player.StateChanged += value;
        remove => _player.StateChanged -= value;
    }

    private void InitializeVideo()
    {
        try { Add(Video, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero); }
        catch { Video.Dispose(); throw; }
        IsDragEnabled = false;
        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
    }

    private static IVideoPlayer DefaultPlayerFactory() => new VlcVideoPlayer();

    private static Func<IVideoPlayer> OwnedPlayerFactory(IVideoPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return () => player;
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds), bounds, "Video widget size must be positive.");
        return bounds;
    }
}
