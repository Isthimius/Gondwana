using NAudio.Wave;

namespace Gondwana.Audio.Midi;

public class WaveProviderToWaveStream : WaveStream
{
    private readonly IWaveProvider source;
    private readonly WaveFormat waveFormat;

    private long position;

    public WaveProviderToWaveStream(IWaveProvider source)
    {
        this.source = source;
        this.waveFormat = source.WaveFormat;
    }

    public override WaveFormat WaveFormat => waveFormat;

    public override long Length => long.MaxValue; // Unknown length

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException("Seeking not supported");
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        position += read;
        return read;
    }
}
