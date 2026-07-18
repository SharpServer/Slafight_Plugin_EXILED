using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class Spear : CItem
{
    public override string DisplayName => "槍";
    public override string Description => "W.I.P";
    protected override string UniqueKey => "Spear";
    protected override ItemType BaseItem => ItemType.Jailbird;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.red;

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        ev.Amount = 50f;
        base.OnHurtingOthers(ev);
    }
}