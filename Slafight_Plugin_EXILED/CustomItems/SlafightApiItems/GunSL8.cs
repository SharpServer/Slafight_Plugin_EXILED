using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.DamageHandlers;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Firearms.Attachments;
using MEC;
using PlayerRoles;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunSL8 : CItemWeapon
{
    public override string DisplayName => "SL-8G";
    public override string Description => "カオスが所有するスナイパーライフル。質はそこそこ";
    protected override string UniqueKey => "GunSL8";
    protected override ItemType BaseItem => ItemType.GunFRMG0;
    protected override float Damage => 40f;
    protected override byte MagazineSize => 5;
    protected override Vector3 Scale => new(1f, 1f, 1.75f);
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => CustomColor.ChaoticGreen.ToUnityColor();

    protected override void ApplyFirearmCustomization(Item item)
    {
        if (item is Firearm firearm)
        {
            firearm.MaxMagazineAmmo = MagazineSize;
            firearm.ClearAttachments();
            firearm.AddAttachment(AttachmentName.ScopeSight);
            firearm.AddAttachment(AttachmentName.SoundSuppressor);
        }
        base.ApplyFirearmCustomization(item);
    }

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        ev.DamageHandler.StartVelocity *= 1.025f;
        if (ev.DamageHandler.Base is StandardDamageHandler { Hitbox: HitboxType.Headshot })
        {
            ev.Amount += 15f;
        }
        base.OnHurtingOthers(ev);
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
    }
}
