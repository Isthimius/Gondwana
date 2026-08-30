using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Collisions;
using Gondwana.Drawing.Sprites;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;
using SpriteHorizontalAlignment = Gondwana.Drawing.Sprites.HorizontalAlignment;
using SpriteVerticalAlignment = Gondwana.Drawing.Sprites.VerticalAlignment;

namespace Gondwana.ZeldaPrototype;

internal sealed partial class ZeldaGameHost
{
    private readonly Dictionary<SceneLayerTile, ICollider> _fixedTileColliders = [];

    private void BuildWorld()
    {
        FillGround();
        BuildOverworld();
        BuildDungeon();
    }

    private void FillGround()
    {
        for (int y = 0; y < WorldRows; y++)
        {
            for (int x = 0; x < WorldColumns; x++)
            {
                int frame = x >= 52
                    ? GameArt.DungeonFloor
                    : GameArt.Grass;

                _groundLayer[x, y]!.CurrentFrame = GameArt.GetFrame(frame);
            }
        }

        for (int x = 1; x <= 46; x++)
        {
            for (int y = 14; y <= 16; y++)
                _groundLayer[x, y]!.CurrentFrame = GameArt.GetFrame(GameArt.Path);
        }

        for (int y = 3; y <= 27; y++)
            _groundLayer[8, y]!.CurrentFrame = GameArt.GetFrame(GameArt.Path);

        foreach ((int x, int y) in new[]
                 {
                     (4, 5), (10, 7), (15, 22), (20, 11), (22, 25),
                     (29, 5), (34, 18), (38, 8), (43, 25), (46, 6)
                 })
        {
            _groundLayer[x, y]!.CurrentFrame = GameArt.GetFrame(GameArt.Flower);
        }
    }

    private void BuildOverworld()
    {
        for (int x = 0; x <= 48; x++)
        {
            SetSolidObject(x, 0, GameArt.Tree);
            SetSolidObject(x, 29, GameArt.Tree);
        }

        for (int y = 1; y < 29; y++)
        {
            SetSolidObject(0, y, GameArt.Tree);
            SetSolidObject(48, y, GameArt.Tree);
        }

        for (int y = 1; y < 29; y++)
        {
            if (y is >= 14 and <= 16)
                continue;

            SetSolidObject(25, y, GameArt.Water);
        }

        foreach ((int x, int y) in new[]
                 {
                     (3, 9), (4, 9), (15, 4), (16, 4), (19, 24),
                     (30, 21), (31, 21), (35, 5), (40, 12), (43, 9)
                 })
        {
            SetSolidObject(x, y, GameArt.Tree);
        }

        foreach ((int x, int y) in new[]
                 {
                     (6, 20), (14, 10), (21, 6), (28, 24), (33, 8),
                     (37, 19), (41, 4), (44, 22)
                 })
        {
            SetSolidObject(x, y, GameArt.Rock);
        }

        SetObject(45, 15, GameArt.Entrance);
    }

    private void BuildDungeon()
    {
        for (int x = 51; x < WorldColumns; x++)
        {
            SetSolidObject(x, 2, GameArt.DungeonWall);
            SetSolidObject(x, 27, GameArt.DungeonWall);
        }

        for (int y = 3; y < 27; y++)
        {
            SetSolidObject(51, y, GameArt.DungeonWall);
            SetSolidObject(79, y, GameArt.DungeonWall);
        }

        SetObject(53, 15, GameArt.Entrance);

        for (int y = 3; y <= 13; y++)
            SetSolidObject(65, y, GameArt.DungeonWall);

        for (int y = 17; y <= 26; y++)
            SetSolidObject(65, y, GameArt.DungeonWall);

        for (int y = 14; y <= 16; y++)
        {
            SceneLayerTile tile = SetSolidObject(65, y, GameArt.Gate);
            _gateTiles.Add(tile);
        }

        foreach ((int x, int y) in new[]
                 {
                     (57, 7), (58, 7), (61, 23), (62, 23),
                     (69, 7), (70, 7), (75, 22), (76, 22)
                 })
        {
            SetSolidObject(x, y, GameArt.DungeonWall);
        }
    }

    private void SetObject(int x, int y, int frame)
    {
        _objectLayer[x, y]!.CurrentFrame = GameArt.GetFrame(frame);
    }

    private SceneLayerTile SetSolidObject(int x, int y, int frame)
    {
        SceneLayerTile tile = _objectLayer[x, y]!;
        tile.CurrentFrame = GameArt.GetFrame(frame);

        ICollider collider = GetOrCreateFixedTileCollider(tile);
        collider.CollisionGroup = _objectLayer.CollisionGroups.WorldStatic;
        collider.CollidesWith = _objectLayer.CollisionGroups.Actors;
        collider.ResponseType = CollisionResponseType.Solid;
        SetFixedTileCollisionEnabled(tile, enabled: true);
        return tile;
    }

    private ICollider GetOrCreateFixedTileCollider(SceneLayerTile tile)
    {
        if (_fixedTileColliders.TryGetValue(tile, out ICollider? collider))
            return collider;

        // Gondwana 2.5.2 creates an internal fixed-tile collider but does not
        // expose it through Tile.Collider. Current source does. Creating a public
        // TileCollider only for the older package keeps both paths playable.
        collider = tile.Collider ?? new TileCollider(
            tile,
            collisionGroup: CollisionMasks.None,
            collidesWith: CollisionMasks.None);

        _fixedTileColliders.Add(tile, collider);
        return collider;
    }

    private void SetFixedTileCollisionEnabled(SceneLayerTile tile, bool enabled)
    {
        if (!_fixedTileColliders.TryGetValue(tile, out ICollider? collider))
            return;

        if (enabled)
            _objectLayer.ColliderRegistry.Register(collider);
        else
            _objectLayer.ColliderRegistry.Unregister(collider);
    }

    private void CreateWorldSprites()
    {
        _player = CreateActorSprite(
            "player",
            GameArt.PlayerDown,
            OverworldSpawn,
            new Size(28, 28));
        ConfigureActorCollision(_player, new CollisionAdjust(4, 3, 6, 6));

        _sword = CreateActorSprite(
            "player-sword",
            GameArt.SwordDown,
            OverworldSpawn,
            new Size(32, 32));
        _sword.Visible = false;
        _sword.ZOrder = 900;

        _elder = CreateActorSprite(
            "elder-rowan",
            GameArt.Elder,
            ElderPosition,
            new Size(32, 32));
        _elder.ZOrder = 150;

        CreatePickup("field-potion", InventoryItem.Potion, 1, GameArt.Potion, new Vector2(18f, 6f));
        CreatePickup("rusted-key", InventoryItem.RustedKey, 1, GameArt.Key, new Vector2(40f, 21f));
        CreatePickup("sun-relic", InventoryItem.SunRelic, 1, GameArt.Relic, new Vector2(60f, 8f));

        CreateEnemy("moss-slime-one", GameArt.Slime, new Vector2(17f, 20f), WorldArea.Overworld, 2, 2.0f, 1);
        CreateEnemy("moss-slime-two", GameArt.Slime, new Vector2(32f, 10f), WorldArea.Overworld, 2, 2.1f, 1);
        CreateEnemy("moss-slime-three", GameArt.Slime, new Vector2(38f, 24f), WorldArea.Overworld, 3, 2.2f, 1);
        CreateEnemy("road-bat", GameArt.Bat, new Vector2(43f, 7f), WorldArea.Overworld, 2, 2.7f, 1);

        CreateEnemy("crypt-bat-one", GameArt.Bat, new Vector2(58f, 10f), WorldArea.Dungeon, 2, 2.8f, 1);
        CreateEnemy("crypt-bat-two", GameArt.Bat, new Vector2(61f, 20f), WorldArea.Dungeon, 2, 2.8f, 1);
        CreateEnemy("crypt-slime", GameArt.Slime, new Vector2(62f, 15f), WorldArea.Dungeon, 3, 2.2f, 1);
        CreateEnemy(
            "hollow-king",
            GameArt.Boss,
            new Vector2(73f, 15f),
            WorldArea.Dungeon,
            maximumHealth: 12,
            speed: 2.45f,
            contactDamage: 2,
            isBoss: true);
    }

    private Sprite CreateActorSprite(
        string id,
        int frame,
        Vector2 position,
        Size renderSize)
    {
        Sprite sprite = Engine.Managers.Sprites.CreateSprite(
            _actorLayer,
            GameArt.GetFrame(frame),
            id);

        sprite.RenderSize = renderSize;
        sprite.HorizAlign = SpriteHorizontalAlignment.Center;
        sprite.VertAlign = SpriteVerticalAlignment.Middle;
        sprite.SetPosition(position);
        sprite.Visible = true;
        sprite.ZOrder = 100 + (int)(position.Y * 10f);
        return sprite;
    }

    private void ConfigureActorCollision(Sprite sprite, CollisionAdjust adjust)
    {
        sprite.AdjustCollisionArea = adjust;
        sprite.Collider!.CollisionGroup = Scene!.CollisionGroups.Actors;
        sprite.Collider.CollidesWith = Scene.CollisionGroups.WorldStatic;
        sprite.Collider.ResponseType = CollisionResponseType.Solid;
        sprite.CollisionsEnabled = true;
    }

    private void CreateEnemy(
        string id,
        int frame,
        Vector2 position,
        WorldArea area,
        int maximumHealth,
        float speed,
        int contactDamage,
        bool isBoss = false)
    {
        Size size = isBoss ? new Size(64, 64) : new Size(30, 30);
        Sprite sprite = CreateActorSprite(id, frame, position, size);
        ConfigureActorCollision(
            sprite,
            isBoss
                ? new CollisionAdjust(10, 8, 11, 11)
                : new CollisionAdjust(5, 4, 5, 5));

        _enemies.Add(new EnemyState(
            id,
            sprite,
            position,
            area,
            maximumHealth,
            speed,
            contactDamage,
            isBoss));
    }

    private void CreatePickup(
        string id,
        InventoryItem item,
        int amount,
        int frame,
        Vector2 position)
    {
        Sprite sprite = CreateActorSprite(id, frame, position, new Size(24, 24));
        sprite.ZOrder = 80;
        _pickups.Add(new PickupState(id, item, amount, sprite));
    }
}
