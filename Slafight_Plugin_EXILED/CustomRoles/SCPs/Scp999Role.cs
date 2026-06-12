using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp999Role : CRole
{
    private const ushort Nato9Limit = 500;
    private const float Nato9AutoDropCleanupRadiusSqr = 2.25f;
    private readonly Dictionary<int, ushort> _reserveNato9ByPlayerId = [];
    private readonly Dictionary<int, Nato9SwitchState> _pendingNato9SwitchesByPlayerId = [];

    protected override string RoleName { get; set; } = "SCP-999";
    protected override string Description { get; set; } = "<size=24><color=#FF1493>SCP-999</color>\n全員とたわむれましょう！\n※勝敗には影響しません。可愛いペット的にふるまって\n攻撃してきた奴らに痛い一撃を喰らわせてやりましょう。";
    protected override float DescriptionDuration { get; set; } = 10f;
    protected override bool DescriptionShowRoleName { get; set; } = false;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp999;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp999";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp173;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Fade, 255)
    ];

    protected override IReadOnlyList<SpecificFlagType> SpawnSpecificFlags =>
    [
        SpecificFlagType.GunsDisabled
    ];

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.SpawningRagdoll += CancelRagdoll;
        Exiled.Events.Handlers.Player.ChangingItem += OnChangingItem;
        Exiled.Events.Handlers.Player.ChangedItem += OnChangedItem;
        Exiled.Events.Handlers.Player.DroppingAmmo += OnDroppingAmmo;
        Exiled.Events.Handlers.Player.ReloadingWeapon += OnReloadingWeapon;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.SpawningRagdoll -= CancelRagdoll;
        Exiled.Events.Handlers.Player.ChangingItem -= OnChangingItem;
        Exiled.Events.Handlers.Player.ChangedItem -= OnChangedItem;
        Exiled.Events.Handlers.Player.DroppingAmmo -= OnDroppingAmmo;
        Exiled.Events.Handlers.Player.ReloadingWeapon -= OnReloadingWeapon;
        _reserveNato9ByPlayerId.Clear();
        _pendingNato9SwitchesByPlayerId.Clear();
        base.UnregisterEvents();
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.Role.Set(RoleTypeId.Tutorial,RoleSpawnFlags.AssignInventory);
        player.MaxHealth = 999;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        RememberNato9(player);

        player.SetCustomInfo("SCP-999");
        LabApiHandler.Schem999(LabApi.Features.Wrappers.Player.Get(player.ReferenceHub));
    }
    
    private void CancelRagdoll(SpawningRagdollEventArgs ev)
    {
        if (Check(ev.Player))
            ev.IsAllowed = false;
    }

    private void OnChangingItem(ChangingItemEventArgs ev)
    {
        if (!Check(ev.Player)) return;

        PrepareNato9Switch(ev.Player);
        ApplyNato9Profile(ev.Player);
    }

    private void OnChangedItem(ChangedItemEventArgs ev)
    {
        if (!Check(ev.Player)) return;

        int playerId = ev.Player.Id;
        _pendingNato9SwitchesByPlayerId.TryGetValue(ev.Player.Id, out Nato9SwitchState switchState);
        RestoreNato9Profile(ev.Player);
        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var player = GetRolePlayer(playerId);
            if (player != null)
                RestoreNato9Profile(player);
        });
        Timing.CallDelayed(RoleSpawnTimings.RoleStateReapply, () =>
        {
            var player = GetRolePlayer(playerId);
            if (player != null)
                RestoreNato9Profile(player);
        });
        Timing.CallDelayed(0.5f, () => ClearNato9Switch(playerId, switchState));
    }

    private void OnDroppingAmmo(DroppingAmmoEventArgs ev)
    {
        if (!Check(ev.Player) || ev.AmmoType != AmmoType.Nato9) return;

        ev.IsAllowed = false;
        RestoreNato9Profile(ev.Player);
    }

    private void OnReloadingWeapon(ReloadingWeaponEventArgs ev)
    {
        if (!Check(ev.Player)) return;

        int playerId = ev.Player.Id;
        ApplyNato9Profile(ev.Player);
        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var player = GetRolePlayer(playerId);
            if (player != null)
                ApplyNato9Profile(player);
        });
    }

    private void RememberNato9(Player player)
    {
        _reserveNato9ByPlayerId[player.Id] = player.GetAmmo(AmmoType.Nato9);
    }

    private void PrepareNato9Switch(Player player)
    {
        if (!Check(player)) return;

        ushort reserve = player.GetAmmo(AmmoType.Nato9);
        _reserveNato9ByPlayerId[player.Id] = reserve;
        _pendingNato9SwitchesByPlayerId[player.Id] = new Nato9SwitchState
        {
            Reserve = reserve,
            Position = player.Position,
            ExistingPickupSerials = GetNearbyOwnedNato9PickupSerials(player),
        };
    }

    private void ApplyNato9Profile(Player player)
    {
        if (!Check(player)) return;

        player.SetAmmoLimit(AmmoType.Nato9, Nato9Limit);

        if (player.GetAmmo(AmmoType.Nato9) > Nato9Limit)
            player.SetAmmo(AmmoType.Nato9, Nato9Limit);
    }

    private void RestoreNato9Profile(Player player)
    {
        if (!Check(player)) return;

        player.SetAmmoLimit(AmmoType.Nato9, Nato9Limit);

        ushort current = player.GetAmmo(AmmoType.Nato9);
        Nato9SwitchState? switchState = _pendingNato9SwitchesByPlayerId.TryGetValue(player.Id, out Nato9SwitchState state)
            ? state
            : null;
        ushort remembered = switchState?.Reserve
            ?? (_reserveNato9ByPlayerId.TryGetValue(player.Id, out ushort value)
            ? value
            : current);

        ushort restored = Math.Min(Nato9Limit, Math.Max(current, remembered));
        CleanupAutomaticNato9Drops(player, switchState);
        player.SetAmmo(AmmoType.Nato9, restored);
        _reserveNato9ByPlayerId[player.Id] = restored;
    }

    private static HashSet<ushort> GetNearbyOwnedNato9PickupSerials(Player player)
        => Pickup.List
            .OfType<AmmoPickup>()
            .Where(pickup => IsOwnedNato9PickupNear(player, pickup, player.Position))
            .Select(pickup => pickup.Serial)
            .ToHashSet();

    private static void CleanupAutomaticNato9Drops(Player player, Nato9SwitchState? switchState)
    {
        if (switchState == null) return;

        foreach (AmmoPickup pickup in Pickup.List.OfType<AmmoPickup>().ToList())
        {
            if (!IsOwnedNato9PickupNear(player, pickup, switchState.Position)) continue;
            if (switchState.ExistingPickupSerials.Contains(pickup.Serial)) continue;

            pickup.Destroy();
        }
    }

    private void ClearNato9Switch(int playerId, Nato9SwitchState? switchState)
    {
        if (switchState == null) return;
        if (!_pendingNato9SwitchesByPlayerId.TryGetValue(playerId, out Nato9SwitchState current)) return;
        if (!ReferenceEquals(current, switchState)) return;

        _pendingNato9SwitchesByPlayerId.Remove(playerId);
    }

    private Player GetRolePlayer(int playerId)
    {
        var player = Player.Get(playerId);
        return player?.ReferenceHub != null && Check(player) ? player : null;
    }

    private void CleanupPlayerState(Player player)
    {
        if (player == null)
            return;

        _reserveNato9ByPlayerId.Remove(player.Id);
        _pendingNato9SwitchesByPlayerId.Remove(player.Id);
    }

    private static bool IsOwnedNato9PickupNear(Player player, AmmoPickup pickup, UnityEngine.Vector3 origin)
        => pickup.IsSpawned
           && pickup.AmmoType == AmmoType.Nato9
           && pickup.PreviousOwner == player
           && (pickup.Position - origin).sqrMagnitude <= Nato9AutoDropCleanupRadiusSqr;

    private sealed class Nato9SwitchState
    {
        public ushort Reserve { get; init; }
        public UnityEngine.Vector3 Position { get; init; }
        public HashSet<ushort> ExistingPickupSerials { get; init; } = [];
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CleanupPlayerState(ev.Player);
        CassieHelper.AnnounceTermination(ev, "SCP 9 9 9", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }

    protected override void OnRoleChanging(ChangingRoleEventArgs ev)
    {
        CleanupPlayerState(ev.Player);
        base.OnRoleChanging(ev);
    }

    protected override void OnRoleLeft(LeftEventArgs ev)
    {
        CleanupPlayerState(ev.Player);
        base.OnRoleLeft(ev);
    }
}
