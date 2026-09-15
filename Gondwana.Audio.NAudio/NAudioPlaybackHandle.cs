using Gondwana.Audio;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Gondwana.Audio.NAudio;

internal sealed class NAudioPlaybackHandle : IAudioPlaybackHandle
{
    private readonly string _key;
    private readonly object _controlLock = new();
    private readonly WaveStream _waveStream;
    private readonly IWavePlayer _outputDevice;
    private readonly VariableSpeedSampleProvider _speedProvider;
    private PanningSampleProvider? _monoPanProvider;
    private StereoPanSampleProvider? _stereoPanProvider;
    private VolumeSampleProvider? _volumeProvider;
    private bool _stopRequested;
    private bool? _pendingPlay;
    private bool _disposed;
    private bool _isLooping;
    private float _volume;
    private float _pan;
    private float _playbackSpeed;

    public NAudioPlaybackHandle(
        string key,
        WaveStream waveStream,
        string? temporaryFilePath,
        float volume,
        float pan,
        float playbackSpeed)
    {
        _key = key;
        _waveStream = waveStream;
        TemporaryFilePath = temporaryFilePath;
        _volume = Math.Clamp(volume, 0f, 1f);
        _pan = Math.Clamp(pan, -1f, 1f);
        _playbackSpeed = Math.Clamp(playbackSpeed, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed);

        var source = _waveStream.ToSampleProvider();
        _speedProvider = new VariableSpeedSampleProvider(source)
        {
            PlaybackSpeed = _playbackSpeed
        };

        _outputDevice = new WaveOutEvent();
        try
        {
            _outputDevice.Init(BuildAudioGraph(_speedProvider));
        }
        catch
        {
            _outputDevice.Dispose();
            throw;
        }
        _outputDevice.PlaybackStopped += OnPlaybackStopped;
    }

    public event EventHandler? PlaybackCompleted;

    public AudioPlaybackState State => _outputDevice.PlaybackState switch
    {
        PlaybackState.Playing => AudioPlaybackState.Playing,
        PlaybackState.Paused => AudioPlaybackState.Paused,
        _ => AudioPlaybackState.Stopped
    };

    public TimeSpan CurrentTime => _waveStream.CurrentTime;
    public TimeSpan Duration => _waveStream.TotalTime;
    public string? TemporaryFilePath { get; }

    public bool IsLooping
    {
        get => _isLooping;
        set => _isLooping = value;
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_volumeProvider is not null)
                _volumeProvider.Volume = _volume;
        }
    }

    public float Pan
    {
        get => _pan;
        set
        {
            _pan = Math.Clamp(value, -1f, 1f);
            if (_monoPanProvider is not null)
                _monoPanProvider.Pan = _pan;
            else if (_stereoPanProvider is not null)
                ApplyStereoPan(_stereoPanProvider, _pan);
        }
    }

    public float PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            _playbackSpeed = Math.Clamp(value, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed);
            _speedProvider.PlaybackSpeed = _playbackSpeed;
        }
    }

    public void Play(bool fromStart = true)
    {
        lock (_controlLock)
        {
            ThrowIfDisposed();

            // WaveOutEvent.Stop completes asynchronously. Restart only after its worker exits.
            if (_stopRequested)
            {
                _pendingPlay = fromStart;
                return;
            }

            if (fromStart)
            {
                if (State != AudioPlaybackState.Stopped)
                {
                    _stopRequested = true;
                    _pendingPlay = true;
                    _outputDevice.Stop();
                    return;
                }

                _speedProvider.Reset(() => _waveStream.Position = 0);
            }

            _stopRequested = false;
            if (State != AudioPlaybackState.Playing)
                _outputDevice.Play();
        }
    }

    public void Pause()
    {
        lock (_controlLock)
        {
            ThrowIfDisposed();
            if (State == AudioPlaybackState.Playing)
                _outputDevice.Pause();
        }
    }

    public void Resume()
    {
        lock (_controlLock)
        {
            ThrowIfDisposed();
            if (State == AudioPlaybackState.Paused)
                _outputDevice.Play();
        }
    }

    public void Seek(TimeSpan position)
    {
        lock (_controlLock)
        {
            ThrowIfDisposed();
            var clamped = position < TimeSpan.Zero
                ? TimeSpan.Zero
                : position > Duration ? Duration : position;

            var wasPlaying = State == AudioPlaybackState.Playing;
            if (wasPlaying)
                _outputDevice.Pause();

            _speedProvider.Reset(() => _waveStream.CurrentTime = clamped);

            if (wasPlaying)
                _outputDevice.Play();
        }
    }

    public void Stop()
    {
        lock (_controlLock)
        {
            ThrowIfDisposed();
            _pendingPlay = null;
            if (State != AudioPlaybackState.Stopped)
            {
                _stopRequested = true;
                _outputDevice.Stop();
            }
        }
    }

    private ISampleProvider BuildAudioGraph(ISampleProvider source)
    {
        ISampleProvider current = source;
        var channels = current.WaveFormat.Channels;

        switch (channels)
        {
            case <= 0:
                Engine.Logger.LogWarning("AudioResource {Key} has invalid channel count: {ChannelCount}", _key, channels);
                break;

            case 1:
                _monoPanProvider = new PanningSampleProvider(current) { Pan = _pan };
                current = _monoPanProvider;
                break;

            case 2:
                _stereoPanProvider = new StereoPanSampleProvider(current);
                ApplyStereoPan(_stereoPanProvider, _pan);
                current = _stereoPanProvider;
                break;

            default:
                var mux = new MultiplexingSampleProvider(new[] { current }, 2);
                mux.ConnectInputToOutput(0, 0);
                mux.ConnectInputToOutput(1, 1);
                current = mux;
                _stereoPanProvider = new StereoPanSampleProvider(current);
                ApplyStereoPan(_stereoPanProvider, _pan);
                current = _stereoPanProvider;
                break;
        }

        _volumeProvider = new VolumeSampleProvider(current) { Volume = _volume };
        return _volumeProvider;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        var completed = false;
        lock (_controlLock)
        {
            if (_disposed)
                return;

            if (e.Exception is not null)
            {
                _stopRequested = false;
                _pendingPlay = null;
                Engine.Logger.LogError(e.Exception, "NAudio playback stopped due to an error for audio resource {Key}", _key);
                return;
            }

            if (_stopRequested)
            {
                _stopRequested = false;
                var pendingPlay = _pendingPlay;
                _pendingPlay = null;
                if (pendingPlay.HasValue)
                    Play(pendingPlay.Value);
                return;
            }

            var reachedEnd = _waveStream.Position >= _waveStream.Length;
            if (_isLooping && reachedEnd)
            {
                Play(true);
                return;
            }

            completed = reachedEnd;
        }

        // User callbacks may unload through the manager; never invoke them under
        // the device control lock (manager disposal takes these locks in reverse).
        if (completed)
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyStereoPan(StereoPanSampleProvider provider, float pan)
    {
        var value = Math.Clamp(pan, -1f, 1f);
        var angle = (value + 1f) * 0.5f * (float)(Math.PI / 2);
        provider.LeftVolume = MathF.Cos(angle);
        provider.RightVolume = MathF.Sin(angle);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        lock (_controlLock)
        {
            if (_disposed)
                return;

            _outputDevice.PlaybackStopped -= OnPlaybackStopped;
            try
            {
                _stopRequested = true;
                _outputDevice.Stop();
            }
            catch
            {
                // Best effort during teardown.
            }

            _outputDevice.Dispose();
            _waveStream.Dispose();
            NAudioReaderRegistry.TryDelete(TemporaryFilePath);
            _disposed = true;
        }
    }
}
