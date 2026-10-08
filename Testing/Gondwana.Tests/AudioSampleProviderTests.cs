using Gondwana.Audio.NAudio;
using NAudio.Wave;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for audio sample provider.
/// </summary>
public sealed class AudioSampleProviderTests
{
    /// <summary>
    /// Verifies speed changes frame count and keeps stereo channels aligned.
    /// </summary>
    /// <param name="speed">The speed value for this test case.</param>
    /// <param name="expectedFrames">The expected frames value for this test case.</param>
    [Theory]
    [InlineData(.25f, 32)]
    [InlineData(1f, 8)]
    [InlineData(4f, 2)]
    public void SpeedChangesFrameCountAndKeepsStereoChannelsAligned(float speed, int expectedFrames)
    {
        var source = new Samples(Enumerable.Range(0, 8).SelectMany(i => new[] { (float)i, i + 10f }).ToArray());
        var provider = new VariableSpeedSampleProvider(source) { PlaybackSpeed = speed };
        var buffer = new float[100];
        var read = provider.Read(buffer, 2, 96);
        Assert.Equal(expectedFrames * 2, read);
        for (var frame = 0; frame < expectedFrames; frame++)
        {
            Assert.Equal(Math.Min(frame * speed, 7), buffer[2 + frame * 2]);
            Assert.Equal(buffer[2 + frame * 2] + 10, buffer[3 + frame * 2]);
        }
        Assert.Equal(0, provider.Read(buffer, 0, buffer.Length));
        provider.Reset(() => source.Position = 0);
        Assert.Equal(read, provider.Read(buffer, 2, 96));
    }

    /// <summary>
    /// Verifies reads across chunks retain interpolation and pan respects offset.
    /// </summary>
    [Fact]
    public void ReadsAcrossChunksRetainInterpolationAndPanRespectsOffset()
    {
        var provider = new VariableSpeedSampleProvider(new Samples([0, 10, 2, 12])) { PlaybackSpeed = .5f };
        var pan = new StereoPanSampleProvider(provider) { LeftVolume = 0, RightVolume = 1 };
        var buffer = new float[] { -1, -1, -1, -1 };
        Assert.Equal(2, pan.Read(buffer, 1, 2));
        Assert.Equal(new float[] { -1, 0, 10, -1 }, buffer);
        Assert.Equal(2, pan.Read(buffer, 1, 2));
        Assert.Equal(new float[] { -1, 0, 11, -1 }, buffer);
    }

    private sealed class Samples(float[] samples) : ISampleProvider
    {
        /// <summary>
        /// Gets or sets the position.
        /// </summary>
        public int Position { get; set; }
        /// <summary>
        /// Gets the wave format.
        /// </summary>
        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        /// <summary>
        /// Reads an Aseprite file.
        /// </summary>
        /// <param name="buffer">The buffer value for this test case.</param>
        /// <param name="offset">The offset value for this test case.</param>
        /// <param name="count">The count value for this test case.</param>
        /// <returns>The read.</returns>
        public int Read(float[] buffer, int offset, int count)
        {
            var read = Math.Min(count, samples.Length - Position);
            Array.Copy(samples, Position, buffer, offset, read);
            Position += read;
            return read;
        }
    }
}
