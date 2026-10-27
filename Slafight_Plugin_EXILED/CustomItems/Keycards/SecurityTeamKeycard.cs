using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.Keycards;

public class SecurityTeamKeycard : CustomKeycard
{
    public override string Name => "保安部隊キーカード";
    public override string Description => "緊急時に派遣されてくる保安部隊に渡されるキーカード。";
    protected override string ItemName => "保安部隊キーカード";
    protected override string Label => "SECURITY TEAM";
    protected override Color32? LabelColor => Color.white;
    protected override Color32? TintColor => new Color32(68, 68, 68, 255);
    protected override Color32? PermissionsColor => new Color32(0, 0, 0, 255);

    protected override KeycardPermissions Permissions => KeycardPermissions.ContainmentLevelOne |
                                                         KeycardPermissions.ArmoryLevelOne |
                                                         KeycardPermissions.ArmoryLevelTwo |
                                                         KeycardPermissions.Checkpoints |
                                                         KeycardPermissions.Intercom;
    protected override CustomKeycardType KeycardType => CustomKeycardType.MetalCase;
}