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

public class GunDisarmerRifle : CItemWeapon
{
    public override string DisplayName => "Disarmer Rifle";
    public override string Description => "当たった対象を拘束出来るスナイパーライフル";
    protected override string UniqueKey => "GunDisarmerRifle";
    protected override ItemType BaseItem => ItemType.GunE11SR;
    protected override float Damage => 1f;
    protected override byte MagazineSize => 1;
    protected override Vector3 Scale => new(1f, 1f, 1.045f);
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.grey;

    protected override void ApplyFirearmCustomization(Item item)
    {
        if (item is Firearm firearm)
        {
            firearm.MaxMagazineAmmo = MagazineSize;
            firearm.ClearAttachments();
            firearm.AddAttachment(AttachmentName.ScopeSight);
        }
        base.ApplyFirearmCustomization(item);
    }

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Attacker is null) return;
        ev.Player?.Handcuff(ev.Attacker);
        base.OnHurtingOthers(ev);
    }

    protected override void OnShot(ShotEventArgs ev)
    {
        Timing.CallDelayed(20f, () => ev.Firearm?.TryReload());
        base.OnShot(ev);
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
