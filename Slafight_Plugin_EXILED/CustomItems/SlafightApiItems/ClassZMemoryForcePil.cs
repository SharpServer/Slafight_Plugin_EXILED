using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class ClassZMemoryForcePil : CItem
{
    public override string DisplayName => "クラスZ-記憶補強剤";
    public override string Description =>
        "反ミーム性の現象等に対抗するために使用される強力な薬。\n反ミームの影響を無効化する\n効果時間：---\n注意書き：<color=red>とても危険です！使用を控えるべきです！</color>";

    protected override string UniqueKey => "ClassZMemoryForcePil";
    protected override ItemType BaseItem => ItemType.SCP500;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => CustomColor.Purple.ToUnityColor();

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
        ev.Player.EnableEffect(EffectType.Scp207, 4);
        ev.Player.TryAddFlag(SpecificFlagType.AntiMemeEffectDisabled);
    }
}
