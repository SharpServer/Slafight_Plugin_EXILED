using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardArmoryLevel1 : CItemKeycard
{
    public override string DisplayName => "Armory Level 1 Keycard";
    public override string Description => "軽武装ロッカーを開けられる武器アクセスカード。";

    protected override string UniqueKey => "KeycardArmoryLevel1";
    protected override ItemType BaseItem => ItemType.KeycardCustomTaskForce;

    protected override string KeycardLabel => "ARMORY I";
    protected override Color32? KeycardLabelColor => new Color32(245, 245, 245, 255);

    protected override string KeycardName => "Armory I";
    protected override Color32? TintColor => new Color32(58, 84, 104, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(10, 18, 26, 255);
    protected override string SerialNumber => "A-1";
    protected override byte Rank => 3;

    protected override KeycardPermissions Permissions =>
        KeycardPermissions.ArmoryLevelOne |
        KeycardPermissions.Checkpoints;

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new(0.35f, 0.55f, 0.8f);
}
