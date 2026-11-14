using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.Keycards;

public class FifthistKeycard : CustomKeycard
{
    public override string Name => "第五教会 第五デバイス";
    public override string Description => "第五教会が目的を達成するために作られたアクセスデバイス。\n扉やゲートを第五することで施設に侵入する。";
    protected override string Label => "第五教会 第五デバイス";
    protected override Color32? LabelColor => new(255, 0, 250, 255);
    protected override string HolderName => "Rsc. Fifth";
    protected override Color32? TintColor => new(255, 0, 250, 255);
    protected override Color32? PermissionsColor => new(255, 255, 255, 255);
    protected override CustomKeycardType KeycardType => CustomKeycardType.TaskForce;
    protected override byte? Rank => 1;
    protected override string SerialNumber => "555555555555";
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.magenta;
    protected override KeycardPermissions Permissions =>
        KeycardPermissions.ContainmentLevelOne |
        KeycardPermissions.ContainmentLevelTwo |
        KeycardPermissions.ContainmentLevelThree |
        KeycardPermissions.ArmoryLevelOne |
        KeycardPermissions.ArmoryLevelTwo |
        KeycardPermissions.ArmoryLevelThree |
        KeycardPermissions.Checkpoints |
        KeycardPermissions.ExitGates |
        KeycardPermissions.Intercom;
}
