using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace Gondwana.Video;

/// <summary>
/// Provides a video player implementation using LibVLC for video playback and frame rendering.
/// </summary>
public sealed class VlcVideoPlayer : IVideoPlayer
{
    private readonly LibVLC _vlc;
    private readonly MediaPlayer _player;

    // keep delegates alive; initialize with null-forgiving default
    private MediaPlayer.LibVLCVideoLockCb _lockCb = default!;

    private MediaPlayer.LibVLCVideoUnlockCb _unlockCb = default!;
    private MediaPlayer.LibVLCVideoDisplayCb _displayCb = default!;

    private int _width, _height, _stride;
    private GCHandle _frameHandle;
    private IntPtr _framePtr = IntPtr.Zero;
    private byte[]? _frameBuffer;
    private readonly object _lock = new();

    /// <summary>
    /// Gets or sets a value indicating whether the video should loop when playback ends.
    /// </summary>
    public bool Loop { get; set; }
    
    /// <summary>
    /// Gets a value indicating whether the video is currently playing.
    /// </summary>
    public bool IsPlaying => _player.IsPlaying;
    
    /// <summary>
    /// Gets the total duration of the currently loaded video.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromMilliseconds(_player.Length);
    
    /// <summary>
    /// Gets the current playback position of the video.
    /// </summary>
    public TimeSpan Position => TimeSpan.FromMilliseconds(_player.Time);
    
    /// <summary>
    /// Gets the natural size (width and height) of the video in pixels.
    /// </summary>
    public (int width, int height) NaturalSize => (_width, _height);
    
    /// <summary>
    /// Gets a value indicating whether the currently loaded media has an audio track.
    /// </summary>
    public bool HasAudio { get; private set; } = true;

    /// <summary>
    /// Occurs when video playback has started.
    /// </summary>
    public event EventHandler? Started;

    /// <summary>
    /// Occurs when video playback has been paused.
    /// </summary>
    public event EventHandler? Paused;

    /// <summary>
    /// Occurs when video playback has been stopped.
    /// </summary>
    public event EventHandler? Stopped;

    /// <summary>
    /// Occurs when video playback has reached the end of the media.
    /// </summary>
    public event EventHandler? Ended;

    /// <summary>
    /// Occurs when the video player state changes.
    /// </summary>
    public event EventHandler<VideoStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Occurs when a new video frame is ready for rendering.
    /// </summary>
    public event EventHandler<VideoFrameReadyEventArgs>? FrameReady;

    private static bool _vlcCoreInitialized = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="VlcVideoPlayer"/> class.
    /// </summary>
    /// <param name="vlcArgs">Optional command-line arguments to pass to LibVLC.</param>
    /// <param name="initialWidth">The initial width of the video frame buffer in pixels. Default is 1280.</param>
    /// <param name="initialHeight">The initial height of the video frame buffer in pixels. Default is 720.</param>
    public VlcVideoPlayer(string[]? vlcArgs = null, int initialWidth = 1280, int initialHeight = 720)
    {
        // Call Core.Initialize once per process, but ALWAYS create _vlc/_player.
        if (!_vlcCoreInitialized)
        {
            Core.Initialize();
            _vlcCoreInitialized = true;
        }

        _vlc = new LibVLC(vlcArgs ?? Array.Empty<string>());
        _player = new MediaPlayer(_vlc);

        _width = initialWidth;
        _height = initialHeight;
        _stride = _width * 4;
        AllocateFrameBuffer(_width, _height);

        _player.Playing += OnStarted;
        _player.Paused += OnPaused;
        _player.Stopped += OnStopped;
        _player.EndReached += OnEndReached;
    }

    private void OnStarted(object? s, EventArgs e)
    {
        Started?.Invoke(this, EventArgs.Empty);
    }

    private void OnPaused(object? s, EventArgs e)
    {
        Paused?.Invoke(this, EventArgs.Empty);
    }

    private void OnStopped(object? s, EventArgs e)
    {
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    private void OnEndReached(object? s, EventArgs e)
    {
        Ended?.Invoke(this, EventArgs.Empty);

        if (Loop)
        {
            _player.Position = 0f;
            _player.Play();
        }
    }

    private Media? _media;

    /// <summary>
    /// Opens a video from the specified URI for playback.
    /// </summary>
    /// <param name="source">The URI of the video source to open.</param>
    public void Open(Uri source)
    {
        _media?.Dispose();                  // cleanup previous
        _media = new Media(_vlc, source);
        _player.Media = _media;

        // optional; populates duration/tracks
        _media.Parse(MediaParseOptions.ParseNetwork);
        HasAudio = _media.Tracks != null && Array.Exists(_media.Tracks, t => t.TrackType == TrackType.Audio);
        StateChanged?.Invoke(this, new VideoStateChangedEventArgs("MediaOpened"));

        // Fixed pixel format (RV32 = RGBA), using our preallocated buffer size
        _player.SetVideoFormat("RV32", (uint)_width, (uint)_height, (uint)_stride);

        // ---- keep delegates alive on fields; assign BEFORE SetVideoCallbacks ----
        _lockCb = (IntPtr opaque, IntPtr planes) =>
        {
            // planes points to an array of plane pointers; for RV32 it's one element.
            lock (_lock)
            {
                Marshal.WriteIntPtr(planes, _framePtr);   // planes[0] = _framePtr
                return IntPtr.Zero;                       // return picture (opaque), unused
            }
        };

        _unlockCb = (IntPtr opaque, IntPtr picture, IntPtr planes) =>
        {
            // no-op; buffer is pinned for the life of the player
        };

        _displayCb = (IntPtr opaque, IntPtr picture) =>
        {
            long pts100ns;
            IntPtr ptr;
            int w, h, stride;

            lock (_lock)
            {
                if (_framePtr == IntPtr.Zero) return;
                pts100ns = (long)(_player.Time * 10_000);
                ptr = _framePtr;
                w = _width;
                h = _height;
                stride = _stride;
            }

            FrameReady?.Invoke(this, new VideoFrameReadyEventArgs(ptr, w, h, stride, pts100ns));
        };

        _player.SetVideoCallbacks(_lockCb, _unlockCb, _displayCb);
    }

    /// <summary>
    /// Starts or resumes video playback.
    /// </summary>
    public void Play() => _player.Play();

    /// <summary>
    /// Pauses video playback.
    /// </summary>
    public void Pause() => _player.Pause();

    /// <summary>
    /// Stops video playback and resets the position.
    /// </summary>
    public void Stop() => _player.Stop();

    /// <summary>
    /// Seeks to the specified position in the video.
    /// </summary>
    /// <param name="position">The target position to seek to.</param>
    public void Seek(TimeSpan position) => _player.Time = (long)position.TotalMilliseconds;

    /// <summary>
    /// Sets the playback rate (speed) of the video.
    /// </summary>
    /// <param name="rate">The playback rate multiplier (e.g., 1.0 for normal speed, 2.0 for double speed).</param>
    public void SetRate(double rate) => _player.SetRate((float)rate);

    // ---------- buffer plumbing ----------
    private void AllocateFrameBuffer(int width, int height)
    {
        var bytes = width * 4 * height; // RGBA
        lock (_lock)
        {
            if (_frameHandle.IsAllocated) _frameHandle.Free();
            _frameBuffer = new byte[bytes];
            _frameHandle = GCHandle.Alloc(_frameBuffer, GCHandleType.Pinned);
            _framePtr = _frameHandle.AddrOfPinnedObject();
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="VlcVideoPlayer"/>.
    /// </summary>
    public void Dispose()
    {
        Stop();

        _player.Playing -= OnStarted;
        _player.Paused -= OnPaused;
        _player.Stopped -= OnStopped;
        _player.EndReached -= OnEndReached;

        _player?.Dispose();
        _media?.Dispose(); _media = null;

        lock (_lock)
        {
            if (_frameHandle.IsAllocated)
                _frameHandle.Free();

            _frameBuffer = null;
            _framePtr = IntPtr.Zero;
        }

        _vlc?.Dispose();
    }
}