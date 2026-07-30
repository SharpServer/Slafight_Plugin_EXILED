using CameraShaking;
using Exiled.API.Features.Items;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunFSP18 : CItemWeapon
{
    public override string DisplayName => "FSP-18";
    public override string Description => "財団が開発したFSP-9の改良版。\n" +
                                          "反動の制御に優れており主に上級警備隊に配備されている。";
    protected override string UniqueKey => "GunFSP18";
    protected override ItemType BaseItem => ItemType.GunFSP9;
    protected override float Damage => 27f;
    protected override byte MagazineSize => 42;
    protected override Vector3 Scale => new(1f, 1f, 1.62f);
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.white;
    
    protected override void CustomizeItem(Item item)
    {
        if (item is Firearm firearm)
        {
            firearm.Recoil = new RecoilSettings(0.08f, 0.12f, 0.75f, 0.11f, 0.15f);
        }
        base.CustomizeItem(item);
    }
}
