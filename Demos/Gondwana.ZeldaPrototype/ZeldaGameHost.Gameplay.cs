using System.Numerics;
using System.Windows.Forms;
using Gondwana.Drawing.Sprites;

namespace Gondwana.ZeldaPrototype;

internal sealed partial class ZeldaGameHost
{
    private void UpdatePlayerMovement()
    {
        Vector2 movement = GetKeyboardMovement();
        Vector2 gamepadMovement = GetGamepadMovement();

        if (gamepadMovement.LengthSquared() > 0f)
            movement = gamepadMovement;

        if (movement.LengthSquared() > 1f)
            movement = Vector2.Normalize(movement);

        if (movement.LengthSquared() > 0f)
        {
            UpdateFacing(movement);
            _player.Movement.SetVelocity(movement * PlayerSpeed);
        }
        else
        {
            _player.Movement.SetVelocity(Vector2.Zero);
        }

        _player.ZOrder = 100 + (int)(_player.GetPosition().Y * 10f);
    }

    private Vector2 GetKeyboardMovement()
    {
        float x = 0f;
        float y = 0f;

        if (_keysDown.Contains(Keys.A) || _keysDown.Contains(Keys.Left))
            x -= 1f;
        if (_keysDown.Contains(Keys.D) || _keysDown.Contains(Keys.Right))
            x += 1f;
        if (_keysDown.Contains(Keys.W) || _keysDown.Contains(Keys.Up))
            y -= 1f;
        if (_keysDown.Contains(Keys.S) || _keysDown.Contains(Keys.Down))
            y += 1f;

        return new Vector2(x, y);
    }

    private Vector2 GetGamepadMovement()
    {
        var adapter = Engine.Input.GamepadManager?.ConnectedAdapters.FirstOrDefault();
        Vector2 movement = Vector2.Zero;

        if (adapter?.LeftStick is { } stick)
        {
            var deadzoned = stick.WithDeadzone(0.2f);
            movement = new Vector2(deadzoned.X, -deadzoned.Y);
        }

        if (_gamepadButtonsDown.Contains("DPadLeft"))
            movement.X = -1f;
        if (_gamepadButtonsDown.Contains("DPadRight"))
            movement.X = 1f;
        if (_gamepadButtonsDown.Contains("DPadUp"))
            movement.Y = -1f;
        if (_gamepadButtonsDown.Contains("DPadDown"))
            movement.Y = 1f;

        return movement;
    }

    private void UpdateFacing(Vector2 movement)
    {
        Facing next = MathF.Abs(movement.X) > MathF.Abs(movement.Y)
            ? movement.X < 0f ? Facing.Left : Facing.Right
            : movement.Y < 0f ? Facing.Up : Facing.Down;

        if (_facing == next)
            return;

        _facing = next;
        _player.CurrentFrame = GameArt.GetFrame(GetPlayerFrame(_facing));
    }

    private static int GetPlayerFrame(Facing facing) => facing switch
    {
        Facing.Up => GameArt.PlayerUp,
        Facing.Down => GameArt.PlayerDown,
        Facing.Left => GameArt.PlayerLeft,
        Facing.Right => GameArt.PlayerRight,
        _ => GameArt.PlayerDown
    };

    private static int GetSwordFrame(Facing facing) => facing switch
    {
        Facing.Up => GameArt.SwordUp,
        Facing.Down => GameArt.SwordDown,
        Facing.Left => GameArt.SwordLeft,
        Facing.Right => GameArt.SwordRight,
        _ => GameArt.SwordDown
    };

    private void BeginSwordAttack()
    {
        if (_mode != GameMode.Playing || _swordTimer > 0f || GetItemCount(InventoryItem.Sword) <= 0)
            return;

        _swordTimer = SwordDurationSeconds;
        _swingHitIds.Clear();
        _sword.CurrentFrame = GameArt.GetFrame(GetSwordFrame(_facing));
        _sword.Visible = true;
        UpdateSword();
    }

    private void UpdateSword()
    {
        if (!_sword.Visible)
            return;

        Vector2 offset = _facing switch
        {
            Facing.Up => new Vector2(0f, -0.75f),
            Facing.Down => new Vector2(0f, 0.75f),
            Facing.Left => new Vector2(-0.75f, 0f),
            Facing.Right => new Vector2(0.75f, 0f),
            _ => Vector2.Zero
        };

        _sword.SetPosition(_player.GetPosition() + offset);
        _sword.ZOrder = _facing == Facing.Up
            ? _player.ZOrder - 1
            : _player.ZOrder + 1;
    }

    private void UpdateEnemies()
    {
        Vector2 playerPosition = _player.GetPosition();

        foreach (EnemyState enemy in _enemies)
        {
            if (!enemy.IsAlive || enemy.Area != _currentArea)
            {
                enemy.Sprite.Movement.SetVelocity(Vector2.Zero);
                continue;
            }

            Vector2 delta = playerPosition - enemy.Sprite.GetPosition();
            float distance = delta.Length();
            float awareness = enemy.IsBoss ? 13f : 8f;

            if (distance is > 0.05f && distance < awareness)
                enemy.Sprite.Movement.SetVelocity(Vector2.Normalize(delta) * enemy.Speed);
            else
                enemy.Sprite.Movement.SetVelocity(Vector2.Zero);

            enemy.Sprite.ZOrder = 100 + (int)(enemy.Sprite.GetPosition().Y * 10f);
        }
    }

    private void ResolveSwordHits()
    {
        if (!_sword.Visible)
            return;

        foreach (EnemyState enemy in _enemies)
        {
            if (!enemy.IsAlive ||
                enemy.Area != _currentArea ||
                _swingHitIds.Contains(enemy.Id) ||
                !_sword.CollisionArea.IntersectsWith(enemy.Sprite.CollisionArea))
            {
                continue;
            }

            _swingHitIds.Add(enemy.Id);
            enemy.Health = Math.Max(0, enemy.Health - 1);
            enemy.HealthBar.Value = enemy.Health;

            Vector2 push = enemy.Sprite.GetPosition() - _player.GetPosition();
            if (push.LengthSquared() > 0.001f)
            {
                push = Vector2.Normalize(push) * 12f;
                enemy.Sprite.TranslateWorldPx((int)push.X, (int)push.Y);
            }

            if (enemy.Health <= 0)
                DefeatEnemy(enemy);
        }
    }

    private void DefeatEnemy(EnemyState enemy)
    {
        enemy.Sprite.Movement.StopAllMovement();
        enemy.Sprite.Visible = false;
        enemy.HealthBar.Hide();

        if (enemy.IsBoss)
        {
            WinGame();
            return;
        }

        ShowMessage($"{FormatEnemyName(enemy.Id)} defeated.", 1.3d);
    }

    private static string FormatEnemyName(string id) =>
        id.Replace("-one", "", StringComparison.Ordinal)
            .Replace("-two", "", StringComparison.Ordinal)
            .Replace("-three", "", StringComparison.Ordinal)
            .Replace('-', ' ')
            .Trim();

    private void ResolveEnemyContact()
    {
        if (_damageCooldown > 0f)
            return;

        EnemyState? enemy = _enemies.FirstOrDefault(candidate =>
            candidate.IsAlive &&
            candidate.Area == _currentArea &&
            _player.CollisionArea.IntersectsWith(candidate.Sprite.CollisionArea));

        if (enemy is null)
            return;

        _damageCooldown = DamageCooldownSeconds;
        _playerHealth = Math.Max(0, _playerHealth - enemy.ContactDamage);
        _playerHealthBar.Value = _playerHealth;

        Vector2 push = _player.GetPosition() - enemy.Sprite.GetPosition();
        if (push.LengthSquared() > 0.001f)
        {
            push = Vector2.Normalize(push) * 18f;
            _player.TranslateWorldPx((int)push.X, (int)push.Y);
        }

        if (_playerHealth <= 0)
            LoseGame();
        else
            ShowMessage($"The {FormatEnemyName(enemy.Id)} strikes for {enemy.ContactDamage}.", 1.2d);
    }

    private void CollectPickups()
    {
        foreach (PickupState pickup in _pickups)
        {
            if (!pickup.Sprite.Visible ||
                !_player.CollisionArea.IntersectsWith(pickup.Sprite.CollisionArea))
            {
                continue;
            }

            _collectedPickups.Add(pickup.Id);
            AddItem(pickup.Item, pickup.Amount);
            pickup.Sprite.Visible = false;

            if (pickup.Item == InventoryItem.RustedKey)
                ApplyGateState();

            string message = pickup.Item switch
            {
                InventoryItem.Potion => "You found a red potion. H or B uses it.",
                InventoryItem.RustedKey => "You found the rusted key. The crypt gate will yield.",
                InventoryItem.SunRelic => "The sun relic warms your pack.",
                _ => $"Collected {pickup.Item}."
            };

            ShowMessage(message, 2.2d);
            UpdateHud(force: true);
        }
    }

    private void Interact()
    {
        Vector2 position = _player.GetPosition();

        if (_currentArea == WorldArea.Overworld && IsNear(position, ElderPosition, 1.7f))
        {
            BeginDialogue();
            return;
        }

        if (_currentArea == WorldArea.Overworld && IsNear(position, DungeonEntrancePosition, 1.8f))
        {
            Teleport(DungeonSpawn, WorldArea.Dungeon);
            ShowMessage("The air below is old and cold. Find the gate—and what waits beyond it.", 2.6d);
            return;
        }

        if (_currentArea == WorldArea.Dungeon && IsNear(position, DungeonExitPosition, 1.8f))
        {
            Teleport(OverworldReturn, WorldArea.Overworld);
            ShowMessage("Fresh air. The barrow remains behind you.", 1.8d);
            return;
        }

        if (_currentArea == WorldArea.Dungeon && position.X < 66f && IsNear(position, new Vector2(64f, 15f), 2f))
        {
            ShowMessage(
                HasItem(InventoryItem.RustedKey)
                    ? "The rusted key has already opened the gate."
                    : "A barred gate blocks the eastern crypt. Its lock is old, but stubborn.",
                2.1d);
            return;
        }

        ShowMessage("There is nothing to do here.", 1.1d);
    }

    private static bool IsNear(Vector2 first, Vector2 second, float distance) =>
        Vector2.DistanceSquared(first, second) <= distance * distance;

    private void BeginDialogue()
    {
        _mode = GameMode.Dialogue;
        StopActorMovement();
        _dialogueIndex = 0;
        _messageText.SetText(_elderDialogue[_dialogueIndex] + "\n\nE / Enter / A to continue");
        SetMessageVisible(true);
    }

    private void AdvanceDialogue()
    {
        _dialogueIndex++;

        if (_dialogueIndex >= _elderDialogue.Length)
        {
            _mode = GameMode.Playing;
            SetMessageVisible(false);
            return;
        }

        _messageText.SetText(_elderDialogue[_dialogueIndex] + "\n\nE / Enter / A to continue");
    }

    private void Teleport(Vector2 destination, WorldArea area)
    {
        _currentArea = area;
        _player.Movement.StopAllMovement();
        _player.SetPosition(destination);
        _sword.Visible = false;
        _swordTimer = 0f;
        _swingHitIds.Clear();
        RenderSurface.Host.ViewManager.Views[0].Camera.CenterOnGrid(
            _actorLayer,
            (int)destination.X,
            (int)destination.Y);
    }

    private void AddItem(InventoryItem item, int amount)
    {
        _inventory[item] = GetItemCount(item) + amount;
    }

    private int GetItemCount(InventoryItem item) =>
        _inventory.TryGetValue(item, out int count) ? count : 0;

    private bool HasItem(InventoryItem item) => GetItemCount(item) > 0;

    private void OpenInventory()
    {
        _mode = GameMode.Inventory;
        StopActorMovement();
        UpdateInventoryText();
        SetInventoryVisible(true);
        SetMessageVisible(false);
    }

    private void CloseInventory()
    {
        _mode = GameMode.Playing;
        SetInventoryVisible(false);
    }

    private void UsePotion()
    {
        if (_mode is not (GameMode.Playing or GameMode.Inventory))
            return;

        if (_playerHealth >= PlayerMaximumHealth)
        {
            if (_mode == GameMode.Playing)
                ShowMessage("Your health is already full.", 1.1d);
            return;
        }

        if (!HasItem(InventoryItem.Potion))
        {
            if (_mode == GameMode.Playing)
                ShowMessage("You have no potion.", 1.1d);
            return;
        }

        _inventory[InventoryItem.Potion]--;
        _playerHealth = Math.Min(PlayerMaximumHealth, _playerHealth + 4);
        _playerHealthBar.Value = _playerHealth;
        UpdateHud(force: true);

        if (_mode == GameMode.Inventory)
            UpdateInventoryText();
        else
            ShowMessage("The potion restores four health.", 1.4d);
    }

    private void UpdateInventoryText()
    {
        string key = HasItem(InventoryItem.RustedKey) ? "Rusted Key" : "—";
        string relic = HasItem(InventoryItem.SunRelic) ? "Sun Relic" : "—";

        _inventoryText.SetText(
            "INVENTORY\n\n" +
            "Sword\n" +
            $"{key}\n" +
            $"Red Potion × {GetItemCount(InventoryItem.Potion)}\n" +
            $"{relic}\n\n" +
            "H / A / B: use potion\n" +
            "I / Y / Start: close");
    }

    private void PauseGame()
    {
        _mode = GameMode.Paused;
        StopActorMovement();
        SetPauseVisible(true);
        SetMessageVisible(false);
    }

    private void ResumeGame()
    {
        _mode = GameMode.Playing;
        SetPauseVisible(false);
    }

    private void StartNewGame()
    {
        _inventory.Clear();
        _collectedPickups.Clear();
        AddItem(InventoryItem.Sword, 1);

        _playerHealth = PlayerMaximumHealth;
        _playerHealthBar.Value = _playerHealth;
        _facing = Facing.Down;
        _player.CurrentFrame = GameArt.GetFrame(GameArt.PlayerDown);
        _damageCooldown = 0f;
        _swordTimer = 0f;
        _sword.Visible = false;
        _swingHitIds.Clear();

        ResetEnemies();
        ApplyPickupState();
        ApplyGateState();
        Teleport(OverworldSpawn, WorldArea.Overworld);

        _mode = GameMode.Playing;
        SetTitleVisible(false);
        SetInventoryVisible(false);
        SetPauseVisible(false);
        ShowMessage("Find the rusted key, enter the eastern barrow, and defeat the Hollow King.", 3.2d);
        UpdateHud(force: true);
    }

    private void ResetEnemies()
    {
        foreach (EnemyState enemy in _enemies)
        {
            enemy.Health = enemy.MaximumHealth;
            enemy.Sprite.Movement.StopAllMovement();
            enemy.Sprite.SetPosition(enemy.SpawnPosition);
            enemy.Sprite.Visible = true;
            enemy.HealthBar.Value = enemy.MaximumHealth;
            enemy.HealthBar.RefreshPosition();
            enemy.HealthBar.Show();
        }
    }

    private void ApplyPickupState()
    {
        foreach (PickupState pickup in _pickups)
            pickup.Sprite.Visible = !_collectedPickups.Contains(pickup.Id);
    }

    private void ApplyGateState()
    {
        bool open = HasItem(InventoryItem.RustedKey);

        foreach (var tile in _gateTiles)
        {
            tile.Visible = !open;
            SetFixedTileCollisionEnabled(tile, enabled: !open);
        }
    }

    private void LoseGame()
    {
        _mode = GameMode.GameOver;
        StopActorMovement();
        _sword.Visible = false;
        SetMessageVisible(false);
        SetInventoryVisible(false);
        SetPauseVisible(false);
        SetTitleVisible(true);
        _titleText.SetText("THE GREENWARD FALLS");
        _titleOptionsText.SetText(
            "Enter / A: return to title\n" +
            "L / F9 / Y: load saved game\n\n" +
            "A hard lesson, fairly taught.");
    }

    private void WinGame()
    {
        _mode = GameMode.Victory;
        StopActorMovement();
        _sword.Visible = false;
        SetMessageVisible(false);
        SetInventoryVisible(false);
        SetPauseVisible(false);
        SetTitleVisible(true);
        _titleText.SetText("THE GREENWARD RESTORED");
        _titleOptionsText.SetText(
            "The Hollow King is no more.\n" +
            "The sun relic answers the morning.\n\n" +
            "Enter / A: return to title\n" +
            "L / F9 / Y: load saved game");
    }

    private void EnterTitleMode()
    {
        _mode = GameMode.Title;
        StopActorMovement();
        _sword.Visible = false;
        SetMessageVisible(false);
        SetInventoryVisible(false);
        SetPauseVisible(false);
        SetTitleVisible(true);

        _titleText.SetText("THE GREENWARD KEY\nA GONDWANA PROTOTYPE");
        _titleOptionsText.SetText(
            "Enter / N / A: new game\n" +
            "L / F9 / Y: load game\n\n" +
            "Keyboard and XInput controller supported\n" +
            (SaveGameService.Exists
                ? "A save game is available."
                : "No save game exists yet."));
    }

    private void TrySaveGame()
    {
        if (_mode != GameMode.Playing)
            return;

        try
        {
            Vector2 position = _player.GetPosition();
            SaveGame save = SaveGameService.Create(
                position.X,
                position.Y,
                _currentArea,
                _facing,
                _playerHealth,
                _inventory,
                _collectedPickups,
                _enemies);

            SaveGameService.Save(save);
            ShowMessage("Game saved.", 1.2d);
        }
        catch (Exception ex)
        {
            ShowMessage($"Save failed: {ex.Message}", 2.4d);
        }
    }

    private void TryLoadGame()
    {
        if (!SaveGameService.Exists)
        {
            if (_mode == GameMode.Title)
            {
                _titleOptionsText.SetText(
                    "No save game exists yet.\n\nEnter / N / A: begin a new game");
            }
            else
            {
                ShowMessage("No save game exists yet.", 1.4d);
            }

            return;
        }

        try
        {
            ApplySave(SaveGameService.Load());
            ShowMessage("Game loaded.", 1.2d);
        }
        catch (Exception ex)
        {
            if (_mode == GameMode.Title)
                _titleOptionsText.SetText($"Load failed: {ex.Message}\n\nEnter / N / A: begin a new game");
            else
                ShowMessage($"Load failed: {ex.Message}", 2.4d);
        }
    }

    private void ApplySave(SaveGame save)
    {
        _inventory.Clear();
        foreach ((InventoryItem item, int count) in save.Inventory)
            _inventory[item] = Math.Max(0, count);

        if (!HasItem(InventoryItem.Sword))
            AddItem(InventoryItem.Sword, 1);

        _collectedPickups.Clear();
        _collectedPickups.UnionWith(save.CollectedPickups);

        var healthById = save.Enemies.ToDictionary(enemy => enemy.Id, StringComparer.Ordinal);
        foreach (EnemyState enemy in _enemies)
        {
            int health = healthById.TryGetValue(enemy.Id, out EnemySave? savedEnemy)
                ? savedEnemy.Health
                : enemy.MaximumHealth;

            enemy.Health = Math.Clamp(health, 0, enemy.MaximumHealth);
            enemy.Sprite.Movement.StopAllMovement();
            enemy.Sprite.SetPosition(enemy.SpawnPosition);
            enemy.Sprite.Visible = enemy.IsAlive;
            enemy.HealthBar.Value = enemy.Health;

            if (enemy.IsAlive)
            {
                enemy.HealthBar.RefreshPosition();
                enemy.HealthBar.Show();
            }
            else
            {
                enemy.HealthBar.Hide();
            }
        }

        _playerHealth = Math.Clamp(save.Health, 1, PlayerMaximumHealth);
        _playerHealthBar.Value = _playerHealth;
        _facing = save.Facing;
        _player.CurrentFrame = GameArt.GetFrame(GetPlayerFrame(_facing));
        _damageCooldown = 0f;
        _swordTimer = 0f;
        _sword.Visible = false;
        _swingHitIds.Clear();

        ApplyPickupState();
        ApplyGateState();
        Teleport(new Vector2(save.PlayerX, save.PlayerY), save.Area);

        SetTitleVisible(false);
        SetInventoryVisible(false);
        SetPauseVisible(false);
        _mode = GameMode.Playing;
        UpdateHud(force: true);

        EnemyState boss = _enemies.Single(enemy => enemy.IsBoss);
        if (!boss.IsAlive)
            WinGame();
    }

    private void ShowMessage(string text, double seconds)
    {
        if (_mode != GameMode.Playing)
            return;

        _messageText.SetText(text);
        _messageExpiresUtc = DateTime.UtcNow.AddSeconds(seconds);
        SetMessageVisible(true);
    }

    private void UpdateMessageVisibility()
    {
        if (!_messageText.Visible || DateTime.UtcNow < _messageExpiresUtc)
            return;

        SetMessageVisible(false);
    }

    private void UpdateHud(bool force = false)
    {
        int enemiesRemaining = _enemies.Count(enemy => enemy.IsAlive);
        string area = _currentArea == WorldArea.Overworld ? "Greenward" : "Old Barrow";
        string key = HasItem(InventoryItem.RustedKey) ? "Key ✓" : "Key —";
        string hud =
            $"Health {_playerHealth}/{PlayerMaximumHealth}   {key}   Potions {GetItemCount(InventoryItem.Potion)}   Foes {enemiesRemaining}   {area}\n" +
            "WASD/Arrows move   Space sword   E talk/use   I inventory   F5/F9 save/load";

        if (!force && string.Equals(hud, _lastHud, StringComparison.Ordinal))
            return;

        _lastHud = hud;
        _hudText.SetText(hud);
    }

    private void SetMessageVisible(bool visible)
    {
        _messagePanel.Visible = visible;
        _messageText.Visible = visible;
    }

    private void SetInventoryVisible(bool visible)
    {
        _inventoryPanel.Visible = visible;
        _inventoryText.Visible = visible;
    }

    private void SetPauseVisible(bool visible)
    {
        _pausePanel.Visible = visible;
        _pauseText.Visible = visible;
    }

    private void SetTitleVisible(bool visible)
    {
        _titlePanel.Visible = visible;
        _titleText.Visible = visible;
        _titleOptionsText.Visible = visible;
    }
}
