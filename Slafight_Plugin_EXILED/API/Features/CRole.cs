using System;
using System.Collections.Generic;
using System.Linq;
using CentralAuth;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.CustomItems.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using MEC;
using Mirror;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

// イベント系はエイリアス付けて衝突回避
using PlayerHandlers = Exiled.Events.Handlers.Player;
using MapHandlers = Exiled.Events.Handlers.Map;
using PlayerEvents = Exiled.Events.EventArgs.Player;
using MapEvents = Exiled.Events.EventArgs.Map;

namespace Slafight_Plugin_EXILED.API.Features;

public readonly record struct CRoleEffect(EffectType EffectType, byte Intensity = 1, float Duration = 0f)
{
    public void Apply(Player? player)
    {
        if (player == null) return;

        if (Duration > 0f)
            player.EnableEffect(EffectType, Intensity, Duration);
        else
            player.EnableEffect(EffectType, Intensity);
    }
}

public abstract class CRole
{
    // 全インスタンスを追跡（主に自動生成分）
    private static readonly HashSet<CRole> RegisteredInstances = [];

    // 全Roleタイプ
    private static readonly List<Type> RoleTypes;

    // UniqueRole 文字列 → CRole インスタンス（バリアント含む）
    private static readonly Dictionary<string, CRole> UniqueRoleToRole = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<CRoleTypeId, CRole> RoleIdToRole = new();
    private static readonly Dictionary<Type, CRole> TypeToRole = new();
    private static readonly Dictionary<int, TeamNpcInfo> TeamNpcs = new();
    private static readonly Dictionary<int, CoroutineHandle> RoleEffectCoroutines = new();
    private const ulong AuthSyncedUserIdDirtyBit = 1UL;

    private static readonly IReadOnlyList<object> EmptyItems = [];
    private static readonly IReadOnlyDictionary<AmmoType, ushort> EmptyAmmo = new Dictionary<AmmoType, ushort>();
    private static readonly IReadOnlyList<CRoleEffect> EmptyEffects = [];
    private static readonly IReadOnlyList<SpecificFlagType> EmptySpecificFlags = [];

    private static bool _eventsSubscribed;

    private readonly struct TeamNpcInfo
    {
        public TeamNpcInfo(int npcId, string uniqueRole)
        {
            NpcId = npcId;
            UniqueRole = uniqueRole;
        }

        public int NpcId { get; }
        public string UniqueRole { get; }
    }

    static CRole()
    {
        var asm = typeof(CRole).Assembly;
        RoleTypes = asm.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(CRole)) && !t.IsAbstract)
            .ToList();
    }

    /// <summary>
    /// 自動登録から除外したい CRole 用属性。
    /// 「RegisterAllEvents で RegisterEvents を自動呼び出ししない」ための印。
    /// UniqueRole マップへの登録はこの属性が付いていても行われます。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CRoleAutoRegisterIgnoreAttribute : Attribute { }

    // ==== Plugin から呼ぶ入り口 ====

    public static bool IsTeamNpc(Player? player)
    {
        if (player == null)
            return false;

        return TeamNpcs.Values.Any(info => info.NpcId == player.Id);
    }

    /// <summary>
    /// 全ての CRole 派生クラスをインスタンス化し、
    /// 共通イベント(Dying, AnnouncingScpTermination)と
    /// UniqueRole マップ登録を行う。
    /// Ignore 属性付きのクラスは RegisterEvents を自動では呼ばない。
    /// </summary>
    public static void RegisterAllEvents()
    {
        if (!_eventsSubscribed)
        {
            PlayerHandlers.Dying += OnAnyPlayerDying;
            PlayerHandlers.ChangingRole += OnAnyPlayerChangingRole;
            PlayerHandlers.Left += OnAnyPlayerLeft;
            PlayerHandlers.Hurting += OnAnyPlayerHurting;
            PlayerHandlers.SpawningRagdoll += OnAnyPlayerSpawningRagdoll;
            PlayerHandlers.ChangingItem += OnAnyPlayerChangingItem;
            PlayerHandlers.ChangedItem += OnAnyPlayerChangedItem;
            PlayerHandlers.PickingUpItem += OnAnyPlayerPickingUpItem;
            PlayerHandlers.DroppingItem += OnAnyPlayerDroppingItem;
            PlayerHandlers.UsingItem += OnAnyPlayerUsingItem;
            PlayerHandlers.UsedItem += OnAnyPlayerUsedItem;
            PlayerHandlers.DroppingAmmo += OnAnyPlayerDroppingAmmo;
            PlayerHandlers.ReloadingWeapon += OnAnyPlayerReloadingWeapon;
            PlayerHandlers.InteractingDoor += OnAnyPlayerInteractingDoor;
            PlayerHandlers.Handcuffing += OnAnyPlayerHandcuffing;
            PlayerHandlers.ReceivingVoiceMessage += OnAnyPlayerReceivingVoiceMessage;
            PlayerHandlers.VoiceChatting += OnAnyPlayerVoiceChatting;
            MapHandlers.AnnouncingScpTermination += OnAnyAnnouncingScpTermination;
            _eventsSubscribed = true;
        }

        foreach (var type in RoleTypes)
        {
            try
            {
                var instance = (CRole)Activator.CreateInstance(type);

                if (instance == null)
                {
                    Log.Warn($"CRole.RegisterAllEvents: Activator.CreateInstance returned null for {type.Name}, skipping");
                    continue;
                }

                if (string.IsNullOrEmpty(instance.UniqueRoleKey))
                {
                    Log.Warn($"CRole.RegisterAllEvents: {type.Name} has null/empty UniqueRoleKey, skipping");
                    continue;
                }

                bool autoRegisterEvents =
                    type.GetCustomAttributes(typeof(CRoleAutoRegisterIgnoreAttribute), true).Length == 0;

                instance.InternalRegisterEvents(autoRegisterEvents);
            }
            catch (Exception ex)
            {
                Log.Error($"CRole.RegisterAllEvents failed for {type.Name}: {ex}");
            }
        }
    }

    /// <summary>
    /// 全ての CRole 派生クラスのイベント登録を解除する。
    /// Plugin.OnDisabled から 1 回呼ぶ想定。
    /// </summary>
    public static void UnregisterAllEvents()
    {
        foreach (var instance in RegisteredInstances.ToList())
            instance.InternalUnregisterEvents();

        RegisteredInstances.Clear();
        UniqueRoleToRole.Clear();
        RoleIdToRole.Clear();
        TypeToRole.Clear();
        CleanupAllTeamNpcs();
        CleanupAllRoleEffectCoroutines();

        if (_eventsSubscribed)
        {
            PlayerHandlers.Dying -= OnAnyPlayerDying;
            PlayerHandlers.ChangingRole -= OnAnyPlayerChangingRole;
            PlayerHandlers.Left -= OnAnyPlayerLeft;
            PlayerHandlers.Hurting -= OnAnyPlayerHurting;
            PlayerHandlers.SpawningRagdoll -= OnAnyPlayerSpawningRagdoll;
            PlayerHandlers.ChangingItem -= OnAnyPlayerChangingItem;
            PlayerHandlers.ChangedItem -= OnAnyPlayerChangedItem;
            PlayerHandlers.PickingUpItem -= OnAnyPlayerPickingUpItem;
            PlayerHandlers.DroppingItem -= OnAnyPlayerDroppingItem;
            PlayerHandlers.UsingItem -= OnAnyPlayerUsingItem;
            PlayerHandlers.UsedItem -= OnAnyPlayerUsedItem;
            PlayerHandlers.DroppingAmmo -= OnAnyPlayerDroppingAmmo;
            PlayerHandlers.ReloadingWeapon -= OnAnyPlayerReloadingWeapon;
            PlayerHandlers.InteractingDoor -= OnAnyPlayerInteractingDoor;
            PlayerHandlers.Handcuffing -= OnAnyPlayerHandcuffing;
            PlayerHandlers.ReceivingVoiceMessage -= OnAnyPlayerReceivingVoiceMessage;
            PlayerHandlers.VoiceChatting -= OnAnyPlayerVoiceChatting;
            MapHandlers.AnnouncingScpTermination -= OnAnyAnnouncingScpTermination;
            _eventsSubscribed = false;
        }
    }

    /// <summary>
    /// Ignore付きロール用:
    /// 手動で生成したインスタンスを UniqueRole マップの「本体」に差し替える。
    /// これを呼ぶと、Dying などがこのインスタンスに飛ぶようになる。
    /// </summary>
    public static void OverrideRoleInstance(string uniqueRole, CRole? instance)
    {
        if (string.IsNullOrEmpty(uniqueRole) || instance == null)
            return;

        UniqueRoleToRole[uniqueRole] = instance;
        RegisterLookup(instance);

        // ★ RegisteredInstances に含まれていない場合のみ追加
        if (!RegisteredInstances.Contains(instance))
            RegisteredInstances.Add(instance);

        Log.Debug($"CRole.OverrideRoleInstance: {uniqueRole} -> {instance.GetType().Name}");
    }

    // ==== 共通イベントハンドラ（static） ====

    private static bool TryGetCurrentRole(Player? player, out CRole? role)
    {
        role = null;
        return player != null
               && !string.IsNullOrEmpty(player.UniqueRole)
               && UniqueRoleToRole.TryGetValue(player.UniqueRole, out role);
    }

    private static void Dispatch(Player? player, Action<CRole> body, string tag)
    {
        if (player == null) return;
        if (!TryGetCurrentRole(player, out var role)) return;

        try
        {
            body(role);
        }
        catch (Exception ex)
        {
            Log.Error($"CRole.{tag} error in {role?.GetType().Name}: {ex}");
        }
    }

    private static void OnAnyPlayerDying(PlayerEvents.DyingEventArgs? ev)
    {
        if (ev?.Player == null)
        {
            Log.Debug("OnAnyPlayerDying: ev or ev.Player is null, skipping");
            return;
        }

        // ★ 死亡時に必ずロール効果コルーチンを停止
        StopRoleEffectCoroutine(ev.Player);

        string uniqueRole = ev.Player.UniqueRole;

        if (string.IsNullOrEmpty(uniqueRole))
        {
            Log.Debug($"OnAnyPlayerDying: UniqueRole is null/empty for {ev.Player.Nickname}, skipping");
            return;
        }

        if (!UniqueRoleToRole.TryGetValue(uniqueRole, out var role))
            return;

        try
        {
            role.OnRoleDying(ev);
        }
        catch (Exception ex)
        {
            Log.Error($"CRole.OnDying error in {role.GetType().Name}: {ex}");
        }
    }

    private static void OnAnyPlayerChangingRole(PlayerEvents.ChangingRoleEventArgs? ev)
    {
        if (ev?.Player == null || !ev.IsAllowed) return;

        Dispatch(ev.Player, role => role.OnRoleChanging(ev), nameof(OnRoleChanging));
        if (!ev.IsAllowed) return;

        StopRoleEffectCoroutine(ev.Player);

        if (!TeamNpcs.TryGetValue(ev.Player.Id, out var oldInfo)) return;

        // ★ ev.Player をクロージャで直接キャプチャしない
        var playerId = ev.Player.Id;
        var oldNpcId = oldInfo.NpcId;

        Timing.CallDelayed(RoleSpawnTimings.TeamNpcCleanupAfterRoleChange, () =>
        {
            var player = Player.Get(playerId);
            if (player == null || !player.IsConnected) return;

            if (!TeamNpcs.TryGetValue(player.Id, out var current)) return;
            if (current.NpcId != oldNpcId) return;

            CleanupTeamNpc(player);
        });
    }

    private static void OnAnyPlayerLeft(PlayerEvents.LeftEventArgs? ev)
    {
        if (ev?.Player == null) return;

        Dispatch(ev.Player, role => role.OnRoleLeft(ev), nameof(OnRoleLeft));
        StopRoleEffectCoroutine(ev.Player);
        CleanupTeamNpc(ev.Player);
    }

    private static void OnAnyPlayerHurting(PlayerEvents.HurtingEventArgs? ev)
    {
        if (ev == null) return;

        Dispatch(ev.Player, role => role.OnRoleHurting(ev), nameof(OnRoleHurting));
        Dispatch(ev.Attacker, role => role.OnRoleHurtingOthers(ev), nameof(OnRoleHurtingOthers));
    }

    private static void OnAnyPlayerSpawningRagdoll(PlayerEvents.SpawningRagdollEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleSpawningRagdoll(ev), nameof(OnRoleSpawningRagdoll));
    }

    private static void OnAnyPlayerChangingItem(PlayerEvents.ChangingItemEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleChangingItem(ev), nameof(OnRoleChangingItem));
    }

    private static void OnAnyPlayerChangedItem(PlayerEvents.ChangedItemEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleChangedItem(ev), nameof(OnRoleChangedItem));
    }

    private static void OnAnyPlayerPickingUpItem(PlayerEvents.PickingUpItemEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRolePickingUpItem(ev), nameof(OnRolePickingUpItem));
    }

    private static void OnAnyPlayerDroppingItem(PlayerEvents.DroppingItemEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleDroppingItem(ev), nameof(OnRoleDroppingItem));
    }

    private static void OnAnyPlayerUsingItem(PlayerEvents.UsingItemEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleUsingItem(ev), nameof(OnRoleUsingItem));
    }

    private static void OnAnyPlayerUsedItem(PlayerEvents.UsedItemEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleUsedItem(ev), nameof(OnRoleUsedItem));
    }

    private static void OnAnyPlayerDroppingAmmo(PlayerEvents.DroppingAmmoEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleDroppingAmmo(ev), nameof(OnRoleDroppingAmmo));
    }

    private static void OnAnyPlayerReloadingWeapon(PlayerEvents.ReloadingWeaponEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleReloadingWeapon(ev), nameof(OnRoleReloadingWeapon));
    }

    private static void OnAnyPlayerInteractingDoor(PlayerEvents.InteractingDoorEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleInteractingDoor(ev), nameof(OnRoleInteractingDoor));
    }

    private static void OnAnyPlayerHandcuffing(PlayerEvents.HandcuffingEventArgs? ev)
    {
        if (ev == null) return;

        Dispatch(ev.Player, role => role.OnRoleHandcuffing(ev), nameof(OnRoleHandcuffing));
        Dispatch(ev.Target, role => role.OnRoleBeingHandcuffed(ev), nameof(OnRoleBeingHandcuffed));
    }

    private static void OnAnyPlayerReceivingVoiceMessage(PlayerEvents.ReceivingVoiceMessageEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleReceivingVoiceMessage(ev), nameof(OnRoleReceivingVoiceMessage));
    }

    private static void OnAnyPlayerVoiceChatting(PlayerEvents.VoiceChattingEventArgs? ev)
    {
        if (ev?.Player == null) return;
        Dispatch(ev.Player, role => role.OnRoleVoiceChatting(ev), nameof(OnRoleVoiceChatting));
    }

    private static void OnAnyAnnouncingScpTermination(MapEvents.AnnouncingScpTerminationEventArgs? ev)
    {
        if (ev?.Player == null)
            return;

        var uniqueRole = ev.Player.UniqueRole;
        if (string.IsNullOrEmpty(uniqueRole))
            return;

        if (!UniqueRoleToRole.TryGetValue(uniqueRole, out var role))
            return;

        try
        {
            role.OnRoleDyingCassie(ev);
        }
        catch (Exception ex)
        {
            Log.Error($"CRole.OnDyingCassie error in {role.GetType().Name}: {ex}");
        }
    }

    // ==== インスタンス管理 ====

    private void InternalRegisterEvents(bool autoRegisterEvents)
    {
        if (RegisteredInstances.Add(this))
        {
            RegisterLookup(this);

            if (autoRegisterEvents)
                RegisterEvents();

            Log.Debug($"CRole registered: {GetType().Name} (autoEvents={autoRegisterEvents})");
        }
        else
        {
            // ★ 二重登録防止ログ
            Log.Warn($"CRole.InternalRegisterEvents: {GetType().Name} already registered");
        }
    }

    private void InternalUnregisterEvents()
    {
        if (RegisteredInstances.Remove(this))
        {
            if (!string.IsNullOrEmpty(UniqueRoleKey) &&
                UniqueRoleToRole.TryGetValue(UniqueRoleKey, out var inst) &&
                ReferenceEquals(inst, this))
            {
                UniqueRoleToRole.Remove(UniqueRoleKey);
            }

            if (RoleIdToRole.TryGetValue(CRoleTypeId, out var roleInst) && ReferenceEquals(roleInst, this))
                RoleIdToRole.Remove(CRoleTypeId);

            if (TypeToRole.TryGetValue(GetType(), out var typeInst) && ReferenceEquals(typeInst, this))
                TypeToRole.Remove(GetType());

            UnregisterEvents();
            CleanupTeamNpcs(UniqueRoleKey);
            Log.Debug($"CRole unregistered: {GetType().Name}");
        }
        else
        {
            Log.Warn($"CRole.InternalUnregisterEvents: {GetType().Name} was not in RegisteredInstances");
        }
    }

    private static void RegisterLookup(CRole? instance)
    {
        if (instance == null) return;

        if (!string.IsNullOrEmpty(instance.UniqueRoleKey))
            UniqueRoleToRole[instance.UniqueRoleKey] = instance;

        if (instance.CRoleTypeId != CRoleTypeId.None)
            RoleIdToRole[instance.CRoleTypeId] = instance;

        TypeToRole[instance.GetType()] = instance;
    }

    // ==== 派生クラス用フック ====

    public virtual void RegisterEvents() { }

    public virtual void UnregisterEvents() { }

    protected virtual void OnRoleChanging(PlayerEvents.ChangingRoleEventArgs ev) { }
    protected virtual void OnRoleLeft(PlayerEvents.LeftEventArgs ev) { }
    protected virtual void OnRoleHurting(PlayerEvents.HurtingEventArgs ev) { }
    protected virtual void OnRoleHurtingOthers(PlayerEvents.HurtingEventArgs ev) { }
    protected virtual void OnRoleSpawningRagdoll(PlayerEvents.SpawningRagdollEventArgs ev) { }
    protected virtual void OnRoleChangingItem(PlayerEvents.ChangingItemEventArgs ev) { }
    protected virtual void OnRoleChangedItem(PlayerEvents.ChangedItemEventArgs ev) { }
    protected virtual void OnRolePickingUpItem(PlayerEvents.PickingUpItemEventArgs ev) { }
    protected virtual void OnRoleDroppingItem(PlayerEvents.DroppingItemEventArgs ev) { }
    protected virtual void OnRoleUsingItem(PlayerEvents.UsingItemEventArgs ev) { }
    protected virtual void OnRoleUsedItem(PlayerEvents.UsedItemEventArgs ev) { }
    protected virtual void OnRoleDroppingAmmo(PlayerEvents.DroppingAmmoEventArgs ev) { }
    protected virtual void OnRoleReloadingWeapon(PlayerEvents.ReloadingWeaponEventArgs ev) { }
    protected virtual void OnRoleInteractingDoor(PlayerEvents.InteractingDoorEventArgs ev) { }
    protected virtual void OnRoleHandcuffing(PlayerEvents.HandcuffingEventArgs ev) { }
    protected virtual void OnRoleBeingHandcuffed(PlayerEvents.HandcuffingEventArgs ev) { }
    protected virtual void OnRoleReceivingVoiceMessage(PlayerEvents.ReceivingVoiceMessageEventArgs ev) { }
    protected virtual void OnRoleVoiceChatting(PlayerEvents.VoiceChattingEventArgs ev) { }

    // ==== メタ情報 ====
    protected abstract string RoleName { get; set; }
    protected abstract string Description { get; set; }
    protected virtual float DescriptionDuration { get; set; } = 8f;
    protected virtual bool DescriptionShowRoleName { get; set; } = true;
    protected virtual string DisplayName => RoleName;
    protected virtual RoleTypeId? TeamNpcRoleTypeId { get; set; } = null;
    protected virtual bool ClearInventoryBeforeDeath { get; set; } = false;

    protected abstract CRoleTypeId CRoleTypeId { get; set; }

    protected abstract CTeam Team { get; set; }

    protected abstract string UniqueRoleKey { get; set; }

    public string UniqueRoleName => UniqueRoleKey;
    public CRoleTypeId TypeId => CRoleTypeId;
    public CTeam TeamId => Team;
    public string Name => RoleName;
    public string RoleDescription => Description;
    public string RoleDisplayName => DisplayName;
    public string? CustomInfo => SpawnCustomInfo;
    public virtual bool CanUseProximityChat => false;
    public virtual bool ProximityChatEnabledByDefault => CanUseProximityChat;

    // ==== 逆引き ====

    public static CRoleTypeId GetRoleIdFromUnique(string uniqueRole)
    {
        if (string.IsNullOrEmpty(uniqueRole))
            return CRoleTypeId.None;

        return UniqueRoleToRole.TryGetValue(uniqueRole, out var role)
            ? role.CRoleTypeId
            : CRoleTypeId.None;
    }

    public static CTeam GetTeamFromUnique(string uniqueRole)
    {
        if (string.IsNullOrEmpty(uniqueRole))
            return CTeam.Others;

        return UniqueRoleToRole.TryGetValue(uniqueRole, out var role)
            ? role.Team
            : CTeam.Others;
    }

    protected bool Check(Player? player)
    {
        if (player == null) return false;
        return GetRoleIdFromUnique(player.UniqueRole) == CRoleTypeId;
    }

    public bool Is(Player? player) => Check(player);

    public static IReadOnlyCollection<CRole> GetAllInstances()
        => RegisteredInstances;

    public static bool TryGet(CRoleTypeId roleTypeId, out CRole role)
    {
        if (RoleIdToRole.TryGetValue(roleTypeId, out role))
            return true;

        return TryCreateUnregisteredInstance(roleTypeId, out role);
    }

    public static bool TryGetByUniqueRole(string uniqueRole, out CRole? role)
    {
        role = null;
        return !string.IsNullOrEmpty(uniqueRole) && UniqueRoleToRole.TryGetValue(uniqueRole, out role);
    }

    public static T? Get<T>() where T : CRole
    {
        if (TypeToRole.TryGetValue(typeof(T), out var role))
            return role as T;

        var registered = RegisteredInstances.OfType<T>().FirstOrDefault();
        if (registered != null)
            return registered;

        var created = Activator.CreateInstance(typeof(T)) as T;
        return created;
    }

    public static bool TrySpawn(Player? player, CRoleTypeId roleTypeId,
        RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        if (player == null) return false;

        if (roleTypeId == CRoleTypeId.None)
        {
            player.UniqueRole = null;
            return true;
        }

        if (!TryGet(roleTypeId, out var role))
            return false;

        role.SpawnRole(player, roleSpawnFlags);
        return true;
    }

    private static bool TryCreateUnregisteredInstance(CRoleTypeId roleTypeId, out CRole? role)
    {
        role = null;

        foreach (var type in RoleTypes)
        {
            try
            {
                if (type.IsAbstract) continue;
                var instance = (CRole)Activator.CreateInstance(type);
                if (instance == null) continue;
                if (instance.CRoleTypeId != roleTypeId) continue;

                RegisterLookup(instance);
                role = instance;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"CRole.TryCreateUnregisteredInstance failed for {type.Name}: {ex}");
            }
        }

        return false;
    }

    // ==== 共通ロジック ====

    protected virtual RoleTypeId? SpawnBaseRole => null;
    protected virtual RoleSpawnFlags? SpawnBaseRoleFlags => null;
    protected virtual float? SpawnMaxHealth => null;
    protected virtual IReadOnlyList<object> SpawnItems => EmptyItems;
    protected virtual IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => EmptyAmmo;
    protected virtual IReadOnlyList<CRoleEffect> SpawnEffects => EmptyEffects;
    protected virtual IReadOnlyList<SpecificFlagType> SpawnSpecificFlags => EmptySpecificFlags;
    protected virtual float SpawnEffectRefreshInterval => 1f;
    protected virtual string? SpawnCustomInfo => null;
    protected virtual Vector3? SpawnPosition => null;
    protected virtual bool SpawnClearsInventory => SpawnItems.Count > 0;
    protected virtual bool UseConfiguredSpawn =>
        SpawnBaseRole != null ||
        SpawnMaxHealth != null ||
        SpawnClearsInventory ||
        SpawnItems.Count > 0 ||
        SpawnAmmo.Count > 0 ||
        !string.IsNullOrEmpty(SpawnCustomInfo) ||
        SpawnPosition != null;

    protected virtual void OnRoleSpawnStarting(Player player, RoleSpawnFlags roleSpawnFlags) { }
    protected virtual void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags) { }
    protected virtual void GiveCustomItems(Player player) { }

    protected virtual void OnRoleDyingCassie(
        MapEvents.AnnouncingScpTerminationEventArgs ev,
        bool isEnable = false,
        string? cassieString = null,
        string? localizedString = null)
    {
        if (!isEnable) return;
        if (!Check(ev.Player)) return;

        ev.IsAllowed = false;
        RoleSpecificTextProvider.Clear(ev.Player);
        CleanupTeamNpc(ev.Player);
        Exiled.API.Features.Cassie.MessageTranslated(cassieString, localizedString);
    }

    protected virtual void OnRoleDying(PlayerEvents.DyingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (!Check(ev.Player)) return;

        if (ClearInventoryBeforeDeath)
            ev.Player.ClearInventory();

        RoleSpecificTextProvider.Clear(ev.Player);
        CleanupTeamNpc(ev.Player);
    }

    public virtual void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
        => SpawnConfiguredRole(player, roleSpawnFlags);

    protected void SpawnConfiguredRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        if (player == null) return;

        RunCommonSpawnLifecycle(player, roleSpawnFlags);
        OnRoleSpawnStarting(player, roleSpawnFlags);
        ApplyConfiguredSpawn(player, roleSpawnFlags);

        // ApplyBaseRole (Role.Set) が ChangingRole を発火させ CustomRoleRemover が
        // player.Clear() でフラグを全消しするため、Role.Set() の直後に同期で付け直す。
        // これが旧版で SpawnRole override 内に TryAddFlag を直書きしていたのと等価。
        AssignIdentity(player);
        ApplySpawnSpecificFlags(player);

        OnRoleSpawned(player, roleSpawnFlags);
        AssignIdentity(player);

        var playerId = player.Id;

        Timing.CallDelayed(RoleSpawnTimings.AfterSpawnFinalize, () =>
        {
            var p = Player.Get(playerId);
            if (p is null) return;

            ApplyCustomInfo(p, SpawnCustomInfo);
            // 遅延後にも再適用。0.25秒以内に何らかの理由で消えた場合の保険。
            ApplySpawnSpecificFlags(p);
            StartRoleEffectCoroutine(p);
        });
    }

    protected void RunCommonSpawnLifecycle(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        if (player == null) return;

        StopRoleEffectCoroutine(player);
        player.ShowHint(string.Empty);
        player.DisableAllEffects();

        switch (roleSpawnFlags)
        {
            case RoleSpawnFlags.None:
            {
                var savePosition = player.Position + new Vector3(0f, 0.1f, 0f);
                var items = player.Items.ToList();
                var ammos = player.Ammo.ToList();
                var playerId = player.Id;

                Timing.CallDelayed(RoleSpawnTimings.RestoreRoleState, () =>
                {
                    var p = Player.Get(playerId);
                    if (p == null || !p.IsConnected) return;

                    p.Position = savePosition;
                    p.ClearInventory();

                    foreach (var item in items)
                        p.AddItem(item);

                    foreach (var ammo in ammos)
                        p.SetAmmo((AmmoType)ammo.Key, ammo.Value);
                });
                break;
            }
            case RoleSpawnFlags.AssignInventory:
            {
                var savePosition = player.Position + new Vector3(0f, 0.1f, 0f);
                var playerId = player.Id;

                Timing.CallDelayed(RoleSpawnTimings.RestoreRoleState, () =>
                {
                    var p = Player.Get(playerId);
                    if (p == null || !p.IsConnected) return;

                    p.Position = savePosition;
                });
                break;
            }
            case RoleSpawnFlags.UseSpawnpoint:
            {
                var items = player.Items.ToList();
                var ammos = player.Ammo.ToList();
                var playerId = player.Id;

                Timing.CallDelayed(RoleSpawnTimings.RestoreRoleState, () =>
                {
                    var p = Player.Get(playerId);
                    if (p == null || !p.IsConnected) return;

                    p.ClearInventory();

                    foreach (var item in items)
                        p.AddItem(item);

                    foreach (var ammo in ammos)
                        p.SetAmmo((AmmoType)ammo.Key, ammo.Value);
                });
                break;
            }
        }

        var id = player.Id;

        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var p = Player.Get(id);
            if (p == null || !p.IsConnected) return;

            if (DescriptionDuration <= 0f) return;
            Hint hint;
            if (DescriptionShowRoleName)
            {
                hint = new Hint()
                {
                    Alignment = HintAlignment.Center, XCoordinate = 0, YCoordinate = 770,
                    Text = $"<size=24><color={Team.GetTeamColor()}>{RoleName}</color>\n{Description}</size>", Id = HudConstId.CRoleSpawned
                };
            }
            else
            {
                hint = new Hint()
                {
                    Alignment = HintAlignment.Center, XCoordinate = 0, YCoordinate = 770,
                    Text = $"<size=24>{Description}</size>", Id = HudConstId.CRoleSpawned
                };
            }

            var display = p.GetPlayerDisplay();
            display.TryGetHint(HudConstId.CRoleSpawned, out var oldHint);
            if (oldHint != null) p.RemoveHint(oldHint);
            p.AddHint(hint);

            Timing.CallDelayed(DescriptionDuration, () =>
            {
                var p2 = Player.Get(id);
                if (p2 == null || !p2.IsConnected) return;

                p2.RemoveHint(hint);
            });
        });

        Timing.CallDelayed(RoleSpawnTimings.TeamNpcSpawn, () =>
        {
            var p = Player.Get(id);
            if (p == null || !p.IsConnected) return;

            TryCreateTeamNpc(p);
        });
    }

    protected virtual void ApplyConfiguredSpawn(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        if (!UseConfiguredSpawn) return;

        ApplyBaseRole(player, roleSpawnFlags);
        ApplyHealth(player);

        if (SpawnClearsInventory)
            player.ClearInventory();

        GiveItems(player, SpawnItems);
        GiveCustomItems(player);
        ApplyAmmo(player, SpawnAmmo);
        ApplySpawnPosition(player);
    }

    protected void ApplyBaseRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        if (player == null || SpawnBaseRole == null) return;
        player.Role.Set(SpawnBaseRole.Value, SpawnBaseRoleFlags ?? roleSpawnFlags);
    }

    protected void AssignIdentity(Player? player)
    {
        if (player == null) return;
        player.UniqueRole = UniqueRoleKey;
    }

    protected void ApplyHealth(Player? player)
    {
        if (player == null || SpawnMaxHealth == null) return;

        player.MaxHealth = SpawnMaxHealth.Value;
        player.Health = player.MaxHealth;
    }

    protected void GiveItems(Player? player, IEnumerable<object>? items)
    {
        if (player == null || items == null) return;

        foreach (var item in items)
            GiveSpawnItem(player, item);
    }

    protected void GiveItem(Player? player, ItemType item)
    {
        if (player == null) return;
        player.AddItem(item);
    }

    protected void GiveCItem<T>(Player player, bool displayMessage = false) where T : CItem
        => CItem.Get<T>()?.Give(player, displayMessage);

    protected bool GiveSpawnItem(Player? player, object? spawnItem, bool displayMessage = false)
    {
        if (player == null || spawnItem == null) return false;

        switch (spawnItem)
        {
            case ItemType itemType:
                GiveItem(player, itemType);
                return true;

            case CItem cItem:
                return cItem.Give(player, displayMessage) != null;

            case CustomItem customItem:
                customItem.Give(player, displayMessage);
                return true;

            case Type type when typeof(CItem).IsAssignableFrom(type):
                return GiveCItem(player, type, displayMessage);

            case Type type when typeof(CustomItem).IsAssignableFrom(type):
                return GiveCustomItem(player, type, displayMessage);

            case uint customItemId:
                return CustomItem.TryGive(player, customItemId, displayMessage);

            default:
                Log.Warn($"CRole.GiveSpawnItem: unsupported SpawnItems entry in {GetType().Name}: {spawnItem}");
                return false;
        }
    }

    protected bool GiveCItem(Player? player, Type? cItemType, bool displayMessage = false)
    {
        if (player == null || cItemType == null || !typeof(CItem).IsAssignableFrom(cItemType))
            return false;

        var method = typeof(CItem)
            .GetMethods()
            .FirstOrDefault(m => m.Name == nameof(CItem.Get)
                                 && m.IsGenericMethodDefinition
                                 && m.GetParameters().Length == 0);
        var item = method?.MakeGenericMethod(cItemType).Invoke(null, null) as CItem;
        return item?.Give(player, displayMessage) != null;
    }

    protected bool GiveCustomItem<T>(Player player, bool displayMessage = false) where T : CustomItem
        => GiveCustomItem(player, typeof(T), displayMessage);

    protected bool GiveCustomItem(Player? player, Type? customItemType, bool displayMessage = false)
    {
        if (player == null || customItemType == null || !typeof(CustomItem).IsAssignableFrom(customItemType))
            return false;

        foreach (var item in CustomItem.Registered)
        {
            if (!customItemType.IsInstanceOfType(item)) continue;
            item.Give(player, displayMessage);
            return true;
        }

        Log.Warn($"CRole.GiveCustomItem: Exiled CustomItem not registered: {customItemType.FullName}");
        return false;
    }

    protected void ApplyAmmo(Player? player, IEnumerable<KeyValuePair<AmmoType, ushort>>? ammo)
    {
        if (player == null || ammo == null) return;

        foreach (var kv in ammo)
            player.SetAmmo(kv.Key, kv.Value);
    }

    protected void SetAmmo(Player? player, AmmoType ammoType, ushort amount)
    {
        if (player == null) return;
        player.SetAmmo(ammoType, amount);
    }

    protected void ApplyCustomInfo(Player? player, string? customInfo)
    {
        if (player == null || string.IsNullOrEmpty(customInfo)) return;
        player.SetCustomInfo(customInfo);
    }

    protected void ApplySpawnPosition(Player? player)
    {
        if (player == null || SpawnPosition == null) return;
        player.Position = SpawnPosition.Value;
    }

    protected void ApplySpawnSpecificFlags(Player? player)
    {
        if (player == null || SpawnSpecificFlags.Count == 0) return;

        foreach (var flag in SpawnSpecificFlags)
            player.TryAddFlag(flag);
    }

    // SpawnSpecificFlags の手動除去が必要な場合は派生クラスから直接呼ぶ。
    // ChangingRole イベントからは呼ばない（CustomRoleRemover の Clear() で済むため）。
    protected void RemoveSpawnSpecificFlags(Player? player)
    {
        if (player == null || SpawnSpecificFlags.Count == 0) return;

        foreach (var flag in SpawnSpecificFlags)
            player.TryRemoveFlag(flag);
    }

    private void StartRoleEffectCoroutine(Player? player)
    {
        if (player == null) return;

        StopRoleEffectCoroutine(player);

        var effects = SpawnEffects;
        if (effects is not { Count: > 0 }) return;

        RoleEffectCoroutines[player.Id] = Timing.RunCoroutine(RoleEffectCoroutine(player.Id, effects));
    }

    private IEnumerator<float> RoleEffectCoroutine(int playerId, IReadOnlyList<CRoleEffect> effects)
    {
        var wait = Math.Max(0.1f, SpawnEffectRefreshInterval);

        while (true)
        {
            var player = Player.Get(playerId);
            if (player == null || !player.IsConnected || !player.IsAlive || !Check(player))
                break;

            ApplyRoleEffects(player, effects);
            yield return Timing.WaitForSeconds(wait);
        }

        RoleEffectCoroutines.Remove(playerId);
    }

    private void ApplyRoleEffects(Player? player, IReadOnlyList<CRoleEffect>? effects)
    {
        if (player == null || effects == null) return;

        foreach (var effect in effects)
        {
            try
            {
                effect.Apply(player);
            }
            catch (Exception ex)
            {
                Log.Warn($"CRole.ApplyRoleEffects: failed to apply {effect.EffectType} for {DisplayName}: {ex.Message}");
            }
        }
    }

    private static void StopRoleEffectCoroutine(Player? player)
    {
        if (player == null) return;
        StopRoleEffectCoroutine(player.Id);
    }

    private static void StopRoleEffectCoroutine(int playerId)
    {
        if (!RoleEffectCoroutines.TryGetValue(playerId, out var handle)) return;

        Timing.KillCoroutines(handle);
        RoleEffectCoroutines.Remove(playerId);
    }

    private void TryCreateTeamNpc(Player? player)
    {
        if (TeamNpcRoleTypeId == null) return;
        if (player == null || !player.IsConnected) return;
        if (player.ReferenceHub == null || player.IsHost || player.ReferenceHub.IsHost || IsTeamNpc(player)) return;
        if (!Check(player)) return;

        CleanupTeamNpc(player);

        try
        {
            var npc = Npc.Spawn($"{DisplayName}-TeamNpc", TeamNpcRoleTypeId.Value);
            if (npc == null)
            {
                Log.Warn($"{DisplayName} team NPC spawn returned null for {player.Nickname}");
                return;
            }

            if (npc.ReferenceHub == null || npc.ReferenceHub.authManager == null)
            {
                Log.Warn($"{DisplayName} team NPC has null ReferenceHub/authManager for {player.Nickname}");
                Npc.Get(npc.Id)?.Destroy();
                return;
            }

            npc.HideTeamNpcFromClientPlayerList($"{DisplayName}:spawn");

            var npcId = npc.Id;
            var playerId = player.Id;

            Timing.CallDelayed(RoleSpawnTimings.TeamNpcPostSpawnSetup, () =>
            {
                // ★ Destroy 済みかもしれないので再取得
                var currentNpc = Npc.Get(npcId);
                if (currentNpc?.ReferenceHub == null) return;

                currentNpc.HideTeamNpcFromClientPlayerList($"{DisplayName}:post-spawn");
                currentNpc.IsGodModeEnabled = true;
                currentNpc.IsSpectatable = false;
                currentNpc.EnableEffect(EffectType.Invisible, 255);
            });

            TeamNpcs[player.Id] = new TeamNpcInfo(npc.Id, UniqueRoleKey);
        }
        catch (Exception e)
        {
            Log.Error($"{DisplayName} team NPC spawn failed for {player.Nickname}: {e}");
        }
    }

    protected static void CleanupTeamNpc(Player? player)
    {
        if (player == null) return;
        if (!TeamNpcs.TryGetValue(player.Id, out var info)) return;

        var npc = Npc.Get(info.NpcId);
        if (npc != null)
        {
            try
            {
                npc.Destroy();
            }
            catch (Exception e)
            {
                Log.Warn($"CleanupTeamNpc: failed to destroy NPC {info.NpcId} for player {player.Nickname}: {e.Message}");
            }
        }

        TeamNpcs.Remove(player.Id);
    }

    private static void CleanupTeamNpcs(string uniqueRole)
    {
        if (string.IsNullOrEmpty(uniqueRole)) return;

        foreach (var kvp in TeamNpcs.ToList())
        {
            if (!string.Equals(kvp.Value.UniqueRole, uniqueRole, StringComparison.OrdinalIgnoreCase)) continue;

            var npc = Npc.Get(kvp.Value.NpcId);
            if (npc != null)
            {
                try
                {
                    npc.Destroy();
                }
                catch (Exception e)
                {
                    Log.Warn($"CleanupTeamNpcs({uniqueRole}): failed to destroy NPC {kvp.Value.NpcId}: {e.Message}");
                }
            }

            TeamNpcs.Remove(kvp.Key);
        }
    }

    private static void CleanupAllTeamNpcs()
    {
        foreach (var kvp in TeamNpcs.ToList())
        {
            var npc = Npc.Get(kvp.Value.NpcId);
            if (npc != null)
            {
                try
                {
                    npc.Destroy();
                }
                catch (Exception e)
                {
                    Log.Warn($"CleanupAllTeamNpcs: failed to destroy NPC {kvp.Value.NpcId}: {e.Message}");
                }
            }
        }

        TeamNpcs.Clear();
    }

    private static void CleanupAllRoleEffectCoroutines()
    {
        foreach (var handle in RoleEffectCoroutines.Values.ToList())
            Timing.KillCoroutines(handle);

        RoleEffectCoroutines.Clear();
    }
}
