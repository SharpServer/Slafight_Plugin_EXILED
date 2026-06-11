using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class SerumC : CItemUsable
{
    public override string DisplayName => "Serum-C";
    public override string Description =>
        "Serum-Dを元に開発された上級のセラム。\n短時間、器用さと早さを大幅に向上させる";
    protected override string UniqueKey => "SerumC";
    protected override ItemType BaseItem => ItemType.Adrenaline;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.green;

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        ev.Player.EnableEffect(EffectType.Scp1853, 4, 30);
        ev.Player.EnableEffect(EffectType.MovementBoost, 15, 30);
    }
}
