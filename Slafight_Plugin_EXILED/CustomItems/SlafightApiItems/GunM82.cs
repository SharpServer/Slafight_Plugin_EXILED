using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.DamageHandlers;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Firearms.Attachments;
using PlayerRoles;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunM82 : CItemWeapon
{
    public override string DisplayName => "MTF-E11-AR";
    public override string Description => "E11が所持する対物ライフル";
    protected override string UniqueKey => "GunM82";
    protected override ItemType BaseItem => ItemType.GunE11SR;
    protected override float Damage => 80f;
    protected override byte MagazineSize => 30;
    protected override Vector3 Scale => new(1f, 1f, 2.25f);
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

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        ev.DamageHandler.StartVelocity *= 4f;
        if (ev.DamageHandler.Base is StandardDamageHandler { Hitbox: HitboxType.Headshot })
        {
            ev.Amount += 30f;
        }

        if (ev.Player.IsVanillaOrCustom(RoleTypeId.Scp173, CRoleTypeId.Scp173))
        {
            ev.Amount *= 2f;
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
        if (!Check(ev.Firearm) || ev.AudioIndex is not (0 or 1 or 2)) return;
        ev.IsAllowed = false;
        Map.ExplodeEffect(ev.SendingPosition, ProjectileType.FragGrenade);
    }
}
