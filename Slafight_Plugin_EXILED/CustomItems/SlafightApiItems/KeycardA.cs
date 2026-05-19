using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardA : CItem
{
    public override string DisplayName => "Unknown Keycard";
    public override string Description =>
        "bruh";
    protected override string UniqueKey => "KeycardA";
    protected override ItemType BaseItem => ItemType.SurfaceAccessPass;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.red;
    protected override string? PickupSchematicName => "";
}
