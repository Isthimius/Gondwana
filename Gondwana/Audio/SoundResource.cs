using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;

using static Gondwana.Audio.PlatformAudioFactory;

namespace Gondwana.Audio;

/// <summary>
/// Represents a sound resource that can be played, paused, resumed, and disposed.
/// </summary>
[JsonObject(IsReference = true)]
public class SoundResource : IDisposable
{
    private readonly IWavePlayer outputDevice;
    private readonly WaveStream waveStream;
    private PanningSampleProvider? monoPanProvider;         // for mono sources only
    private StereoPanSampleProvider? stereoPanProvider;     // for stereo sources only
    private VolumeSampleProvider? volumeProvider;           // final stage
    private bool disposed;

    /// <summary>
    /// Event that is raised when playback completes.
    /// Will not be raised if the sound is looping.
    /// </summary>
    public event EventHandler PlaybackCompleted;

    /// <summary>
    /// Asynchronous callback that is invoked when playback completes.
    /// Will not be invoked if the sound is looping.
    /// </summary>
    public Func<Task>? PlaybackCompletedAsync;

    /// <summary>
    /// Event that is raised when the sound resource is disposed.
    /// </summary>
    public event EventHandler Disposed;

    private SoundResource()
    { }

    internal SoundResource(
        string key,
        WaveStream soundStream,
        float volume = 1.0f,
        float pan = 0.0f,
        string? filePathOrExt = null,
        byte[]? rawBytes = null,
        string? tempFilePath = null)
    {
        Key = key;
        waveStream = soundStream;
        outputDevice = new WaveOutEvent();
        outputDevice.Init(BuildAudioGraph(waveStream, volume, pan));
        outputDevice.PlaybackStopped += OnPlaybackStopped;
        FilePathOrExtension = filePathOrExt;
        var ext = Path.GetExtension(filePathOrExt ?? string.Empty);
        Extension = string.IsNullOrEmpty(ext) ? null : NormalizeExt(ext);
        OriginalBytes = rawBytes;
        TempFilePath = tempFilePath;
    }

    private ISampleProvider BuildAudioGraph(WaveStream source, float volume, float pan)
    {
        _pan = Math.Clamp(pan, -1f, 1f);
        ISampleProvider baseProvider = source.ToSampleProvider();

        int ch = baseProvider.WaveFormat.Channels;
        if (ch < 1)
        {
            Engine.Logger.LogWarning(
                "SoundResource {Key} has invalid channel count: {ChannelCount}", Key, ch);

            // just pass through, no pan stage
            volumeProvider = new VolumeSampleProvider(baseProvider)
            {
                Volume = Math.Clamp(volume, 0f, 1f)
            };

            return volumeProvider;
        }

        switch (ch)
        {
            case 1:
                // MONO -> use PanningSampleProvider (expects mono, outputs stereo)
                monoPanProvider = new PanningSampleProvider(baseProvider)
                {
                    Pan = Math.Clamp(pan, -1f, 1f)
                };
                stereoPanProvider = null;
                baseProvider = monoPanProvider; // now stereo
                break;

            case 2:
                // STEREO -> use StereoSampleProvider for balance/pan
                stereoPanProvider = new StereoPanSampleProvider(baseProvider);
                ApplyStereoPan(stereoPanProvider, pan); // set L/R gains from pan
                monoPanProvider = null;
                baseProvider = stereoPanProvider;       // stays stereo
                break;

            default:
                // >2 CH -> pick first two channels, then treat as stereo
                var mux = new MultiplexingSampleProvider(new[] { baseProvider }, 2);
                mux.ConnectInputToOutput(0, 0); // channel 0 -> output L
                mux.ConnectInputToOutput(1, 1); // channel 1 -> output R
                baseProvider = mux;

                stereoPanProvider = new StereoPanSampleProvider(baseProvider);
                ApplyStereoPan(stereoPanProvider, pan);
                monoPanProvider = null;
                baseProvider = stereoPanProvider;
                break;
        }

        // final stage: master volume
        volumeProvider = new VolumeSampleProvider(baseProvider)
        {
            Volume = Math.Clamp(volume, 0f, 1f)
        };

        return volumeProvider;
    }

    /// <summary>
    /// Gets the unique key associated with this sound resource.
    /// </summary>
    public string Key { get; private set; }

    /// <summary>
    /// Gets the original byte array of the audio data, if available.
    /// </summary>
    [JsonIgnore]
    public byte[]? OriginalBytes { get; private set; }

    /// <summary>
    /// Gets the file extension associated with the object, if available.
    /// </summary>
    [JsonIgnore]
    public string? Extension { get; private set; }

    /// <summary>
    /// Gets the file path associated with the current object.
    /// </summary>
    /// <remarks>If passing an extension, it must begin with "."</remarks>
    [JsonIgnore]
    public string? FilePathOrExtension { get; }

    /// <summary>
    /// Gets or sets the temporary file path used for WaveReaders that require a file on disk.
    /// </summary>
    [JsonIgnore]
    public string? TempFilePath { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the playback is currently paused.
    /// </summary>
    [JsonIgnore]
    public bool IsPaused => outputDevice.PlaybackState == PlaybackState.Paused;

    /// <summary>
    /// Gets a value indicating whether audio playback is currently active.
    /// </summary>
    [JsonIgnore]
    public bool IsPlaying => outputDevice.PlaybackState == PlaybackState.Playing;

    /// <summary>
    /// Gets the current playback state of the output device.
    /// </summary>
    [JsonIgnore]
    public PlaybackState State => outputDevice.PlaybackState;

    /// <summary>
    /// Gets or sets the current playback position within the audio stream.
    /// </summary>
    [JsonIgnore]
    public TimeSpan CurrentTime
    {
        get => waveStream.CurrentTime;
        set => Seek(value);
    }

    /// <summary>
    /// Gets the total duration of the audio represented by the wave stream.
    /// </summary>
    [JsonIgnore]
    public TimeSpan Duration => waveStream.TotalTime;

    /// <summary>
    /// Gets or sets a value indicating whether the playback is set to loop.
    /// </summary>
    public bool IsLooping { get; set; }

    /// <summary>
    /// Gets or sets the volume of the audio output.
    /// 0.0 is silent, 1.0 is full volume.
    /// </summary>
    public float Volume
    {
        get => volumeProvider?.Volume ?? 1.0f;
        set
        {
            if (volumeProvider != null)
                volumeProvider.Volume = Math.Clamp(value, 0.0f, 1.0f);
        }
    }

    private float _pan;

    /// <summary>
    /// Gets or sets the stereo pan position of the audio output.
    /// -1 is full left, 0 is center, and 1 is full right.
    /// </summary>
    public float Pan
    {
        get => _pan;
        set
        {
            _pan = Math.Clamp(value, -1f, 1f);

            if (monoPanProvider != null)
                monoPanProvider.Pan = _pan;
            else if (stereoPanProvider != null)
                ApplyStereoPan(stereoPanProvider, _pan);
        }
    }

    /// <summary>
    /// Starts playback of the audio stream.
    /// </summary>
    /// <remarks>If the audio is already playing, calling this method has no effect.  Ensure the audio stream
    /// is properly initialized before invoking this method.</remarks>
    /// <param name="fromStart">A value indicating whether playback should start from the beginning of the audio stream.  <see langword="true"/>
    /// to start from the beginning; otherwise, playback resumes from the current position.</param>
    public void Play(bool fromStart = true)
    {
        if (fromStart)
            waveStream.Position = 0;

        if (!IsPlaying)
            outputDevice.Play();
    }

    /// <summary>
    /// Pauses playback if it is currently active.
    /// </summary>
    /// <remarks>This method pauses the playback only if it is currently in progress.  If playback is already
    /// paused or not started, calling this method has no effect.</remarks>
    public void Pause()
    {
        if (IsPlaying)
            outputDevice.Pause();
    }

    /// <summary>
    /// Resumes playback if the output device is currently paused.
    /// </summary>
    /// <remarks>This method has no effect if the output device is not paused. Ensure that the output device
    /// is properly initialized and in a paused state before calling this method.</remarks>
    public void Resume()
    {
        if (IsPaused)
            outputDevice.Play();
    }

    /// <summary>
    /// Seeks to the specified position within the audio stream.
    /// </summary>
    /// <remarks>If the audio is currently playing, it will be paused during the seek operation and resumed
    /// afterward.</remarks>
    /// <param name="position">The position to seek to, specified as a <see cref="TimeSpan"/>.  If the value is less than <see
    /// cref="TimeSpan.Zero"/>, the position is set to the start of the stream.  If the value exceeds the total duration
    /// of the stream, the position is set to the end of the stream.</param>
    public void Seek(TimeSpan position)
    {
        if (position < TimeSpan.Zero)
            position = TimeSpan.Zero;

        if (position > waveStream.TotalTime)
            position = waveStream.TotalTime;

        var wasPlaying = IsPlaying;

        Pause();
        waveStream.CurrentTime = position;

        if (wasPlaying)
            Resume();
    }

    /// <summary>
    /// Stops the output device, halting any ongoing audio playback.
    /// </summary>
    public void Stop() => outputDevice.Stop();

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            Engine.Logger.LogError(e.Exception, "PlaybackStopped due to error for sound: {Key}\r\n{ErrorDescription}", Key, e.ToString());
        }
        else
        {
            HandlePlaybackStopped();
        }
    }

    private void HandlePlaybackStopped()
    {
        try
        {
            if (IsLooping)
            {
                Play();
            }
            else
            {
                if (PlaybackCompletedAsync is not null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await PlaybackCompletedAsync();
                        }
                        catch (Exception ex)
                        {
                            Engine.Logger.LogError(ex, "PlaybackCompletedAsync threw an exception for sound resource: {Key}", Key);
                        }
                    });
                }

                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Engine.Logger.LogError(ex, "Error during playback completion handling for sound resource: {Key}", Key);
        }
    }

    private static void ApplyStereoPan(StereoPanSampleProvider s, float pan)
    {
        pan = Math.Clamp(pan, -1f, 1f);
        // equal-power: map [-1..1] to [0..pi/2]
        float angle = (pan + 1f) * 0.5f * (float)(Math.PI / 2);
        s.LeftVolume = MathF.Cos(angle);
        s.RightVolume = MathF.Sin(angle);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~SoundResource() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
        {
            try
            {
                outputDevice.PlaybackStopped -= OnPlaybackStopped;
            }
            catch
            {
                /* noop */
            }

            Stop();
        }

        outputDevice.Dispose();
        waveStream.Dispose();

        if (TempFilePath is not null)
        {
            try
            {
                File.Delete(TempFilePath);
            }
            catch (Exception ex)
            {
                Engine.Logger.LogError(ex, "Failed to delete temporary file {TempFilePath} for sound resource {Key}", TempFilePath, Key);
            }
        }

        disposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
    }
}