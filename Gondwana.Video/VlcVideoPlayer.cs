using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace Gondwana.Video;

/// <summary>Desktop/native LibVLC 3 player. Controls are serialized; event handlers must not
/// block on the engine thread. FrameReady runs on a decoder thread and is copy-only.</summary>
public sealed class VlcVideoPlayer : IVideoPlayer
{
    private readonly LibVLC _vlc;
    private readonly MediaPlayer _player;
    private readonly object _control = new();
    private readonly object _frames = new();
    private readonly VideoMetadataState _metadata = new();
    private readonly MediaPlayer.LibVLCVideoLockCb _lockCb;
    private readonly MediaPlayer.LibVLCVideoUnlockCb _unlockCb;
    private readonly MediaPlayer.LibVLCVideoDisplayCb _displayCb;
    private readonly MediaPlayer.LibVLCVideoFormatCb _formatCb;
    private readonly MediaPlayer.LibVLCVideoCleanupCb _cleanupCb;
    private Media? _media;
    private StreamMediaInput? _input;
    private Stream? _ownedStream;
    private CancellationTokenSource? _parseCancellation;
    private Task<MediaParsedStatus>? _parseTask;
    private long _generation;
    private bool _disposed, _acceptFrames;
    private volatile bool _loop;
    private (int width, int height) _naturalSize;
    private Exception? _lastError;

    public bool Loop { get => _loop; set => _loop = value; }
    public bool IsPlaying { get { lock (_control) return !_disposed && _player.IsPlaying; } }
    public VideoMetadata Metadata => _metadata.Current;
    public bool IsMetadataReady => Metadata.Status == VideoMetadataStatus.Ready;
    public bool HasAudio => Metadata.HasAudio;
    public TimeSpan Duration => Metadata.Duration;
    public TimeSpan Position { get { lock (_control) return _disposed ? TimeSpan.Zero : TimeSpan.FromMilliseconds(Math.Max(0, _player.Time)); } }
    /// <summary>Decoded source dimensions, or (0,0) before format negotiation. No presentation scaling is requested.</summary>
    public (int width, int height) NaturalSize { get { lock (_frames) return _naturalSize; } }
    /// <summary>The most recent native callback or playback failure, if any.</summary>
    public Exception? LastError => Volatile.Read(ref _lastError);
    public event EventHandler? Started;
    public event EventHandler? Paused;
    public event EventHandler? Stopped;
    public event EventHandler? Ended;
    public event EventHandler<VideoStateChangedEventArgs>? StateChanged;
    public event EventHandler<VideoFrameReadyEventArgs>? FrameReady;

    /// <summary>Creates a native desktop player. Native libraries are supplied by the application.</summary>
    /// <param name="vlcArgs">Optional LibVLC arguments.</param>
    /// <param name="initialWidth">Compatibility fallback hint; no buffer or scaling is forced from this value.</param>
    /// <param name="initialHeight">Compatibility fallback hint; actual dimensions come from format negotiation.</param>
    public VlcVideoPlayer(string[]? vlcArgs = null, int initialWidth = 1280, int initialHeight = 720)
    {
        if (initialWidth <= 0 || initialHeight <= 0) throw new ArgumentOutOfRangeException(nameof(initialWidth));
        if (OperatingSystem.IsBrowser() || !BitConverter.IsLittleEndian)
            throw new PlatformNotSupportedException("Gondwana.Video requires a little-endian native desktop LibVLC 3 runtime.");
        try
        {
            // LibVLCSharp serializes process-wide initialization itself.
            Core.Initialize();
            _vlc = new LibVLC(vlcArgs ?? []);
            try { _player = new MediaPlayer(_vlc); }
            catch { _vlc.Dispose(); throw; }
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or TypeInitializationException or VLCException)
        {
            throw new InvalidOperationException("Gondwana.Video could not load native LibVLC 3. Add VideoLAN.LibVLC.Windows to the Windows app, or VideoLAN.LibVLC.Mac to a compatible macOS app; on Linux install libvlc-dev and VLC codec plugins. Match the process architecture and deploy the plugins directory. See Gondwana.Video/README.md. For a custom runtime call LibVLCSharp.Shared.Core.Initialize(path) before creating the player.", ex);
        }
        _lockCb = LockFrame;
        _unlockCb = (_, _, _) => { };
        _displayCb = DisplayFrame;
        _formatCb = SetupFormat;
        _cleanupCb = CleanupFormat;
        _player.SetVideoCallbacks(_lockCb, _unlockCb, _displayCb);
        _player.SetVideoFormatCallbacks(_formatCb, _cleanupCb);
        _player.Playing += OnStarted;
        _player.Paused += OnPaused;
        _player.Stopped += OnStopped;
        _player.EndReached += OnEnded;
        _player.EncounteredError += OnError;
    }

    public void Open(Uri source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.IsAbsoluteUri) source = new Uri(Path.GetFullPath(source.OriginalString));
        lock (_control)
        {
            ThrowIfDisposed();
            ReplaceMedia(() => new Media(_vlc, source));
            var media = _media!;
            long generation = _generation;
            _parseCancellation = new CancellationTokenSource();
            _parseTask = media.Parse(source.IsFile ? MediaParseOptions.ParseLocal : MediaParseOptions.ParseNetwork,
                cancellationToken: _parseCancellation.Token);
            // The continuation does not own the media. Generation validation under _control
            // prevents any access after replacement/disposal. Open never waits for network parse.
            _ = ObserveParseAsync(_parseTask, media, generation);
        }
    }

    public void Open(Stream source, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) throw new ArgumentException("Video stream must be readable.", nameof(source));
        lock (_control)
        {
            ThrowIfDisposed();
            var input = new StreamMediaInput(source);
            try
            {
                ReplaceMedia(() => new Media(_vlc, input));
                _input = input;
                _ownedStream = leaveOpen ? null : source;
            }
            catch { input.Dispose(); throw; }
            // Do not preparse and play against the same stream cursor concurrently.
            // LibVLC discovers stream tracks during playback; Playing publishes metadata.
        }
    }

    private void ReplaceMedia(Func<Media> create)
    {
        lock (_frames) _acceptFrames = false;
        ++_generation;
        ReleaseMedia();
        lock (_frames) _naturalSize = (0, 0);
        _metadata.Reset();
        Volatile.Write(ref _lastError, null);
        StateChanged?.Invoke(this, new("MediaOpening"));
        _media = create();
        _player.Media = _media;
        _generation = _metadata.Begin();
        StateChanged?.Invoke(this, new("MediaOpened"));
        lock (_frames) _acceptFrames = true;
    }

    private async Task ObserveParseAsync(Task<MediaParsedStatus> parse, Media media, long generation)
    {
        MediaParsedStatus status;
        try { status = await parse.ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { Volatile.Write(ref _lastError, ex); status = MediaParsedStatus.Failed; }
        // Always queue: even a synchronous Parse completion must not raise user events inside Open.
        QueueForSource(generation, () => PublishMetadata(media, status == MediaParsedStatus.Done));
    }

    private void PublishMetadata(Media media, bool ready)
    {
        var metadata = ready
            ? new VideoMetadata(VideoMetadataStatus.Ready, Array.Exists(media.Tracks, t => t.TrackType == TrackType.Audio),
                TimeSpan.FromMilliseconds(Math.Max(0, media.Duration)))
            : new VideoMetadata(VideoMetadataStatus.Failed, false, TimeSpan.Zero);
        if (_metadata.Complete(_generation, metadata))
            StateChanged?.Invoke(this, new(ready ? "MetadataReady" : "MetadataFailed"));
    }

    private void QueueForSource(long generation, Action action) => ThreadPool.QueueUserWorkItem(_ =>
    {
        lock (_control)
        {
            if (_disposed || generation != _generation) return;
            try { action(); }
            catch (Exception ex) { Volatile.Write(ref _lastError, ex); }
        }
    });

    private void OnStarted(object? sender, EventArgs e)
    {
        long generation = Volatile.Read(ref _generation);
        QueueForSource(generation, () =>
        {
            if (_input is not null && !IsMetadataReady) PublishMetadata(_media!, true);
            Started?.Invoke(this, EventArgs.Empty);
        });
    }
    private void OnPaused(object? sender, EventArgs e) => QueueForSource(Volatile.Read(ref _generation), () => Paused?.Invoke(this, EventArgs.Empty));
    private void OnStopped(object? sender, EventArgs e) => QueueForSource(Volatile.Read(ref _generation), () => Stopped?.Invoke(this, EventArgs.Empty));
    private void OnError(object? sender, EventArgs e) => QueueForSource(Volatile.Read(ref _generation), () =>
    {
        Volatile.Write(ref _lastError, new InvalidOperationException("LibVLC could not play the active source. Check its format, access, and installed codec plugins."));
        StateChanged?.Invoke(this, new("Error"));
    });
    private void OnEnded(object? sender, EventArgs e) => QueueForSource(Volatile.Read(ref _generation), () =>
    {
        long generation = _generation;
        Ended?.Invoke(this, EventArgs.Empty);
        if (!_disposed && generation == _generation && Loop)
        {
            // Never reenter libvlc from its EndReached callback (documented deadlock risk).
            _player.Stop();
            _player.Play();
        }
    });

    private uint SetupFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        try
        {
            var buffer = new VideoDecodeBuffer(checked((int)width), checked((int)height));
            opaque = buffer.Context;
            // LibVLC 3 vmem masks: R=0xff0000 G=0xff00 B=0xff. On little endian
            // RV32 is B,G,R,X (not alpha). Skia uses Bgra8888 + Opaque without swizzling.
            Marshal.Copy(new byte[] { (byte)'R', (byte)'V', (byte)'3', (byte)'2' }, 0, chroma, 4);
            pitches = (uint)buffer.Stride;
            lines = (uint)buffer.Lines;
            lock (_frames) _naturalSize = (buffer.Width, buffer.Height);
            return 1;
        }
        catch (Exception ex) { Volatile.Write(ref _lastError, ex); return 0; }
    }
    private static IntPtr LockFrame(IntPtr opaque, IntPtr planes)
    {
        var buffer = (VideoDecodeBuffer)GCHandle.FromIntPtr(Marshal.ReadIntPtr(opaque)).Target!;
        Marshal.WriteIntPtr(planes, buffer.Pixels);
        return opaque;
    }
    private void DisplayFrame(IntPtr opaque, IntPtr picture)
    {
        try
        {
            lock (_frames)
            {
                if (!_acceptFrames) return;
                var buffer = (VideoDecodeBuffer)GCHandle.FromIntPtr(Marshal.ReadIntPtr(opaque)).Target!;
                FrameReady?.Invoke(this, new(buffer.Pixels, buffer.Width, buffer.Height, buffer.Stride, 0));
            }
        }
        catch (Exception ex) { Volatile.Write(ref _lastError, ex); }
    }
    private static void CleanupFormat(ref IntPtr opaque)
    {
        var handle = GCHandle.FromIntPtr(opaque);
        ((VideoDecodeBuffer)handle.Target!).Dispose();
        // VideoDecodeBuffer releases its context and handle.
    }

    public void Play() { lock (_control) { ThrowIfDisposed(); _player.Play(); } }
    public void Pause() { lock (_control) { ThrowIfDisposed(); _player.SetPause(true); } }
    public void Stop() { lock (_control) { ThrowIfDisposed(); _player.Stop(); } }
    public void Seek(TimeSpan position) { lock (_control) { ThrowIfDisposed(); _player.Time = Math.Max(0, (long)position.TotalMilliseconds); } }
    public void SetRate(double rate)
    {
        if (!double.IsFinite(rate) || rate <= 0) throw new ArgumentOutOfRangeException(nameof(rate));
        lock (_control) { ThrowIfDisposed(); _player.SetRate((float)rate); }
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private void ReleaseMedia()
    {
        _player.Stop(); // Joins decoder callbacks before media/input/buffer ownership is released.
        _parseCancellation?.Cancel();
        if (_parseTask is not null)
        {
            try { _parseTask.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
        }
        _parseTask = null;
        _parseCancellation?.Dispose();
        _parseCancellation = null;
        _player.Media = null;
        _media?.Dispose(); _media = null;
        _input?.Dispose(); _input = null;
        _ownedStream?.Dispose(); _ownedStream = null;
    }
    public void Dispose()
    {
        lock (_control)
        {
            if (_disposed) return;
            _disposed = true;
            ++_generation;
            lock (_frames) _acceptFrames = false;
            ReleaseMedia();
            _metadata.Reset();
            _player.Playing -= OnStarted;
            _player.Paused -= OnPaused;
            _player.Stopped -= OnStopped;
            _player.EndReached -= OnEnded;
            _player.EncounteredError -= OnError;
            _player.Dispose();
            _vlc.Dispose();
        }
    }
}



