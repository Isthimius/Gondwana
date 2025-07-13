using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Text.Json.Serialization;

namespace Gondwana.Audio;

/// <summary>
/// Represents a sound resource that can be played, paused, resumed, and disposed.
/// </summary>
public class SoundResource : IDisposable
{
    private readonly IWavePlayer outputDevice;
    private readonly WaveStream waveStream;
    private VolumeSampleProvider volumeProvider;
    private PanningSampleProvider panningProvider;
    private bool disposed;

    /// <summary>
    /// Event that is raised when playback completes.
    /// Will not be raised if the sound is looping.
    /// </summary>
    public event EventHandler PlaybackCompleted;

    /// <summary>
    /// Asynchronous event that is invoked when playback completes.
    /// Will not be invoked if the sound is looping.
    /// </summary>
    public Func<Task>? PlaybackCompletedAsync;

    /// <summary>
    /// Event that is raised when the sound resource is disposed.
    /// </summary>
    public event EventHandler Disposed;

    private SoundResource() { }

    internal SoundResource(string key, WaveStream soundStream, float volume = 1.0f, float pan = 0.0f, string? filePath = null, bool isTempFile = false, byte[]? rawBytes = null, string? extension = null)
    {
        Key = key;
        waveStream = soundStream;
        outputDevice = new WaveOutEvent();
        outputDevice.Init(BuildAudioGraph(soundStream, volume, pan));
        outputDevice.PlaybackStopped += OnPlaybackStopped;
        FilePath = filePath;
        IsTempFile = isTempFile;
        OriginalBytes = rawBytes;
        OriginalExtension = extension ?? Path.GetExtension(filePath ?? "") ?? ".wav";
    }

    private ISampleProvider BuildAudioGraph(WaveStream source, float volume, float pan)
    {
        ISampleProvider baseProvider = source.ToSampleProvider();

        if (baseProvider.WaveFormat.Channels > 1)
            baseProvider = new StereoToMonoSampleProvider(baseProvider);

        volumeProvider = new VolumeSampleProvider(baseProvider) { Volume = volume };
        panningProvider = new PanningSampleProvider(volumeProvider) { Pan = pan };

        return panningProvider;
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
    /// Gets the original file extension associated with the object, if available.
    /// </summary>
    [JsonIgnore]
    public string? OriginalExtension { get; private set; }

    /// <summary>
    /// Gets the file path associated with the current object.
    /// </summary>
    [JsonIgnore]
    public string? FilePath { get; } = null;

    /// <summary>
    /// Gets a value indicating whether the file is a temporary file saved from an input Stream.
    /// </summary>
    [JsonIgnore]
    public bool IsTempFile { get; } = false;

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

    /// <summary>
    /// Gets or sets the stereo pan position of the audio output.
    /// -1 is full left, 0 is center, and 1 is full right.
    /// </summary>
    public float Pan
    {
        get => panningProvider?.Pan ?? 0.0f;
        set
        {
            if (panningProvider != null)
                panningProvider.Pan = Math.Clamp(value, -1.0f, 1.0f);
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
    /// <remarks>This method stops the audio output immediately. Ensure that any necessary cleanup or state
    /// management is handled before calling this method, as it does not automatically reset or dispose of the output
    /// device.</remarks>
    public void Stop() => outputDevice.Stop();

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _ = HandlePlaybackStoppedAsync();
    }

    private async Task HandlePlaybackStoppedAsync()
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
                    await PlaybackCompletedAsync();

                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Engine.Logger.LogError(ex, "Error during playback completion handling for sound resource: {Key}", Key);
            throw;
        }
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
            Stop();

        outputDevice.Dispose();
        waveStream.Dispose();

        if (IsTempFile && File.Exists(FilePath))
        {
            try
            {
                File.Delete(FilePath);
            }
            catch (Exception ex)
            {
                Engine.Logger.LogWarning(ex, "Failed to delete temporary sound file: {FilePath} for SoundResource: {Key}", FilePath, Key);
            }
        }

        disposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
    }
}
