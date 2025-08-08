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

            // clamp
            var clamped = Math.Max(0, Math.Min(value, Length));
            // align to frame (BlockAlign is bytes per frame)
            clamped -= clamped % waveFormat.BlockAlign;

            position = clamped;

            var seconds = (double)position / waveFormat.AverageBytesPerSecond;
            _seekHandler(TimeSpan.FromSeconds(seconds));
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        position += read;
        return read;
    }
}
