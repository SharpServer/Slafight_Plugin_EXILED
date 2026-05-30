using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;

namespace Slafight_Plugin_EXILED.API.Features;

public static class PlayerSpeakerManager
{
    private static readonly Dictionary<int, SpeakerApi.LivePlayback> PersonalSpeakers = new();
    private static readonly Dictionary<int, CoroutineHandle> FollowCoroutines = new();
    private static bool _registered;

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

    public static bool TryGetSpeaker(Player player, out SpeakerApi.LivePlayback speaker)
    {
        speaker = default;
        if (player == null) return false;
        
        return PersonalSpeakers.TryGetValue(player.Id, out speaker) && speaker.IsValid;
    }

    public static SpeakerApi.LivePlayback GetOrCreateSpeaker(Player player)
    {
        if (player == null) return default;

        if (PersonalSpeakers.TryGetValue(player.Id, out var speaker) && speaker.IsValid)
        {
            Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker: Returning existing speaker for {player.Nickname} (ID: {player.Id}, ControllerId: {speaker.ControllerId})");
            return speaker;
        }

        speaker = SpeakerApi.CreateLiveSpeaker(
            $"PlayerSpeaker_{player.Id}",
            player.Position,
            null, 
            speakerName: "Voice",
            isSpatial: ProximityChat.Handler.AudioIsSpatial,
            maxDistance: ProximityChat.Handler.AudioMaxDistance,
            minDistance: ProximityChat.Handler.AudioMinDistance);

        PersonalSpeakers[player.Id] = speaker;
        Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker: Created new speaker for {player.Nickname} (ID: {player.Id}, ControllerId: {speaker.ControllerId}) at {player.Position}");

        if (FollowCoroutines.TryGetValue(player.Id, out var oldHandle))
        {
            Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker: Killing old follow coroutine for {player.Nickname}");
            Timing.KillCoroutines(oldHandle);
        }

        FollowCoroutines[player.Id] = Timing.RunCoroutine(FollowPlayerCoroutine(player, speaker));
        Log.Debug($"[PlayerSpeakerManager] GetOrCreateSpeaker: Started follow coroutine for {player.Nickname}");

        return speaker;
    }

    public static void DestroySpeaker(Player player)
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
            // Player may be partially disposed; skip safely
            return;
        }

        Log.Debug($"[PlayerSpeakerManager] DestroySpeaker: Request to destroy speaker for {nickname} (ID: {playerId})");

        if (FollowCoroutines.TryGetValue(playerId, out var handle))
        {
            Timing.KillCoroutines(handle);
            FollowCoroutines.Remove(playerId);
            Log.Debug($"[PlayerSpeakerManager] DestroySpeaker: Killed follow coroutine for {nickname}");
        }

        if (PersonalSpeakers.TryGetValue(playerId, out var speaker))
        {
            try
            {
                speaker.DestroyAudioPlayer();
            }
            catch (Exception ex)
            {
                Log.Error($"[PlayerSpeakerManager] DestroySpeaker: Failed to destroy speaker: {ex.Message}");
            }
            PersonalSpeakers.Remove(playerId);
            Log.Debug($"[PlayerSpeakerManager] DestroySpeaker: Destroyed speaker for {nickname} (ControllerId: {speaker.ControllerId})");
        }
    }

    public static void DestroyAll()
    {
        Log.Debug("[PlayerSpeakerManager] DestroyAll: Destroying all speakers and follow coroutines.");
        foreach (var handle in FollowCoroutines.Values)
            Timing.KillCoroutines(handle);
        FollowCoroutines.Clear();

        foreach (var speaker in PersonalSpeakers.Values)
        {
            if (speaker.IsValid)
                speaker.DestroyAudioPlayer();
        }
        PersonalSpeakers.Clear();
    }

    private static void OnPlayerSpawned(SpawnedEventArgs ev)
    {
        if (ev.Player == null) return;

        Log.Debug($"[PlayerSpeakerManager] OnPlayerSpawned: Player {ev.Player.Nickname} spawned as {ev.Player.Role} (IsAlive: {ev.Player.IsAlive})");

        if (ev.Player.IsAlive && ev.Player.Role != PlayerRoles.RoleTypeId.Spectator && ev.Player.Role != PlayerRoles.RoleTypeId.None)
        {
            // Delay creation so player position stabilizes (avoids (0,0,0) leaks)
            var player = ev.Player;
            Timing.CallDelayed(1.5f, () =>
            {
                try
                {
                    if (player != null && player.IsAlive && player.IsConnected)
                        GetOrCreateSpeaker(player);
                }
                catch (Exception ex)
                {
                    Log.Error($"[PlayerSpeakerManager] OnPlayerSpawned delayed creation error: {ex}");
                }
            });
        }
        else
        {
            DestroySpeaker(ev.Player);
        }
    }

    private static void OnPlayerDied(DiedEventArgs ev)
    {
        if (ev.Player != null)
        {
            Log.Debug($"[PlayerSpeakerManager] OnPlayerDied: Player {ev.Player.Nickname} died.");
            DestroySpeaker(ev.Player);
        }
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player != null)
        {
            Log.Debug($"[PlayerSpeakerManager] OnPlayerLeft: Player {ev.Player.Nickname} left.");
            DestroySpeaker(ev.Player);
        }
    }

    private static void OnRoundRestarted()
    {
        Log.Debug("[PlayerSpeakerManager] OnRoundRestarted: Round restarting, cleaning up.");
        DestroyAll();
    }

    private static IEnumerator<float> FollowPlayerCoroutine(Player player, SpeakerApi.LivePlayback liveSpeaker)
    {
        Log.Debug($"[PlayerSpeakerManager] FollowPlayerCoroutine: Started for {player.Nickname} (Speaker ControllerId: {liveSpeaker.ControllerId})");
        while (true)
        {
            bool shouldContinue;
            try
            {
                shouldContinue = player != null && player.IsAlive && player.IsConnected && liveSpeaker.IsValid;
                if (shouldContinue)
                    liveSpeaker.SetTransform(player.Position, null);
            }
            catch (Exception ex)
            {
                Log.Error($"[PlayerSpeakerManager] FollowPlayerCoroutine: Error: {ex.Message}");
                shouldContinue = false;
            }

            if (!shouldContinue)
                break;

            yield return Timing.WaitForSeconds(0.1f);
        }
        Log.Debug($"[PlayerSpeakerManager] FollowPlayerCoroutine: Stopped for {player?.Nickname ?? "Unknown"}");
    }
}
