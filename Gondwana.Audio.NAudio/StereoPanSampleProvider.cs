using NAudio.Wave;

namespace Gondwana.Audio.NAudio;

/// <summary>Applies independent left/right gain to a stereo floating-point source.</summary>
internal sealed class StereoPanSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public StereoPanSampleProvider(ISampleProvider source)
    {
        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("StereoPanSampleProvider requires a stereo source.", nameof(source));

        _source = source;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;
    public float LeftVolume { get; set; } = 1.0f;
    public float RightVolume { get; set; } = 1.0f;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        for (var index = 0; index + 1 < read; index += 2)
        {
            buffer[offset + index] *= LeftVolume;
            buffer[offset + index + 1] *= RightVolume;
        }

        return read;
    }
}
