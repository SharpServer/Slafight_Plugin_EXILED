using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardSiteDirector : CItemKeycard
{
    public override string DisplayName => "Site Director Keycard";
    public override string Description => "施設長用の管理カード。Facility Manager相当の権限を持つ。";

    protected override string UniqueKey => "KeycardSiteDirector";
    protected override ItemType BaseItem => ItemType.KeycardCustomManagement;

    protected override string KeycardLabel => "SITE DIRECTOR";
    protected override Color32? KeycardLabelColor => new Color32(255, 255, 255, 255);

    protected override string KeycardName => "Site Director";
    protected override Color32? TintColor => new Color32(156, 41, 62, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(35, 8, 14, 255);
    protected override string SerialNumber => "SD-02";

    protected override KeycardPermissions Permissions =>
        KeycardPermissions.ContainmentLevelOne |
        KeycardPermissions.ContainmentLevelTwo |
        KeycardPermissions.ContainmentLevelThree |
        KeycardPermissions.ArmoryLevelOne |
        KeycardPermissions.ArmoryLevelTwo |
        KeycardPermissions.ArmoryLevelThree |
        KeycardPermissions.Checkpoints |
        KeycardPermissions.ExitGates |
        KeycardPermissions.Intercom |
        KeycardPermissions.AlphaWarhead;

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new(1f, 0.25f, 0.35f);
}
