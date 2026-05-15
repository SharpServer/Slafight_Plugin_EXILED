using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunScp7381 : CItemWeapon
{
    public override string DisplayName => "SCP-7381";
    public override string Description => "W.I.P";

    protected override string UniqueKey => "GunScp7381";
    protected override ItemType BaseItem => ItemType.ParticleDisruptor;
    protected override Vector3 Scale => new(3f, 1f, 1.15f);
    protected override float Damage => 35f;
    protected override ushort MaxMagazineAmmo => 999;
    protected override ushort InitialMagazineAmmo => 999;
    protected override byte AmmoDrain => 1;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.cyan;
}
