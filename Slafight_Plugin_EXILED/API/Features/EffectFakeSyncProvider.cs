using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Keeps an effect visible only from a specific owner's client.
/// The real server-side effect state is not changed.
/// </summary>
public static class EffectFakeSyncProvider
{
    public const byte DefaultIntensity = 1;
    public const float DefaultRefreshInterval = 0.75f;

    private static readonly Dictionary<SessionKey, EffectFakeSyncSession> Sessions = [];
    private static readonly Dictionary<SessionKey, CoroutineHandle> SyncCoroutines = [];

    public static bool IsEnabled(Player owner, EffectType effect)
        => IsValid(owner) && Sessions.ContainsKey(new SessionKey(owner.Id, effect));

    public static void SetTargets(
        Player owner,
        EffectType effect,
        IEnumerable<Player>? targets,
        byte intensity = DefaultIntensity,
        float refreshInterval = DefaultRefreshInterval,
        bool restoreRealStateOnDisable = true)
    {
        if (targets is null)
        {
            Disable(owner, effect);
            return;
        }

        var targetIds = new HashSet<int>(targets.Where(IsValid).Select(target => target.Id));

        SetTargetRule(
            owner,
            effect,
            target => targetIds.Contains(target.Id),
            intensity,
            refreshInterval,
            restoreRealStateOnDisable,
            targetIds);
    }

    public static void SetTargetRule(
        Player owner,
        EffectType effect,
        Func<Player, bool> shouldShowEffect,
        byte intensity = DefaultIntensity,
        float refreshInterval = DefaultRefreshInterval,
        bool restoreRealStateOnDisable = true)
        => SetTargetRule(
            owner,
            effect,
            shouldShowEffect,
            intensity,
            refreshInterval,
            restoreRealStateOnDisable,
            null);

    public static void Refresh(Player owner, EffectType effect)
    {
        if (!IsValid(owner)) return;

        var key = new SessionKey(owner.Id, effect);
        if (Sessions.TryGetValue(key, out var session))
            SyncOwner(owner, session);
    }

    public static void Refresh(Player owner)
    {
        if (!IsValid(owner)) return;

        foreach (var session in Sessions.Values.Where(session => session.OwnerId == owner.Id).ToArray())
            SyncOwner(owner, session);
    }

    public static void RefreshAll()
    {
        foreach (var key in Sessions.Keys.ToArray())
        {
            var owner = GetPlayer(key.OwnerId);
            if (!IsValid(owner))
            {
                Disable(key);
                continue;
            }

            SyncOwner(owner, Sessions[key]);
        }
    }

    public static void Disable(Player? owner, EffectType effect)
    {
        if (owner is null) return;
        Disable(new SessionKey(owner.Id, effect), owner);
    }

    public static void Disable(Player? owner)
    {
        if (owner is null) return;

        foreach (var key in Sessions.Keys.Where(key => key.OwnerId == owner.Id).ToArray())
            Disable(key, owner);
    }

    public static void DisableAll()
    {
        foreach (var key in Sessions.Keys.ToArray())
            Disable(key);
    }

    public static void RemovePlayer(Player? player)
    {
        if (player is null) return;

        Disable(player);

        var changed = false;
        foreach (var session in Sessions.Values)
        {
            if (session.TargetIds?.Remove(player.Id) == true)
                changed = true;
        }

        if (changed)
            RefreshAll();
    }

    private static void SetTargetRule(
        Player owner,
        EffectType effect,
        Func<Player, bool>? shouldShowEffect,
        byte intensity,
        float refreshInterval,
        bool restoreRealStateOnDisable,
        HashSet<int> targetIds)
    {
        if (!IsValid(owner) || shouldShowEffect is null || effect == EffectType.None)
            return;

        var key = new SessionKey(owner.Id, effect);
        StopSync(key);

        var session = new EffectFakeSyncSession(
            owner.Id,
            effect,
            shouldShowEffect,
            targetIds,
            NormalizeIntensity(intensity),
            Math.Max(0.1f, refreshInterval),
            restoreRealStateOnDisable);

        Sessions[key] = session;
        SyncOwner(owner, session);
        SyncCoroutines[key] = Timing.RunCoroutine(SyncLoop(key));
    }

    private static IEnumerator<float> SyncLoop(SessionKey key)
    {
        while (Sessions.TryGetValue(key, out var session))
        {
            var owner = GetPlayer(key.OwnerId);
            if (!IsValid(owner))
            {
                Disable(key);
                yield break;
            }

            SyncOwner(owner, session);
            yield return Timing.WaitForSeconds(session.RefreshInterval);
        }
    }

    private static void SyncOwner(Player owner, EffectFakeSyncSession session)
    {
        if (!IsValid(owner)) return;

        foreach (var target in Player.List)
        {
            if (!IsValid(target)) continue;

            var intensity = ShouldShowEffect(owner, target, session)
                ? session.Intensity
                : (byte)0;

            target.SendFakeEffectTo(owner, session.Effect, intensity);
        }
    }

    private static bool ShouldShowEffect(Player owner, Player target, EffectFakeSyncSession session)
    {
        if (target.Id == owner.Id) return false;

        try
        {
            return session.ShouldShowEffect(target);
        }
        catch (Exception ex)
        {
            Log.Warn($"EffectFakeSyncProvider: rule failed for owner={owner.Nickname}, target={target.Nickname}, effect={session.Effect}: {ex.Message}");
            return false;
        }
    }

    private static void Disable(SessionKey key, Player owner = null)
    {
        StopSync(key);

        if (!Sessions.TryGetValue(key, out var session))
            return;

        Sessions.Remove(key);

        if (!session.RestoreRealStateOnDisable)
            return;

        owner ??= GetPlayer(key.OwnerId);
        if (!IsValid(owner)) return;

        RestoreRealStates(owner, session.Effect);
    }

    private static void StopSync(SessionKey key)
    {
        if (!SyncCoroutines.TryGetValue(key, out var handle))
            return;

        Timing.KillCoroutines(handle);
        SyncCoroutines.Remove(key);
    }

    private static void RestoreRealStates(Player owner, EffectType effect)
    {
        foreach (var target in Player.List)
        {
            if (!IsValid(target)) continue;
            target.SendFakeEffectTo(owner, effect, GetRealIntensity(target, effect));
        }
    }

    private static byte GetRealIntensity(Player target, EffectType effect)
    {
        var statusEffect = target.GetEffect(effect);
        if (statusEffect is null || !statusEffect.IsEnabled)
            return 0;

        return statusEffect.Intensity;
    }

    private static Player GetPlayer(int playerId)
        => Player.List.FirstOrDefault(player => player != null && player.Id == playerId);

    private static bool IsValid(Player player)
        => player != null && player.IsConnected;

    private static byte NormalizeIntensity(byte intensity)
        => intensity == 0 ? (byte)1 : intensity;

    private readonly record struct SessionKey(int OwnerId, EffectType Effect);

    private sealed class EffectFakeSyncSession
    {
        public EffectFakeSyncSession(
            int ownerId,
            EffectType effect,
            Func<Player, bool> shouldShowEffect,
            HashSet<int> targetIds,
            byte intensity,
            float refreshInterval,
            bool restoreRealStateOnDisable)
        {
            OwnerId = ownerId;
            Effect = effect;
            ShouldShowEffect = shouldShowEffect;
            TargetIds = targetIds;
            Intensity = intensity;
            RefreshInterval = refreshInterval;
            RestoreRealStateOnDisable = restoreRealStateOnDisable;
        }

        public int OwnerId { get; }
        public EffectType Effect { get; }
        public Func<Player, bool> ShouldShowEffect { get; }
        public HashSet<int> TargetIds { get; }
        public byte Intensity { get; }
        public float RefreshInterval { get; }
        public bool RestoreRealStateOnDisable { get; }
    }
}
