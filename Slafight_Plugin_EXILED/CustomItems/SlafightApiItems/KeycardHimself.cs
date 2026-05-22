using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardHimself : CItemKeycard
{
    public override string DisplayName => "なぞのキーカード";
    public override string Description => "死体が持っていたと思われるなぞのキーカード";
    protected override string UniqueKey => "KeycardHimself";
    protected override string KeycardLabel => "CONTAINMENT ENGINEER";
    protected override Color32? KeycardLabelColor => new Color32(238, 246, 255, 255);

    protected override string KeycardName => "Containment Engineer";
    protected override Color32? TintColor => new Color32(255, 0, 0, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(255, 255, 255, 255);

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
