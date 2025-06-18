using System.Runtime.Serialization;
using NAudio.Wave;
using Gondwana.Resource;

namespace Gondwana.Media;

[DataContract(IsReference = true)]
public class MediaFile : IDisposable
{
    private IWavePlayer? outputDevice;
    private AudioFileReader? audioFile;
    private readonly bool isTempFile = false;       // only set in constructgor
    private bool disposed = false;                  // to detect redundant calls

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
    public event EventHandler? PlaybackStarted;
    public event EventHandler? PlaybackPaused;
    public event EventHandler? PlaybackStopped;

    public MediaFile(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public MediaFile(EngineResourceFileIdentifier resId)
    {
        if (resId == null || resId.Data == null)
            throw new ArgumentException("Invalid resource identifier or empty data stream.");

        string extension = Path.GetExtension(resId.ResourceName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".wav"; // default if unknown

        FilePath = SaveStreamToTempFile(resId.Data, extension);
        isTempFile = true;
    }

    private static string SaveStreamToTempFile(Stream inputStream, string extension)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + extension);
        using var fileStream = File.Create(tempPath);
        inputStream.CopyTo(fileStream);
        return tempPath;
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

        PlaybackStarted?.Invoke(this, System.EventArgs.Empty);
    }

    public void Pause()
    {
        if (IsPlaying && !IsPaused)
        {
            outputDevice?.Pause();
            IsPaused = true;
            PlaybackPaused?.Invoke(this, System.EventArgs.Empty);
        }
        else if (IsPaused)
        {
            outputDevice?.Play();
            IsPaused = false;
            PlaybackStarted?.Invoke(this, System.EventArgs.Empty);
        }
    }

    public void Stop()
    {
        outputDevice?.Stop();
        Cleanup();

        IsPlaying = false;
        IsPaused = false;

        PlaybackStopped?.Invoke(this, System.EventArgs.Empty);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (Looping)
        {
            Play(); // Replay from start
        }
        else
        {
            IsPlaying = false;
            IsPaused = false;
            PlaybackStopped?.Invoke(this, System.EventArgs.Empty);
        }
    }

    private void Cleanup()
    {
        outputDevice?.Dispose();
        audioFile?.Dispose();
        outputDevice = null;
        audioFile = null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                Stop();
            }

            // Dispose unmanaged resources
            if (isTempFile && File.Exists(FilePath))
            {
                try { File.Delete(FilePath); } catch { /* ignore */ }
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~MediaFile()
    {
        Dispose(disposing: false);
    }
}
