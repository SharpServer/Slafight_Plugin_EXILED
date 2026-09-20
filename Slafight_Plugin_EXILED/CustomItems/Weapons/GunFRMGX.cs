using UnityEngine;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomItems.Weapons;

public class GunFRMGX : CustomFirearm
{
    public override string Name => "FRMG-X";
    public override string Description => "財団の無理を押し通して購入された最新式のFRMG-0。全体的に強化されている。";
    public override ItemType BaseType => ItemType.GunFRMG0;
    public override Rarity Rarity => Rarity.Rare;
    protected override Vector3 PickupScale => new(1.08f, 1f, 1.35f);

    protected override float? Damage => 28f;
    protected override int? MagazineCapacity => 80;
}
