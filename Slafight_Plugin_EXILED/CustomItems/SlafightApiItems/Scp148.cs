using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp1509;
using Exiled.Events.Handlers;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class Scp148 : CItem
{
    public override string DisplayName => "SCP-148";
    public override string Description =>
        "プロメテウス研究所製の精神遮断合金剣。\nSCP特効ダメージ+75%、テレパシー完全防御。\n質量増加で効果増幅放出のリスク注意。";

    protected override string UniqueKey => "Scp148";
    protected override ItemType BaseItem => ItemType.SCP1509;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.white;

    public override void RegisterEvents()
    {
        Scp1509.Resurrecting += OnResurrecting;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Scp1509.Resurrecting -= OnResurrecting;
        base.UnregisterEvents();
    }

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player == null) return;
        if (!ev.Player.IsHuman)
            ev.Amount *= 1.75f;
    }

    private void OnResurrecting(ResurrectingEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        ev.IsAllowed = false;
    }
}
