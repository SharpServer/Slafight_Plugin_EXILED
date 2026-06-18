using CustomPlayerEffects;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp1509;
using ProjectMER.Features.Extensions;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class Bloodyknife : CItem
{
    public override string DisplayName => "Bloodyknife";
    public override string Description =>
        "Class-D Bloodfiendが脱走時に殺害した警備員から失敬したナイフ。\n" +
        "黒かった柄の部分は赤く染まり、刃には鮮血がこびり付いている。 ";

    protected override string UniqueKey => "Bloodyknife";
    protected override ItemType BaseItem => ItemType.SCP1509;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => ServerColors.Crimson.GetColorFromString();

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp1509.Resurrecting += OnResurrecting;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp1509.Resurrecting -= OnResurrecting;
        base.UnregisterEvents();
    }

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player == null) return;
        ev.Amount = 20f;
        ev.Player.EnableEffect<Bleeding>(15);
    }

    private void OnResurrecting(ResurrectingEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        ev.IsAllowed = false;
    }
}
