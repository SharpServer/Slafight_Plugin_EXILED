using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;

namespace Slafight_Plugin_EXILED.API.Features;

public static class PlayerSpeakerManager
{
    // playerId -> purposeKey -> speaker
    private static readonly Dictionary<int, Dictionary<string, SpeakerApi.LivePlayback>> Speakers
        = new();

    // playerId -> purposeKey -> coroutine
    private static readonly Dictionary<int, Dictionary<string, CoroutineHandle>> FollowCoroutines
        = new();

    private static bool _registered;

    // 用途名の定数例
    public const string PurposeProximity = "proximity";
    public const string PurposeInternalMusic = "internal_music";
    public const string PurposeChaseTheme = "chase_theme";

    public static void RegisterEvents()
    {
        if (_registered) return;

        Log.Debug("[PlayerSpeakerManager] Registering events.");
        Exiled.Events.Handlers.Player.Spawned += OnPlayerSpawned;
        Exiled.Events.Handlers.Player.Died += OnPlayerDied;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        Exiled.Events.Handlers.Server.RestartingRound += OnRoundRestarted;

        _registered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_registered) return;

        Log.Debug("[PlayerSpeakerManager] Unregistering events.");
        Exiled.Events.Handlers.Player.Spawned -= OnPlayerSpawned;
        Exiled.Events.Handlers.Player.Died -= OnPlayerDied;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRoundRestarted;

        DestroyAll();
        _registered = false;
    }

    /// <summary>
    /// 特定プレイヤー・特定用途のスピーカーを取得。
    /// </summary>
    public static bool TryGetSpeaker(Player player, string purpose, out SpeakerApi.LivePlayback speaker)
    {
        speaker = default;
        if (player == null || string.IsNullOrWhiteSpace(purpose))
            return false;

        if (!Speakers.TryGetValue(player.Id, out var dict))
            return false;

        if (!dict.TryGetValue(purpose, out var s) || !s.IsValid)
            return false;

        speaker = s;
        return true;
    }

    /// <summary>
    /// 特定用途のスピーカーを取得 or 作成。
    /// </summary>
    public static SpeakerApi.LivePlayback GetOrCreateSpeaker(
        Player player,
        string purpose,
        bool isSpatial,
        float maxDistance,
        float minDistance,
        string speakerName = null)
    {
        if (player == null || string.IsNullOrWhiteSpace(purpose))
            return default;

        if (!Speakers.TryGetValue(player.Id, out var dict))
        {
            dict = new Dictionary<string, SpeakerApi.LivePlayback>(StringComparer.OrdinalIgnoreCase);
            Speakers[player.Id] = dict;
        }

        if (dict.TryGetValue(purpose, out var speaker) && speaker.IsValid)
        {
            Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: existing for {player.Nickname} (ID: {player.Id}, ControllerId: {speaker.ControllerId})");
            return speaker;
        }

        speakerName ??= purpose;

        speaker = SpeakerApi.CreateLiveSpeaker(
            $"PlayerSpeaker_{player.Id}_{purpose}",
            player.Position,
            null,
            speakerName: speakerName,
            isSpatial: isSpatial,
            maxDistance: maxDistance,
            minDistance: minDistance);

        dict[purpose] = speaker;

        Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: created for {player.Nickname} (ID: {player.Id}, ControllerId: {speaker.ControllerId}) at {player.Position}");

        // 追従コルーチンの管理
        if (!FollowCoroutines.TryGetValue(player.Id, out var followDict))
        {
            followDict = new Dictionary<string, CoroutineHandle>(StringComparer.OrdinalIgnoreCase);
            FollowCoroutines[player.Id] = followDict;
        }

        if (followDict.TryGetValue(purpose, out var oldHandle))
        {
            Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: killing old follow coroutine for {player.Nickname}");
            Timing.KillCoroutines(oldHandle);
        }

        // 用途ごとに追従させるかは呼び出し側で決めたいなら引数を足してもOK
        followDict[purpose] = Timing.RunCoroutine(FollowPlayerCoroutine(player, speaker, purpose));
        Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: started follow coroutine for {player.Nickname}");

        return speaker;
    }

    /// <summary>
    /// 特定用途のスピーカーを破棄。
    /// </summary>
    public static void DestroySpeaker(Player player, string purpose)
    {
        if (player == null || string.IsNullOrWhiteSpace(purpose))
            return;

        int playerId;
        string nickname;
        try
        {
            playerId = player.Id;
            nickname = player.Nickname;
        }
        catch
        {
            return;
        }

        Log.Debug($"[PlayerSpeakerManager] DestroySpeaker[{purpose}]: request for {nickname} (ID: {playerId})");

        if (FollowCoroutines.TryGetValue(playerId, out var followDict) &&
            followDict.TryGetValue(purpose, out var handle))
        {
            Timing.KillCoroutines(handle);
            followDict.Remove(purpose);
            Log.Debug($"[PlayerSpeakerManager] DestroySpeaker[{purpose}]: killed follow coroutine for {nickname}");
        }

        if (Speakers.TryGetValue(playerId, out var dict) &&
            dict.TryGetValue(purpose, out var speaker))
        {
            try
            {
                if (speaker.IsValid)
                    speaker.DestroyAudioPlayer();
            }
            catch (Exception ex)
            {
                Log.Error($"[PlayerSpeakerManager] DestroySpeaker[{purpose}]: failed to destroy speaker: {ex.Message}");
            }

            dict.Remove(purpose);
            Log.Debug($"[PlayerSpeakerManager] DestroySpeaker[{purpose}]: destroyed speaker for {nickname} (ControllerId: {speaker.ControllerId})");
        }
    }

    /// <summary>
    /// そのプレイヤーのスピーカー（全用途）を破棄。
    /// </summary>
    public static void DestroyAllForPlayer(Player player)
    {
        if (player == null) return;

        int playerId;
        string nickname;
        try
        {
            playerId = player.Id;
            nickname = player.Nickname;
        }
        catch
        {
            return;
        }

        Log.Debug($"[PlayerSpeakerManager] DestroyAllForPlayer: {nickname} (ID: {playerId})");

        if (FollowCoroutines.TryGetValue(playerId, out var followDict))
        {
            foreach (var handle in followDict.Values)
                Timing.KillCoroutines(handle);
            FollowCoroutines.Remove(playerId);
        }

        if (Speakers.TryGetValue(playerId, out var dict))
        {
            foreach (var s in dict.Values)
            {
                if (s.IsValid)
                    s.DestroyAudioPlayer();
            }
            Speakers.Remove(playerId);
        }
    }

    public static void DestroyAll()
    {
        Log.Debug("[PlayerSpeakerManager] DestroyAll: destroying all speakers and follow coroutines.");

        foreach (var followDict in FollowCoroutines.Values)
        {
            foreach (var handle in followDict.Values)
                Timing.KillCoroutines(handle);
        }
        FollowCoroutines.Clear();

        foreach (var dict in Speakers.Values)
        {
            foreach (var speaker in dict.Values)
            {
                if (speaker.IsValid)
                    speaker.DestroyAudioPlayer();
            }
        }
        Speakers.Clear();
    }

    // ==== イベント ====

    private static void OnPlayerSpawned(SpawnedEventArgs ev)
    {
        if (ev.Player == null) return;

        Log.Debug($"[PlayerSpeakerManager] OnPlayerSpawned: {ev.Player.Nickname} as {ev.Player.Role} (IsAlive: {ev.Player.IsAlive})");

        if (!ShouldManageSpeaker(ev.Player))
        {
            DestroyAllForPlayer(ev.Player);
            return;
        }

        if (ev.Player.IsAlive &&
            ev.Player.Role != PlayerRoles.RoleTypeId.Spectator &&
            ev.Player.Role != PlayerRoles.RoleTypeId.None)
        {
            var player = ev.Player;
            // Proximity 用だけ自動生成する例
            Timing.CallDelayed(1.5f, () =>
            {
                try
                {
                    if (ShouldManageSpeaker(player) && player.IsAlive)
                    {
                        GetOrCreateSpeaker(
                            player,
                            PurposeProximity,
                            isSpatial: ProximityChat.Handler.AudioIsSpatial,
                            maxDistance: ProximityChat.Handler.AudioMaxDistance,
                            minDistance: ProximityChat.Handler.AudioMinDistance,
                            speakerName: "ProximityVoice");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[PlayerSpeakerManager] OnPlayerSpawned delayed creation error: {ex}");
                }
            });
        }
        else
        {
            DestroyAllForPlayer(ev.Player);
        }
    }

    private static void OnPlayerDied(DiedEventArgs ev)
    {
        if (ev.Player != null)
        {
            Log.Debug($"[PlayerSpeakerManager] OnPlayerDied: {ev.Player.Nickname} died.");
            DestroyAllForPlayer(ev.Player);
        }
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player != null)
        {
            Log.Debug($"[PlayerSpeakerManager] OnPlayerLeft: {ev.Player.Nickname} left.");
            DestroyAllForPlayer(ev.Player);
        }
    }

    private static void OnRoundRestarted()
    {
        Log.Debug("[PlayerSpeakerManager] OnRoundRestarted: round restarting, cleaning up.");
        DestroyAll();
    }

    // 用途キーをログに出せるように拡張
    private static IEnumerator<float> FollowPlayerCoroutine(
        Player player,
        SpeakerApi.LivePlayback liveSpeaker,
        string purpose)
    {
        Log.Debug($"[PlayerSpeakerManager] FollowPlayerCoroutine[{purpose}]: started for {player.Nickname} (ControllerId: {liveSpeaker.ControllerId})");

        while (true)
        {
            bool shouldContinue;
            try
            {
                shouldContinue = ShouldManageSpeaker(player) && player.IsAlive && liveSpeaker.IsValid;
                if (shouldContinue)
                    liveSpeaker.SetTransform(player.Position, null);
            }
            catch (Exception ex)
            {
                Log.Error($"[PlayerSpeakerManager] FollowPlayerCoroutine[{purpose}]: error: {ex.Message}");
                shouldContinue = false;
            }

            if (!shouldContinue)
                break;

            yield return Timing.WaitForSeconds(0.1f);
        }

        Log.Debug($"[PlayerSpeakerManager] FollowPlayerCoroutine[{purpose}]: stopped for {player?.Nickname ?? "Unknown"}");
    }

    private static bool ShouldManageSpeaker(Player player)
    {
        return player != null
               && player.IsConnected
               && player.ReferenceHub != null
               && !player.IsNPC
               && !player.IsHost
               && !player.ReferenceHub.IsHost
               && !CRole.IsTeamNpc(player);
    }
}
