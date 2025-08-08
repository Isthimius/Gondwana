using NAudio.Wave;

namespace Gondwana.Audio.Midi;

public class WaveProviderToWaveStream : WaveStream
{
    private readonly IWaveProvider source;
    private readonly WaveFormat waveFormat;
    private long position;
    private readonly long _midiLengthBytes;

    // Delegate for seeking into the underlying synthesizer
    private readonly Action<TimeSpan>? _seekHandler;

    public WaveProviderToWaveStream(IWaveProvider source, double durationSeconds, Action<TimeSpan>? seekHandler)
    {
        this.source = source;
        this.waveFormat = source.WaveFormat;
        _midiLengthBytes = (long)(durationSeconds * waveFormat.AverageBytesPerSecond);
        _seekHandler = seekHandler;
    }

    public override WaveFormat WaveFormat => waveFormat;

    public override long Length => _midiLengthBytes;

    public override long Position
    {
        get => position;
        set
        {
            if (_seekHandler == null)
                throw new NotSupportedException("Seeking is not supported for this stream.");

            position = value;
            var time = TimeSpan.FromSeconds((double)position / waveFormat.AverageBytesPerSecond);
            _seekHandler?.Invoke(time); // hook into synth seeking
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        position += read;
        return read;
    }
}
