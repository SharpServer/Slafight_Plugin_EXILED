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

    private static readonly Dictionary<int, int> PlayerVersions = new();

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
        if (!ShouldManageSpeaker(player) || string.IsNullOrWhiteSpace(purpose))
            return false;

        var playerId = player.Id;
        if (!Speakers.TryGetValue(playerId, out var dict))
            return false;

        if (!dict.TryGetValue(purpose, out var s) || !s.IsValid)
        {
            DestroySpeaker(playerId, purpose);
            return false;
        }

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
        float volume = 1f,
        string speakerName = null)
    {
        if (!ShouldManageSpeaker(player) || string.IsNullOrWhiteSpace(purpose))
            return default;

        var playerId = player.Id;
        if (!Speakers.TryGetValue(playerId, out var dict))
        {
            dict = new Dictionary<string, SpeakerApi.LivePlayback>(StringComparer.OrdinalIgnoreCase);
            Speakers[playerId] = dict;
        }

        if (dict.TryGetValue(purpose, out var speaker) && speaker.IsValid)
        {
            Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: existing for {player.Nickname} (ID: {playerId}, ControllerId: {speaker.ControllerId})");
            return speaker;
        }

        if (dict.ContainsKey(purpose))
        {
            DestroySpeaker(playerId, purpose);
            if (!Speakers.TryGetValue(playerId, out dict))
            {
                dict = new Dictionary<string, SpeakerApi.LivePlayback>(StringComparer.OrdinalIgnoreCase);
                Speakers[playerId] = dict;
            }
        }

        speakerName ??= purpose;

        speaker = SpeakerApi.CreateLiveSpeaker(
            $"PlayerSpeaker_{playerId}_{purpose}",
            player.Position,
            null,
            speakerName: speakerName,
            isSpatial: isSpatial,
            maxDistance: maxDistance,
            minDistance: minDistance,
            volume: volume);

        dict[purpose] = speaker;

        Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: created for {player.Nickname} (ID: {playerId}, ControllerId: {speaker.ControllerId}) at {player.Position}");

        // 追従コルーチンの管理
        if (!FollowCoroutines.TryGetValue(playerId, out var followDict))
        {
            followDict = new Dictionary<string, CoroutineHandle>(StringComparer.OrdinalIgnoreCase);
            FollowCoroutines[playerId] = followDict;
        }

        if (followDict.TryGetValue(purpose, out var oldHandle))
        {
            Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: killing old follow coroutine for {player.Nickname}");
            Timing.KillCoroutines(oldHandle);
        }

        // 用途ごとに追従させるかは呼び出し側で決めたいなら引数を足してもOK
        followDict[purpose] = Timing.RunCoroutine(FollowPlayerCoroutine(playerId, speaker, purpose));
        Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: started follow coroutine for {player.Nickname}");

        return speaker;
    }

    /// <summary>
    /// 特定用途のスピーカーを破棄。
    /// </summary>
    public static void DestroySpeaker(Player player, string purpose)
    {
        if (player == null)
            return;

        DestroySpeaker(player.Id, purpose);
    }

    public static void DestroySpeaker(int playerId, string purpose)
    {
        if (playerId <= 0 || string.IsNullOrWhiteSpace(purpose))
            return;

        var nickname = GetPlayerName(playerId);
        Log.Debug($"[PlayerSpeakerManager] DestroySpeaker[{purpose}]: request for {nickname} (ID: {playerId})");

        if (FollowCoroutines.TryGetValue(playerId, out var followDict) &&
            followDict.TryGetValue(purpose, out var handle))
        {
            Timing.KillCoroutines(handle);
            followDict.Remove(purpose);
            if (followDict.Count == 0)
                FollowCoroutines.Remove(playerId);

            Log.Debug($"[PlayerSpeakerManager] DestroySpeaker[{purpose}]: killed follow coroutine for {nickname}");
        }

        if (Speakers.TryGetValue(playerId, out var dict) &&
            dict.TryGetValue(purpose, out var speaker))
        {
            try
            {
                speaker.DestroyAudioPlayer();
            }
            catch (Exception ex)
            {
                Log.Error($"[PlayerSpeakerManager] DestroySpeaker[{purpose}]: failed to destroy speaker: {ex.Message}");
            }

            dict.Remove(purpose);
            if (dict.Count == 0)
                Speakers.Remove(playerId);

            Log.Debug($"[PlayerSpeakerManager] DestroySpeaker[{purpose}]: destroyed speaker for {nickname} (ControllerId: {speaker.ControllerId})");
        }
    }

    /// <summary>
    /// そのプレイヤーのスピーカー（全用途）を破棄。
    /// </summary>
    public static void DestroyAllForPlayer(Player player)
    {
        if (player == null) return;

        DestroyAllForPlayer(player.Id);
    }

    public static void DestroyAllForPlayer(int playerId)
    {
        if (playerId <= 0) return;

        var nickname = GetPlayerName(playerId);
        Log.Debug($"[PlayerSpeakerManager] DestroyAllForPlayer: {nickname} (ID: {playerId})");
        InvalidatePlayerVersion(playerId);

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
                speaker.DestroyAudioPlayer();
            }
        }
        Speakers.Clear();
        PlayerVersions.Clear();
    }

    // ==== イベント ====

    private static void OnPlayerSpawned(SpawnedEventArgs ev)
    {
        if (ev.Player == null) return;

        Log.Debug($"[PlayerSpeakerManager] OnPlayerSpawned: {ev.Player.Nickname} as {ev.Player.Role} (IsAlive: {ev.Player.IsAlive})");
        var playerId = ev.Player.Id;
        var version = NextPlayerVersion(playerId);

        if (!ShouldManageSpeaker(ev.Player))
        {
            DestroyAllForPlayer(playerId);
            return;
        }

        if (ev.Player.IsAlive &&
            ev.Player.Role != PlayerRoles.RoleTypeId.Spectator &&
            ev.Player.Role != PlayerRoles.RoleTypeId.None)
        {
            // Proximity 用だけ自動生成する例
            Timing.CallDelayed(1.5f, () =>
            {
                try
                {
                    if (!IsCurrentPlayerVersion(playerId, version))
                        return;

                    var player = GetManagedPlayer(playerId);
                    if (ShouldManageSpeaker(player) && player.IsAlive)
                    {
                        GetOrCreateSpeaker(
                            player,
                            PurposeProximity,
                            isSpatial: ProximityChat.Handler.AudioIsSpatial,
                            maxDistance: ProximityChat.Handler.AudioMaxDistance,
                            minDistance: ProximityChat.Handler.AudioMinDistance,
                            volume: ProximityChat.Handler.AudioVolume,
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
            DestroyAllForPlayer(playerId);
        }
    }

    private static void OnPlayerDied(DiedEventArgs ev)
    {
        if (ev.Player != null)
        {
            Log.Debug($"[PlayerSpeakerManager] OnPlayerDied: {ev.Player.Nickname} died.");
            DestroyAllForPlayer(ev.Player.Id);
        }
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player != null)
        {
            Log.Debug($"[PlayerSpeakerManager] OnPlayerLeft: {ev.Player.Nickname} left.");
            DestroyAllForPlayer(ev.Player.Id);
        }
    }

    private static void OnRoundRestarted()
    {
        Log.Debug("[PlayerSpeakerManager] OnRoundRestarted: round restarting, cleaning up.");
        DestroyAll();
    }

    // 用途キーをログに出せるように拡張
    private static IEnumerator<float> FollowPlayerCoroutine(
        int playerId,
        SpeakerApi.LivePlayback liveSpeaker,
        string purpose)
    {
        Log.Debug($"[PlayerSpeakerManager] FollowPlayerCoroutine[{purpose}]: started for {GetPlayerName(playerId)} (ControllerId: {liveSpeaker.ControllerId})");

        while (true)
        {
            bool shouldContinue;
            try
            {
                var player = GetManagedPlayer(playerId);
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

        CleanupStoppedFollow(playerId, purpose, liveSpeaker);
        Log.Debug($"[PlayerSpeakerManager] FollowPlayerCoroutine[{purpose}]: stopped for {GetPlayerName(playerId)}");
    }

    private static bool ShouldManageSpeaker(Player player)
    {
        try
        {
            return player != null
                   && player.ReferenceHub != null
                   && !player.IsHost
                   && !player.ReferenceHub.IsHost;
        }
        catch
        {
            return false;
        }
    }

    private static void CleanupStoppedFollow(int playerId, string purpose, SpeakerApi.LivePlayback liveSpeaker)
    {
        if (FollowCoroutines.TryGetValue(playerId, out var followDict))
        {
            followDict.Remove(purpose);
            if (followDict.Count == 0)
                FollowCoroutines.Remove(playerId);
        }

        if (!Speakers.TryGetValue(playerId, out var dict))
            return;

        if (dict.TryGetValue(purpose, out var current) && current.ControllerId == liveSpeaker.ControllerId)
        {
            current.DestroyAudioPlayer();
            dict.Remove(purpose);
        }

        if (dict.Count == 0)
            Speakers.Remove(playerId);
    }

    private static Player GetManagedPlayer(int playerId)
    {
        var player = Player.List.FirstOrDefault(p => p != null && p.Id == playerId);
        return ShouldManageSpeaker(player) ? player : null;
    }

    private static string GetPlayerName(int playerId)
        => GetManagedPlayer(playerId)?.Nickname ?? $"#{playerId}";

    private static int NextPlayerVersion(int playerId)
    {
        var version = PlayerVersions.GetValueOrDefault(playerId) + 1;
        PlayerVersions[playerId] = version;
        return version;
    }

    private static void InvalidatePlayerVersion(int playerId)
        => PlayerVersions[playerId] = PlayerVersions.GetValueOrDefault(playerId) + 1;

    private static bool IsCurrentPlayerVersion(int playerId, int version)
        => PlayerVersions.TryGetValue(playerId, out var current) && current == version;
}
