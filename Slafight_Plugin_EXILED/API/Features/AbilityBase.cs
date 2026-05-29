#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine; // ← 追加（Time.time 用）

namespace Slafight_Plugin_EXILED.API.Features;

public abstract class AbilityBase
{
    // プレイヤーIDごとのアビリティ状態（Typeごと）
    protected static readonly Dictionary<int, Dictionary<Type, AbilityState>> playerStates = new();

    protected class AbilityState
    {
        public bool CanUse { get; set; } = true;
        public int MaxUses { get; set; } = -1;   // -1 = 無制限
        public int UsedCount { get; set; } = 0;

        // ★ 追加: 終了時刻（Time.time ベース）
        public float CooldownEndTime { get; set; } = 0f;

        public float CooldownSeconds { get; set; } = 10f;
        public CoroutineHandle CooldownHandle;
        public AbilityBase? OwnerAbility { get; set; }
    }

    // ===== 外部API =====
    public static bool HasAbility(int playerId) => playerStates.ContainsKey(playerId);

    public static bool HasAbility(int playerId, Type abilityType) =>
        playerStates.TryGetValue(playerId, out var states) && states.ContainsKey(abilityType);

    public static bool CanUseNow(int playerId) =>
        AbilityManager.TryGetLoadout(Player.Get(playerId), out var loadout) &&
        CanUseSelectedAbility(playerId);

    public static bool CanUseNow(int playerId, Type abilityType) =>
        playerStates.TryGetValue(playerId, out var states) &&
        states.TryGetValue(abilityType, out var state) && state.CanUse;

    public static bool IsOnCooldown(int playerId) =>
        playerStates.TryGetValue(playerId, out var states) &&
        states.Values.Any(s => !s.CanUse);

    public static bool IsOnCooldown(int playerId, Type abilityType) =>
        playerStates.TryGetValue(playerId, out var states) &&
        states.TryGetValue(abilityType, out var state) && !state.CanUse;

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

    // ★ 現在選択中のアビリティ使用可能か
    public static bool CanUseSelectedAbility(int playerId)
    {
        if (!AbilityManager.TryGetLoadout(Player.Get(playerId), out var loadout))
            return false;

        var activeAbility = loadout.Slots[loadout.ActiveIndex];
        return activeAbility != null && CanUseNow(playerId, activeAbility.GetType());
    }

    // プレイヤーごとの状態取得
    protected static bool TryGetState(int playerId, Type abilityType, out AbilityState state)
    {
        if (playerStates.TryGetValue(playerId, out var states) &&
            states.TryGetValue(abilityType, out state))
            return true;

        state = null!;
        return false;
    }

    public static void GrantAbility(int playerId, Type abilityType, float cooldown = 10f, int maxUses = -1)
    {
        if (!playerStates.TryGetValue(playerId, out var states))
        {
            states = new Dictionary<Type, AbilityState>();
            playerStates[playerId] = states;
        }

        states[abilityType] = new AbilityState
        {
            CanUse = true,
            CooldownSeconds = cooldown,
            MaxUses = maxUses,
            OwnerAbility = null,
            CooldownEndTime = 0f,
        };
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
            catch { }

            states.Remove(abilityType);
        }
    }

    public static void RevokeAbility(int playerId)
    {
        if (playerStates.TryGetValue(playerId, out var states))
        {
            foreach (var state in states.Values)
            {
                try
                {
                    Timing.KillCoroutines(state.CooldownHandle);
                }
                catch { }
            }
            playerStates.Remove(playerId);
        }
    }

    public static void RevokeAllPlayers()
    {
        foreach (var kvp in playerStates)
        {
            foreach (var state in kvp.Value.Values)
            {
                try
                {
                    Timing.KillCoroutines(state.CooldownHandle);
                }
                catch { }
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
        if (playerStates.TryGetValue(playerId, out var states))
        {
            foreach (var state in states.Values)
            {
                state.CanUse = true;
                state.CooldownEndTime = 0f;
            }
        }
    }

    // Player 拡張
    public static bool GrantAbility(Player player, Type abilityType, float cooldown = 10f, int maxUses = -1)
    {
        GrantAbility(player.Id, abilityType, cooldown, maxUses);
        return true;
    }

    public static bool GrantAbility(Player player, float cooldown = 10f, int maxUses = -1)
        => GrantAbility(player, cooldown);

    public static bool HasAbility(Player player) => HasAbility(player.Id);

    public static bool HasAbility<TAbility>(Player player) where TAbility : AbilityBase
    {
        if (!AbilityManager.Loadouts.TryGetValue(player.Id, out var loadout))
            return false;

        foreach (var ability in loadout.Slots)
        {
            if (ability != null && ability is TAbility)
                return true;
        }
        return false;
    }

    // AbilityInputHandler から叩く入口
    public void TryActivateFromInput(Player player)
    {
        Log.Debug($"[Ability] TryActivateFromInput called for {player.Nickname}, role={player.Role.Type}, team={player.Role.Team}");

        if (player.Role.Team == Team.Dead)
        {
            Log.Debug("TryActivateFromInput: Dead Blocked");
            return;
        }

        // ★ 現在選択中のアビリティかチェック
        if (!AbilityManager.TryGetLoadout(player, out var loadout) ||
            loadout.Slots[loadout.ActiveIndex] != this)
        {
            Log.Debug($"[Ability] Not active ability for {player.Nickname} (this={GetType().Name}, active={loadout.Slots[loadout.ActiveIndex]?.GetType().Name})");
            return;
        }

        var canUse = TryUseAbility(player);
        Log.Debug($"[Ability] TryUseAbility result={canUse} for {player.Nickname}");

        if (canUse)
            ExecuteAbility(player);
    }

    // ===== 初期化（ラウンド跨ぎのクールダウンクリア用）=====
    private static bool _initialized;

    // 抽象デフォルト値
    protected abstract float DefaultCooldown { get; }
    protected abstract int DefaultMaxUses { get; }

    private readonly float _defaultCooldown;
    private readonly int _defaultMaxUses;

    protected AbilityBase(Player owner, float? cooldownSeconds = null, int? maxUses = null)
    {
        _defaultCooldown = cooldownSeconds ?? DefaultCooldown;
        _defaultMaxUses  = maxUses ?? DefaultMaxUses;

        GrantAbility(owner.Id, GetType(), _defaultCooldown, _defaultMaxUses);
    }

    protected AbilityBase(Player owner)
        : this(owner, null, null)
    {
    }

    internal static void RegisterEvents()
    {
        if (_initialized) return;

        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Player.Joined += OnPlayerJoined;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        _initialized = true;
    }

    internal static void UnregisterEvents()
    {
        if (!_initialized) return;

        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
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

    // ★★★ ここを修正: 終了時刻を使ったクールダウン開始
    protected bool TryUseAbility(Player player)
    {
        var myType = GetType();
        if (!playerStates.TryGetValue(player.Id, out var states) ||
            !states.TryGetValue(myType, out var state))
        {
            state = new AbilityState
            {
                CooldownSeconds = _defaultCooldown,
                MaxUses = _defaultMaxUses,
                OwnerAbility = this,
                CooldownEndTime = 0f,
            };
            states[myType] = state;
        }
        else if (state.OwnerAbility == null)
        {
            state.OwnerAbility = this;
        }

        if (state.MaxUses > 0 && state.UsedCount >= state.MaxUses)
            return false;

        // 時間ベースで CanUse を更新
        if (!state.CanUse && Time.time >= state.CooldownEndTime)
            state.CanUse = true;

        if (!state.CanUse)
            return false;

        state.CanUse = false;
        state.UsedCount++;

        // 終了時刻をセット
        state.CooldownEndTime = Time.time + state.CooldownSeconds;

        try
        {
            Timing.KillCoroutines(state.CooldownHandle);
        }
        catch { }

        state.CooldownHandle = Timing.RunCoroutine(
            CooldownCoroutine(player.Id, state));

        return true;
    }

    // ★★★ ここも修正: 終了時刻まで待つ
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
        if (player == null || !player.IsConnected || updatedState.OwnerAbility == null)
            yield break;

        updatedState.OwnerAbility.OnCooldownEnd(player);
    }

    protected abstract void ExecuteAbility(Player player);

    // ★ 現在選択中アビリティのみヒント表示
    protected virtual void OnCooldownEnd(Player player)
    {
        if (player != null && player.IsConnected &&
            AbilityManager.TryGetLoadout(player, out var loadout) &&
            loadout.Slots[loadout.ActiveIndex] == this)
        {
            var abilityName = GetType().Name;
            player.ShowRueiPlus(
                $"<color=yellow>{abilityName} のクールダウンが終了しました。</color>",
                3f);
        }
    }
    
    // ===== 外部からのHUD用API =====
    public static bool TryGetAbilityState(Player player, AbilityBase? ability,
        out bool canUse, out float cooldownSecondsRemaining, out int usesLeft, out int maxUses)
    {
        canUse = false;
        cooldownSecondsRemaining = 0f;
        usesLeft = 0;
        maxUses = -1;

        if (ability == null || !playerStates.TryGetValue(player.Id, out var states) ||
            !states.TryGetValue(ability.GetType(), out var state))
            return false;

        // 時間ベースでCanUse更新
        if (!state.CanUse && Time.time >= state.CooldownEndTime)
            state.CanUse = true;

        canUse = state.CanUse;

        if (state.CanUse)
            cooldownSecondsRemaining = 0f;
        else
            cooldownSecondsRemaining = Mathf.Max(0f, state.CooldownEndTime - Time.time);

        maxUses = state.MaxUses;
        usesLeft = (state.MaxUses < 0) ? -1 : (state.MaxUses - state.UsedCount);

        return true;
    }
}
