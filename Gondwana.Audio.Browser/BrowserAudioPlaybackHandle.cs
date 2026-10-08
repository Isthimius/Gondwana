using System.Runtime.Versioning;

namespace Gondwana.Audio.Browser;

[SupportedOSPlatform("browser")]
internal sealed class BrowserAudioPlaybackHandle : IAudioPlaybackHandle
{
    private readonly string _key;
    private bool _disposed;
    private bool _isLooping;
    private float _volume;
    private float _pan;
    private float _playbackSpeed;

    /// <summary>
    /// Initializes a new instance of the <c>BrowserAudioPlaybackHandle</c> class.
    /// </summary>
    /// <param name="key">The lookup key for the resource.</param>
    /// <param name="uri">The URI identifying the media source.</param>
    /// <param name="volume">The playback volume.</param>
    /// <param name="pan">The stereo pan, from -1 (left) to 1 (right).</param>
    /// <param name="playbackSpeed">The playback speed multiplier.</param>
    public BrowserAudioPlaybackHandle(string key, string uri, float volume, float pan, float playbackSpeed)
    {
        // Each handle owns its JS entry, including during replacement of a manager key.
        _key = Guid.NewGuid().ToString("N");
        _volume = Math.Clamp(volume, 0f, 1f);
        _pan = Math.Clamp(pan, -1f, 1f);
        _playbackSpeed = Math.Clamp(playbackSpeed, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed);
        BrowserAudioInterop.Load(_key, uri, loop: false, _volume, _pan, _playbackSpeed, OnEnded);
    }

    /// <summary>
    /// Initializes a new instance of the <c>BrowserAudioPlaybackHandle</c> class.
    /// </summary>
    /// <param name="key">The lookup key for the resource.</param>
    /// <param name="data">The encoded audio bytes.</param>
    /// <param name="mimeType">The MIME type of the encoded audio data.</param>
    /// <param name="volume">The playback volume.</param>
    /// <param name="pan">The stereo pan, from -1 (left) to 1 (right).</param>
    /// <param name="playbackSpeed">The playback speed multiplier.</param>
    public BrowserAudioPlaybackHandle(string key, byte[] data, string mimeType, float volume, float pan, float playbackSpeed)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Packed/stream-backed browser audio is exposed to HTMLAudioElement through a
        // short-lived Blob URL owned by the JavaScript entry.
        _key = Guid.NewGuid().ToString("N");
        _volume = Math.Clamp(volume, 0f, 1f);
        _pan = Math.Clamp(pan, -1f, 1f);
        _playbackSpeed = Math.Clamp(playbackSpeed, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed);
        BrowserAudioInterop.LoadBytes(
            _key,
            Convert.ToBase64String(data),
            mimeType,
            loop: false,
            _volume,
            _pan,
            _playbackSpeed,
            OnEnded);
    }

    /// <inheritdoc/>
    public event EventHandler? PlaybackCompleted;

    private void OnEnded()
    {
        if (!_disposed && !_isLooping)
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public AudioPlaybackState State => BrowserAudioInterop.GetState(_key) switch
    {
        1 => AudioPlaybackState.Playing,
        2 => AudioPlaybackState.Paused,
        _ => AudioPlaybackState.Stopped
    };

    /// <inheritdoc/>
    public TimeSpan CurrentTime => TimeSpan.FromSeconds(Math.Max(0d, BrowserAudioInterop.GetCurrentTime(_key)));

    /// <inheritdoc/>
    public TimeSpan Duration => TimeSpan.FromSeconds(Math.Max(0d, BrowserAudioInterop.GetDuration(_key)));

    /// <inheritdoc/>
    public string? TemporaryFilePath => null;

    /// <inheritdoc/>
    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            _isLooping = value;
            BrowserAudioInterop.SetLoop(_key, value);
        }
    }

    /// <inheritdoc/>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            BrowserAudioInterop.SetVolume(_key, _volume);
        }
    }

    /// <inheritdoc/>
    public float Pan
    {
        get => _pan;
        set
        {
            _pan = Math.Clamp(value, -1f, 1f);
            BrowserAudioInterop.SetPan(_key, _pan);
        }
    }

    /// <inheritdoc/>
    public float PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            _playbackSpeed = Math.Clamp(value, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed);
            BrowserAudioInterop.SetPlaybackSpeed(_key, _playbackSpeed);
        }
    }

    /// <inheritdoc/>
    public void Play(bool fromStart = true)
    {
        ThrowIfDisposed();
        BrowserAudioInterop.Play(_key, fromStart);
    }

    /// <inheritdoc/>
    public void Pause()
    {
        ThrowIfDisposed();
        BrowserAudioInterop.Pause(_key);
    }

    /// <inheritdoc/>
    public void Resume()
    {
        ThrowIfDisposed();
        if (State == AudioPlaybackState.Paused)
            BrowserAudioInterop.Play(_key, fromStart: false);
    }

    /// <inheritdoc/>
    public void Seek(TimeSpan position)
    {
        ThrowIfDisposed();
        var seconds = Math.Max(0d, position.TotalSeconds);
        BrowserAudioInterop.SetCurrentTime(_key, seconds);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        ThrowIfDisposed();
        BrowserAudioInterop.Stop(_key);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Releases the resources owned by this instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        BrowserAudioInterop.Unload(_key);
        PlaybackCompleted = null;
        _disposed = true;
    }
}
