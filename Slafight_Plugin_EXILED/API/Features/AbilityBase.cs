#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using UnityEngine;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features;

public abstract class AbilityBase
{
    protected static readonly Dictionary<int, Dictionary<Type, AbilityState>> playerStates = new();

    protected sealed class AbilityState
    {
        public bool CanUse { get; set; } = true;
        public int MaxUses { get; set; } = -1;
        public int UsedCount { get; set; }
        public float CooldownEndTime { get; set; }
        public float CooldownSeconds { get; set; } = 10f;
        public CoroutineHandle CooldownHandle;
        public AbilityBase? OwnerAbility { get; set; }
    }

    private float _cooldownSeconds;
    private int _maxUses;

    public Player? Owner { get; private set; }
    public bool IsInitialized { get; private set; }

    protected abstract float DefaultCooldown { get; }
    protected abstract int DefaultMaxUses { get; }

    protected float CooldownSeconds => IsInitialized ? _cooldownSeconds : DefaultCooldown;
    protected int MaxUses => IsInitialized ? _maxUses : DefaultMaxUses;

    public virtual bool HasSelectableOptions => false;

    public virtual string GetSelectedOptionName(Player player) => string.Empty;

    public virtual string GetSelectedOptionDescription(Player player) => string.Empty;

    public virtual bool TrySwitchOptionFromInput(Player player, AbilityOptionDirection direction) => false;

    internal void Initialize(Player owner, float? cooldownSeconds = null, int? maxUses = null)
    {
        if (owner?.ReferenceHub == null)
            throw new ArgumentNullException(nameof(owner));

        Owner = owner;
        _cooldownSeconds = cooldownSeconds ?? DefaultCooldown;
        _maxUses = maxUses ?? DefaultMaxUses;
        IsInitialized = true;

        GrantAbility(owner.Id, GetType(), _cooldownSeconds, _maxUses, this);
        OnInitialized(owner);
    }

    protected virtual void OnInitialized(Player owner)
    {
    }

    public static bool HasAbility(int playerId) => playerStates.ContainsKey(playerId);

    public static bool HasAbility(int playerId, Type abilityType) =>
        playerStates.TryGetValue(playerId, out var states) && states.ContainsKey(abilityType);

    public static bool CanUseNow(int playerId) =>
        AbilityManager.TryGetLoadout(Player.Get(playerId), out _) &&
        CanUseSelectedAbility(playerId);

    public static bool CanUseNow(int playerId, Type abilityType)
    {
        if (!playerStates.TryGetValue(playerId, out var states) ||
            !states.TryGetValue(abilityType, out var state))
            return false;

        RefreshCooldownState(state);
        return state.CanUse;
    }

    public static bool IsOnCooldown(int playerId)
    {
        if (!playerStates.TryGetValue(playerId, out var states))
            return false;

        foreach (var state in states.Values)
            RefreshCooldownState(state);

        return states.Values.Any(state => !state.CanUse);
    }

    public static bool IsOnCooldown(int playerId, Type abilityType)
    {
        if (!playerStates.TryGetValue(playerId, out var states) ||
            !states.TryGetValue(abilityType, out var state))
            return false;

        RefreshCooldownState(state);
        return !state.CanUse;
    }

    public static int GetUsedCount(int playerId) =>
        playerStates.TryGetValue(playerId, out var states) ? states.Values.Sum(s => s.UsedCount) : 0;

    public static int GetUsedCount(int playerId, Type abilityType) =>
        playerStates.TryGetValue(playerId, out var states) &&
        states.TryGetValue(abilityType, out var state) ? state.UsedCount : 0;

    public static bool HasUsesLeft(int playerId) =>
        playerStates.TryGetValue(playerId, out var states) &&
        states.Values.All(s => s.MaxUses < 0 || s.UsedCount < s.MaxUses);

    public static bool HasUsesLeft(int playerId, Type abilityType) =>
        playerStates.TryGetValue(playerId, out var states) &&
        states.TryGetValue(abilityType, out var state) &&
        (state.MaxUses < 0 || state.UsedCount < state.MaxUses);

    public static bool CanUseSelectedAbility(int playerId)
    {
        if (!AbilityManager.TryGetLoadout(Player.Get(playerId), out var loadout))
            return false;

        var activeAbility = loadout.ActiveAbility;
        return activeAbility != null && CanUseNow(playerId, activeAbility.GetType());
    }

    protected static bool TryGetState(int playerId, Type abilityType, out AbilityState state)
    {
        if (playerStates.TryGetValue(playerId, out var states) &&
            states.TryGetValue(abilityType, out state))
            return true;

        state = null!;
        return false;
    }

    public static void GrantAbility(
        int playerId,
        Type abilityType,
        float cooldown = 10f,
        int maxUses = -1,
        AbilityBase? ownerAbility = null)
    {
        if (!playerStates.TryGetValue(playerId, out var states))
        {
            states = new Dictionary<Type, AbilityState>();
            playerStates[playerId] = states;
        }

        if (!states.TryGetValue(abilityType, out var state))
        {
            state = new AbilityState();
            states[abilityType] = state;
        }

        state.CanUse = true;
        state.UsedCount = 0;
        state.CooldownSeconds = Math.Max(0f, cooldown);
        state.MaxUses = maxUses;
        state.OwnerAbility = ownerAbility;
        state.CooldownEndTime = 0f;

        try
        {
            Timing.KillCoroutines(state.CooldownHandle);
        }
        catch
        {
            // ignored
        }
    }

    public static void GrantAbility(int playerId, float cooldown = 10f, int maxUses = -1)
        => GrantAbility(playerId, typeof(AbilityBase), cooldown, maxUses);

    public static void RevokeAbility(int playerId, Type abilityType)
    {
        if (playerStates.TryGetValue(playerId, out var states) &&
            states.TryGetValue(abilityType, out var state))
        {
            try
            {
                Timing.KillCoroutines(state.CooldownHandle);
            }
            catch
            {
                // ignored
            }

            states.Remove(abilityType);
        }
    }

    public static void RevokeAbility(int playerId)
    {
        if (!playerStates.TryGetValue(playerId, out var states))
            return;

        foreach (var state in states.Values)
        {
            try
            {
                Timing.KillCoroutines(state.CooldownHandle);
            }
            catch
            {
                // ignored
            }
        }

        playerStates.Remove(playerId);
    }

    public static void RevokeAllPlayers()
    {
        foreach (var states in playerStates.Values)
        {
            foreach (var state in states.Values)
            {
                try
                {
                    Timing.KillCoroutines(state.CooldownHandle);
                }
                catch
                {
                    // ignored
                }
            }
        }

        playerStates.Clear();
    }

    public static void ResetCooldown(int playerId, Type abilityType)
    {
        if (playerStates.TryGetValue(playerId, out var states) &&
            states.TryGetValue(abilityType, out var state))
        {
            state.CanUse = true;
            state.CooldownEndTime = 0f;
        }
    }

    public static void ResetCooldown(int playerId)
    {
        if (!playerStates.TryGetValue(playerId, out var states))
            return;

        foreach (var state in states.Values)
        {
            state.CanUse = true;
            state.CooldownEndTime = 0f;
        }
    }

    public static bool GrantAbility(Player player, Type abilityType, float cooldown = 10f, int maxUses = -1)
    {
        GrantAbility(player.Id, abilityType, cooldown, maxUses);
        return true;
    }

    public static bool GrantAbility(Player player, float cooldown = 10f, int maxUses = -1)
        => GrantAbility(player, typeof(AbilityBase), cooldown, maxUses);

    public static bool HasAbility(Player player) => HasAbility(player.Id);

    public static bool HasAbility<TAbility>(Player player) where TAbility : AbilityBase
    {
        if (!AbilityManager.Loadouts.TryGetValue(player.Id, out var loadout))
            return false;

        foreach (var ability in loadout.Slots)
        {
            if (ability is TAbility)
                return true;
        }

        return false;
    }

    public void TryActivateFromInput(Player player)
    {
        Log.Debug($"[Ability] TryActivateFromInput called for {player.Nickname}, role={player.Role.Type}, team={player.Role.Team}");

        if (player.Role.Team == Team.Dead)
        {
            Log.Debug("TryActivateFromInput: Dead Blocked");
            return;
        }

        if (!AbilityManager.TryGetLoadout(player, out var loadout))
        {
            Log.Debug($"[Ability] No loadout for {player.Nickname}");
            return;
        }

        if (loadout.ActiveAbility != this)
        {
            Log.Debug($"[Ability] Not active ability for {player.Nickname} (this={GetType().Name}, active={loadout.ActiveAbility?.GetType().Name})");
            return;
        }

        if (!CanActivate(player, out var failureReason))
        {
            Log.Debug($"[Ability] CanActivate rejected {GetType().Name} for {player.Nickname}");
            if (!string.IsNullOrWhiteSpace(failureReason))
                player.ShowHint($"<color=yellow>{failureReason}</color>");
            return;
        }

        var canUse = TryUseAbility(player);
        Log.Debug($"[Ability] TryUseAbility result={canUse} for {player.Nickname}");

        if (canUse)
            ExecuteAbility(player);
    }

    private static bool _initialized;

    internal static void RegisterEvents()
    {
        if (_initialized)
            return;

        Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Player.Joined += OnPlayerJoined;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        _initialized = true;
    }

    internal static void UnregisterEvents()
    {
        if (!_initialized)
            return;

        Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Player.Joined -= OnPlayerJoined;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        RevokeAllPlayers();
        _initialized = false;
    }

    private static void OnWaitingForPlayers() => RevokeAllPlayers();

    private static void OnPlayerJoined(JoinedEventArgs ev) =>
        playerStates[ev.Player.Id] = new Dictionary<Type, AbilityState>();

    private static void OnPlayerLeft(LeftEventArgs ev) =>
        RevokeAbility(ev.Player.Id);

    protected bool TryUseAbility(Player player)
    {
        if (!IsInitialized)
            Initialize(player);

        var myType = GetType();
        if (!playerStates.TryGetValue(player.Id, out var states))
        {
            states = new Dictionary<Type, AbilityState>();
            playerStates[player.Id] = states;
        }

        if (!states.TryGetValue(myType, out var state))
        {
            state = new AbilityState
            {
                CooldownSeconds = CooldownSeconds,
                MaxUses = MaxUses,
                OwnerAbility = this,
            };
            states[myType] = state;
        }
        else
        {
            state.OwnerAbility ??= this;
        }

        if (state.MaxUses > 0 && state.UsedCount >= state.MaxUses)
            return false;

        RefreshCooldownState(state);

        if (!state.CanUse)
            return false;

        state.CanUse = false;
        state.UsedCount++;
        state.CooldownEndTime = Time.time + state.CooldownSeconds;

        try
        {
            Timing.KillCoroutines(state.CooldownHandle);
        }
        catch
        {
            // ignored
        }

        state.CooldownHandle = Timing.RunCoroutine(CooldownCoroutine(player.Id, state));
        return true;
    }

    private static IEnumerator<float> CooldownCoroutine(int playerId, AbilityState state)
    {
        var waitTime = Mathf.Max(0f, state.CooldownEndTime - Time.time);
        yield return Timing.WaitForSeconds(waitTime);

        if (state.OwnerAbility == null)
            yield break;

        var myType = state.OwnerAbility.GetType();
        if (!playerStates.TryGetValue(playerId, out var states) ||
            !states.TryGetValue(myType, out var updatedState) ||
            updatedState != state)
            yield break;

        updatedState.CanUse = true;

        var player = Player.Get(playerId);
        if (player == null || player.ReferenceHub == null || updatedState.OwnerAbility == null)
            yield break;

        updatedState.OwnerAbility.OnCooldownEnd(player);
    }

    protected abstract void ExecuteAbility(Player player);

    protected virtual bool CanActivate(Player player, out string failureReason)
    {
        failureReason = string.Empty;

        if (player?.ReferenceHub == null ||
            (!player.IsNPC && !player.IsConnected) ||
            !player.IsAlive)
        {
            failureReason = "現在このアビリティは使用できません。";
            return false;
        }

        return true;
    }

    protected virtual void OnCooldownEnd(Player player)
    {
        if (player != null && player.ReferenceHub != null && player.IsConnected && !player.IsNPC &&
            AbilityManager.TryGetLoadout(player, out var loadout) &&
            loadout.ActiveAbility == this)
        {
            var abilityName = AbilityLocalization.GetDisplayName(GetType().Name, player);
            player.ShowHint($"<color=yellow>{abilityName} のクールダウンが終了しました。</color>");
            AbilityManager.UpdateAbilityHint(player, loadout);
        }
    }

    public static bool TryGetAbilityState(
        Player player,
        AbilityBase? ability,
        out bool canUse,
        out float cooldownSecondsRemaining,
        out int usesLeft,
        out int maxUses)
    {
        canUse = false;
        cooldownSecondsRemaining = 0f;
        usesLeft = 0;
        maxUses = -1;

        if (ability == null || !playerStates.TryGetValue(player.Id, out var states) ||
            !states.TryGetValue(ability.GetType(), out var state))
            return false;

        RefreshCooldownState(state);

        canUse = state.CanUse;
        cooldownSecondsRemaining = state.CanUse
            ? 0f
            : Mathf.Max(0f, state.CooldownEndTime - Time.time);
        maxUses = state.MaxUses;
        usesLeft = state.MaxUses < 0 ? -1 : state.MaxUses - state.UsedCount;

        return true;
    }

    private static void RefreshCooldownState(AbilityState state)
    {
        if (!state.CanUse && Time.time >= state.CooldownEndTime)
            state.CanUse = true;
    }
}
