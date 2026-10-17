using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomEffects;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.ClassD;

public class Vampire : CRole
{
    protected override string RoleName { get; set; } = "Vampire";
    protected override string Description { get; set; } = "<size=23>魔改造施術を受けた元クラス。\n" +
                                                          "強い吸血衝動に苛まれており、SCP-096収容室で拘束されていたが、\n" +
                                                          "処分直前に狂暴化して脱走し096と共に複数の死傷者を出した。\n" +
                                                          "異常な能力が多数確認されており、吸血衝動に支配された仲間を生み出せるとか。</size>";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Vampire;
    protected override CTeam Team { get; set; } = CTeam.ClassD;
    protected override string UniqueRoleKey { get; set; } = "Vampire";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ClassD;
    protected override float? SpawnMaxHealth => 200f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(Bloodyknife),
    ];
    protected override Vector3? SpawnPosition => Room.Get(RoomType.EzSmallrooms).WorldPosition(Vector3.up * 0.8f);
    protected override string SpawnCustomInfo => "Vampire";

    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        if (ev.Player?.Health > 45f) return;
        ev.Player?.EnableEffect<DamageBoost>(12, 20f);
        base.OnRoleHurting(ev);
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player is null) return;
        if (ev.Player.IsEffectActive<Bleeding>())
        {
            if (ev.IsDeathExpected())
            {
                ev.Attacker?.Heal(45f);
            }
            else
            {
                ev.Attacker?.Heal(10f);
            }
        }
        base.OnRoleHurtingOthers(ev);
    }
}