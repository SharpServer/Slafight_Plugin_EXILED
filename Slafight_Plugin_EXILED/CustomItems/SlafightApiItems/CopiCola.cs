using CustomPlayerEffects;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomEffects;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class CopiCola : CItemUsable
{
    public override string DisplayName => "Copi-Cola";
    public override string Description =>
        "「スッカリ冴える コピ・コーラ」";

    protected override string UniqueKey => "CopiCola";
    protected override ItemType BaseItem => ItemType.SCP207;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.red;
    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        ev.Player.DisableEffect<Scp207>();
        ev.Player.AddAhp(25, decay: 3.5f);
        ev.Player.EnableEffect<DamageBoost>(25, 10f);
        ev.Player.EnableEffect<NaturalHeal>(5, 1.5f);
    }
}
