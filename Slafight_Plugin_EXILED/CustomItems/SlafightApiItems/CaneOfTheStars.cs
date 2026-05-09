using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using PlayerHandlers = Exiled.Events.Handlers.Player;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class CaneOfTheStars : CItem
{
    public override string DisplayName => "Cane of the Stars";
    public override string Description =>
        "第五教会の案内人が持つ杖。\n殴った対象の脳内に第五主義思想を直接流し込み、\n強制的に第五主義者に改宗させる能力を持つ";

    protected override string UniqueKey => "CaneOfTheStars";
    protected override ItemType BaseItem => ItemType.Jailbird;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.magenta;

    protected override void CustomizeItem(Item item)
    {
        base.CustomizeItem(item);
        if (item is Jailbird jailbird)
        {
            jailbird.ChargeDamage = 555f;
            jailbird.MeleeDamage  = 55f;
        }
    }

    public override void RegisterEvents()
    {
        PlayerHandlers.Dying += OnDying;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        PlayerHandlers.Dying -= OnDying;
        base.UnregisterEvents();
    }

    private void OnDying(DyingEventArgs ev)
    {
        if (!CheckHeld(ev.Attacker)) return;
        ev.IsAllowed = false;
        ev.Player?.SetRole(CRoleTypeId.FifthistConvert, RoleSpawnFlags.AssignInventory);
    }
}
