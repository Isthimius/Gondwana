using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Gondwana.Audio;

[DataContract(IsReference = true)]
public class SoundResource : IDisposable
{
    private readonly IWavePlayer outputDevice;
    private readonly WaveStream waveStream;
    private VolumeSampleProvider volumeProvider;
    private PanningSampleProvider panningProvider;
    private bool disposed;

    public event EventHandler PlaybackCompleted;
    public Func<Task>? PlaybackCompletedAsync;
    public event EventHandler Disposed;

    private SoundResource() { }

    internal SoundResource(WaveStream soundStream, float volume = 1.0f, float pan = 0.0f, string? filePath = null, bool isTempFile = false)
    {
        waveStream = soundStream;
        outputDevice = new WaveOutEvent();
        outputDevice.Init(BuildAudioGraph(soundStream, volume, pan));
        outputDevice.PlaybackStopped += OnPlaybackStopped;
        FilePath = filePath;
        IsTempFile = isTempFile;
    }

    private ISampleProvider BuildAudioGraph(WaveStream source, float volume, float pan)
    {
        ISampleProvider baseProvider = source.ToSampleProvider();

        if (baseProvider.WaveFormat.Channels == 1)
            baseProvider = new MonoToStereoSampleProvider(baseProvider);

        volumeProvider = new VolumeSampleProvider(baseProvider) { Volume = volume };
        panningProvider = new PanningSampleProvider(volumeProvider) { Pan = pan };

        return panningProvider;
    }

    [JsonIgnore]
    public string? FilePath { get; } = null;

    [JsonIgnore]
    public bool IsTempFile { get; } = false;

    [JsonIgnore]
    public bool IsPaused => outputDevice.PlaybackState == PlaybackState.Paused;

    [JsonIgnore]
    public bool IsPlaying => outputDevice.PlaybackState == PlaybackState.Playing;

    [JsonIgnore]
    public TimeSpan CurrentTime
    { 
        get => waveStream.CurrentTime;
        set => Seek(value);
    }

    [JsonIgnore]
    public TimeSpan Duration => waveStream.TotalTime;

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

    public void Play(bool fromStart = true)
    {
        if (fromStart)
            waveStream.Position = 0;

        if (!IsPlaying)
            outputDevice.Play();
    }

    public void Pause()
    {
        if (IsPlaying)
            outputDevice.Pause();
    }

    public void Resume()
    {
        if (IsPaused)
            outputDevice.Play();
    }

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
                PlaybackCompleted?.Invoke(this, EventArgs.Empty);

                if (PlaybackCompletedAsync is not null)
                    await PlaybackCompletedAsync();
            }
        }
        catch (Exception ex)
        {
            // TODO: Logging
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
            try { File.Delete(FilePath); }
            catch { /* ignore */ }
        }

        disposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
    }
}
