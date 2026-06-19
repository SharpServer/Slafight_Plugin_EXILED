using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class SerumD : CItemUsable
{
    public override string DisplayName => "Serum-D";
    public override string Description =>
        "SCP-1853の性質を参考に開発された強化用セラム。\n短時間、器用さを大幅に向上させる";

    protected override string UniqueKey => "SerumD";
    protected override ItemType BaseItem => ItemType.Adrenaline;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.cyan;
    protected override bool CancelDefaultUse => false;

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        ev.Player.EnableEffect(EffectType.Scp1853, 4, 15);
    }
}
