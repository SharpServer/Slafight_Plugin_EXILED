using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardSurveillance : CItemKeycard
{
    public override string DisplayName => "監視部門キーカード";
    public override string Description => "監視担当職員に支給される、Research Coordinator互換のキーカード。";

    protected override string UniqueKey => "KeycardSurveillance";

    protected override string KeycardLabel => "SURVEILLANCE";
    protected override Color32? KeycardLabelColor => new Color32(238, 246, 255, 255);

    protected override string KeycardName => "Surveillance";
    protected override Color32? TintColor => new Color32(55, 78, 116, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(12, 22, 40, 255);

    protected override KeycardPermissions Permissions =>
        KeycardPermissions.ContainmentLevelOne |
        KeycardPermissions.ContainmentLevelTwo |
        KeycardPermissions.Checkpoints |
        KeycardPermissions.Intercom;

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new(0.45f, 0.65f, 1f);
}
