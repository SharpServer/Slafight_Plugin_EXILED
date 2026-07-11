using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class ClassBMemoryRemovePill : CItemUsable
{
    public override string DisplayName => "クラスB-記憶処理剤";
    public override string Description =>
        "ここしばらくの出来事や大きな影響を忘却することが出来る。";

    protected override string UniqueKey => "ClassBMemoryRemovePill";
    protected override ItemType BaseItem => ItemType.Adrenaline;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.blue;

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        ev.Player.EnableEffect<Concussed>(25, 15f);
        foreach (var scp096 in Player.List.Where(p => p.Role is Scp096Role role && role.Targets.Contains(ev.Player)).ToList())
        {
            if (scp096?.Role is Scp096Role role)
            {
                role.RemoveTarget(ev.Player);
            }
        }

        if (ev.Player.GetCustomRole() is CRoleTypeId.FifthistConvert)
        {
            RoleTypeId roleTypeId = ev.Player.PreviousRole;
            if (ev.Player.PreviousRole.IsDead()) roleTypeId = RoleTypeId.ClassD;
            ev.Player.SetRole(roleTypeId, RoleSpawnFlags.None);
        }
        base.OnUsedEffect(ev);
    }
}
