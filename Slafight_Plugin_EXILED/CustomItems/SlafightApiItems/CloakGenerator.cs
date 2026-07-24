using CustomPlayerEffects;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class CloakGenerator : CItem
{
    public override string DisplayName => "外套ジェネレータ";
    public override string Description => "ホワイトスーツが持つ特殊な透明化装置。\n使用することで透明・非透明を切り替えられる";

    protected override string UniqueKey => "CloakGenerator";
    protected override ItemType BaseItem => ItemType.SCP268;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.white;

    public override void RegisterEvents()
    {
        Player.UsingItemCompleted += OnUsingCompleted;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Player.UsingItemCompleted -= OnUsingCompleted;
        base.UnregisterEvents();
    }

    private void OnUsingCompleted(UsingItemCompletedEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        ev.IsAllowed = false;

        if (ev.Player.IsEffectActive<Invisible>())
            ev.Player.DisableEffect<Invisible>();
        else
            ev.Player.EnableEffect<Invisible>();
    }
}
