using System.Runtime.Serialization;
using NAudio.Wave;
using Gondwana.Resource;

namespace Gondwana.Media;

[DataContract(IsReference = true)]
public class MediaFile : IDisposable
{
    private IWavePlayer? outputDevice;
    private AudioFileReader? audioFile;
    private readonly bool isTempFile;
    private bool disposed;

    public string FilePath { get; }
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public bool Looping { get; set; }

    public float Volume
    {
        get => audioFile?.Volume ?? 1.0f;
        set { if (audioFile != null) audioFile.Volume = Math.Clamp(value, 0.0f, 1.0f); }
    }

    public event EventHandler? PlaybackStarted;
    public event EventHandler? PlaybackPaused;
    public event EventHandler? PlaybackStopped;

    public MediaFile(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public MediaFile(EngineResourceFileIdentifier resId)
    {
        ArgumentNullException.ThrowIfNull(resId);
        ArgumentNullException.ThrowIfNull(resId.Data);

        string extension = Path.GetExtension(resId.ResourceName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".wav";

        FilePath = SaveStreamToTempFile(resId.Data, extension);
        isTempFile = true;
    }

    private static string SaveStreamToTempFile(Stream input, string extension)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        using var fs = File.Create(tempPath);
        input.CopyTo(fs);
        return tempPath;
    }

    public void Play()
    {
        Stop();

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
            Play(); // Recursion is safe because Stop() clears state.
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~MediaFile() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;
        if (disposing) Stop();

        if (isTempFile && File.Exists(FilePath))
        {
            try { File.Delete(FilePath); } catch { /* ignore */ }
        }

        disposed = true;
    }
}
