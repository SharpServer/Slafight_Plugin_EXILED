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
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp610Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-610";
    protected override string Description { get; set; } = "SCP-610に哀れにも感染し、変異してしまった人間の成れの果て。\n" +
                                                          "生存者を攻撃すると感染させ、仲間を増やすことができる。";
    protected override float DescriptionDuration { get; set; } = 15f;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp610;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp610";
    protected override RoleTypeId? TeamNpcRoleTypeId { get; set; } = RoleTypeId.Scp0492;
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 800f;
    protected override bool SpawnClearsInventory => true;
    protected override IReadOnlyList<object> SpawnItems => [ItemType.SCP1509];
    protected override string SpawnCustomInfo => "<color=#C50000>SCP-610</color>";
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Fade, 255),
        new(EffectType.DamageReduction, 80)
    ];

    protected override IReadOnlyList<SpecificFlagType> SpawnSpecificFlags =>
    [
        SpecificFlagType.PickingDisabled,
        SpecificFlagType.DroppingDisabled
    ];

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.Position = Room.Get(RoomType.Hcz939).WorldPosition(Vector3.up * 0.65f);

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
        ev.Amount /= 3.5f;
        if (ev.Player.Health <= 40f && !ev.Player.HasFlag(SpecificFlagType.Infecting610))
        {
MeowExtensions.ShowHint(            ev.Attacker, "<size=22><color=yellow><b>相手を感染させる事に成功した！3分後には同胞になっているであろう！</b></color></size>", 5);
MeowExtensions.ShowHint(            ev.Player, "<size=22><color=red><b>SCP-610に感染してしまった！\nSCP-500で治療しなければ三分後には同胞になってしまうぞ！");
            ev.Player.TryAddFlag(SpecificFlagType.Infecting610);
            ev.Player.EnableEffect<Concussed>(255);
            ev.Player.EnableEffect<DamageReduction>(60);
            Timing.RunCoroutine(InfectionCoroutine(ev.Player));
        }
        else if (ev.Player.HasFlag(SpecificFlagType.Infecting610))
        {
            ev.Amount /= 10f;
MeowExtensions.ShowHint(            ev.Attacker, "<size=24>相手はもうすでに感染しています！</size>");
        }
    }

    private IEnumerator<float> InfectionCoroutine(Player player)
    {
        float elapsedTime = 0f;
        while (true)
        {
            if (Round.IsLobby || player.GetTeam() is CTeam.SCPs || !player.IsAlive || !player.HasFlag(SpecificFlagType.Infecting610))
            {
                yield break;
            }

            if (elapsedTime >= 200f)
            {
                player.TryRemoveFlag(SpecificFlagType.Infecting610);
                player.SetRole(CRoleTypeId.Scp610, RoleSpawnFlags.AssignInventory);
            }

            elapsedTime += 1f;
            yield return Timing.WaitForSeconds(1f);
        }
    }
}
