using Gondwana.Audio.NAudio;
using NAudio.Wave;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for n audio control.
/// </summary>
public sealed class NAudioControlTests
{
    /// <summary>
    /// Verifies stop rewinds after worker exit and queues resume.
    /// </summary>
    /// <param name="paused">The paused value for this test case.</param>
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

    /// <summary>
    /// Verifies stop already stopped rewinds.
    /// </summary>
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

    /// <summary>
    /// Verifies seek after stop is applied before queued play.
    /// </summary>
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

    /// <summary>
    /// Verifies supports accepts documented extension forms.
    /// </summary>
    /// <param name="source">The source value for this test case.</param>
    [Theory]
    [InlineData("mp3")]
    [InlineData(".MP3")]
    [InlineData("music.mp3")]
    [InlineData("assets/music.MP3")]
    public void Supports_AcceptsDocumentedExtensionForms(string source)
        => Assert.True(NAudioReaderRegistry.Supports(source));

    /// <summary>
    /// Verifies supports does not treat paths as bare extensions.
    /// </summary>
    /// <param name="source">The source value for this test case.</param>
    [Theory]
    [InlineData("folder/noextension")]
    [InlineData("folder\\noextension")]
    public void Supports_DoesNotTreatPathsAsBareExtensions(string source)
        => Assert.Throws<InvalidOperationException>(() => NAudioReaderRegistry.Supports(source));

    private sealed class Device : IWavePlayer
    {
        /// <summary>
        /// Gets the playback state.
        /// </summary>
        public PlaybackState PlaybackState { get; private set; }
        /// <summary>
        /// Gets or sets the playback volume.
        /// </summary>
        public float Volume { get; set; }
        /// <summary>
        /// Gets the output wave format.
        /// </summary>
        public WaveFormat OutputWaveFormat { get; private set; } = new WaveFormat();
        /// <summary>
        /// Gets the play calls.
        /// </summary>
        public int PlayCalls { get; private set; }
        /// <summary>
        /// Occurs when playback stopped.
        /// </summary>
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        /// <summary>
        /// Initializes the test audio player with a wave provider.
        /// </summary>
        /// <param name="provider">The provider value for this test case.</param>
        public void Init(IWaveProvider provider) => OutputWaveFormat = provider.WaveFormat;
        /// <summary>
        /// Starts playback.
        /// </summary>
        public void Play() { PlaybackState = PlaybackState.Playing; PlayCalls++; }
        /// <summary>
        /// Pauses playback at the current position.
        /// </summary>
        public void Pause() => PlaybackState = PlaybackState.Paused;
        /// <summary>
        /// Stops playback.
        /// </summary>
        public void Stop() => PlaybackState = PlaybackState.Stopped;
        /// <summary>
        /// Simulates completion of a stop request.
        /// </summary>
        public void CompleteStop() => PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        /// <summary>
        /// Releases the resources owned by this instance.
        /// </summary>
        public void Dispose() { }
    }
}
