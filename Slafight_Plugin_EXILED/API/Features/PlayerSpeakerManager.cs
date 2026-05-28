using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using UnityEngine;

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
            isSpatial: true,
            maxDistance: 20f,
            minDistance: 1f);

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

        Log.Debug($"[PlayerSpeakerManager] DestroySpeaker: Request to destroy speaker for {player.Nickname} (ID: {player.Id})");

        if (FollowCoroutines.TryGetValue(player.Id, out var handle))
        {
            Timing.KillCoroutines(handle);
            FollowCoroutines.Remove(player.Id);
            Log.Debug($"[PlayerSpeakerManager] DestroySpeaker: Killed follow coroutine for {player.Nickname}");
        }

        if (PersonalSpeakers.TryGetValue(player.Id, out var speaker))
        {
            speaker.DestroyAudioPlayer();
            PersonalSpeakers.Remove(player.Id);
            Log.Debug($"[PlayerSpeakerManager] DestroySpeaker: Destroyed speaker for {player.Nickname} (ControllerId: {speaker.ControllerId})");
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
            GetOrCreateSpeaker(ev.Player);
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
        while (player != null && player.IsAlive && liveSpeaker.IsValid)
        {
            liveSpeaker.SetTransform(player.Position, null);
            yield return Timing.WaitForSeconds(0.1f);
        }
        Log.Debug($"[PlayerSpeakerManager] FollowPlayerCoroutine: Stopped for {player?.Nickname ?? "Unknown"} (Player IsAlive: {player?.IsAlive ?? false}, Speaker Valid: {liveSpeaker.IsValid})");
    }
}
