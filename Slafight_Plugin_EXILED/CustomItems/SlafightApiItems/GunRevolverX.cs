using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunRevolverX : CItemWeapon
{
    public override string DisplayName => "Revolver-X";
    public override string Description => string.Empty;

    protected override string UniqueKey => "GunRevolverX";
    protected override ItemType BaseItem => ItemType.GunRevolver;
    protected override float Damage => 15f;
    protected override byte MagazineSize => 8;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.white;
    protected override string? PickupSchematicName => "";
}
