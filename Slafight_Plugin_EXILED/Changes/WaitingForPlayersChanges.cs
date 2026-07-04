using System;
using System.Collections.Generic;
using System.Linq;
using AdminToys;
using CustomPlayerEffects;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using Mirror;
using PlayerRoles;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.SpecialEvents;
using UnityEngine;
using VoiceChat.Networking;
using Object = UnityEngine.Object;

namespace Slafight_Plugin_EXILED.Changes;

public class WaitingForPlayersChanges : IBootstrapHandler
{
    private const string WaitingRoomSchematicName = "OldMenuRoom";
    private const string PlayerCountBlockName = "PlayerCountText";
    private const string NextEventBlockName = "NextEventText";
    private const string RemainingTimeBlockName = "RemainingTimeText";

    private static readonly Vector3 WaitingRoomPosition = new(246.92f, 198.50f, -60.89f);

    private const string WaitingMusicClipName = "finalflash.ogg";
    private const float WaitingMusicStartDelay = 1.5f; // メニューテーマが消えるまで待つ
    private const float WaitingMusicFadeInDuration = 3f;

    // 実ファイルは未用意。差し替えるまでは LoadClip/Play が失敗し警告ログのみ出る。
    private const string RoundStartOutroClipName = "finalflash_outro.ogg";

    private const float RoundStartTriggerRemainingTime = 1f;
    private static readonly Vector3 RoundStartMovePosition = new(247.15f, 199.30f, -63.33f);
    private static readonly Vector3 RoundStartFadeEndPosition = new(247.15f, 199.30f, -63.64f);
    private static readonly Quaternion RoundStartRotation = Quaternion.Euler(0f, 180f, 0f);
    private const float RoundStartMoveDuration = 1.6f;
    private const float RoundStartFadeDuration = 3f;
    private const float RoundStartOutroExtraHold = -7.77f; // 開始タイミング調整用

    public static void Register()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Player.Verified += OnVerified;
        Exiled.Events.Handlers.Player.VoiceChatting += OnVoiceChatting;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Player.Verified -= OnVerified;
        Exiled.Events.Handlers.Player.VoiceChatting -= OnVoiceChatting;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        ResetWaitingRoomTextRefs();
        TutorialWaitingPlayers.Clear();
        StopAllWaitingMusic();
        _roundStartTransitionTriggered = false;
    }

    /// <summary>
    /// Waiting 中にこの Changes が Tutorial へ移行させたプレイヤー。
    /// FirstRolesHandler.SetupRandomRoles は IsRoleUnassigned() (Spectator/None) しか拾わないため、
    /// ここに集めて合流させる。
    /// </summary>
    public static readonly HashSet<Player> TutorialWaitingPlayers = new();

    // プレイヤーごとのロビー BGM(finalflash.ogg)の再生管理
    private static readonly Dictionary<int, SpeakerApi.Playback> WaitingMusicPlaybacks = new();

    private static CoroutineHandle _handle;
    private static readonly List<CoroutineHandle> _roundStartTransitionHandles = new();
    private static TextToy? _playerCountText;
    private static TextToy? _nextEventText;
    private static TextToy? _remainingTimeText;
    private static bool _roundStartTransitionTriggered;

    private static void OnWaitingForPlayers()
    {
        GameObject.Find("StartRound")?.transform.localScale = Vector3.zero;
        ResetWaitingRoomTextRefs();
        TutorialWaitingPlayers.Clear();
        StopAllWaitingMusic();
        _roundStartTransitionTriggered = false;
        Round.IsLobbyLocked = false; // 前ラウンドの演出が異常終了した場合の保険
        KillRoundStartTransitionCoroutines();
        _handle = Timing.RunCoroutine(Coroutine());

        // スムーズにアウトロへ切り替えられるよう、Waiting 開始時点でクリップを先読みしておく
        TryPreloadClip(WaitingMusicClipName);
        TryPreloadClip(RoundStartOutroClipName);
    }

    private static void TryPreloadClip(string fileName)
    {
        try
        {
            SpeakerApi.LoadClip(fileName);
        }
        catch (Exception ex)
        {
            Log.Warn($"[WaitingForPlayersChanges] Failed to preload clip '{fileName}': {ex.Message}");
        }
    }

    private static void ResetWaitingRoomTextRefs()
    {
        _playerCountText = null;
        _nextEventText = null;
        _remainingTimeText = null;
    }

    private static void OnVerified(VerifiedEventArgs ev)
    {
        if (ev.Player is null || !ev.Player.IsConnected || ev.Player.IsNPC || !ev.Player.IsNotHost()) return;
        if (!Round.IsLobby) return;
        ev.Player.Role.Set(RoleTypeId.Tutorial, RoleSpawnFlags.None);
        ev.Player.Rotation *= Quaternion.Euler(0f, 158f, 0f);

        foreach (Player other in TutorialWaitingPlayers)
        {
            PlayerVisibilitySyncProvider.TrySetHiddenFor(ev.Player, other, true);
            PlayerVisibilitySyncProvider.TrySetHiddenFor(other, ev.Player, true);
        }

        TutorialWaitingPlayers.Add(ev.Player);

        Player joined = ev.Player;
        Timing.CallDelayed(WaitingMusicStartDelay, () => StartWaitingMusic(joined));
        Timing.CallDelayed(0.1f, () =>
        {
            if (ev.Player?.ReferenceHub?.playerEffectsController is null) return;
            joined.EnableEffect<Fade>();
        });
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player == null) return;

        if (WaitingMusicPlaybacks.TryGetValue(ev.Player.Id, out var playback))
        {
            playback.Stop();
            WaitingMusicPlaybacks.Remove(ev.Player.Id);
        }
    }

    private static void StartWaitingMusic(Player player)
    {
        if (player?.ReferenceHub == null || !player.IsConnected) return;
        if (!Round.IsLobby || _roundStartTransitionTriggered) return;
        if (!TutorialWaitingPlayers.Contains(player)) return;
        if (WaitingMusicPlaybacks.ContainsKey(player.Id)) return;

        int ownerId = player.Id;
        SpeakerApi.Playback playback = SpeakerApi.PlayLoop(
            WaitingMusicClipName,
            $"WaitingForPlayers_RoomMusic_{player.Id}",
            player.Position,
            player.Transform,
            maxDistance: 10f,
            minDistance: 0.1f,
            volume: 0f,
            listeners: p => p != null && p.Id == ownerId);

        WaitingMusicPlaybacks[player.Id] = playback;
        Timing.RunCoroutine(FadeInWaitingMusic(player.Id, playback));
    }

    private static IEnumerator<float> FadeInWaitingMusic(int playerId, SpeakerApi.Playback playback)
    {
        float elapsed = 0f;
        while (elapsed < WaitingMusicFadeInDuration)
        {
            if (!playback.IsValid || _roundStartTransitionTriggered)
                yield break;

            elapsed += Time.deltaTime;
            playback.SetVolume(Mathf.Clamp01(elapsed / WaitingMusicFadeInDuration));
            yield return 0f;
        }

        if (playback.IsValid)
            playback.SetVolume(1f);
    }

    private static void StopAllWaitingMusic()
    {
        foreach (SpeakerApi.Playback playback in WaitingMusicPlaybacks.Values)
            playback.Stop();

        WaitingMusicPlaybacks.Clear();
    }

    private static void OnVoiceChatting(VoiceChattingEventArgs ev)
    {
        if (ev.Player is null || !TutorialWaitingPlayers.Contains(ev.Player))
            return;

        // 疑似待機画面ではチャンネル判定(距離・ミュート等)を無視し、本来の Lobby と同様に全プレイヤーへ直接中継する
        ev.IsAllowed = false;

        VoiceMessage message = ev.VoiceMessage;
        foreach (ReferenceHub hub in ReferenceHub.AllHubs)
        {
            if (hub == null || hub.connectionToClient == null)
                continue;

            hub.connectionToClient.Send(message);
        }
    }

    private static void OnRoundStarted()
    {
        Timing.KillCoroutines(_handle);
        KillRoundStartTransitionCoroutines();
    }

    private static void KillRoundStartTransitionCoroutines()
    {
        foreach (CoroutineHandle handle in _roundStartTransitionHandles)
            Timing.KillCoroutines(handle);

        _roundStartTransitionHandles.Clear();
    }

    private static void TriggerRoundStartTransition()
    {
        // 演出が終わるまで実際のラウンド開始を足止めする。
        // CharacterClassManager.Init() の内部ループは自前の timeLeft を毎秒 NetworkTimer に書き戻すため、
        // LobbyWaitingTime を直接いじっても次の tick で上書きされてしまう。LobbyLock はそのループ自体が
        // 参照している分岐条件なので、こちらで確実に足止めできる。
        Round.IsLobbyLocked = true;

        // ラウンド再開までの待ち時間 = outro の実際の長さ + ExtraHold。
        // Move/Fade の演出時間による下限は設けない。ExtraHold を十分小さくすれば
        // 移動/Blindness コルーチンの途中でもラウンドを開始できる(意図的な調整幅として許容する)。
        float outroDuration = SpeakerApi.GetClipDuration(RoundStartOutroClipName);
        float resumeDelay = Mathf.Max(0f, outroDuration + RoundStartOutroExtraHold);

        foreach (Player player in TutorialWaitingPlayers.ToArray())
        {
            if (player?.ReferenceHub == null || !player.IsConnected)
                continue;

            // それまで流れていたロビー BGM を打ち切り、代わりにラウンド開始用の outro を再生する
            if (WaitingMusicPlaybacks.TryGetValue(player.Id, out var musicPlayback))
            {
                musicPlayback.Stop();
                WaitingMusicPlaybacks.Remove(player.Id);
            }

            try
            {
                int ownerId = player.Id;
                SpeakerApi.Play(
                    RoundStartOutroClipName,
                    $"WaitingForPlayers_RoundStartOutro_{player.Id}",
                    player.Position,
                    destroyOnEnd: true,
                    parent: player.Transform,
                    maxDistance: 10f,
                    minDistance: 0.1f,
                    volume: 1f,
                    listeners: p => p != null && p.Id == ownerId);
            }
            catch (Exception ex)
            {
                Log.Warn($"[WaitingForPlayersChanges] Failed to play round start outro for {player.Nickname}: {ex.Message}");
            }

            _roundStartTransitionHandles.Add(Timing.RunCoroutine(RoundStartTransitionCoroutine(player)));
        }

        Timing.CallDelayed(resumeDelay, () =>
        {
            // 演出 / outro 終了。ロックを解除し、自然なタイマー再開を待たず直接ラウンドを開始する
            Round.IsLobbyLocked = false;
            if (!Round.IsLobby) return;
            Round.Start();
        });
    }

    private static IEnumerator<float> RoundStartTransitionCoroutine(Player player)
    {
        if (player?.ReferenceHub == null || !Round.IsLobby) yield break;

        player.EnableEffect<Blindness>(0);

        Vector3 startPos = player.Position;
        Quaternion startRotation = player.Rotation;
        float elapsed = 0f;
        while (elapsed < RoundStartMoveDuration)
        {
            // ExtraHold を切り詰めるとラウンドが先に始まりうる。その場合は実スポーンの位置を壊さないよう中断する。
            if (player?.ReferenceHub == null || !Round.IsLobby) yield break;

            elapsed += Time.deltaTime;
            float moveT = elapsed / RoundStartMoveDuration;
            player.Position = Vector3.Lerp(startPos, RoundStartMovePosition, moveT);
            player.Rotation = Quaternion.Lerp(startRotation, RoundStartRotation, moveT);
            yield return 0f;
        }

        if (player?.ReferenceHub == null || !Round.IsLobby) yield break;
        player.Position = RoundStartMovePosition;
        player.Rotation = RoundStartRotation;

        elapsed = 0f;
        while (elapsed < RoundStartFadeDuration)
        {
            if (player?.ReferenceHub == null || !Round.IsLobby) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / RoundStartFadeDuration;
            player.Position = Vector3.Lerp(RoundStartMovePosition, RoundStartFadeEndPosition, t);
            player.Rotation = RoundStartRotation;

            if (player.TryGetEffect(out Blindness blindness))
                blindness.Intensity = (byte)Mathf.RoundToInt(255 * t);

            yield return 0f;
        }

        if (player?.ReferenceHub == null || !Round.IsLobby) yield break;
        player.Position = RoundStartFadeEndPosition;
        player.Rotation = RoundStartRotation;
        if (player.TryGetEffect(out Blindness finalBlindness))
            finalBlindness.Intensity = 255;
    }

    private static IEnumerator<float> Coroutine()
    {
        yield return Timing.WaitForSeconds(0.5f);
        while (true)
        {
            if (!Round.IsLobby) yield break;

            // Timer は人数不足でカウントダウン未開始のとき -2 になるため、0 より大きい実カウントダウン中のみ判定する
            if (!_roundStartTransitionTriggered
                && Round.LobbyWaitingTime > 0
                && Round.LobbyWaitingTime <= RoundStartTriggerRemainingTime)
            {
                _roundStartTransitionTriggered = true;
                TriggerRoundStartTransition();
            }

            if (!_roundStartTransitionTriggered)
            {
                var list = Player.List.Where(p => !p.IsNPC && p.IsNotHost()).ToList();
                list.ForEach(p =>
                {
                    p.Position = WaitingRoomPosition;
                    p.IsNoclipEnabled = true;
                    p.IsGodModeEnabled = true;
                });
            }

            UpdateWaitingRoomTexts(Player.List.Count(p => p.IsNotHost()));

            yield return Timing.WaitForSeconds(0.05f);
        }
    }

    private static void UpdateWaitingRoomTexts(int playerCount)
    {
        if (!EnsureWaitingRoomTextRefs())
            return;

        _playerCountText?.TextFormat = $"<b><u>{playerCount} / {Server.MaxPlayerCount}</u></b>";

        SpecialEventType nextEvent = SpecialEventsHandler.Instance.EventQueue.FirstOrDefault();
        string nextEventName = nextEvent.ToString();
        _nextEventText?.TextFormat = $"<b><u>Next Event: {nextEventName}</u></b>";

        // 演出中(LobbyWaitingTime を RoundStartStallTime まで足止めしている間)は 0 固定表示にする
        float time = _roundStartTransitionTriggered ? 0f : Round.LobbyWaitingTime;
        _remainingTimeText?.TextFormat = $"<b><u>Remaining Time to Start: {(int)time}</u></b>";
    }

    private static bool EnsureWaitingRoomTextRefs()
    {
        if (_playerCountText != null && _nextEventText != null && _remainingTimeText != null)
            return true;

        SchematicObject schematic = Object
            .FindObjectsByType<SchematicObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(s => s.Name == WaitingRoomSchematicName);

        if (schematic == null)
            return false;

        _playerCountText ??= schematic.FindBlock(PlayerCountBlockName, allowPartial: false)?.GetComponent<TextToy>();
        _nextEventText ??= schematic.FindBlock(NextEventBlockName, allowPartial: false)?.GetComponent<TextToy>();
        _remainingTimeText ??= schematic.FindBlock(RemainingTimeBlockName, allowPartial: false)?.GetComponent<TextToy>();

        return _playerCountText != null && _nextEventText != null && _remainingTimeText != null;
    }
}
