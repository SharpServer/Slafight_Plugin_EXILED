using Exiled.API.Enums;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using Interactables.Interobjects.DoorUtils;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardA : CItem
{
    public override string DisplayName => "認証キー - 赤";
    public override string Description =>
        "???";
    protected override string UniqueKey => "KeycardA";
    protected override ItemType BaseItem => ItemType.SurfaceAccessPass;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.red;
    protected override string? PickupSchematicName => "Alienisolation_keycard";
    
    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Item.KeycardInteracting += OnKeycardInteractingDoor;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Item.KeycardInteracting -= OnKeycardInteractingDoor;
        base.UnregisterEvents();
    }

    protected override void CustomizeItem(Item item)
    {
        item.As<Keycard>()?.Permissions = KeycardPermissions.None;
        base.CustomizeItem(item);
    }

    private void OnKeycardInteractingDoor(KeycardInteractingEventArgs ev)
    {
        if (!Check(ev.Pickup)) return;
        ev.IsAllowed = false;
    }
}
