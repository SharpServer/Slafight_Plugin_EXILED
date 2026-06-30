using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomEffects;
using Slafight_Plugin_EXILED.Extensions;
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

    private static readonly Dictionary<Player, byte> MovementStats = [];

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        TrySetPlayerPosition(player, Room.Get(RoomType.Hcz939).WorldPosition(Vector3.up * 0.65f), nameof(Scp076Role));

        //player.TryWear("scp-610", player.Transform, out var schematicObject, (Vector3.down * 1f));
        //schematicObject.Scale *= 1.185f;
        //LabApi.Features.Wrappers.Player.Get(player.NetId)!.DestroySchematic(schematicObject);

        player.AddAbility<AbsolutePowerAbility>();
        
        MovementStats[player] = 25;
        Timing.RunCoroutine(Coroutine(player));
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CassieHelper.AnnounceTermination(ev, "SCP 0 7 6", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (!ev.IsDeathExpected()) return;
        Timing.CallDelayed(60f, () =>
        {
            if (ev.Attacker is null) return;
            ev.Attacker.EnableEffect<DamageBoost>(20, 30f);
            if (MovementStats.TryGetValue(ev.Attacker, out var value))
            {
                MovementStats[ev.Attacker] = 40;
                Timing.RunCoroutine(MovementSetCoroutine(ev.Attacker));
            }
        });
    }

    private IEnumerator<float> Coroutine(Player player)
    {
        while (true)
        {
            if (Round.IsEnded || !Check(player))
            {
                MovementStats.Remove(player);
                yield break;
            }

            if (MovementStats.TryGetValue(player, out var value))
            {
                if (player.TryGetEffect(out MovementBoost mb))
                {
                    mb.Intensity = value;
                }
                else
                {
                    player.EnableEffect<MovementBoost>(intensity: value, duration: 5f);
                }
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static IEnumerator<float> MovementSetCoroutine(Player player)
    {
        float elapsedTime = 0f;
        while (elapsedTime < 30f)
        {
            if (Round.IsLobby) yield break;
            elapsedTime++;
            yield return Timing.WaitForSeconds(1f);
        }

        if (MovementStats.TryGetValue(player, out var value))
        {
            MovementStats[player] = 25;
        }
    }
}
