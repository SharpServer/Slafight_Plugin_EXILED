using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ProximityChat;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features;

public static class PlayerSpeakerManager
{
    // playerId -> purposeKey -> speaker
    private static readonly Dictionary<int, Dictionary<string, SpeakerApi.LivePlayback>> Speakers
        = new();

    // playerId -> purposeKey -> file playback
    private static readonly Dictionary<int, Dictionary<string, SpeakerApi.Playback>> Playbacks
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
        Exiled.Events.Handlers.Player.ChangingRole += OnPlayerChangingRole;
        Exiled.Events.Handlers.Player.Died += OnPlayerDied;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        Server.RestartingRound += OnRoundRestarted;

        _registered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_registered) return;

        Log.Debug("[PlayerSpeakerManager] Unregistering events.");
        Exiled.Events.Handlers.Player.Spawned -= OnPlayerSpawned;
        Exiled.Events.Handlers.Player.ChangingRole -= OnPlayerChangingRole;
        Exiled.Events.Handlers.Player.Died -= OnPlayerDied;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        Server.RestartingRound -= OnRoundRestarted;

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
        string speakerName = null,
        Predicate<Player> listeners = null)
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
            speaker.SetListeners(listeners);
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

        try
        {
            speaker = SpeakerApi.CreateLiveSpeaker(
                $"PlayerSpeaker_{playerId}_{purpose}",
                player.Position,
                speakerName: speakerName,
                isSpatial: isSpatial,
                maxDistance: maxDistance,
                minDistance: minDistance,
                volume: volume,
                listeners: listeners);
        }
        catch (Exception ex)
        {
            Log.Error($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: failed to create speaker for {player.Nickname}: {ex}");
            if (dict.Count == 0)
                Speakers.Remove(playerId);
            return default;
        }

        if (!speaker.IsValid)
        {
            Log.Warn($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: speaker creation returned an invalid speaker for {player.Nickname}.");
            if (dict.Count == 0)
                Speakers.Remove(playerId);
            return default;
        }

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
        try
        {
            followDict[purpose] = Timing.RunCoroutine(FollowPlayerCoroutine(playerId, speaker, purpose));
        }
        catch (Exception ex)
        {
            Log.Error($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: failed to start follow coroutine for {player.Nickname}: {ex}");
            followDict.Remove(purpose);
            if (followDict.Count == 0)
                FollowCoroutines.Remove(playerId);
            DestroySpeaker(playerId, purpose);
            return default;
        }

        Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker[{purpose}]: started follow coroutine for {player.Nickname}");

        return speaker;
    }

    /// <summary>
    /// プレイヤーに追従する音声を1回再生する。
    /// 同じ用途キーの音声がある場合は置き換える。
    /// </summary>
    public static SpeakerApi.Playback Play(
        Player player,
        string fileName,
        string purpose = null,
        bool isSpatial = true,
        float maxDistance = 5f,
        float minDistance = 1f,
        float volume = 1f,
        Predicate<Player> listeners = null)
        => PlayManaged(
            player,
            fileName,
            purpose,
            loop: false,
            isSpatial,
            maxDistance,
            minDistance,
            volume,
            listeners);

    /// <summary>
    /// プレイヤーに追従する音声をループ再生する。
    /// 同じ用途キーの音声がある場合は置き換える。
    /// </summary>
    public static SpeakerApi.Playback PlayLoop(
        Player player,
        string fileName,
        string purpose = null,
        bool isSpatial = true,
        float maxDistance = 1f,
        float minDistance = 0.1f,
        float volume = 1f,
        Predicate<Player> listeners = null)
        => PlayManaged(
            player,
            fileName,
            purpose,
            loop: true,
            isSpatial,
            maxDistance,
            minDistance,
            volume,
            listeners);

    public static bool TryGetPlayback(Player player, string purpose, out SpeakerApi.Playback playback)
    {
        playback = default;
        if (!ShouldManageSpeaker(player) || string.IsNullOrWhiteSpace(purpose))
            return false;

        if (!Playbacks.TryGetValue(player.Id, out var dict) ||
            !dict.TryGetValue(purpose, out var current))
            return false;

        if (!current.IsValid)
        {
            RemovePlaybackEntry(player.Id, purpose);
            return false;
        }

        playback = current;
        return true;
    }

    public static bool Stop(Player player, string purpose)
        => player != null && Stop(player.Id, purpose);

    public static bool SetVolume(Player player, string purpose, float volume)
    {
        if (player == null || string.IsNullOrWhiteSpace(purpose))
            return false;

        if (TryGetPlayback(player, purpose, out var playback))
        {
            playback.SetVolume(volume);
            return true;
        }

        if (TryGetSpeaker(player, purpose, out var speaker))
        {
            speaker.SetVolume(volume);
            return true;
        }

        return false;
    }

    public static bool SetListeners(
        Player player,
        string purpose,
        Predicate<Player>? listeners)
    {
        if (player == null || string.IsNullOrWhiteSpace(purpose))
            return false;

        if (TryGetPlayback(player, purpose, out var playback))
        {
            playback.SetListeners(listeners);
            return true;
        }

        if (TryGetSpeaker(player, purpose, out var speaker))
        {
            speaker.SetListeners(listeners);
            return true;
        }

        return false;
    }

    public static bool Stop(int playerId, string purpose)
    {
        if (playerId <= 0 || string.IsNullOrWhiteSpace(purpose) ||
            !Playbacks.TryGetValue(playerId, out var dict) ||
            !dict.TryGetValue(purpose, out var playback))
            return false;

        var stopped = playback.Stop();
        RemovePlaybackEntry(playerId, purpose);
        return stopped;
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

        if (Playbacks.TryGetValue(playerId, out var playbackDict))
        {
            foreach (var playback in playbackDict.Values)
                playback.Stop();

            Playbacks.Remove(playerId);
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

        foreach (var dict in Playbacks.Values)
        {
            foreach (var playback in dict.Values)
                playback.Stop();
        }
        Playbacks.Clear();
        PlayerVersions.Clear();
    }

    // ==== イベント ====

    private static void OnPlayerSpawned(SpawnedEventArgs ev)
    {
        if (ev.Player == null || ev.Player.IsNPC || !ev.Player.IsSafePlayer()) return;

        Log.Debug($"[PlayerSpeakerManager] OnPlayerSpawned: {ev.Player.Nickname} as {ev.Player.Role} (IsAlive: {ev.Player.IsAlive})");
        var playerId = ev.Player.Id;
        DestroyAllForPlayer(playerId);
        var version = NextPlayerVersion(playerId);

        if (!ShouldManageSpeaker(ev.Player))
        {
            DestroyAllForPlayer(playerId);
            return;
        }

        if (ev.Player.IsAlive &&
            ev.Player.Role != RoleTypeId.Spectator &&
            ev.Player.Role != RoleTypeId.None)
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
                            isSpatial: Handler.AudioIsSpatial,
                            maxDistance: Handler.AudioMaxDistance,
                            minDistance: Handler.AudioMinDistance,
                            volume: Handler.AudioVolume,
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

    private static void OnPlayerChangingRole(ChangingRoleEventArgs ev)
    {
        if (ev.Player == null || ev.Player.IsNPC || !ev.Player.IsSafePlayer()) return;

        DestroyAllForPlayer(ev.Player.Id);
    }

    private static void OnPlayerDied(DiedEventArgs ev)
    {
        if (ev.Player == null || ev.Player.IsNPC || !ev.Player.IsSafePlayer()) return;

        Log.Debug($"[PlayerSpeakerManager] OnPlayerDied: {ev.Player.Nickname} died.");
        DestroyAllForPlayer(ev.Player.Id);
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player == null || ev.Player.IsNPC || !ev.Player.IsSafePlayer()) return;

        Log.Debug($"[PlayerSpeakerManager] OnPlayerLeft: {ev.Player.Nickname} left.");
        DestroyAllForPlayer(ev.Player.Id);
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
                    liveSpeaker.SetTransform(player.Position);
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
                   && !player.IsNPC
                   && player.IsSafePlayer();
        }
        catch
        {
            return false;
        }
    }

    private static SpeakerApi.Playback PlayManaged(
        Player player,
        string fileName,
        string purpose,
        bool loop,
        bool isSpatial,
        float maxDistance,
        float minDistance,
        float volume,
        Predicate<Player> listeners)
    {
        if (!ShouldManageSpeaker(player))
            return default;
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        purpose = string.IsNullOrWhiteSpace(purpose) ? fileName : purpose.Trim();
        Stop(player.Id, purpose);

        var audioPlayerName = $"PlayerAudio_{player.Id}_{purpose}";
        SpeakerApi.Playback playback;
        try
        {
            playback = loop
                ? SpeakerApi.PlayLoop(
                    fileName,
                    audioPlayerName,
                    player.Position,
                    player.Transform,
                    isSpatial,
                    maxDistance,
                    minDistance,
                    volume: volume,
                    listeners: listeners)
                : SpeakerApi.Play(
                    fileName,
                    audioPlayerName,
                    player.Position,
                    destroyOnEnd: true,
                    parent: player.Transform,
                    isSpatial,
                    maxDistance,
                    minDistance,
                    volume: volume,
                    listeners: listeners);
        }
        catch (Exception ex)
        {
            Log.Error($"[PlayerSpeakerManager] PlayManaged[{purpose}]: failed for {player.Nickname}: {ex}");
            return default;
        }

        if (!playback.IsValid)
        {
            Log.Warn($"[PlayerSpeakerManager] PlayManaged[{purpose}]: playback creation failed for {player.Nickname}.");
            return default;
        }

        if (!Playbacks.TryGetValue(player.Id, out var dict))
        {
            dict = new Dictionary<string, SpeakerApi.Playback>(StringComparer.OrdinalIgnoreCase);
            Playbacks[player.Id] = dict;
        }

        dict[purpose] = playback;
        return playback;
    }

    private static void RemovePlaybackEntry(int playerId, string purpose)
    {
        if (!Playbacks.TryGetValue(playerId, out var dict))
            return;

        dict.Remove(purpose);
        if (dict.Count == 0)
            Playbacks.Remove(playerId);
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
