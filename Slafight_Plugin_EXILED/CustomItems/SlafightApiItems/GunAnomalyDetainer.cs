using Exiled.API.Enums;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Firearms.Attachments;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunAnomalyDetainer : CItemWeapon
{
    private const byte DetainerMagazineSize = 5;
    private const byte SlownessIntensity = 35;
    private const float SlownessDuration = 7f;

    public override string DisplayName => "XE-11 ANOMALY DETAINER";
    public override string Description => "被弾したSCPに強力な鈍足を付与する試作対異常兵装";
    protected override string UniqueKey => "GunAnomalyDetainer";
    protected override ItemType BaseItem => ItemType.GunE11SR;
    protected override float Damage => 20f;
    protected override byte MagazineSize => DetainerMagazineSize;
    protected override Vector3 Scale => new(1f, 1f, 1.1f);
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new(0.35f, 0.8f, 1f);

    protected override void ApplyFirearmCustomization(Item item)
    {
        if (item is Firearm firearm)
        {
            firearm.MaxMagazineAmmo = MagazineSize;
            firearm.ClearAttachments();
            firearm.AddAttachment(AttachmentName.RifleBody);
            firearm.AddAttachment(AttachmentName.NightVisionSight);
            firearm.AddAttachment(AttachmentName.Foregrip);
            firearm.AddAttachment(AttachmentName.StandardMagJHP);
            firearm.AddAttachment(AttachmentName.FlashHider);
        }

        base.ApplyFirearmCustomization(item);
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        ev.Item.Cast<Firearm>()?.TryReload();
        base.OnAcquired(ev, displayMessage);
    }

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (!ev.IsAllowed || ev.Player is null)
            return;

        if (ev.Player.IsScp || ev.Player.GetTeam() == CTeam.SCPs)
            ev.Player.EnableEffect(EffectType.Slowness, SlownessIntensity, SlownessDuration);

        base.OnHurtingOthers(ev);
    }

    protected override void OnReloading(ReloadingWeaponEventArgs ev)
    {
        ev.IsAllowed = false;
        base.OnReloading(ev);
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Item.ChangingAttachments += OnAttachmentChanging;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Item.ChangingAttachments -= OnAttachmentChanging;
        base.UnregisterEvents();
    }

    private void OnAttachmentChanging(ChangingAttachmentsEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        ev.IsAllowed = false;
    }
}
