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

public enum SnakeGridGameMode
{
    PacMan,
    Tetris,
}

/// <summary>
/// Server-authoritative grid games for the Chaos Keycard's 18-by-11 display.
/// </summary>
public sealed class SnakeGridGameEngine : ISnakeGameSession
{
    private const int DisplayWidth = 18;
    private const int DisplayHeight = 11;
    private const float TickInterval = 0.1f;
    private const int TetrisWidth = 10;
    private const int TetrisHeight = 11;
    private const int TetrisOffsetX = 4;

    private static readonly IReadOnlyDictionary<char, string[]> Font =
        new Dictionary<char, string[]>
        {
            ['D'] = new[] { "110", "101", "101", "101", "110" },
            ['E'] = new[] { "111", "100", "110", "100", "111" },
            ['I'] = new[] { "111", "010", "010", "010", "111" },
            ['N'] = new[] { "101", "111", "111", "111", "101" },
            ['W'] = new[] { "101", "101", "111", "111", "101" },
        };

    private static readonly Vector2Int[][] TetrisPieces =
    {
        new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1) },
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) },
        new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2) },
        new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
    };

    private readonly int _playerId;
    private readonly ChaosKeycardItem _keycard;
    private readonly System.Random _random;
    private readonly Action<SnakeGridGameEngine>? _onStopped;

    private readonly bool[,] _pacWalls = new bool[DisplayHeight, DisplayWidth];
    private readonly bool[,] _pacPellets = new bool[DisplayHeight, DisplayWidth];
    private readonly List<PacGhost> _pacGhosts = new();
    private Vector2Int _pacPlayer;
    private float _pacGhostTimer;
    private int _pacAnimationTick;
    private bool _pacEnded;
    private bool _pacWon;

    private readonly bool[,] _tetrisBoard = new bool[TetrisHeight, TetrisWidth];
    private Vector2Int[] _tetrisPiece = Array.Empty<Vector2Int>();
    private Vector2Int _tetrisPosition;
    private float _tetrisFallTimer;
    private int _tetrisLines;
    private bool _tetrisEnded;

    private CoroutineHandle _gameLoop;
    private bool _ownerSessionTakenOver;
    private bool _stopped;

    public SnakeGridGameEngine(
        Player player,
        ushort serial,
        SnakeGridGameMode mode,
        Action<SnakeGridGameEngine>? onStopped = null)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));
        if (player.IsNPC || !player.IsSafePlayer())
            throw new InvalidOperationException("Snake grid games require a verified real client.");

        Item? item = Item.Get(serial);
        if (item is not Keycard keycard ||
            keycard.Base is not ChaosKeycardItem chaosKeycard ||
            keycard.Owner?.Id != player.Id)
        {
            throw new InvalidOperationException(
                "The serial does not identify a Chaos Keycard owned by the player.");
        }

        Serial = serial;
        Mode = mode;
        _playerId = player.Id;
        _keycard = chaosKeycard;
        _onStopped = onStopped;
        _random = new System.Random(serial * 397 ^ (int)mode);
        ResetGame();
    }

    public ushort Serial { get; }
    public int PlayerId => _playerId;
    public bool IsRunning => !_stopped;
    public SnakeGridGameMode Mode { get; }

    public void Start()
    {
        if (_stopped)
            throw new ObjectDisposedException(nameof(SnakeGridGameEngine));
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

        if (Mode == SnakeGridGameMode.PacMan)
            HandlePacInput(direction);
        else
            HandleTetrisInput(direction);

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

            if (Mode == SnakeGridGameMode.PacMan)
                UpdatePacMan(TickInterval);
            else
                UpdateTetris(TickInterval);

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
        if (Mode == SnakeGridGameMode.PacMan)
            ResetPacMan();
        else
            ResetTetris();
    }

    private void ResetPacMan()
    {
        Array.Clear(_pacWalls, 0, _pacWalls.Length);
        Array.Clear(_pacPellets, 0, _pacPellets.Length);
        _pacGhosts.Clear();

        for (var y = 0; y < DisplayHeight; y++)
        {
            for (var x = 0; x < DisplayWidth; x++)
            {
                _pacWalls[y, x] =
                    x == 0 || x == DisplayWidth - 1 ||
                    y == 0 || y == DisplayHeight - 1;
            }
        }

        AddPacWall(2, 2, 5, 2);
        AddPacWall(8, 2, 10, 2);
        AddPacWall(13, 2, 15, 2);
        AddPacWall(2, 5, 4, 5);
        AddPacWall(7, 5, 10, 5);
        AddPacWall(13, 5, 15, 5);
        AddPacWall(2, 8, 5, 8);
        AddPacWall(8, 8, 10, 8);
        AddPacWall(13, 8, 15, 8);
        AddPacWall(6, 1, 6, 3);
        AddPacWall(11, 1, 11, 3);
        AddPacWall(6, 7, 6, 9);
        AddPacWall(11, 7, 11, 9);

        _pacPlayer = new Vector2Int(1, 1);
        _pacGhosts.Add(new PacGhost(16, 1));
        _pacGhosts.Add(new PacGhost(16, 9));
        for (var y = 1; y < DisplayHeight - 1; y++)
        {
            for (var x = 1; x < DisplayWidth - 1; x++)
            {
                if (!_pacWalls[y, x] &&
                    (x + y) % 2 == 0 &&
                    _pacPlayer != new Vector2Int(x, y) &&
                    _pacGhosts.All(ghost => ghost.Position != new Vector2Int(x, y)))
                {
                    _pacPellets[y, x] = true;
                }
            }
        }

        _pacGhostTimer = 0f;
        _pacAnimationTick = 0;
        _pacEnded = false;
        _pacWon = false;
    }

    private void AddPacWall(int startX, int startY, int endX, int endY)
    {
        int stepX = Math.Sign(endX - startX);
        int stepY = Math.Sign(endY - startY);
        int x = startX;
        int y = startY;
        while (true)
        {
            _pacWalls[y, x] = true;
            if (x == endX && y == endY)
                break;
            x += stepX;
            y += stepY;
        }
    }

    private void HandlePacInput(Vector2Int direction)
    {
        if (_pacEnded)
        {
            ResetPacMan();
            return;
        }

        Vector2Int next = _pacPlayer + direction;
        if (IsPacOpen(next))
            _pacPlayer = next;

        ConsumePacPellet();
        CheckPacCollision();
    }

    private void UpdatePacMan(float deltaTime)
    {
        if (_pacEnded)
            return;

        _pacAnimationTick++;
        _pacGhostTimer += deltaTime;
        if (_pacGhostTimer < 0.4f)
            return;

        _pacGhostTimer = 0f;
        foreach (PacGhost ghost in _pacGhosts)
        {
            Vector2Int best = ghost.Position;
            int bestDistance = int.MaxValue;
            foreach (Vector2Int direction in PacDirections())
            {
                Vector2Int candidate = ghost.Position + direction;
                if (!IsPacOpen(candidate))
                    continue;

                int distance =
                    Math.Abs(candidate.x - _pacPlayer.x) +
                    Math.Abs(candidate.y - _pacPlayer.y);
                if (distance < bestDistance ||
                    distance == bestDistance && _random.Next(2) == 0)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            ghost.Position = best;
        }

        CheckPacCollision();
    }

    private static IEnumerable<Vector2Int> PacDirections()
    {
        yield return Vector2Int.left;
        yield return Vector2Int.right;
        yield return Vector2Int.up;
        yield return Vector2Int.down;
    }

    private bool IsPacOpen(Vector2Int position)
        => position.x >= 0 && position.x < DisplayWidth &&
           position.y >= 0 && position.y < DisplayHeight &&
           !_pacWalls[position.y, position.x];

    private void ConsumePacPellet()
    {
        _pacPellets[_pacPlayer.y, _pacPlayer.x] = false;
        if (_pacPellets.Cast<bool>().Any(value => value))
            return;

        _pacEnded = true;
        _pacWon = true;
    }

    private void CheckPacCollision()
    {
        if (_pacGhosts.Any(ghost => ghost.Position == _pacPlayer))
        {
            _pacEnded = true;
            _pacWon = false;
        }
    }

    private void ResetTetris()
    {
        Array.Clear(_tetrisBoard, 0, _tetrisBoard.Length);
        _tetrisFallTimer = 0f;
        _tetrisLines = 0;
        _tetrisEnded = false;
        SpawnTetrisPiece();
    }

    private void HandleTetrisInput(Vector2Int direction)
    {
        if (_tetrisEnded)
        {
            ResetTetris();
            return;
        }

        if (direction == Vector2Int.left)
            TryMoveTetris(new Vector2Int(-1, 0));
        else if (direction == Vector2Int.right)
            TryMoveTetris(new Vector2Int(1, 0));
        else if (direction == Vector2Int.down)
            DropTetrisOneRow();
        else if (direction == Vector2Int.up)
            TryRotateTetris();
    }

    private void UpdateTetris(float deltaTime)
    {
        if (_tetrisEnded)
            return;

        _tetrisFallTimer += deltaTime;
        float fallInterval = Math.Max(0.18f, 0.58f - _tetrisLines * 0.015f);
        if (_tetrisFallTimer < fallInterval)
            return;

        _tetrisFallTimer = 0f;
        DropTetrisOneRow();
    }

    private void DropTetrisOneRow()
    {
        if (CanPlaceTetris(_tetrisPiece, _tetrisPosition + Vector2Int.down))
        {
            _tetrisPosition += Vector2Int.down;
            return;
        }

        LockTetrisPiece();
    }

    private void TryMoveTetris(Vector2Int offset)
    {
        Vector2Int position = _tetrisPosition + offset;
        if (CanPlaceTetris(_tetrisPiece, position))
            _tetrisPosition = position;
    }

    private void TryRotateTetris()
    {
        if (_tetrisPiece.Length == 4 &&
            _tetrisPiece.Contains(new Vector2Int(0, 0)) &&
            _tetrisPiece.Contains(new Vector2Int(1, 0)) &&
            _tetrisPiece.Contains(new Vector2Int(0, 1)) &&
            _tetrisPiece.Contains(new Vector2Int(1, 1)))
        {
            return;
        }

        Vector2Int[] rotated = _tetrisPiece
            .Select(cell => new Vector2Int(-cell.y, cell.x))
            .ToArray();
        foreach (int kick in new[] { 0, -1, 1, -2, 2 })
        {
            Vector2Int position = _tetrisPosition + new Vector2Int(kick, 0);
            if (!CanPlaceTetris(rotated, position))
                continue;

            _tetrisPiece = rotated;
            _tetrisPosition = position;
            return;
        }
    }

    private bool CanPlaceTetris(IEnumerable<Vector2Int> piece, Vector2Int position)
    {
        foreach (Vector2Int cell in piece)
        {
            int x = position.x + cell.x;
            int y = position.y + cell.y;
            if (x < 0 || x >= TetrisWidth || y < 0 || y >= TetrisHeight)
                return false;
            if (_tetrisBoard[y, x])
                return false;
        }

        return true;
    }

    private void LockTetrisPiece()
    {
        foreach (Vector2Int cell in _tetrisPiece)
        {
            int x = _tetrisPosition.x + cell.x;
            int y = _tetrisPosition.y + cell.y;
            _tetrisBoard[y, x] = true;
        }

        ClearTetrisLines();
        SpawnTetrisPiece();
    }

    private void ClearTetrisLines()
    {
        for (var y = 0; y < TetrisHeight; y++)
        {
            bool full = true;
            for (var x = 0; x < TetrisWidth; x++)
                full &= _tetrisBoard[y, x];
            if (!full)
                continue;

            for (var moveY = y; moveY < TetrisHeight - 1; moveY++)
            {
                for (var x = 0; x < TetrisWidth; x++)
                    _tetrisBoard[moveY, x] = _tetrisBoard[moveY + 1, x];
            }

            for (var x = 0; x < TetrisWidth; x++)
                _tetrisBoard[TetrisHeight - 1, x] = false;
            _tetrisLines++;
            y--;
        }
    }

    private void SpawnTetrisPiece()
    {
        _tetrisPiece = TetrisPieces[_random.Next(TetrisPieces.Length)]
            .Select(cell => cell)
            .ToArray();
        _tetrisPosition = new Vector2Int(3, TetrisHeight - 3);
        if (!CanPlaceTetris(_tetrisPiece, _tetrisPosition))
            _tetrisEnded = true;
    }

    private List<Vector2Int> RenderFrame()
    {
        var pixels = new bool[DisplayHeight, DisplayWidth];
        if (Mode == SnakeGridGameMode.PacMan)
            RenderPacMan(pixels);
        else
            RenderTetris(pixels);
        return EncodeSolidPixels(pixels);
    }

    private void RenderPacMan(bool[,] pixels)
    {
        if (_pacEnded)
        {
            RenderEndScreen(pixels, _pacWon ? "WIN" : "END");
            return;
        }

        for (var y = 0; y < DisplayHeight; y++)
        {
            for (var x = 0; x < DisplayWidth; x++)
            {
                if (_pacWalls[y, x] ||
                    _pacPellets[y, x] && _pacAnimationTick % 6 < 4)
                {
                    pixels[y, x] = true;
                }
            }
        }

        SetPixel(pixels, _pacPlayer.x, _pacPlayer.y);
        if (_pacAnimationTick % 4 < 2)
            SetPixel(pixels, _pacPlayer.x - 1, _pacPlayer.y);
        else
            SetPixel(pixels, _pacPlayer.x, _pacPlayer.y - 1);

        foreach (PacGhost ghost in _pacGhosts)
        {
            SetPixel(pixels, ghost.Position.x, ghost.Position.y);
            SetPixel(pixels, ghost.Position.x - 1, ghost.Position.y);
            SetPixel(pixels, ghost.Position.x + 1, ghost.Position.y);
            SetPixel(pixels, ghost.Position.x, ghost.Position.y - 1);
        }
    }

    private void RenderTetris(bool[,] pixels)
    {
        if (_tetrisEnded)
        {
            RenderEndScreen(pixels, "END");
            return;
        }

        for (var y = 0; y < TetrisHeight; y++)
        {
            for (var x = 0; x < TetrisWidth; x++)
            {
                if (_tetrisBoard[y, x])
                    pixels[y, TetrisOffsetX + x] = true;
            }
        }

        foreach (Vector2Int cell in _tetrisPiece)
        {
            int x = _tetrisPosition.x + cell.x;
            int y = _tetrisPosition.y + cell.y;
            if (x >= 0 && x < TetrisWidth && y >= 0 && y < TetrisHeight)
                pixels[y, TetrisOffsetX + x] = true;
        }

        for (var y = 0; y < DisplayHeight; y++)
        {
            pixels[y, TetrisOffsetX - 1] = y % 2 == 0;
            pixels[y, TetrisOffsetX + TetrisWidth] = y % 2 == 0;
        }

        int scorePixels = Math.Min(DisplayHeight, _tetrisLines);
        for (var y = 0; y < scorePixels; y++)
            pixels[DisplayHeight - 1 - y, 0] = true;
    }

    private static void SetPixel(bool[,] pixels, int x, int y)
    {
        if (x >= 0 && x < DisplayWidth && y >= 0 && y < DisplayHeight)
            pixels[y, x] = true;
    }

    private static void RenderEndScreen(bool[,] pixels, string text)
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
            Log.Warn($"[SnakeGridGameEngine] Failed to take over serial {Serial}: {ex}");
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
            Log.Warn($"[SnakeGridGameEngine] Failed to restore serial {Serial}: {ex.Message}");
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
                $"Grid game frame contains {result.Count} segments; the network limit is {byte.MaxValue}.");
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

    private sealed class PacGhost
    {
        public PacGhost(int x, int y)
        {
            Position = new Vector2Int(x, y);
        }

        public Vector2Int Position { get; set; }
    }
}
