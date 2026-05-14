using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp999Role : CRole
{
    private const ushort Nato9Limit = 500;
    private readonly Dictionary<int, ushort> _reserveNato9ByPlayerId = [];

    protected override string RoleName { get; set; } = "SCP-999";
    protected override string Description { get; set; } = "<size=24><color=#FF1493>SCP-999</color>\n全員とたわむれましょう！\n※勝敗には影響しません。可愛いペット的にふるまって\n攻撃してきた奴らに痛い一撃を喰らわせてやりましょう。";
    protected override float DescriptionDuration { get; set; } = 10f;
    protected override bool DescriptionShowRoleName { get; set; } = false;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp999;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp999";

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
        base.UnregisterEvents();
    }

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.Tutorial,RoleSpawnFlags.All);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 99999;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.AddItem(ItemType.GunCOM15);
        player.SetAmmoLimit(AmmoType.Nato9, Nato9Limit);
        player.SetAmmo(AmmoType.Nato9, Nato9Limit);
        RememberNato9(player);

        player.SetCustomInfo("SCP-999");

        player.Position = Door.Get(DoorType.Scp173NewGate).Position + new Vector3(0f, 1f, 0f);
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

        RememberNato9(ev.Player);
        ApplyNato9Profile(ev.Player);
    }

    private void OnChangedItem(ChangedItemEventArgs ev)
    {
        if (!Check(ev.Player)) return;

        RestoreNato9Profile(ev.Player);
        Timing.CallDelayed(0.05f, () => RestoreNato9Profile(ev.Player));
        Timing.CallDelayed(0.25f, () => RestoreNato9Profile(ev.Player));
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

        ApplyNato9Profile(ev.Player);
        Timing.CallDelayed(0.05f, () => ApplyNato9Profile(ev.Player));
    }

    private void RememberNato9(Player player)
    {
        if (!Check(player)) return;

        _reserveNato9ByPlayerId[player.Id] = player.GetAmmo(AmmoType.Nato9);
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
        ushort remembered = _reserveNato9ByPlayerId.TryGetValue(player.Id, out ushort value)
            ? value
            : current;

        ushort restored = Math.Min(Nato9Limit, Math.Max(current, remembered));
        player.SetAmmo(AmmoType.Nato9, restored);
        _reserveNato9ByPlayerId[player.Id] = restored;
    }

    protected override void OnDying(DyingEventArgs ev)
    {
        CassieHelper.AnnounceTermination(ev, "SCP 9 9 9", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnDying(ev);
    }
}
