using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Gondwana.Audio.NAudio;

/// <summary>
/// Represents a playback handle for audio resources using NAudio.
/// Manages playback state, volume, panning, looping, and playback speed for a single audio stream.
/// </summary>
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
    private TimeSpan? _pendingPosition;
    private bool _disposed;
    private bool _isLooping;
    private float _volume;
    private float _pan;
    private float _playbackSpeed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NAudioPlaybackHandle"/> class.
    /// </summary>
    /// <param name="key">Logical key for the audio resource (used for logging).</param>
    /// <param name="waveStream">The underlying <see cref="WaveStream"/> that provides audio samples.</param>
    /// <param name="temporaryFilePath">Optional temporary file path associated with the resource.</param>
    /// <param name="volume">Initial volume in the range [0,1].</param>
    /// <param name="pan">Initial pan value in the range [-1,1] where -1 is full left and 1 is full right.</param>
    /// <param name="playbackSpeed">Initial playback speed (clamped to supported range).</param>
    /// <param name="outputDevice">Optional output device for backend testing; defaults to WaveOutEvent.</param>
    public NAudioPlaybackHandle(
        string key,
        WaveStream waveStream,
        string? temporaryFilePath,
        float volume,
        float pan,
        float playbackSpeed,
        IWavePlayer? outputDevice = null)
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

        _outputDevice = outputDevice ?? new WaveOutEvent();

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

    /// <summary>
    /// Occurs when playback completes naturally (reached end and not looping).
    /// </summary>
    public event EventHandler? PlaybackCompleted;

    /// <summary>
    /// Gets the current high-level playback state for this handle.
    /// </summary>
    /// <value>A <see cref="AudioPlaybackState"/> value indicating whether the audio is playing, paused, or stopped.</value>
    public AudioPlaybackState State => _outputDevice.PlaybackState switch
    {
        PlaybackState.Playing => AudioPlaybackState.Playing,
        PlaybackState.Paused => AudioPlaybackState.Paused,
        _ => AudioPlaybackState.Stopped
    };

    /// <summary>
    /// Gets or sets the current playback position within the audio stream.
    /// </summary>
    /// <value>The current playback position.</value>
    public TimeSpan CurrentTime
    {
        get { lock (_controlLock) return _pendingPosition ?? _waveStream.CurrentTime; }
    }

    /// <summary>
    /// Gets the total duration of the audio stream.
    /// </summary>
    /// <value>The total duration.</value>
    public TimeSpan Duration => _waveStream.TotalTime;

    /// <summary>
    /// Gets the optional temporary file path associated with this playback resource.
    /// </summary>
    public string? TemporaryFilePath { get; }

    /// <summary>
    /// Gets or sets a value indicating whether playback should loop when the end is reached.
    /// </summary>
    public bool IsLooping
    {
        get => _isLooping;
        set => _isLooping = value;
    }

    /// <summary>
    /// Gets or sets the playback volume.
    /// </summary>
    /// <value>Volume in the range [0,1]. Setting updates the audio graph if available.</value>
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

    /// <summary>
    /// Gets or sets the stereo pan for playback.
    /// </summary>
    /// <value>Pan in the range [-1,1] where -1 is full left and 1 is full right.</value>
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

    /// <summary>
    /// Gets or sets the playback speed multiplier.
    /// </summary>
    /// <value>The playback speed; values are clamped to supported limits defined on <see cref="AudioResource"/>.</value>
    public float PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            _playbackSpeed = Math.Clamp(value, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed);
            _speedProvider.PlaybackSpeed = _playbackSpeed;
        }
    }

    /// <summary>
    /// Starts playback on this handle.
    /// </summary>
    /// <param name="fromStart">If true, playback begins from the start of the stream; otherwise resumes from current position.</param>
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

    /// <summary>
    /// Pauses playback if currently playing.
    /// </summary>
    public void Pause()
    {
        lock (_controlLock)
        {
            ThrowIfDisposed();
            if (State == AudioPlaybackState.Playing)
                _outputDevice.Pause();
        }
    }

    /// <summary>
    /// Resumes playback if currently paused.
    /// </summary>
    public void Resume()
    {
        lock (_controlLock)
        {
            ThrowIfDisposed();
            if (State == AudioPlaybackState.Paused)
                _outputDevice.Play();
        }
    }

    /// <summary>
    /// Seeks the playback position to the specified time.
    /// </summary>
    /// <param name="position">Target playback position. Values outside the valid range are clamped to [0, Duration].</param>
    public void Seek(TimeSpan position)
    {
        lock (_controlLock)
        {
            ThrowIfDisposed();
            var clamped = position < TimeSpan.Zero
                ? TimeSpan.Zero
                : position > Duration ? Duration : position;

            if (_stopRequested)
            {
                _pendingPosition = clamped;
                return;
            }

            var wasPlaying = State == AudioPlaybackState.Playing;
            if (wasPlaying)
                _outputDevice.Pause();

            _speedProvider.Reset(() => _waveStream.CurrentTime = clamped);

            if (wasPlaying)
                _outputDevice.Play();
        }
    }

    /// <summary>
    /// Stops playback and resets the logical position to the beginning. The stream
    /// is rewound when the asynchronous device stop completes, before a queued play.
    /// </summary>
    public void Stop()
    {
        lock (_controlLock)
        {
            ThrowIfDisposed();
            _pendingPlay = null;
            if (_stopRequested)
            {
                _pendingPosition = TimeSpan.Zero;
                return;
            }
            if (State != AudioPlaybackState.Stopped)
            {
                _pendingPosition = TimeSpan.Zero;
                _stopRequested = true;
                _outputDevice.Stop();
            }
            else
                _speedProvider.Reset(() => _waveStream.Position = 0);
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

            if (_pendingPosition is { } position)
            {
                _speedProvider.Reset(() => _waveStream.CurrentTime = position);
                _pendingPosition = null;
            }

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

    /// <summary>
    /// Disposes the playback handle and releases associated native resources.
    /// After disposal the instance must not be used.
    /// </summary>
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
