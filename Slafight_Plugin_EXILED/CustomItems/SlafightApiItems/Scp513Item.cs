#nullable enable
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Entities;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class Scp513Item : CItem
{
    public override string DisplayName => "SCP-513";
    public override string Description => "???";
    protected override string UniqueKey => "Scp513Item";
    protected override ItemType BaseItem => ItemType.Coin;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.gray;
    protected override string PickupSchematicName => "SCP513ItemModel";

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.FlippingCoin += OnFlipping;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.FlippingCoin -= OnFlipping;
        base.UnregisterEvents();
    }

    private void OnFlipping(FlippingCoinEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        Scp513.AddTarget(ev.Player);
        ev.Player.ShowRueiPlus("<size=25>何か視線を感じる気がする...</size>");
        var room = Room.Random(ZoneType.HeavyContainment);
        Spawn(room.WorldPosition(Vector3.up * 0.25f));
        ev.Item?.Destroy();
    }
}
