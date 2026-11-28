using System.Numerics;

namespace Gondwana.Demos.Asteroids;

/// <summary>Host-independent simulation in logical world pixels.</summary>
internal sealed class AsteroidsGame
{
    internal const int Width = 1280;
    internal const int Height = 720;
    internal const int TileSize = 40;
    private readonly Random _random;
    private readonly List<Body> _bodies = [];
    private int _nextId;
    private float _fireDelay;
    private float _saucerDelay;
    private float _saucerFireDelay;
    private float _respawnDelay;
    private float _invulnerability;
    private float _waveDelay;
    private int _nextExtraLife;

    internal AsteroidsGame(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        Ship = NewBody(Kind.Ship, Center, Vector2.Zero, 18f);
        Reset();
    }

    internal event Action<Sound>? SoundRequested;

    internal static Vector2 Center => new(Width / 2f, Height / 2f);
    internal Body Ship { get; }
    internal IReadOnlyList<Body> Bodies => _bodies;
    internal int Score { get; private set; }
    internal int HighScore { get; private set; }
    internal int Lives { get; private set; }
    internal int Wave { get; private set; }
    internal Phase State { get; private set; }
    internal bool Thrusting { get; private set; }
    internal bool ShipVisible => _respawnDelay <= 0f && State != Phase.GameOver &&
        (_invulnerability <= 0f || (int)(_invulnerability * 10f) % 2 == 0);

    internal void Reset()
    {
        _bodies.Clear();
        Score = 0;
        Lives = 3;
        Wave = 0;
        _nextExtraLife = 10000;
        _fireDelay = 0f;
        _waveDelay = 0f;
        _respawnDelay = 0f;
        _invulnerability = 0f;
        Thrusting = false;
        Ship.Position = Center;
        Ship.Velocity = Vector2.Zero;
        Ship.Angle = -MathF.PI / 2f;
        State = Phase.Ready;
    }

    internal void Start()
    {
        if (State == Phase.GameOver)
            Reset();

        if (State != Phase.Ready)
            return;

        State = Phase.Playing;
        StartWave();
        Respawn();
    }

    internal void TogglePause()
    {
        State = State switch
        {
            Phase.Playing => Phase.Paused,
            Phase.Paused => Phase.Playing,
            _ => State
        };
    }

    internal void Hyperspace()
    {
        if (State != Phase.Playing || _respawnDelay > 0f)
            return;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2 candidate = RandomPosition();

            if (!IsSafe(candidate))
                continue;

            Ship.Position = candidate;
            Ship.Velocity = Vector2.Zero;
            _invulnerability = Math.Max(_invulnerability, 0.5f);
            SoundRequested?.Invoke(Sound.Hyperspace);
            return;
        }
    }

    internal void Update(float elapsed, float turn, bool thrust, bool fire)
    {
        if (State != Phase.Playing)
            return;

        float dt = Math.Clamp(elapsed, 0f, 0.05f);
        int steps = Math.Max(1, (int)MathF.Ceiling(dt * 120f));

        for (int i = 0; i < steps && State == Phase.Playing; i++)
            Step(dt / steps, Math.Clamp(turn, -1f, 1f), thrust, fire);
    }

    private void Step(float dt, float turn, bool thrust, bool fire)
    {
        _invulnerability = Math.Max(0f, _invulnerability - dt);
        _fireDelay = Math.Max(0f, _fireDelay - dt);
        Thrusting = thrust && _respawnDelay <= 0f;

        if (_respawnDelay > 0f)
        {
            _respawnDelay = Math.Max(0f, _respawnDelay - dt);

            if (_respawnDelay == 0f)
                Respawn();
        }
        else
        {
            Ship.Angle = (Ship.Angle + turn * 4.5f * dt) % MathF.Tau;

            if (thrust)
                Ship.Velocity += Direction(Ship.Angle) * 330f * dt;

            Ship.Velocity *= MathF.Exp(-0.18f * dt);
            float speed = Ship.Velocity.Length();

            if (speed > 560f)
                Ship.Velocity *= 560f / speed;

            Ship.Position = Wrap(Ship.Position + Ship.Velocity * dt);

            if (fire && _fireDelay <= 0f && _bodies.Count(b => b.Type == Kind.PlayerShot) < 6)
            {
                Vector2 heading = Direction(Ship.Angle);
                AddBody(Kind.PlayerShot, Ship.Position + heading * 20f, Ship.Velocity + heading * 650f, 3f, 0.85f);
                _fireDelay = 0.16f;
                SoundRequested?.Invoke(Sound.PlayerShot);
            }
        }

        foreach (Body body in _bodies)
        {
            body.Position = Wrap(body.Position + body.Velocity * dt);
            body.Angle = (body.Angle + body.Spin * dt) % MathF.Tau;
            body.Lifetime -= dt;
        }

        _bodies.RemoveAll(b => b.Lifetime <= 0f);
        UpdateSaucer(dt);
        ResolveCollisions();

        if (State != Phase.Playing)
            return;

        if (_bodies.Any(b => b.Type == Kind.Rock))
        {
            _waveDelay = 0f;
        }
        else
        {
            _waveDelay += dt;

            if (_waveDelay >= 1.5f)
                StartWave();
        }
    }

    private void StartWave()
    {
        Wave++;
        _waveDelay = 0f;
        _saucerDelay = 12f + _random.NextSingle() * 8f;
        _bodies.RemoveAll(b => b.Type is Kind.Saucer or Kind.EnemyShot);

        for (int i = 0; i < Math.Min(3 + Wave, 11); i++)
        {
            Vector2 position = RandomPosition();

            for (int attempt = 0; attempt < 40 && DistanceSquared(position, Ship.Position) < 220f * 220f; attempt++)
                position = RandomPosition();

            if (DistanceSquared(position, Ship.Position) < 220f * 220f)
                position = Wrap(Ship.Position + new Vector2(Width / 2f, 0f));

            AddRock(position, 3);
        }
    }

    private void AddRock(Vector2 position, int size)
    {
        float radius = size switch
        {
            3 => 48f,
            2 => 27f,
            _ => 15f
        };
        float speed = (60f + (3 - size) * 55f + Math.Min(Wave, 12) * 7f) * (0.8f + _random.NextSingle() * 0.4f);
        Body rock = AddBody(Kind.Rock, position, Direction(RandomAngle()) * speed, radius);
        rock.Size = size;
        rock.Variant = _random.Next(3);
        rock.Angle = RandomAngle();
        rock.Spin = (_random.NextSingle() - 0.5f) * 1.8f;
    }

    private void UpdateSaucer(float dt)
    {
        Body? saucer = _bodies.Find(b => b.Type == Kind.Saucer);

        if (saucer is null)
        {
            _saucerDelay -= dt;

            if (_saucerDelay > 0f)
                return;

            float direction = _random.Next(2) == 0 ? 1f : -1f;
            Vector2 position = new(direction > 0 ? 0f : Width - 1f, 100f + _random.NextSingle() * (Height - 200f));
            saucer = AddBody(Kind.Saucer, position, new Vector2(direction * 155f, 28f), 25f, 9f);
            _saucerDelay = 15f + _random.NextSingle() * 8f;
            _saucerFireDelay = 0.7f;
        }

        _saucerFireDelay -= dt;

        if (_saucerFireDelay > 0f || _respawnDelay > 0f)
            return;

        Vector2 aim = Delta(saucer.Position, Ship.Position);
        float angle = MathF.Atan2(aim.Y, aim.X) + (_random.NextSingle() - 0.5f) * 0.55f;
        AddBody(Kind.EnemyShot, saucer.Position, Direction(angle) * 310f, 4f, 2.2f);
        _saucerFireDelay = 1.4f;
        SoundRequested?.Invoke(Sound.EnemyShot);
    }

    private void ResolveCollisions()
    {
        // Snapshot candidates because rock splitting and explosions add bodies.
        Body[] targets = _bodies.Where(b => b.Type is Kind.Rock or Kind.Saucer).ToArray();

        foreach (Body shot in _bodies.Where(b => b.Type == Kind.PlayerShot).ToArray())
        {
            foreach (Body target in targets)
            {
                if (target.Lifetime <= 0f || !Overlaps(shot, target))
                    continue;

                shot.Lifetime = 0f;
                DestroyTarget(target);
                break;
            }
        }

        if (_respawnDelay <= 0f && _invulnerability <= 0f)
        {
            Body? hit = _bodies.Find(b => b.Lifetime > 0f &&
                (b.Type is Kind.Rock or Kind.Saucer or Kind.EnemyShot) && Overlaps(b, Ship));

            if (hit is not null)
            {
                if (hit.Type == Kind.EnemyShot)
                    hit.Lifetime = 0f;
                else
                    DestroyTarget(hit);

                Explode(Ship.Position, 20);
                Lives--;
                Ship.Velocity = Vector2.Zero;
                Thrusting = false;
                SoundRequested?.Invoke(Sound.ShipExplosion);

                if (Lives <= 0)
                    State = Phase.GameOver;
                else
                    _respawnDelay = 1.5f;
            }
        }

        _bodies.RemoveAll(b => b.Lifetime <= 0f);
    }

    private void DestroyTarget(Body target)
    {
        target.Lifetime = 0f;
        Score += target.Type == Kind.Saucer ? 200 : target.Size switch { 3 => 20, 2 => 50, _ => 100 };
        HighScore = Math.Max(HighScore, Score);

        while (Score >= _nextExtraLife)
        {
            Lives++;
            _nextExtraLife += 10000;
            SoundRequested?.Invoke(Sound.ExtraLife);
        }

        SoundRequested?.Invoke(target.Type == Kind.Saucer ? Sound.SaucerExplosion : target.Size switch
        {
            3 => Sound.LargeExplosion,
            2 => Sound.MediumExplosion,
            _ => Sound.SmallExplosion
        });
        Explode(target.Position, target.Type == Kind.Saucer ? 16 : 7);

        if (target.Type == Kind.Rock && target.Size > 1)
        {
            AddRock(target.Position, target.Size - 1);
            AddRock(target.Position, target.Size - 1);
        }
    }

    private void Explode(Vector2 position, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Direction(RandomAngle()) * (45f + _random.NextSingle() * 180f);
            AddBody(Kind.Spark, position, velocity, 2f, 0.2f + _random.NextSingle() * 0.45f);
        }
    }

    private void Respawn()
    {
        if (!IsSafe(Center))
        {
            _respawnDelay = 0.25f;
            return;
        }

        Ship.Position = Center;
        Ship.Velocity = Vector2.Zero;
        Ship.Angle = -MathF.PI / 2f;
        _invulnerability = 2.5f;
        _respawnDelay = 0f;
    }

    private bool IsSafe(Vector2 position) => !_bodies.Any(b =>
        (b.Type is Kind.Rock or Kind.Saucer or Kind.EnemyShot) &&
        DistanceSquared(position, b.Position) < (b.Radius + 85f) * (b.Radius + 85f));

    private Body NewBody(Kind type, Vector2 position, Vector2 velocity, float radius, float lifetime = float.PositiveInfinity)
        => new(++_nextId, type, Wrap(position), velocity, radius, lifetime);

    private Body AddBody(Kind type, Vector2 position, Vector2 velocity, float radius, float lifetime = float.PositiveInfinity)
    {
        Body body = NewBody(type, position, velocity, radius, lifetime);
        _bodies.Add(body);
        return body;
    }

    private Vector2 RandomPosition() => new(_random.NextSingle() * Width, _random.NextSingle() * Height);
    private float RandomAngle() => _random.NextSingle() * MathF.Tau;
    private static Vector2 Direction(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    internal static Vector2 Wrap(Vector2 p) => new((p.X % Width + Width) % Width, (p.Y % Height + Height) % Height);

    private static Vector2 Delta(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        d.X -= MathF.Round(d.X / Width) * Width;
        d.Y -= MathF.Round(d.Y / Height) * Height;
        return d;
    }

    internal static float DistanceSquared(Vector2 a, Vector2 b) => Delta(a, b).LengthSquared();

    private static bool Overlaps(Body a, Body b)
    {
        float radius = a.Radius + b.Radius;
        return DistanceSquared(a.Position, b.Position) <= radius * radius;
    }

    internal enum Kind { Ship, Rock, PlayerShot, Saucer, EnemyShot, Spark }
    internal enum Phase { Ready, Playing, Paused, GameOver }
    internal enum Sound
    {
        PlayerShot,
        EnemyShot,
        LargeExplosion,
        MediumExplosion,
        SmallExplosion,
        SaucerExplosion,
        ShipExplosion,
        Hyperspace,
        ExtraLife,
        Thrust,
        Saucer,
        BeatLow,
        BeatHigh
    }

    internal sealed class Body(int id, Kind type, Vector2 position, Vector2 velocity, float radius, float lifetime)
    {
        internal int Id { get; } = id;
        internal Kind Type { get; } = type;
        internal Vector2 Position { get; set; } = position;
        internal Vector2 Velocity { get; set; } = velocity;
        internal float Radius { get; } = radius;
        internal float Lifetime { get; set; } = lifetime;
        internal float Angle { get; set; }
        internal float Spin { get; set; }
        internal int Size { get; set; }
        internal int Variant { get; set; }
    }
}
