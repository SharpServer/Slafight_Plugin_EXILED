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

public class Bloodfiend : CRole
{
    protected override string RoleName { get; set; } = "Bloodfiend";
    protected override string Description { get; set; } = "<size=23>代謝および自然治癒能力を向上させる施術を受けたDクラス。\n" +
                                                          "副作用により強い吸血衝動に苛まれており、医務室で拘束されていたが、\n" +
                                                          "処分直前に狂暴化して脱走し複数の死傷者を出した。\n" +
                                                          "なお、異常な能力は確認されておらず、吸血衝動に支配された人間に過ぎない。</size>";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Bloodfiend;
    protected override CTeam Team { get; set; } = CTeam.ClassD;
    protected override string UniqueRoleKey { get; set; } = "Bloodfiend";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ClassD;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(Bloodyknife),
    ];
    protected override Vector3? SpawnPosition => Room.Get(RoomType.EzSmallrooms).WorldPosition(Vector3.zero);
    protected override string SpawnCustomInfo => "Bloodfiend";

    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        if (ev.Player?.Health > 35f) return;
        ev.Player?.EnableEffect<DamageBoost>(10, 20f);
        base.OnRoleHurting(ev);
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player is null) return;
        if (ev.Player.IsEffectActive<Bleeding>())
        {
            if (ev.IsDeathExpected())
            {
                ev.Attacker?.Heal(30f);
            }
            else
            {
                ev.Attacker?.Heal(5f);
            }
        }
        base.OnRoleHurtingOthers(ev);
    }
}
