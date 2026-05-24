using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunRevolverX : CItemWeapon
{
    public override string DisplayName => "Revolver-X";
    public override string Description =>
        "W.I.P";
    protected override string UniqueKey => "GunRevolverX";
    protected override ItemType BaseItem => ItemType.GunRevolver;
    protected override float Damage => 50f;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => ColorExtensions.ParseHtmlToColor("#5fd647");
    protected override string? PickupSchematicName => "Alienisolation_Revolver";
}
