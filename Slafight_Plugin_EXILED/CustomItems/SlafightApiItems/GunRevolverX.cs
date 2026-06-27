using CameraShaking;
using Exiled.API.Features.Items;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunRevolverX : CItemWeapon
{
    public override string DisplayName => "Revolver-X";
    public override string Description =>
        "強化されたリボルバー。ある博士の特注品らしい";
    protected override string UniqueKey => "GunRevolverX";
    protected override ItemType BaseItem => ItemType.GunRevolver;
    protected override float Damage => 50f;
    protected override ushort InitialMagazineAmmo => 6;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => ColorExtensions.ParseHtmlToColor("#5fd647");
    protected override string? PickupSchematicName => "Alienisolation_Revolver";
    protected override void CustomizeItem(Item item)
    {
        if (item is Firearm firearm)
        {
            firearm.Recoil = new RecoilSettings(0.01f, 1856f, 2000f, 1507f, 296.5f);
        }
        base.CustomizeItem(item);
    }
}
