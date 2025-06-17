using NAudio.Wave;
using System.Runtime.Serialization;

namespace Gondwana.Media;

[DataContract(IsReference = true)]
public class MediaFile : IDisposable
{
    private IWavePlayer outputDevice;
    private AudioFileReader audioFile;

    public string FilePath { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public bool Looping { get; set; }

    public float Volume
    {
        get => audioFile?.Volume ?? 1.0f;
        set
        {
            if (audioFile != null)
                audioFile.Volume = Math.Clamp(value, 0.0f, 1.0f);
        }
    }

    // Events
    public event EventHandler PlaybackStarted;
    public event EventHandler PlaybackPaused;
    public event EventHandler PlaybackStopped;

    public MediaFile(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public void Play()
    {
        Stop(); // Ensure clean state

        audioFile = new AudioFileReader(FilePath);
        outputDevice = new WaveOutEvent();
        outputDevice.Init(audioFile);

        outputDevice.PlaybackStopped += OnPlaybackStopped;
        outputDevice.Play();

        IsPlaying = true;
        IsPaused = false;

        PlaybackStarted?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        if (IsPlaying && !IsPaused)
        {
            outputDevice?.Pause();
            IsPaused = true;
            PlaybackPaused?.Invoke(this, EventArgs.Empty);
        }
        else if (IsPaused)
        {
            outputDevice?.Play();
            IsPaused = false;
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Stop()
    {
        outputDevice?.Stop();
        Cleanup();

        IsPlaying = false;
        IsPaused = false;

        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlaybackStopped(object sender, StoppedEventArgs e)
    {
        if (Looping)
        {
            Play(); // Replay from start
        }
        else
        {
            IsPlaying = false;
            IsPaused = false;
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Cleanup()
    {
        outputDevice?.Dispose();
        audioFile?.Dispose();
        outputDevice = null;
        audioFile = null;
    }

    public void Dispose()
    {
        Stop();
    }
}
