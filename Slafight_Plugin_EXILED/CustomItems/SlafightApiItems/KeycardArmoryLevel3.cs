using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardArmoryLevel3 : CItemKeycard
{
    public override string DisplayName => "Armory Level 3 Keycard";
    public override string Description => "最高位の武器アクセスを持つ戦術カード。";

    protected override string UniqueKey => "KeycardArmoryLevel3";
    protected override ItemType BaseItem => ItemType.KeycardCustomTaskForce;

    protected override string KeycardLabel => "ARMORY III";
    protected override Color32? KeycardLabelColor => new Color32(255, 255, 255, 255);

    protected override string KeycardName => "Armory III";
    protected override Color32? TintColor => new Color32(30, 51, 112, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(4, 9, 24, 255);
    protected override string SerialNumber => "A-3";
    protected override byte Rank => 1;

    protected override KeycardPermissions Permissions =>
        KeycardPermissions.ArmoryLevelOne |
        KeycardPermissions.ArmoryLevelTwo |
        KeycardPermissions.ArmoryLevelThree |
        KeycardPermissions.Checkpoints |
        KeycardPermissions.ExitGates;

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new(0.1f, 0.25f, 1f);
}
