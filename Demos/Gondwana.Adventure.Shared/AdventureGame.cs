using System.Numerics;
using static Gondwana.Demos.Adventure.AdventureWorld;

namespace Gondwana.Demos.Adventure;

/// <summary>Platform-neutral room navigation, persistent objects, combat and puzzle rules.</summary>
internal sealed class AdventureGame
{
    internal const int Width = AdventureWorld.Width;
    internal const int Height = AdventureWorld.Height;
    internal enum ItemKind { GoldKey, WhiteKey, BlackKey, Sword, Bridge, Magnet, Chalice, Dot }
    internal enum Sound { Pickup, Drop, Gate, Dragon, Slay, Bat, Win, Start }
    internal enum Phase { Title, Playing, Paused, Dead, Won }

    internal sealed class Item(ItemKind kind, int room, Vector2 position)
    {
        internal ItemKind Kind { get; } = kind;
        internal int Room { get; set; } = room;
        internal Vector2 Position { get; set; } = position;
    }

    internal sealed class Dragon(int room, Vector2 position, uint color, float speed)
    {
        internal int Room { get; set; } = room;
        internal Vector2 Position { get; set; } = position;
        internal uint Color { get; } = color;
        internal float Speed { get; } = speed;
        internal bool Alive { get; set; } = true;
    }

    private readonly Random _random;
    private readonly HashSet<int> _openCastles = [];
    private float _pickupDelay;
    private float _batDelay;
    private float _portalDelay;
    private bool _secretOpen;
    private Item? _batCargo;

    internal AdventureGame(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        Reset();
    }

    internal event Action<Sound>? SoundRequested;
    internal List<Item> Items { get; } = [];
    internal List<Dragon> Dragons { get; } = [];
    internal Item? Held { get; private set; }
    internal int RoomId { get; private set; }
    internal Vector2 Player { get; private set; }
    internal int BatRoom { get; private set; }
    internal Vector2 BatPosition { get; private set; }
    internal Phase State { get; private set; }
    internal bool Randomized { get; private set; }
    internal bool IsGateOpen(int room) => _openCastles.Contains(room);
    internal bool SecretOpen => _secretOpen;

    internal void Reset(bool randomized = false)
    {
        Randomized = randomized;
        State = Phase.Title;
        Held = null;
        RoomId = GoldCastle;
        Player = new Vector2(640, 430);
        _openCastles.Clear();
        _secretOpen = false;
        _pickupDelay = 0f;
        _portalDelay = 0f;
        _batDelay = 9f;
        _batCargo = null;
        BatRoom = 8;
        BatPosition = new Vector2(640, 340);
        Items.Clear();
        Dragons.Clear();

        Items.AddRange([
            new(ItemKind.GoldKey, 17, new(310, 420)),
            new(ItemKind.WhiteKey, 18, new(440, 340)),
            new(ItemKind.BlackKey, 23, new(640, 320)),
            new(ItemKind.Sword, 2, new(640, 340)),
            new(ItemKind.Bridge, 8, new(640, 350)),
            new(ItemKind.Magnet, 26, new(640, 310)),
            new(ItemKind.Chalice, 21, new(640, 330)),
            new(ItemKind.Dot, 20, new(640, 340))
        ]);

        if (randomized)
        {
            // Shuffle only the non-essential tools among accessible outdoor rooms.
            foreach (Item item in Items.Where(i => i.Kind is ItemKind.Sword or ItemKind.Bridge))
                item.Room = _random.Next(1, 12);
        }

        Dragons.AddRange([
            new(2, new(420, 190), 0xffdfbd35, 105f),
            new(8, new(640, 160), 0xff60bb45, 125f),
            new(21, new(640, 160), 0xffd44040, 155f)
        ]);
    }

    internal void Start()
    {
        if (State is Phase.Dead or Phase.Won)
            Reset(Randomized);

        if (State != Phase.Title)
            return;

        State = Phase.Playing;
        Emit(Sound.Start);
    }

    internal void Pause()
    {
        if (State == Phase.Playing)
            State = Phase.Paused;
        else if (State == Phase.Paused)
            State = Phase.Playing;
    }

    internal void Drop()
    {
        if (State != Phase.Playing || Held is null)
            return;

        Held.Position = Player;
        Held = null;
        _pickupDelay = 0.8f;
        Emit(Sound.Drop);
    }

    internal void Update(float elapsed, Vector2 input)
    {
        if (State != Phase.Playing)
            return;

        float dt = Math.Clamp(elapsed, 0f, 0.05f);
        _pickupDelay = Math.Max(0f, _pickupDelay - dt);
        _portalDelay = Math.Max(0f, _portalDelay - dt);

        if (input.LengthSquared() > 1f)
            input = Vector2.Normalize(input);

        MovePlayer(new Vector2(input.X * 235f * dt, 0));
        MovePlayer(new Vector2(0, input.Y * 235f * dt));
        HandlePortal();
        HandleItems();
        UpdateDragons(dt);
        UpdateBat(dt);

        if (Held?.Kind == ItemKind.Chalice && RoomId == GoldInterior && State == Phase.Playing)
        {
            State = Phase.Won;
            Emit(Sound.Win);
        }
    }

    private void MovePlayer(Vector2 delta)
    {
        Vector2 next = Player + delta;
        Direction? direction = next.X < 0 ? Direction.West : next.X > Width ? Direction.East :
            next.Y < 0 ? Direction.North : next.Y > Floor ? Direction.South : null;

        if (direction.HasValue)
        {
            int target = GetRoom(RoomId).Exit(direction.Value);

            if (RoomId == 3 && direction == Direction.East && _secretOpen)
                target = 30;

            if (target != RoomId)
            {
                Vector2 entry = direction.Value switch
                {
                    Direction.West => new(Width - 18, next.Y),
                    Direction.East => new(18, next.Y),
                    Direction.North => new(next.X, Floor - 18),
                    _ => new(next.X, 18)
                };
                Enter(target, entry);
                return;
            }
        }

        next = Vector2.Clamp(next, Vector2.Zero, new Vector2(Width, Floor));
        bool bridge = Items.Any(i => i.Room == RoomId && i.Kind == ItemKind.Bridge && i != Held &&
            Vector2.DistanceSquared(i.Position, next) < 65f * 65f);

        if (bridge || !Walls(GetRoom(RoomId)).Any(w => IsWithin(next, w)))
            Player = next;
    }

    private void HandlePortal()
    {
        if (_portalDelay > 0f || !IsWithin(Player, Gate))
            return;

        int interior = Interior(RoomId);

        if (interior != 0)
        {
            ItemKind key = RoomId switch
            {
                15 => ItemKind.WhiteKey,
                16 => ItemKind.BlackKey,
                _ => ItemKind.GoldKey
            };

            if (Held?.Kind == key && _openCastles.Add(RoomId))
                Emit(Sound.Gate);

            if (_openCastles.Contains(RoomId))
                Enter(interior, new Vector2(640, 460));
        }
        else
        {
            int exterior = Exterior(RoomId);

            if (exterior != 0)
                Enter(exterior, new Vector2(640, 285));
        }
    }

    private void Enter(int room, Vector2 position)
    {
        RoomId = room;
        Player = Vector2.Clamp(position, new Vector2(12, 12), new Vector2(Width - 12, Floor - 12));
        _portalDelay = 0.6f;

        if (Held is not null)
            Held.Room = room;
    }

    private void HandleItems()
    {
        if (Held is not null)
        {
            Held.Room = RoomId;
            Held.Position = Player + new Vector2(23, 15);

            if (Held.Kind == ItemKind.Dot && RoomId == 3)
                _secretOpen = true;

            if (Held.Kind == ItemKind.Magnet)
            {
                Item? attracted = Items.Where(i => i != Held && i != _batCargo && i.Room == RoomId)
                    .OrderBy(i => Vector2.DistanceSquared(i.Position, Player)).FirstOrDefault();

                if (attracted is not null)
                    attracted.Position = Vector2.Lerp(attracted.Position, Held.Position, 0.025f);
            }

            return;
        }

        if (_pickupDelay > 0f)
            return;

        Item? item = Items.Find(i => i != _batCargo && i.Room == RoomId &&
            Vector2.DistanceSquared(i.Position, Player) < 26f * 26f);

        if (item is not null)
        {
            Held = item;
            Emit(Sound.Pickup);
        }
    }

    private void UpdateDragons(float dt)
    {
        foreach (Dragon dragon in Dragons.Where(d => d.Alive && d.Room == RoomId))
        {
            Vector2 offset = Player - dragon.Position;
            float distance = offset.Length();

            if (distance > 1f)
            {
                Vector2 next = dragon.Position + offset / distance * dragon.Speed * dt;

                if (!Walls(GetRoom(RoomId)).Any(w => IsWithin(next, w, 20f)))
                    dragon.Position = next;
            }

            if (Held?.Kind == ItemKind.Sword && distance < 54f)
            {
                dragon.Alive = false;
                Emit(Sound.Slay);
            }
            else if (distance < 22f)
            {
                State = Phase.Dead;
                Emit(Sound.Dragon);
                return;
            }
        }
    }

    private void UpdateBat(float dt)
    {
        _batDelay -= dt;
        Vector2 destination = new(640, 330);
        Item? target = Items.FirstOrDefault(i => i != Held && i != _batCargo && i.Room == BatRoom &&
            i.Kind is ItemKind.Sword or ItemKind.Bridge or ItemKind.Magnet);

        if (_batCargo is null && target is not null)
            destination = target.Position;

        Vector2 offset = destination - BatPosition;

        if (offset.Length() > 2f)
            BatPosition += Vector2.Normalize(offset) * 145f * dt;

        if (_batCargo is not null)
            _batCargo.Position = BatPosition;

        if (_batDelay > 0f)
            return;

        _batDelay = 8f;

        if (_batCargo is not null)
        {
            _batCargo.Room = BatRoom;
            _batCargo.Position = BatPosition;
            _batCargo = null;
        }
        else if (target is not null)
        {
            _batCargo = target;

            if (BatRoom == RoomId)
                Emit(Sound.Bat);
        }

        BatRoom = _random.Next(1, 12);
        BatPosition = new Vector2(90, 330);

        if (_batCargo is not null)
            _batCargo.Room = BatRoom;
    }

    private void Emit(Sound sound) => SoundRequested?.Invoke(sound);
}
