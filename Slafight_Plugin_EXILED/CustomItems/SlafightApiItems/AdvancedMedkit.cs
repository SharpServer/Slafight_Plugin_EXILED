using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class AdvancedMedkit : CItemUsable
{
    public override string DisplayName => "Advanced Medkit";
    public override string Description => "重篤な負傷も手当てできるよう拡張された、高度な医療キット。";
    protected override string UniqueKey => "AdvancedMedkit";
    protected override ItemType BaseItem => ItemType.Medkit;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.green;

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        ev.Player.Heal(float.MaxValue);
        ev.Player.AddAhp(15);
    }
}
