using NAudio.Wave;

namespace Gondwana.Audio;

/// <summary>
/// Simple stereo balancer: multiplies L/R samples independently.
/// Expects stereo IEEE float (NAudio will convert upstream via ToSampleProvider()).
/// </summary>
public sealed class StereoPanSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public StereoPanSampleProvider(ISampleProvider source)
    {
        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("StereoPanSampleProvider requires a stereo source.");

        if (source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("StereoPanSampleProvider requires IEEE float samples (use ToSampleProvider first).");

        _source = source;
        LeftVolume = 1f;
        RightVolume = 1f;
    }

    public float LeftVolume { get; set; }  // 0..1 (you can allow >1 if you want gain)
    public float RightVolume { get; set; }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        // count is number of floats (interleaved stereo)
        int read = _source.Read(buffer, offset, count);
        // apply per-channel gain
        for (int i = 0; i < read; i += 2)
        {
            buffer[offset + i] *= LeftVolume;  // L
            if (i + 1 < read)
                buffer[offset + i + 1] *= RightVolume; // R
        }
        return read;
    }
}