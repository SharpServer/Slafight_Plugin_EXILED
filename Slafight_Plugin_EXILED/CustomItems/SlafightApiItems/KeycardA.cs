using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardA : CItem
{
    public override string DisplayName => "認証キー - 赤";
    public override string Description =>
        "???";
    protected override string UniqueKey => "KeycardA";
    protected override ItemType BaseItem => ItemType.SurfaceAccessPass;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.red;
    protected override string? PickupSchematicName => "Alienisolation_keycard";
}
