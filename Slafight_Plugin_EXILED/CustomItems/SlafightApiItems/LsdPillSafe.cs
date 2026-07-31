using CustomPlayerEffects;
using CustomRendering;
using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class LsdPillSafe : CItemUsable
{
    public override string DisplayName => "LS-D5";
    public override string Description =>
        "その辺に落ちていた薬を改良して安全にハイになれるようにした薬。";

    protected override string UniqueKey => "LsdPillSafe";
    protected override ItemType BaseItem => ItemType.Adrenaline;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.magenta;
    protected override bool CancelDefaultUse => false;

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        ev.Player.EnableEffect(EffectType.FogControl);
        ev.Player.TryGetEffect(out FogControl fogControl);
        fogControl.SetFogType(FogType.BecomingFlamingo);
    }
}
