using Gondwana.Audio.NAudio;
using NAudio.Wave;

namespace Gondwana.Tests;

public sealed class AudioSampleProviderTests
{
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
        public int Position { get; set; }
        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        public int Read(float[] buffer, int offset, int count)
        {
            var read = Math.Min(count, samples.Length - Position);
            Array.Copy(samples, Position, buffer, offset, read);
            Position += read;
            return read;
        }
    }
}
