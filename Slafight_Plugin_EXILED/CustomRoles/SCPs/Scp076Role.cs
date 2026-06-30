using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using ProjectMER.Features.Extensions;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomEffects;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp076Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-076";
    protected override string Description { get; set; } = "W.I.P";
    protected override float DescriptionDuration { get; set; } = 15f;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp076;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp076";
    protected override RoleTypeId? TeamNpcRoleTypeId { get; set; } = RoleTypeId.Scp0492;
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 1500f;
    protected override bool SpawnClearsInventory => true;
    protected override IReadOnlyList<object> SpawnItems => [ItemType.Jailbird];
    protected override string SpawnCustomInfo => "SCP-076";
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new (EffectType.Scp1853),
    ];

    protected override IReadOnlyList<SpecificFlagType> SpawnSpecificFlags =>
    [
        SpecificFlagType.PickingDisabled,
        SpecificFlagType.DroppingDisabled
    ];

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        TrySetPlayerPosition(player, Room.Get(RoomType.Hcz939).WorldPosition(Vector3.up * 0.65f), nameof(Scp076Role));

        player.TryWear("scp-610", player.Transform, out var schematicObject, (Vector3.down * 1f));
        //schematicObject.Scale *= 1.185f;
        LabApi.Features.Wrappers.Player.Get(player.NetId)!.DestroySchematic(schematicObject);
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CassieHelper.AnnounceTermination(ev, "SCP 6 1 0", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (!ev.IsDeathExpected()) return;
        Timing.CallDelayed(60f, () =>
        {
            if (ev.Attacker is null) return;
            ev.Attacker.EnableEffect<DamageBoost>(20, 30f);
        });
    }

    private IEnumerator<float> Coroutine(Player player)
    {
        if (Round.IsEnded || !Check(player)) yield break;
        
    }
}
