using Exiled.API.Features.Items;
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
            firearm.AddAttachment(AttachmentName.ScopeSight);
        }
        base.ApplyFirearmCustomization(item);
    }
}
