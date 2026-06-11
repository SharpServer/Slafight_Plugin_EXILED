using System.Linq;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GoCRecruitPaper : CItemUsable
{
    public override string DisplayName => "UNGOC一般工作員セット";
    public override string Description => "一般工作員一人分のアイテムが入っている。\nこれを使えば工作員を一人だけ増やせる。";

    protected override string UniqueKey => "GoCRecruitPaper";
    protected override ItemType BaseItem => ItemType.Medkit;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => new(0f, 0f, 200f / 255f);

    protected override bool DestroyItemWhenUsesDepleted => false;

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        if (ev.Player == null) return;

        var player = ev.Player;
        player.SetRole(CRoleTypeId.GoCOperative, RoleSpawnFlags.AssignInventory);

        Timing.CallDelayed(0.25f, () =>
        {
            if (player == null) return;
            foreach (var item in player.Items.ToList())
            {
                if (item == null) continue;
                if (Check(item))
                    item.Destroy();
            }
        });
    }
}
