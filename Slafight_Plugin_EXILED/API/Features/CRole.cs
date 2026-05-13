using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using MEC;
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

public abstract class CRole
{
    // 全インスタンスを追跡（主に自動生成分）
    private static readonly HashSet<CRole> RegisteredInstances = [];

    // 全Roleタイプ
    private static readonly List<Type> RoleTypes;

    // UniqueRole 文字列 → CRole インスタンス（バリアント含む）
    private static readonly Dictionary<string, CRole> UniqueRoleToRole = new(StringComparer.OrdinalIgnoreCase);

    private static bool _eventsSubscribed;

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
            MapHandlers.AnnouncingScpTermination += OnAnyAnnouncingScpTermination;
            _eventsSubscribed = true;
        }

        foreach (var type in RoleTypes)
        {
            try
            {
                var instance = (CRole)Activator.CreateInstance(type);

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

        if (_eventsSubscribed)
        {
            PlayerHandlers.Dying -= OnAnyPlayerDying;
            MapHandlers.AnnouncingScpTermination -= OnAnyAnnouncingScpTermination;
            _eventsSubscribed = false;
        }
    }

    /// <summary>
    /// Ignore付きロール用:
    /// 手動で生成したインスタンスを UniqueRole マップの「本体」に差し替える。
    /// これを呼ぶと、Dying などがこのインスタンスに飛ぶようになる。
    /// </summary>
    public static void OverrideRoleInstance(string uniqueRole, CRole instance)
    {
        if (string.IsNullOrEmpty(uniqueRole) || instance == null)
            return;

        UniqueRoleToRole[uniqueRole] = instance;
        Log.Debug($"CRole.OverrideRoleInstance: {uniqueRole} -> {instance.GetType().Name}");
    }

    // ==== 共通イベントハンドラ（static） ====

    private static void OnAnyPlayerDying(PlayerEvents.DyingEventArgs ev)
    {
        if (ev?.Player == null)
        {
            Log.Debug("OnAnyPlayerDying: ev or ev.Player is null, skipping");
            return;
        }

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
            role.OnDying(ev);
        }
        catch (Exception ex)
        {
            Log.Error($"CRole.OnDying error in {role.GetType().Name}: {ex}");
        }
    }

    private static void OnAnyAnnouncingScpTermination(MapEvents.AnnouncingScpTerminationEventArgs ev)
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
            role.OnDyingCassie(ev);
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
            if (!string.IsNullOrEmpty(UniqueRoleKey))
                UniqueRoleToRole[UniqueRoleKey] = this;

            if (autoRegisterEvents)
                RegisterEvents();

            Log.Debug($"CRole registered: {GetType().Name} (autoEvents={autoRegisterEvents})");
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

            UnregisterEvents();
            Log.Debug($"CRole unregistered: {GetType().Name}");
        }
    }

    // ==== 派生クラス用フック ====

    public virtual void RegisterEvents() { }

    public virtual void UnregisterEvents() { }

    // ==== メタ情報 ====
    protected abstract string RoleName { get; set; }
    protected abstract string Description { get; set; }
    protected virtual float DescriptionDuration { get; set; } = 8f;
    protected virtual bool DescriptionShowRoleName { get; set; } = true;

    protected abstract CRoleTypeId CRoleTypeId { get; set; }

    protected abstract CTeam Team { get; set; }

    protected abstract string UniqueRoleKey { get; set; }

    public string UniqueRoleName => UniqueRoleKey;


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

    // ==== 共通ロジック ====

    protected virtual void OnDyingCassie(
        MapEvents.AnnouncingScpTerminationEventArgs ev,
        bool isEnable = false,
        string cassieString = null,
        string localizedString = null)
    {
        if (!isEnable) return;
        if (!Check(ev.Player)) return;

        ev.IsAllowed = false;
        RoleSpecificTextProvider.Clear(ev.Player);
        Exiled.API.Features.Cassie.MessageTranslated(cassieString, localizedString);
    }

    protected virtual void OnDying(PlayerEvents.DyingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (!Check(ev.Player)) return;

        RoleSpecificTextProvider.Clear(ev.Player);
    }

    public virtual void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        if (player == null) return;
        
        player.ShowHint(string.Empty);
        player.DisableAllEffects();

        switch (roleSpawnFlags)
        {
            case RoleSpawnFlags.None:
            {
                var savePosition = player.Position + new Vector3(0f, 0.1f, 0f);
                var items = player.Items.ToList();
                var ammos = player.Ammo.ToList();

                Timing.CallDelayed(1f, () =>
                {
                    player.Position = savePosition;
                    player.ClearInventory();

                    foreach (var item in items)
                        player.AddItem(item);

                    foreach (var ammo in ammos)
                        player.SetAmmo((AmmoType)ammo.Key, ammo.Value);
                });
                break;
            }
            case RoleSpawnFlags.AssignInventory:
            {
                var savePosition = player.Position + new Vector3(0f, 0.1f, 0f);
                Timing.CallDelayed(1f, () =>
                {
                    player.Position = savePosition;
                });
                break;
            }
            case RoleSpawnFlags.UseSpawnpoint:
            {
                var items = player.Items.ToList();
                var ammos = player.Ammo.ToList();

                Timing.CallDelayed(1f, () =>
                {
                    player.ClearInventory();

                    foreach (var item in items)
                        player.AddItem(item);

                    foreach (var ammo in ammos)
                        player.SetAmmo((AmmoType)ammo.Key, ammo.Value);
                });
                break;
            }
        }

        Timing.CallDelayed(0.05f, () =>
        {
            if (DescriptionDuration <= 0f) return;
            Hint hint;
            if (DescriptionShowRoleName)
            {
                hint = new Hint()
                {
                    Alignment = HintAlignment.Center, XCoordinate = 0, YCoordinate = 770,
                    Text = $"<size=24><color={Team.GetTeamColor()}>{RoleName}</color>\n{Description}</size>", Id = "CRoleSpawnedHint"
                };
            }
            else
            {
                hint = new Hint()
                {
                    Alignment = HintAlignment.Center, XCoordinate = 0, YCoordinate = 770,
                    Text = $"<size=24>{Description}</size>", Id = "CRoleSpawnedHint"
                };
            }

            var display = player.GetPlayerDisplay();
            display.TryGetHint("CRoleSpawnedHint", out var oldHint);
            if (oldHint != null) player.RemoveHint(oldHint);
            player.AddHint(hint);
            Timing.CallDelayed(DescriptionDuration, () =>
            {
                player.RemoveHint(hint);
            });
        });
    }
}