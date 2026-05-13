using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunProject90 : CItemWeapon
{
    public override string DisplayName => "Project-90";
    public override string Description => "昔ながらの、安定した撃ちどけ";

    protected override string UniqueKey => "GunProject90";
    protected override ItemType BaseItem => ItemType.GunCrossvec;

    protected override float Damage => 36f;
    protected override byte MagazineSize => 42;
    protected override Vector3 Scale => new(1f, 1f, 1.15f);

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.cyan;
}
