using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthist;

public class Scp3125Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-3125";
    protected override string Description { get; set; } =
        $"あなたは反ミーム部門を壊滅させる事に成功した！\n" +
        $"残るはかの部門長、<color=#ffa500>マリオンホイーラー</color>を<color=red>殺すだけ</color>だ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp3125;
    protected override CTeam Team { get; set; } = CTeam.Fifthists;
    protected override string UniqueRoleKey { get; set; } = "SCP-3125";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp106;
    protected override RoleSpawnFlags? SpawnBaseRoleFlags => RoleSpawnFlags.AssignInventory;
    public override bool CanUseProximityChat => true;
    public override bool ProximityChatEnabledByDefault => true;

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        TrySetPlayerPosition(player, PositionProvider.GetNtfSpawnPosition(), nameof(Scp3125Role));
        player.SetCustomInfo("<color=#FF0090>SCP-3125</color>");
        const int maxHealth = 55555;
        player.MaxHealth = maxHealth;
        player.Health = maxHealth;
        player.EnableEffect(EffectType.Slowness, 30);

        player.AddAbility<MemeWaveAbility>();
        var playerId = player.Id;
        Timing.CallDelayed(RoleSpawnTimings.Scp3125Startup, () =>
        {
            var current = Player.Get(playerId);
            if (!Check(current) || !IsSafeRolePlayer(current))
                return;

            Timing.RunCoroutine(Scp3125HintSyncCoroutine(current));
            Timing.RunCoroutine(Scp3125Coroutine(current));
        });
    }
    
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        if (FacilityControlRoom.IsAntiMemeProtocolActive && ev.Attacker is null)
        {
            CassieHelper.AnnounceTermination(ev, "SCP 3 1 2 5", $"<color={Team.GetTeamColor()}>{RoleName}</color>", TerminationCause.AntiMeme(), true);
        }
        else
        {
            CassieHelper.AnnounceTermination(ev, "SCP 3 1 2 5", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        }
        base.OnRoleDying(ev);
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Amount <= 10f) return;
        ev.IsAllowed = false;
    }

    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        ev.IsAllowed = false;
    }

    private IEnumerator<float> Scp3125HintSyncCoroutine(Player player)
    {
        while (true)
        {
            var marionWheeler = Player.List.First(p => p.GetCustomRole() is CRoleTypeId.MarionWheeler);
            if (!Check(player) || marionWheeler.GetCustomRole() is not CRoleTypeId.MarionWheeler)
            {
                RoleSpecificTextProvider.Clear(player);
                yield break;
            }
            
            RoleSpecificTextProvider.Set(player, $"[ヘッドスペース]\n- マリオン・ホイーラー -\n階層：{marionWheeler.Zone}\n距離：{Vector3.Distance(player.Position, marionWheeler.Position):F1}\n\n\n\n\n\n\n\n\n\n");

            yield return Timing.WaitForSeconds(0.5f);
        }
    }

    private static IEnumerator<float> Scp3125Coroutine(Player player)
    {
        for (;;)
        {
            if (player.GetCustomRole() != CRoleTypeId.Scp3125)
                yield break;

            foreach (var target in Player.List)
            {
                if (target == null || target == player || !target.IsAlive)
                    continue;

                if (target.GetTeam() == CTeam.SCPs || target.GetTeam() == CTeam.Fifthists)
                    continue;
                    
                var hasGoggles = target.Items
                    .OfType<Scp1344>()
                    .Any(i => CItem.TryGet(i, out var ci) && ci is AntiMemeGoggle && i.IsWorn);
                if (hasGoggles)  continue;

                var distance = Vector3.Distance(player.Position, target.Position);
                if (!(distance <= 2.75f)) continue;
                target.Hurt(player, 1f, DamageType.Unknown,null,  "<color=#ff00fa>第五的</color>な力による影響");
                player.ShowHitMarker();
            }
            
            Player.List.Where(p => p.Role is Scp079Role role && Vector3.Distance(role.CameraPosition, player.Position) < 8.75f).ToList().ForEach(p =>
            {
                if (p.Role is Scp079Role role && p.GetCustomRole() is CRoleTypeId.AraOrun)
                {
                    role.LoseSignal(5f);
                }
            });

            yield return Timing.WaitForSeconds(2f);
        }
    }
}
