using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace Gondwana.Video;

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

    public bool Loop { get; set; }
    public bool IsPlaying => _player.IsPlaying;
    public TimeSpan Duration => TimeSpan.FromMilliseconds(_player.Length);
    public TimeSpan Position => TimeSpan.FromMilliseconds(_player.Time);
    public (int width, int height) NaturalSize => (_width, _height);
    public bool HasAudio { get; private set; } = true;

    public event EventHandler? Started;
    public event EventHandler? Paused;
    public event EventHandler? Stopped;
    public event EventHandler? Ended;
    public event EventHandler<VideoStateChangedEventArgs>? StateChanged;
    public event EventHandler<VideoFrameReadyEventArgs>? FrameReady;

    private static bool _vlcCoreInitialized = false;

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

    public void Play() => _player.Play();
    public void Pause() => _player.Pause();
    public void Stop() => _player.Stop();
    public void Seek(TimeSpan position) => _player.Time = (long)position.TotalMilliseconds;
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
