using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Firearms.Attachments;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunM82 : CItemWeapon
{
    public override string DisplayName => "M82";
    public override string Description => "W.I.P";
    protected override string UniqueKey => "GunM82";
    protected override ItemType BaseItem => ItemType.GunE11SR;
    protected override float Damage => 80f;
    protected override byte MagazineSize => 30;
    protected override Vector3 Scale => new(1f, 1f, 1.15f);
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.cyan;

    protected override void ApplyFirearmCustomization(Item item)
    {
        if (item is Firearm firearm)
        {
            firearm.AmmoDrain = 30;
            firearm.ClearAttachments();
            firearm.AddAttachment(AttachmentName.ScopeSight);
            firearm.AddAttachment(AttachmentName.LowcapMagAP);
            firearm.AddAttachment(AttachmentName.RifleBody);
            firearm.AddAttachment(AttachmentName.RecoilReducingStock);
            firearm.AddAttachment(AttachmentName.SoundSuppressor);
        }
        base.ApplyFirearmCustomization(item);
    }

    protected override void OnShot(ShotEventArgs ev)
    {
        ev.Firearm.MagazineAmmo -= 29;
        base.OnShot(ev);
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Item.ChangingAttachments += OnAttachmentChanging;
        Exiled.Events.Handlers.Player.SendingGunSound += OnSound;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Item.ChangingAttachments -= OnAttachmentChanging;
        Exiled.Events.Handlers.Player.SendingGunSound -= OnSound;
        base.UnregisterEvents();
    }

    private void OnAttachmentChanging(ChangingAttachmentsEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        ev.IsAllowed = false;
    }

    private void OnSound(SendingGunSoundEventArgs ev)
    {
        if (!Check(ev.Firearm)) return;
        ev.Pitch = 999;
    }
}
