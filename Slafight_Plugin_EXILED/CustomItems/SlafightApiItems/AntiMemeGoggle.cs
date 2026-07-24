using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class AntiMemeGoggle : CItem
{
    public override string DisplayName => "反ミームゴーグル";
    public override string Description =>
        "反ミーム的影響を遮断する財団の最新兵器。\n<color=red>SCP-3005への攻撃が通じるようになる</color>";

    protected override string UniqueKey => "AntiMemeGoggle";
    protected override ItemType BaseItem => ItemType.SCP1344;
    protected override bool IsGoggles => true;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.green;

    public override void RegisterEvents()
    {
        Player.Hurting += OnHurting;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Player.Hurting -= OnHurting;
        base.UnregisterEvents();
    }

    /// <summary>
    /// 反ミームゴーグルを所持している攻撃者が第五教会員を撃った時に微増ダメージ。
    /// CItem.OnHurtingOthers は「現在手に持っている item」起点なので使えず、
    /// 「インベントリ内に当 CItem を所持しているか」を見たいので独自購読。
    /// </summary>
    private void OnHurting(HurtingEventArgs ev)
    {
        if (ev.Attacker == null || !HasIn(ev.Attacker)) return;
        if (ev.Player.IsFifthist())
            ev.Amount *= 1.1f;
    }
}
