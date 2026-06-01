using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class ClassXMemoryForcePil : CItem
{
    public override string DisplayName => "クラスX-記憶補強剤";
    public override string Description =>
        "反ミーム性の現象等に対抗するために使用される薬。\n反ミームの影響を軽減する。\n効果時間：1分";

    protected override string UniqueKey => "ClassXMemoryForcePil";
    protected override ItemType BaseItem => ItemType.SCP500;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.yellow;

    protected override void OnUsing(UsingItemEventArgs ev)
    {
        if (ev.Player == null) return;
        if (!ev.Player.HasFlag(SpecificFlagType.AntiMemeEffectDisabled)) return;
        ev.IsAllowed = false;
        ev.Player.ShowHint("既に耐性を得ている為、使用できません。");
    }

    protected override void OnUsed(UsedItemEventArgs ev)
    {
        if (ev.Player == null) return;
        ev.Player.EnableEffect(EffectType.Invigorated, 60);
        ev.Player.TryAddFlag(SpecificFlagType.AntiMemeEffectDisabled);
        ev.Player.WaitAndRemove(SpecificFlagType.AntiMemeEffectDisabled, 60f);
    }
}
