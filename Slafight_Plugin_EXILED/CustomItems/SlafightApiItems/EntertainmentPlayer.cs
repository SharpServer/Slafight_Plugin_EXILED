using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using SNAPI.Events.EventArgs;
using SNAPI.Events.Handlers;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

/// <summary>
/// A single Chaos Keycard entertainment system containing media playback and
/// interactive games. Throw cycles modes; a normal drop remains untouched.
/// </summary>
public sealed class EntertainmentPlayer : CItem
{
    private const string BadAppleSource = "https://www.nicovideo.jp/watch/sm8628149";
    private const float BadAppleFramesPerSecond = 15f;
    private const int BadAppleMaxFrames = 4000;
    private const float AudioMaxDistance = 12f;
    private const float AudioMinDistance = 1f;
    private const float AudioVolume = 1f;

    private static readonly EntertainmentMode[] Modes =
        (EntertainmentMode[])Enum.GetValues(typeof(EntertainmentMode));

    private readonly Dictionary<ushort, EntertainmentMode> _selectedModes = new();
    private readonly Dictionary<ushort, ISnakeGameSession> _gameSessions = new();
    private readonly HashSet<ushort> _mediaSerials = new();

    public override string DisplayName => "Chaos Entertainment System [TEST]";
    public override string Description =>
        "Chaos KeycardのSnake画面でBad Apple、DOOM、PAC-MAN、TETRISを楽しめる端末。\n" +
        "投げる操作でモード切替、Inspectで起動。通常ドロップはそのまま行えます。";

    protected override string UniqueKey => "EntertainmentPlayer";
    protected override ItemType BaseItem => ItemType.KeycardChaosInsurgency;

    public override void RegisterEvents()
    {
        SnakePlayer.SwitchAxes += OnSnakeInput;
        Exiled.Events.Handlers.Player.ChangingItem += OnPlayerChangingItem;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        SnakePlayer.SwitchAxes -= OnSnakeInput;
        Exiled.Events.Handlers.Player.ChangingItem -= OnPlayerChangingItem;
        ClearRuntimeState(restoreSnake: false);
        base.UnregisterEvents();
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        if (ev.Item != null)
            _selectedModes.TryAdd(ev.Item.Serial, EntertainmentMode.BadApple);

        base.OnAcquired(ev, displayMessage);
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        if (!ev.IsThrown)
            return;

        ev.IsAllowed = false;
        ushort serial = ev.Item.Serial;
        StopActivity(serial, restoreSnake: true);

        EntertainmentMode current = GetSelectedMode(serial);
        EntertainmentMode next = Modes[(Array.IndexOf(Modes, current) + 1) % Modes.Length];
        _selectedModes[serial] = next;
        ev.Player.ShowHint(
            "<color=#57d7ff><b>ENTERTAINMENT MODE</b></color>\n" +
            $"<color=#ffffff>{GetModeName(next)}</color>\n" +
            "<color=#aaaaaa>Inspectで起動 / 投げる操作で次へ</color>",
            3.5f);
    }

    protected override void OnInspectedKeycard(PlayerInspectedKeycardEventArgs ev)
    {
        base.OnInspectedKeycard(ev);
        if (ev.Player?.ReferenceHub == null || ev.KeycardItem == null)
            return;

        ushort serial = ev.KeycardItem.Serial;
        if (!TryGet(serial, out CItem? item) || !ReferenceEquals(item, this))
            return;

        Player? player = Player.Get(ev.Player.ReferenceHub);
        if (player == null || player.IsNPC || !player.IsSafePlayer() ||
            player.CurrentItem?.Serial != serial ||
            IsActivityRunning(serial))
        {
            return;
        }

        StartSelectedMode(player, serial);
    }

    protected override void OnReleased(ItemRemovedEventArgs ev)
    {
        if (ev.Item != null)
            StopActivity(ev.Item.Serial, restoreSnake: true);

        base.OnReleased(ev);
    }

    protected override void OnOwnerDying(DyingEventArgs ev)
    {
        if (ev.Player != null)
        {
            foreach (ushort serial in ev.Player.Items
                         .Where(item => item != null && Check(item))
                         .Select(item => item.Serial)
                         .ToArray())
            {
                StopActivity(serial, restoreSnake: false);
            }
        }

        base.OnOwnerDying(ev);
    }

    protected override void OnSerialUntracked(ushort serial)
    {
        StopActivity(serial, restoreSnake: false);
        _selectedModes.Remove(serial);
        base.OnSerialUntracked(serial);
    }

    protected override void OnWaitingForPlayers()
    {
        ClearRuntimeState(restoreSnake: false);
        base.OnWaitingForPlayers();
    }

    private void StartSelectedMode(Player player, ushort serial)
    {
        EntertainmentMode mode = GetSelectedMode(serial);
        try
        {
            switch (mode)
            {
                case EntertainmentMode.BadApple:
                    StartBadApple(player, serial);
                    break;
                case EntertainmentMode.Doom:
                    StartGame(
                        player,
                        serial,
                        new SnakeDoomEngine(player, serial),
                        "<color=#d92323><b>DOOM</b></color>\n" +
                        "<color=#ffffff>↑ 前進　← → 旋回　↓ 射撃</color>\n" +
                        "<color=#aaaaaa>敵を全滅させて出口へ。終了画面では↓で再開。</color>");
                    break;
                case EntertainmentMode.PacMan:
                    StartGame(
                        player,
                        serial,
                        new SnakeGridGameEngine(player, serial, SnakeGridGameMode.PacMan),
                        "<color=#ffe45c><b>PAC-MAN</b></color>\n" +
                        "<color=#ffffff>↑ ↓ ← → 移動</color>\n" +
                        "<color=#aaaaaa>点をすべて集め、ゴーストを避けよう。</color>");
                    break;
                case EntertainmentMode.Tetris:
                    StartGame(
                        player,
                        serial,
                        new SnakeGridGameEngine(player, serial, SnakeGridGameMode.Tetris),
                        "<color=#8be9fd><b>TETRIS</b></color>\n" +
                        "<color=#ffffff>← → 移動　↑ 回転　↓ ソフトドロップ</color>");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            StopActivity(serial, restoreSnake: false);
            Log.Error($"[EntertainmentPlayer] Failed to start {mode} on serial {serial}: {ex}");
            player.ShowHint(
                $"<color=#ff7777>{GetModeName(mode)}を開始できませんでした。</color>\n" +
                $"<size=18>{ex.Message}</size>",
                6f);
        }
    }

    private void StartBadApple(Player player, ushort serial)
    {
        SnakeMediaApi.PlayPixelMedia(
            player,
            serial,
            BadAppleSource,
            BadAppleFramesPerSecond,
            BadAppleMaxFrames,
            invert: true,
            renderStyle: SnakeImageRenderStyle.AbstractSilhouette,
            abstractionLevel: 2,
            maxDistance: AudioMaxDistance,
            minDistance: AudioMinDistance,
            volume: AudioVolume,
            audioPlayerName: $"Entertainment_BadApple_{serial}");
        _mediaSerials.Add(serial);
        player.ShowHint(
            "<color=#ffffff><b>Bad Apple!!</b></color>\n" +
            "<color=#aaaaaa>映像とSpatial音声を同期ロードしています。</color>",
            4f);
    }

    private void StartGame(
        Player player,
        ushort serial,
        ISnakeGameSession session,
        string hint)
    {
        StopActivity(serial, restoreSnake: false);
        _gameSessions.Add(serial, session);
        try
        {
            session.Start();
        }
        catch
        {
            _gameSessions.Remove(serial);
            session.Dispose();
            throw;
        }

        player.ShowHint(hint, 8f);
    }

    private void OnSnakeInput(SwitchAxesEventArgs ev)
    {
        if (ev?.Keycard == null ||
            !_gameSessions.TryGetValue(ev.Keycard.Serial, out ISnakeGameSession? session) ||
            !session.IsRunning ||
            session.PlayerId != ev.Player?.Id)
        {
            return;
        }

        session.HandleInput(ev.Context.Direction);
    }

    private void OnPlayerChangingItem(ChangingItemEventArgs ev)
    {
        var previousItem = ev?.Player?.CurrentItem;
        if (previousItem == null ||
            !Check(previousItem) ||
            ev.Item?.Serial == previousItem.Serial)
        {
            return;
        }

        StopActivity(previousItem.Serial, restoreSnake: true);
    }

    private EntertainmentMode GetSelectedMode(ushort serial)
    {
        if (_selectedModes.TryGetValue(serial, out EntertainmentMode mode))
            return mode;

        _selectedModes[serial] = EntertainmentMode.BadApple;
        return EntertainmentMode.BadApple;
    }

    private bool IsActivityRunning(ushort serial)
    {
        if (_gameSessions.TryGetValue(serial, out ISnakeGameSession? session))
        {
            if (session.IsRunning)
                return true;

            _gameSessions.Remove(serial);
            session.Dispose();
        }

        bool mediaRunning = SnakeMediaApi.Active.Any(playback => playback.Serial == serial);
        if (!mediaRunning)
            _mediaSerials.Remove(serial);
        return mediaRunning;
    }

    private void StopActivity(ushort serial, bool restoreSnake)
    {
        SnakeMediaApi.Stop(serial);
        _mediaSerials.Remove(serial);
        if (!_gameSessions.Remove(serial, out ISnakeGameSession? session))
            return;

        session.Stop(restoreSnake);
    }

    private void ClearRuntimeState(bool restoreSnake)
    {
        foreach (ushort serial in _mediaSerials.ToArray())
            SnakeMediaApi.Stop(serial);
        _mediaSerials.Clear();
        ISnakeGameSession[] sessions = _gameSessions.Values.ToArray();
        _gameSessions.Clear();
        foreach (ISnakeGameSession session in sessions)
            session.Stop(restoreSnake);
        _selectedModes.Clear();
    }

    private static string GetModeName(EntertainmentMode mode)
    {
        return mode switch
        {
            EntertainmentMode.BadApple => "Bad Apple!!",
            EntertainmentMode.Doom => "DOOM",
            EntertainmentMode.PacMan => "PAC-MAN",
            EntertainmentMode.Tetris => "TETRIS",
            _ => mode.ToString(),
        };
    }

    private enum EntertainmentMode
    {
        BadApple,
        Doom,
        PacMan,
        Tetris,
    }
}
