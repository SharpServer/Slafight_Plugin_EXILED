using CustomPlayerEffects;
using CustomRendering;
using Exiled.API.Enums;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Core.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.Utils;

/// <summary>アドレナリンの効果に加えて、視界をフラミンゴ色に染める薬です。</summary>
public sealed class LsdPillSafe : CustomUsableItem
{
    public override string Name => "LS-D5";
    public override string Description => "その辺に落ちていた薬を改良して安全にハイになれるようにした薬。";
    public override ItemType BaseType => ItemType.Adrenaline;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.magenta;

    protected override void OnUseCompleted(PlayerUsedItemEventArgs ev)
    {
        if (Player.Get(ev.Player?.ReferenceHub) is { } player)
        {
            player.EnableEffect(EffectType.FogControl);
            if (player.TryGetEffect(out FogControl fogControl))
                fogControl.SetFogType(FogType.BecomingFlamingo);
        }

        base.OnUseCompleted(ev);
    }
}
