using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomEffects;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class PapsiCola : CItemUsable
{
    public override string DisplayName => "Papsi Cola";
    public override string Description =>
        "「パプシの力で生き生きと」";

    protected override string UniqueKey => "PapsiCola";
    protected override ItemType BaseItem => ItemType.AntiSCP207;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.cyan;
    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        ev.Player.DisableEffect<AntiScp207>();
        ev.Player.AddAhp(25, decay: 3.5f);
        ev.Player.EnableEffect<DamageBoost>(25, 10f);
        ev.Player.EnableEffect<NaturalHeal>(5, 1.5f);
    }
}
