using Gondwana.Audio.NAudio;
using NAudio.Wave;

namespace Gondwana.Tests;

public sealed class NAudioControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stop_RewindsAfterWorkerExit_AndQueuesResume(bool paused)
    {
        var device = new Device();
        using var stream = new RawSourceWaveStream(new MemoryStream(new byte[32000]), new WaveFormat(8000, 16, 1));
        using var handle = new NAudioPlaybackHandle("test", stream, null, 1, 0, 1, device);
        var completions = 0;
        handle.PlaybackCompleted += (_, _) => completions++;
        handle.Play();
        handle.Seek(TimeSpan.FromSeconds(1));
        if (paused) handle.Pause();
        handle.Stop();
        Assert.Equal(TimeSpan.Zero, handle.CurrentTime);
        Assert.Equal(TimeSpan.FromSeconds(1), stream.CurrentTime); // Worker still owns stream.
        var playCalls = device.PlayCalls;
        handle.Play(false);
        Assert.Equal(playCalls, device.PlayCalls);
        device.CompleteStop();
        Assert.Equal(TimeSpan.Zero, stream.CurrentTime);
        Assert.Equal(playCalls + 1, device.PlayCalls);
        Assert.Equal(0, completions);
    }

    [Fact]
    public void Stop_AlreadyStopped_Rewinds()
    {
        var device = new Device();
        using var stream = new RawSourceWaveStream(new MemoryStream(new byte[32000]), new WaveFormat(8000, 16, 1));
        using var handle = new NAudioPlaybackHandle("test", stream, null, 1, 0, 1, device);
        handle.Seek(TimeSpan.FromSeconds(1));
        handle.Stop();
        Assert.Equal(TimeSpan.Zero, stream.CurrentTime);
        handle.Play(false);
        Assert.Equal(TimeSpan.Zero, handle.CurrentTime);
    }

    [Fact]
    public void SeekAfterStop_IsAppliedBeforeQueuedPlay()
    {
        var device = new Device();
        using var stream = new RawSourceWaveStream(new MemoryStream(new byte[32000]), new WaveFormat(8000, 16, 1));
        using var handle = new NAudioPlaybackHandle("test", stream, null, 1, 0, 1, device);
        handle.Play();
        handle.Stop();
        handle.Seek(TimeSpan.FromSeconds(.5));
        handle.Play(false);
        device.CompleteStop();
        Assert.Equal(TimeSpan.FromSeconds(.5), handle.CurrentTime);
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData(".MP3")]
    [InlineData("music.mp3")]
    [InlineData("assets/music.MP3")]
    public void Supports_AcceptsDocumentedExtensionForms(string source)
        => Assert.True(NAudioReaderRegistry.Supports(source));

    [Theory]
    [InlineData("folder/noextension")]
    [InlineData("folder\\noextension")]
    public void Supports_DoesNotTreatPathsAsBareExtensions(string source)
        => Assert.Throws<InvalidOperationException>(() => NAudioReaderRegistry.Supports(source));

    private sealed class Device : IWavePlayer
    {
        public PlaybackState PlaybackState { get; private set; }
        public float Volume { get; set; }
        public WaveFormat OutputWaveFormat { get; private set; } = new WaveFormat();
        public int PlayCalls { get; private set; }
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public void Init(IWaveProvider provider) => OutputWaveFormat = provider.WaveFormat;
        public void Play() { PlaybackState = PlaybackState.Playing; PlayCalls++; }
        public void Pause() => PlaybackState = PlaybackState.Paused;
        public void Stop() => PlaybackState = PlaybackState.Stopped;
        public void CompleteStop() => PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        public void Dispose() { }
    }
}
