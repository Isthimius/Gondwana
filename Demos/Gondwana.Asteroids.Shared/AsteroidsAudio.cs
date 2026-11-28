using Gondwana.Audio;

namespace Gondwana.Demos.Asteroids;

/// <summary>Original procedural arcade sound effects and simulation-driven ambience.</summary>
internal sealed class AsteroidsAudio : IDisposable
{
    private readonly Dictionary<AsteroidsGame.Sound, AudioResource> _sounds = [];
    private bool _thrustPlaying;
    private bool _saucerPlaying;
    private bool _highBeat;
    private double _beatDelay;

    internal bool Available => _sounds.Count > 0;
    internal bool Muted { get; private set; }

    internal void Load()
    {
        var manager = Engine.Instance.Managers.AudioResources;

        if (!manager.IsBackendConfigured)
            return;

        foreach (var sound in Enum.GetValues<AsteroidsGame.Sound>())
        {
            using var wave = CreateWave(sound);
            var resource = manager.LoadFromStream($"asteroids-{sound}", wave, ".wav", 0.35f);
            _sounds.Add(sound, resource);
            resource.IsLooping = sound is AsteroidsGame.Sound.Thrust or AsteroidsGame.Sound.Saucer;
        }
    }

    internal void Play(AsteroidsGame.Sound sound)
    {
        if (!Muted && _sounds.TryGetValue(sound, out var resource))
            resource.Play();
    }

    internal void ToggleMute()
    {
        Muted = !Muted;

        if (Muted)
            StopAll();
    }

    internal void Update(AsteroidsGame game, double deltaSeconds)
    {
        if (!Available || Muted)
            return;

        bool playing = game.State == AsteroidsGame.Phase.Playing;
        SetLoop(AsteroidsGame.Sound.Thrust, playing && game.Thrusting, ref _thrustPlaying);
        SetLoop(AsteroidsGame.Sound.Saucer,
            playing && game.Bodies.Any(body => body.Type == AsteroidsGame.Kind.Saucer), ref _saucerPlaying);

        if (!playing)
        {
            _beatDelay = 0d;
            _highBeat = false;
            return;
        }

        _beatDelay -= deltaSeconds;

        if (_beatDelay > 0d)
            return;

        Play(_highBeat ? AsteroidsGame.Sound.BeatHigh : AsteroidsGame.Sound.BeatLow);
        _highBeat = !_highBeat;
        int rocks = game.Bodies.Count(body => body.Type == AsteroidsGame.Kind.Rock);
        _beatDelay = Math.Clamp(0.20d + rocks * 0.035d - (game.Wave - 1) * 0.025d, 0.16d, 0.72d);
    }

    private void SetLoop(AsteroidsGame.Sound sound, bool desired, ref bool active)
    {
        if (desired == active)
            return;

        if (desired)
            Play(sound);
        else if (_sounds.TryGetValue(sound, out var resource))
            resource.Stop();

        active = desired;
    }

    internal void StopAll()
    {
        foreach (var resource in _sounds.Values)
            resource.Stop();

        _thrustPlaying = false;
        _saucerPlaying = false;
        _highBeat = false;
        _beatDelay = 0d;
    }

    internal static MemoryStream CreateWave(AsteroidsGame.Sound sound)
    {
        const int sampleRate = 22050;
        // Duration, start/end pitch, noise mix and amplitude are deliberately
        // distinct for the different original arcade-inspired effects.
        (float duration, float startHz, float endHz, float noiseMix, float amplitude) = sound switch
        {
            AsteroidsGame.Sound.PlayerShot => (0.14f, 1350f, 230f, 0.05f, 0.70f),
            AsteroidsGame.Sound.EnemyShot => (0.19f, 320f, 1050f, 0.08f, 0.55f),
            AsteroidsGame.Sound.LargeExplosion => (0.45f, 100f, 35f, 0.90f, 0.85f),
            AsteroidsGame.Sound.MediumExplosion => (0.30f, 160f, 55f, 0.85f, 0.73f),
            AsteroidsGame.Sound.SmallExplosion => (0.18f, 260f, 100f, 0.80f, 0.65f),
            AsteroidsGame.Sound.SaucerExplosion => (0.38f, 480f, 50f, 0.75f, 0.80f),
            AsteroidsGame.Sound.ShipExplosion => (0.65f, 140f, 25f, 0.90f, 0.95f),
            AsteroidsGame.Sound.Hyperspace => (0.28f, 240f, 1750f, 0.12f, 0.55f),
            AsteroidsGame.Sound.ExtraLife => (0.45f, 520f, 1040f, 0f, 0.55f),
            AsteroidsGame.Sound.Thrust => (0.40f, 62f, 62f, 0.85f, 0.28f),
            AsteroidsGame.Sound.Saucer => (0.40f, 440f, 440f, 0f, 0.25f),
            AsteroidsGame.Sound.BeatLow => (0.09f, 82f, 58f, 0f, 0.48f),
            AsteroidsGame.Sound.BeatHigh => (0.09f, 112f, 80f, 0f, 0.48f),
            _ => throw new ArgumentOutOfRangeException(nameof(sound))
        };

        bool looping = sound is AsteroidsGame.Sound.Thrust or AsteroidsGame.Sound.Saucer;
        int count = (int)(duration * sampleRate);
        int bytes = count * sizeof(short);
        var stream = new MemoryStream(44 + bytes);
        var random = new Random(1701 + (int)sound);
        float phase = 0f;
        float noise = 0f;

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + bytes);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(bytes);

            for (int i = 0; i < count; i++)
            {
                float progress = i / (float)(count - 1);
                float frequency = startHz + (endHz - startHz) * progress;

                if (sound == AsteroidsGame.Sound.Saucer)
                    frequency += 180f * MathF.Sin(MathF.Tau * progress * 2f);
                else if (sound == AsteroidsGame.Sound.ExtraLife)
                    frequency = startHz * (1f + 0.25f * (int)(progress * 4f));

                phase = (phase + frequency / sampleRate) % 1f;
                float tone = phase < 0.5f ? 1f : -1f;
                noise += (random.NextSingle() * 2f - 1f - noise) * 0.23f;
                float edge = Math.Clamp(Math.Min(i, count - 1 - i) / 100f, 0f, 1f);
                float envelope = looping ? edge : edge * (1f - progress);
                float sample = (tone * (1f - noiseMix) + noise * noiseMix) * amplitude * envelope;
                writer.Write((short)(sample * short.MaxValue));
            }
        }

        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        foreach (var resource in _sounds.Values)
            resource.Dispose();

        _sounds.Clear();
    }
}
