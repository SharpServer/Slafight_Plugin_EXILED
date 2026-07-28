using System;
using System.Collections.Generic;
using System.Linq;
using CentralAuth;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using InventorySystem.Items.Keycards;
using InventorySystem.Items.Keycards.Snake;
using MEC;
using Mirror;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Item = Exiled.API.Features.Items.Item;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// A small, server-authoritative Doom-style raycasting game rendered on the
/// Chaos Keycard's 18-by-11 Snake display.
/// </summary>
public sealed class SnakeDoomEngine : ISnakeGameSession
{
    private const int DisplayWidth = 18;
    private const int DisplayHeight = 11;
    private const float TickInterval = 0.1f;
    private const double FieldOfView = Math.PI / 3d;
    private const double MoveDistance = 0.45d;
    private const double TurnAngle = Math.PI / 12d;
    private const double PlayerRadius = 0.18d;
    private const int StartingHealth = 100;
    private const int StartingAmmo = 50;

    private static readonly string[] MapRows =
    {
        "1111111111111111",
        "1P.....1.......1",
        "1..M...1..M....1",
        "1......1.......1",
        "1..11111..111..1",
        "1..............1",
        "1....M.........1",
        "111.1111.1111..1",
        "1..............1",
        "1..M.....111...1",
        "1........1.....1",
        "1.1111...1..M..1",
        "1........1.....1",
        "1..M...........1",
        "1............E.1",
        "1111111111111111",
    };

    private static readonly IReadOnlyDictionary<char, string[]> Font = new Dictionary<char, string[]>
    {
        ['A'] = new[] { "010", "101", "111", "101", "101" },
        ['D'] = new[] { "110", "101", "101", "101", "110" },
        ['E'] = new[] { "111", "100", "110", "100", "111" },
        ['I'] = new[] { "111", "010", "010", "010", "111" },
        ['N'] = new[] { "101", "111", "111", "111", "101" },
        ['W'] = new[] { "101", "101", "111", "111", "101" },
    };

    private readonly int[,] _map = new int[MapRows.Length, MapRows[0].Length];
    private readonly List<Enemy> _enemies = new();
    private readonly int _playerId;
    private readonly Action<SnakeDoomEngine>? _onStopped;
    private readonly ChaosKeycardItem _keycard;
    private readonly double[] _depthBuffer = new double[DisplayWidth];

    private CoroutineHandle _gameLoop;
    private double _playerX;
    private double _playerY;
    private double _playerAngle;
    private int _health;
    private int _ammo;
    private int _kills;
    private int _muzzleFlashTicks;
    private int _damageFlashTicks;
    private bool _ownerSessionTakenOver;
    private bool _stopped;
    private GameState _state;

    public SnakeDoomEngine(
        Player player,
        ushort serial,
        Action<SnakeDoomEngine>? onStopped = null)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));
        if (player.IsNPC || !player.IsSafePlayer())
            throw new InvalidOperationException("Doom requires a verified real client.");

        Item? item = Item.Get(serial);
        if (item is not Keycard keycard ||
            keycard.Base is not ChaosKeycardItem chaosKeycard ||
            keycard.Owner?.Id != player.Id)
        {
            throw new InvalidOperationException(
                "The serial does not identify a Chaos Keycard owned by the player.");
        }

        Serial = serial;
        _playerId = player.Id;
        _onStopped = onStopped;
        _keycard = chaosKeycard;
        ResetGame();
    }

    public ushort Serial { get; }
    public int PlayerId => _playerId;
    public bool IsRunning => !_stopped;
    public int Health => _health;
    public int Ammo => _ammo;
    public int Kills => _kills;

    public void Start()
    {
        if (_stopped)
            throw new ObjectDisposedException(nameof(SnakeDoomEngine));
        if (_gameLoop.IsRunning)
            return;

        SnakeMediaApi.Stop(Serial);
        SnakeImageApi.Stop(Serial);

        if (!TrySendFrame(RenderFrame()))
            throw new InvalidOperationException("Failed to initialize the owner's Snake display session.");

        _gameLoop = Timing.RunCoroutine(GameLoop());
    }

    public void HandleInput(Vector2Int direction)
    {
        if (_stopped || !IsValidTarget())
            return;

        if (_state != GameState.Playing)
        {
            if (direction == Vector2Int.down)
            {
                ResetGame();
                TrySendFrame(RenderFrame());
            }

            return;
        }

        if (direction == Vector2Int.up)
            TryMove(MoveDistance);
        else if (direction == Vector2Int.left)
            _playerAngle = NormalizeAngle(_playerAngle - TurnAngle);
        else if (direction == Vector2Int.right)
            _playerAngle = NormalizeAngle(_playerAngle + TurnAngle);
        else if (direction == Vector2Int.down)
            Shoot();
        else
            return;

        CheckExit();
        TrySendFrame(RenderFrame());
    }

    public void Stop()
        => Stop(restoreSnake: true);

    public void Stop(bool restoreSnake)
        => StopCore(restoreSnake, killCoroutine: true);

    public void Dispose()
        => Stop();

    private IEnumerator<float> GameLoop()
    {
        while (!_stopped)
        {
            if (!IsValidTarget())
            {
                StopCore(restoreSnake: false, killCoroutine: false);
                yield break;
            }

            if (_state == GameState.Playing)
                UpdateEnemies(TickInterval);

            if (_muzzleFlashTicks > 0)
                _muzzleFlashTicks--;
            if (_damageFlashTicks > 0)
                _damageFlashTicks--;

            if (!TrySendFrame(RenderFrame()))
            {
                StopCore(restoreSnake: false, killCoroutine: false);
                yield break;
            }

            yield return Timing.WaitForSeconds(TickInterval);
        }
    }

    private void StopCore(bool restoreSnake, bool killCoroutine)
    {
        if (_stopped)
            return;

        _stopped = true;
        if (killCoroutine && _gameLoop.IsRunning)
            Timing.KillCoroutines(_gameLoop);

        if (restoreSnake)
            TryRestoreSnake();

        _onStopped?.Invoke(this);
    }

    private void ResetGame()
    {
        _enemies.Clear();
        _playerAngle = 0d;
        _health = StartingHealth;
        _ammo = StartingAmmo;
        _kills = 0;
        _muzzleFlashTicks = 0;
        _damageFlashTicks = 0;
        _state = GameState.Playing;

        for (var y = 0; y < MapRows.Length; y++)
        {
            for (var x = 0; x < MapRows[y].Length; x++)
            {
                char cell = MapRows[y][x];
                _map[y, x] = cell switch
                {
                    '1' => 1,
                    'E' => 2,
                    _ => 0,
                };

                if (cell == 'P')
                {
                    _playerX = x + 0.5d;
                    _playerY = y + 0.5d;
                }
                else if (cell == 'M')
                {
                    _enemies.Add(new Enemy(x + 0.5d, y + 0.5d));
                }
            }
        }
    }

    private void TryMove(double distance)
    {
        double nextX = _playerX + Math.Cos(_playerAngle) * distance;
        double nextY = _playerY + Math.Sin(_playerAngle) * distance;

        if (CanOccupy(nextX, _playerY))
            _playerX = nextX;
        if (CanOccupy(_playerX, nextY))
            _playerY = nextY;
    }

    private bool CanOccupy(double x, double y)
    {
        return !IsSolid(x - PlayerRadius, y - PlayerRadius) &&
               !IsSolid(x + PlayerRadius, y - PlayerRadius) &&
               !IsSolid(x - PlayerRadius, y + PlayerRadius) &&
               !IsSolid(x + PlayerRadius, y + PlayerRadius);
    }

    private bool IsSolid(double x, double y)
    {
        int mapX = (int)Math.Floor(x);
        int mapY = (int)Math.Floor(y);
        if (mapX < 0 || mapX >= _map.GetLength(1) ||
            mapY < 0 || mapY >= _map.GetLength(0))
        {
            return true;
        }

        int tile = _map[mapY, mapX];
        return tile == 1 || tile == 2 && _enemies.Any(enemy => enemy.IsAlive);
    }

    private void Shoot()
    {
        if (_ammo <= 0)
            return;

        _ammo--;
        _muzzleFlashTicks = 2;

        Enemy? target = null;
        double targetDistance = double.MaxValue;
        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.IsAlive)
                continue;

            double dx = enemy.X - _playerX;
            double dy = enemy.Y - _playerY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Abs(NormalizeSignedAngle(Math.Atan2(dy, dx) - _playerAngle));
            double hitAngle = Math.Max(0.08d, Math.Atan2(0.4d, distance));
            if (angle > hitAngle || distance >= targetDistance || !HasLineOfSight(enemy.X, enemy.Y))
                continue;

            target = enemy;
            targetDistance = distance;
        }

        if (target == null)
            return;

        target.Health--;
        if (!target.IsAlive)
            _kills++;
    }

    private void UpdateEnemies(float deltaTime)
    {
        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.IsAlive)
                continue;

            enemy.AttackCooldown = Math.Max(0d, enemy.AttackCooldown - deltaTime);
            double dx = _playerX - enemy.X;
            double dy = _playerY - enemy.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > 7d || !HasLineOfSight(enemy.X, enemy.Y))
                continue;

            if (distance <= 1.15d)
            {
                if (enemy.AttackCooldown <= 0d)
                {
                    _health = Math.Max(0, _health - 12);
                    _damageFlashTicks = 2;
                    enemy.AttackCooldown = 0.8d;
                    if (_health == 0)
                        _state = GameState.Dead;
                }

                continue;
            }

            double step = 0.55d * deltaTime;
            double nextX = enemy.X + dx / distance * step;
            double nextY = enemy.Y + dy / distance * step;
            if (!IsSolid(nextX, enemy.Y))
                enemy.X = nextX;
            if (!IsSolid(enemy.X, nextY))
                enemy.Y = nextY;
        }
    }

    private bool HasLineOfSight(double targetX, double targetY)
    {
        double dx = targetX - _playerX;
        double dy = targetY - _playerY;
        double targetDistance = Math.Sqrt(dx * dx + dy * dy);
        RayHit hit = CastRay(Math.Atan2(dy, dx));
        return hit.Distance + 0.2d >= targetDistance;
    }

    private void CheckExit()
    {
        if (_enemies.Any(enemy => enemy.IsAlive))
            return;

        int mapX = (int)Math.Floor(_playerX);
        int mapY = (int)Math.Floor(_playerY);
        if (_map[mapY, mapX] == 2)
            _state = GameState.Won;
    }

    private List<Vector2Int> RenderFrame()
    {
        var pixels = new bool[DisplayHeight, DisplayWidth];
        if (_state == GameState.Dead)
        {
            RenderEndScreen(pixels, "DEAD");
            return EncodeSolidPixels(pixels);
        }

        if (_state == GameState.Won)
        {
            RenderEndScreen(pixels, "WIN");
            return EncodeSolidPixels(pixels);
        }

        RenderWorld(pixels);
        RenderEnemies(pixels);
        RenderWeaponAndHud(pixels);

        if (_damageFlashTicks > 0)
        {
            for (var x = 0; x < DisplayWidth; x++)
            {
                pixels[0, x] = true;
                pixels[DisplayHeight - 1, x] = true;
            }

            for (var y = 0; y < DisplayHeight; y++)
            {
                pixels[y, 0] = true;
                pixels[y, DisplayWidth - 1] = true;
            }
        }

        return EncodeSolidPixels(pixels);
    }

    private void RenderWorld(bool[,] pixels)
    {
        for (var x = 0; x < DisplayWidth; x++)
        {
            double camera = (x + 0.5d) / DisplayWidth - 0.5d;
            double rayAngle = _playerAngle + camera * FieldOfView;
            RayHit hit = CastRay(rayAngle);
            double correctedDistance = hit.Distance * Math.Cos(rayAngle - _playerAngle);
            correctedDistance = Math.Max(0.1d, correctedDistance);
            _depthBuffer[x] = correctedDistance;

            int wallHeight = Math.Min(
                DisplayHeight - 2,
                Math.Max(1, (int)Math.Round((DisplayHeight - 2) / correctedDistance)));
            int startY = Math.Max(1, (DisplayHeight - wallHeight) / 2);
            int endY = Math.Min(DisplayHeight - 2, startY + wallHeight - 1);
            for (var y = startY; y <= endY; y++)
            {
                bool edge = y == startY || y == endY;
                bool nearTexture = correctedDistance < 2.25d && (x + y) % 2 == 0;
                bool exitTexture = hit.Tile == 2 && (x + y) % 2 == 0;
                bool sideTexture = hit.Side == 1 && y % 3 == 0;
                bool draw = edge || nearTexture || exitTexture || sideTexture;
                if (draw)
                    pixels[y, x] = true;
            }
        }
    }

    private void RenderEnemies(bool[,] pixels)
    {
        foreach (Enemy enemy in _enemies
                     .Where(enemy => enemy.IsAlive)
                     .OrderByDescending(enemy => DistanceSquared(enemy.X, enemy.Y)))
        {
            double dx = enemy.X - _playerX;
            double dy = enemy.Y - _playerY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double relativeAngle = NormalizeSignedAngle(Math.Atan2(dy, dx) - _playerAngle);
            if (Math.Abs(relativeAngle) > FieldOfView * 0.65d || !HasLineOfSight(enemy.X, enemy.Y))
                continue;

            double projected = Math.Tan(relativeAngle) / Math.Tan(FieldOfView / 2d);
            int centerX = (int)Math.Round((projected + 1d) * 0.5d * (DisplayWidth - 1));
            int spriteHeight = Math.Max(3, Math.Min(7, (int)Math.Round(7d / distance)));
            int spriteWidth = Math.Max(2, Math.Min(4, (spriteHeight + 1) / 2));
            int startX = centerX - spriteWidth / 2;
            int startY = Math.Max(1, (DisplayHeight - spriteHeight) / 2);

            for (var screenX = startX; screenX < startX + spriteWidth; screenX++)
            {
                if (screenX < 0 || screenX >= DisplayWidth || distance >= _depthBuffer[screenX])
                    continue;

                for (var screenY = startY; screenY < startY + spriteHeight; screenY++)
                {
                    if (screenY < 0 || screenY >= DisplayHeight)
                        continue;

                    int localX = screenX - startX;
                    int localY = screenY - startY;
                    bool eye = spriteHeight >= 5 &&
                               localY == spriteHeight / 3 &&
                               (localX == 0 || localX == spriteWidth - 1);
                    bool corner = (localY == 0 || localY == spriteHeight - 1) &&
                                  (localX == 0 || localX == spriteWidth - 1);
                    bool centerGap = spriteWidth >= 3 &&
                                     localY == spriteHeight / 2 &&
                                     localX == spriteWidth / 2;
                    if (!eye && !corner && !centerGap)
                        pixels[screenY, screenX] = true;
                }
            }
        }
    }

    private void RenderWeaponAndHud(bool[,] pixels)
    {
        int healthPixels = Math.Max(0, Math.Min(6, (_health + 16) / 17));
        int ammoPixels = Math.Max(0, Math.Min(6, (_ammo + 8) / 9));
        for (var x = 0; x < healthPixels; x++)
            pixels[0, x] = true;
        for (var x = 0; x < ammoPixels; x++)
            pixels[0, DisplayWidth - 1 - x] = true;

        pixels[1, 8] = true;
        pixels[1, 9] = true;
        if (_muzzleFlashTicks > 0)
        {
            pixels[2, 7] = true;
            pixels[2, 8] = true;
            pixels[2, 9] = true;
            pixels[2, 10] = true;
        }
    }

    private RayHit CastRay(double angle)
    {
        double rayDirX = Math.Cos(angle);
        double rayDirY = Math.Sin(angle);
        int mapX = (int)Math.Floor(_playerX);
        int mapY = (int)Math.Floor(_playerY);
        double deltaDistX = Math.Abs(rayDirX) < 0.00001d ? double.MaxValue : Math.Abs(1d / rayDirX);
        double deltaDistY = Math.Abs(rayDirY) < 0.00001d ? double.MaxValue : Math.Abs(1d / rayDirY);
        int stepX;
        int stepY;
        double sideDistX;
        double sideDistY;

        if (rayDirX < 0d)
        {
            stepX = -1;
            sideDistX = (_playerX - mapX) * deltaDistX;
        }
        else
        {
            stepX = 1;
            sideDistX = (mapX + 1d - _playerX) * deltaDistX;
        }

        if (rayDirY < 0d)
        {
            stepY = -1;
            sideDistY = (_playerY - mapY) * deltaDistY;
        }
        else
        {
            stepY = 1;
            sideDistY = (mapY + 1d - _playerY) * deltaDistY;
        }

        int side = 0;
        int tile = 1;
        for (var steps = 0; steps < 64; steps++)
        {
            if (sideDistX < sideDistY)
            {
                sideDistX += deltaDistX;
                mapX += stepX;
                side = 0;
            }
            else
            {
                sideDistY += deltaDistY;
                mapY += stepY;
                side = 1;
            }

            if (mapX < 0 || mapX >= _map.GetLength(1) ||
                mapY < 0 || mapY >= _map.GetLength(0))
            {
                tile = 1;
                break;
            }

            tile = _map[mapY, mapX];
            if (tile == 1 || tile == 2 && _enemies.Any(enemy => enemy.IsAlive))
                break;
        }

        double distance = side == 0
            ? (mapX - _playerX + (1 - stepX) / 2d) / rayDirX
            : (mapY - _playerY + (1 - stepY) / 2d) / rayDirY;
        return new RayHit(Math.Abs(distance), side, tile);
    }

    private void RenderEndScreen(bool[,] pixels, string text)
    {
        int width = text.Length * 4 - 1;
        int startX = Math.Max(0, (DisplayWidth - width) / 2);
        const int startY = 2;
        for (var characterIndex = 0; characterIndex < text.Length; characterIndex++)
        {
            if (!Font.TryGetValue(text[characterIndex], out string[]? glyph))
                continue;

            for (var y = 0; y < glyph.Length; y++)
            {
                for (var x = 0; x < glyph[y].Length; x++)
                {
                    if (glyph[y][x] == '1')
                        pixels[startY + y, startX + characterIndex * 4 + x] = true;
                }
            }
        }

        for (var x = 4; x < DisplayWidth - 4; x++)
            pixels[9, x] = x % 2 == 0;
    }

    private bool TrySendFrame(List<Vector2Int> frame)
    {
        if (!IsValidTarget())
            return false;

        SnakeNetworkMessage message = SnakeNetworkMessage.NewFullResync(
            gameover: false,
            frame,
            nextFood: null);
        if (!_ownerSessionTakenOver)
            return TryTakeOverOwnerSession(message);

        _keycard.ServerSendMessage(message);
        return true;
    }

    private bool TryTakeOverOwnerSession(SnakeNetworkMessage firstFrame)
    {
        ReferenceHub owner = _keycard.Owner;
        if (!IsReadyClient(owner))
            return false;

        try
        {
            KeyValuePair<ushort, SnakeEngine>[] sessions = ChaosKeycardItem.SnakeSessions.ToArray();
            _keycard.ServerSendTargetRpc(owner, writer =>
            {
                writer.WriteByte((byte)KeycardItem.MsgType.Custom);
                writer.WriteByte((byte)ChaosKeycardItem.ChaosMsgType.NewConnectionFullSync);

                bool includedTarget = false;
                foreach (KeyValuePair<ushort, SnakeEngine> session in sessions)
                {
                    writer.WriteUShort(session.Key);
                    if (session.Key == Serial)
                    {
                        firstFrame.WriteSelf(writer);
                        includedTarget = true;
                    }
                    else
                    {
                        session.Value.WriteFullResyncMessage(writer);
                    }
                }

                if (!includedTarget)
                {
                    writer.WriteUShort(Serial);
                    firstFrame.WriteSelf(writer);
                }
            });

            _ownerSessionTakenOver = true;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[SnakeDoomEngine] Failed to take over serial {Serial}: {ex}");
            return false;
        }
    }

    private bool IsValidTarget()
    {
        Player? player = Player.Get(_playerId);
        if (player == null || player.IsNPC || !player.IsSafePlayer() ||
            player.CurrentItem?.Serial != Serial)
        {
            return false;
        }

        Item? current = Item.Get(Serial);
        return current != null && ReferenceEquals(current.Base, _keycard);
    }

    private void TryRestoreSnake()
    {
        try
        {
            Item? current = Item.Get(Serial);
            if (current == null || !ReferenceEquals(current.Base, _keycard))
                return;

            SNAPI.Features.SnakeContext? context = SNAPI.Features.SnakeContext.Get(Serial);
            if (context == null)
                return;

            List<Vector2Int> segments = context.Segments?.Count >= 2
                ? new List<Vector2Int>(context.Segments)
                : SnakeImageApi.CreateDefaultSnakeSegments();
            _keycard.ServerSendMessage(
                SnakeNetworkMessage.NewFullResync(
                    gameover: false,
                    segments,
                    context.NextFoodPosition));
        }
        catch (Exception ex)
        {
            Log.Warn($"[SnakeDoomEngine] Failed to restore serial {Serial}: {ex.Message}");
        }
    }

    private static bool IsReadyClient(ReferenceHub? hub)
    {
        try
        {
            return hub != null &&
                   hub.Mode == ClientInstanceMode.ReadyClient &&
                   hub.netId != 0 &&
                   hub.connectionToClient is { isReady: true };
        }
        catch
        {
            return false;
        }
    }

    private static List<Vector2Int> EncodeSolidPixels(bool[,] pixels)
    {
        var litPixels = new List<Vector2Int>(DisplayWidth * DisplayHeight);
        for (var y = 0; y < DisplayHeight; y++)
        {
            for (var x = 0; x < DisplayWidth; x++)
            {
                if (pixels[y, x])
                    litPixels.Add(new Vector2Int(x, y));
            }
        }

        if (litPixels.Count == 0)
        {
            for (var x = -120; x < -115; x++)
                litPixels.Add(new Vector2Int(x, -120));
            return litPixels;
        }

        const int hiddenCoordinate = -120;
        const int hiddenMinimumSide = -120;
        const int hiddenMaximumSide = 120;
        Vector2Int[][] rows = litPixels
            .GroupBy(pixel => pixel.y)
            .OrderBy(row => row.Key)
            .Select(row => row.OrderBy(pixel => pixel.x).ToArray())
            .ToArray();
        var result = new List<Vector2Int>(litPixels.Count + rows.Length * 2 + 2);
        bool startFromMinimum = true;
        int firstHiddenX = startFromMinimum ? hiddenMaximumSide : hiddenMinimumSide;
        result.Add(new Vector2Int(firstHiddenX, hiddenCoordinate));
        result.Add(new Vector2Int(firstHiddenX, rows[0][0].y));

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            Vector2Int[] row = rows[rowIndex];
            AddAlternatingRow(result, row, startFromMinimum);
            bool endsAtMinimum = row.Length % 2 == 1
                ? startFromMinimum
                : !startFromMinimum;
            int bridgeX = endsAtMinimum ? hiddenMaximumSide : hiddenMinimumSide;
            result.Add(new Vector2Int(bridgeX, row[0].y));

            if (rowIndex + 1 < rows.Length)
            {
                int nextRowY = rows[rowIndex + 1][0].y;
                result.Add(new Vector2Int(bridgeX, nextRowY));
                startFromMinimum = endsAtMinimum;
            }
            else
            {
                result.Add(new Vector2Int(bridgeX, hiddenCoordinate));
            }
        }

        if (result.Count > byte.MaxValue)
            throw new InvalidOperationException(
                $"Doom frame contains {result.Count} segments; the network limit is {byte.MaxValue}.");
        while (result.Count < 5)
            result.Add(result[result.Count - 1]);
        return result;
    }

    private static void AddAlternatingRow(
        ICollection<Vector2Int> output,
        IReadOnlyList<Vector2Int> row,
        bool startFromMinimum)
    {
        int minimum = 0;
        int maximum = row.Count - 1;
        bool takeMinimum = startFromMinimum;
        while (minimum <= maximum)
        {
            output.Add(takeMinimum ? row[minimum++] : row[maximum--]);
            takeMinimum = !takeMinimum;
        }
    }

    private double DistanceSquared(double x, double y)
    {
        double dx = x - _playerX;
        double dy = y - _playerY;
        return dx * dx + dy * dy;
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle < 0d)
            angle += Math.PI * 2d;
        while (angle >= Math.PI * 2d)
            angle -= Math.PI * 2d;
        return angle;
    }

    private static double NormalizeSignedAngle(double angle)
    {
        while (angle < -Math.PI)
            angle += Math.PI * 2d;
        while (angle > Math.PI)
            angle -= Math.PI * 2d;
        return angle;
    }

    private sealed class Enemy
    {
        public Enemy(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; set; }
        public double Y { get; set; }
        public int Health { get; set; } = 2;
        public double AttackCooldown { get; set; }
        public bool IsAlive => Health > 0;
    }

    private readonly struct RayHit
    {
        public RayHit(double distance, int side, int tile)
        {
            Distance = distance;
            Side = side;
            Tile = tile;
        }

        public double Distance { get; }
        public int Side { get; }
        public int Tile { get; }
    }

    private enum GameState
    {
        Playing,
        Dead,
        Won,
    }
}
