using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardArmoryLevel2 : CItemKeycard
{
    public override string DisplayName => "Armory Level 2 Keycard";
    public override string Description => "重武装ロッカーとEPSを扱える武器アクセスカード。";

    protected override string UniqueKey => "KeycardArmoryLevel2";
    protected override ItemType BaseItem => ItemType.KeycardCustomTaskForce;

    protected override string KeycardLabel => "ARMORY II";
    protected override Color32? KeycardLabelColor => new Color32(245, 245, 245, 255);

    protected override string KeycardName => "Armory II";
    protected override Color32? TintColor => new Color32(44, 79, 132, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(7, 16, 31, 255);
    protected override string SerialNumber => "A-2";
    protected override byte Rank => 2;

    protected override KeycardPermissions Permissions =>
        KeycardPermissions.ArmoryLevelOne |
        KeycardPermissions.ArmoryLevelTwo |
        KeycardPermissions.Intercom |
        KeycardPermissions.Checkpoints;

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new(0.25f, 0.45f, 0.95f);
}
