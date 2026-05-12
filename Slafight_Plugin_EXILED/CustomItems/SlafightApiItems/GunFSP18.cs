using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunFSP18 : CItemWeapon
{
    public override string DisplayName => "FSP-18";
    public override string Description => string.Empty;

    protected override string UniqueKey => "GunFSP18";
    protected override ItemType BaseItem => ItemType.GunFSP9;

    protected override float Damage => 30f;
    protected override byte MagazineSize => 42;
    protected override Vector3 Scale => new(1f, 1f, 1.15f);

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.white;
}
