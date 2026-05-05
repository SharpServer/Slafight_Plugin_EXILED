using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp999Role : CRole
{
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
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.SpawningRagdoll -= CancelRagdoll;
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
        player.SetAmmoLimit(AmmoType.Nato9, 500);
        player.AddAmmo(AmmoType.Nato9, 500);

        player.SetCustomInfo("SCP-999");

        player.Position = Door.Get(DoorType.Scp173NewGate).Position + new Vector3(0f, 1f, 0f);
        LabApiHandler.Schem999(LabApi.Features.Wrappers.Player.Get(player.ReferenceHub));
    }
    
    private void CancelRagdoll(SpawningRagdollEventArgs ev)
    {
        if (Check(ev.Player))
            ev.IsAllowed = false;
    }

    protected override void OnDying(DyingEventArgs ev)
    {
        CassieHelper.AnnounceTermination(ev, "SCP 9 9 9", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnDying(ev);
    }
}