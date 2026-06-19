using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardHeadResearcherGeneric : CItemKeycard
{
    public override string DisplayName => "主席研究員キーカード";
    public override string Description => "サイト-02の高位な有数の科学者にしか配布されないキーカード。\n" +
                                          "様々なものにアクセスできる。";
    protected override string UniqueKey => "KeycardHeadResearcherGeneric";
    protected override string KeycardLabel => "HEAD RESEARCHER";
    protected override Color32? KeycardLabelColor => new Color32(238, 246, 255, 255);

    protected override string KeycardName => "Hrs. Site-02";
    protected override Color32? TintColor => new Color32(255, 0, 0, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(0, 0, 0, 255);

    protected override KeycardPermissions Permissions =>
        KeycardPermissions.ContainmentLevelOne |
        KeycardPermissions.ContainmentLevelTwo |
        KeycardPermissions.ContainmentLevelThree |
        KeycardPermissions.ArmoryLevelOne |
        KeycardPermissions.Checkpoints |
        KeycardPermissions.Intercom;

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new(0.45f, 0.65f, 1f);
}
