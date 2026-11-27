using Gondwana.Audio;

namespace Gondwana.Demos.Adventure;

/// <summary>Original synthesized square-wave effects; no sampled or external game audio.</summary>
internal sealed class AdventureAudio : IDisposable
{
    private readonly Dictionary<AdventureGame.Sound, AudioResource> _sounds = [];

    internal bool Available => _sounds.Count != 0;
    internal bool Muted { get; set; }

    internal void Load()
    {
        var manager = Engine.Instance.Managers.AudioResources;

        if (!manager.IsBackendConfigured)
            return;

        foreach (AdventureGame.Sound sound in Enum.GetValues<AdventureGame.Sound>())
        {
            using var wav = CreateWave(sound);
            _sounds.Add(sound, manager.LoadFromStream($"adventure-{sound}", wav, ".wav", 0.35f));
        }
    }

    internal void Play(AdventureGame.Sound sound)
    {
        if (!Muted && _sounds.TryGetValue(sound, out var resource))
            resource.Play();
    }

    internal static MemoryStream CreateWave(AdventureGame.Sound sound)
    {
        int[] tones = sound switch
        {
            AdventureGame.Sound.Pickup => [420, 660],
            AdventureGame.Sound.Drop => [300, 180],
            AdventureGame.Sound.Gate => [120, 190, 260, 350],
            AdventureGame.Sound.Dragon => [220, 155, 90, 55],
            AdventureGame.Sound.Slay => [110, 420, 780],
            AdventureGame.Sound.Bat => [880, 460, 880],
            AdventureGame.Sound.Win => [330, 440, 550, 660, 880],
            _ => [260, 390, 520]
        };
        const int rate = 22050;
        const int samplesPerTone = 1654;
        int dataLength = tones.Length * samplesPerTone * sizeof(short);
        var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataLength);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(rate);
            writer.Write(rate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataLength);

            foreach (int frequency in tones)
            {
                for (int i = 0; i < samplesPerTone; i++)
                {
                    float envelope = Math.Min(1f, Math.Min(i, samplesPerTone - 1 - i) / 80f);
                    float phase = i * (float)frequency / rate;
                    writer.Write((short)((phase % 1f < 0.5f ? 9000f : -9000f) * envelope));
                }
            }
        }

        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        foreach (AudioResource resource in _sounds.Values)
            resource.Dispose();

        _sounds.Clear();
    }
}
