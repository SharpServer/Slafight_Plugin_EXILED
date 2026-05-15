using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardSiteNavigator : CItemKeycard
{
    public override string DisplayName => "S-NAV担当キーカード";
    public override string Description => "S-NAV担当職員に支給される、研究員相当のキーカード。";

    protected override string UniqueKey => "KeycardSiteNavigator";

    protected override string KeycardLabel => "SITE NAVIGATOR";
    protected override Color32? KeycardLabelColor => new Color32(20, 36, 48, 255);

    protected override string KeycardName => "Site Navigator";
    protected override Color32? TintColor => new Color32(96, 190, 176, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(9, 42, 46, 255);

    protected override KeycardPermissions Permissions =>
        KeycardPermissions.ContainmentLevelOne |
        KeycardPermissions.Checkpoints |
        KeycardPermissions.Intercom;

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new(0.35f, 0.95f, 0.85f);
}
