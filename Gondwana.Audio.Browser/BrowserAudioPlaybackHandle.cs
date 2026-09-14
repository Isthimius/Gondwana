using System.Runtime.Versioning;
using Gondwana.Audio;

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

    public BrowserAudioPlaybackHandle(string key, string uri, float volume, float pan, float playbackSpeed)
    {
        _key = key;
        _volume = Math.Clamp(volume, 0f, 1f);
        _pan = Math.Clamp(pan, -1f, 1f);
        _playbackSpeed = Math.Clamp(playbackSpeed, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed);
        BrowserAudioInterop.Load(_key, uri, loop: false, _volume, _pan, _playbackSpeed);
    }

    // HTMLMediaElement exposes completion state but this compatibility layer does not yet marshal
    // the DOM ended callback back into .NET. The common event remains available for backends that do.
    public event EventHandler? PlaybackCompleted;

    public AudioPlaybackState State => BrowserAudioInterop.GetState(_key) switch
    {
        1 => AudioPlaybackState.Playing,
        2 => AudioPlaybackState.Paused,
        _ => AudioPlaybackState.Stopped
    };

    public TimeSpan CurrentTime => TimeSpan.FromSeconds(Math.Max(0d, BrowserAudioInterop.GetCurrentTime(_key)));

    public TimeSpan Duration => TimeSpan.FromSeconds(Math.Max(0d, BrowserAudioInterop.GetDuration(_key)));

    public string? TemporaryFilePath => null;

    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            _isLooping = value;
            BrowserAudioInterop.SetLoop(_key, value);
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            BrowserAudioInterop.SetVolume(_key, _volume);
        }
    }

    public float Pan
    {
        get => _pan;
        set
        {
            _pan = Math.Clamp(value, -1f, 1f);
            BrowserAudioInterop.SetPan(_key, _pan);
        }
    }

    public float PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            _playbackSpeed = Math.Clamp(value, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed);
            BrowserAudioInterop.SetPlaybackSpeed(_key, _playbackSpeed);
        }
    }

    public void Play(bool fromStart = true)
    {
        ThrowIfDisposed();
        BrowserAudioInterop.Play(_key, fromStart);
    }

    public void Pause()
    {
        ThrowIfDisposed();
        BrowserAudioInterop.Pause(_key);
    }

    public void Resume()
    {
        ThrowIfDisposed();
        BrowserAudioInterop.Play(_key, fromStart: false);
    }

    public void Seek(TimeSpan position)
    {
        ThrowIfDisposed();
        var seconds = Math.Max(0d, position.TotalSeconds);
        BrowserAudioInterop.SetCurrentTime(_key, seconds);
    }

    public void Stop()
    {
        ThrowIfDisposed();
        BrowserAudioInterop.Stop(_key);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        BrowserAudioInterop.Unload(_key);
        PlaybackCompleted = null;
        _disposed = true;
    }
}
