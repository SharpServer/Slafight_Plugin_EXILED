using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardHimself : CItemKeycard
{
    public override string DisplayName => "なぞのキーカード";
    public override string Description => "死体が持っていたと思われるなぞのキーカード\n" +
                                          "<color=yellow>独自の認証チップが埋め込まれている・・・？</color>";
    protected override string UniqueKey => "KeycardHimself";
    protected override string KeycardLabel => "HEAD RESEARCHER";
    protected override Color32? KeycardLabelColor => new Color32(238, 246, 255, 255);

    protected override string KeycardName => "Hrs. Redheart";
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

    protected override void OnUnlockingGenerator(UnlockingGeneratorEventArgs ev)
    {
        ev.IsAllowed = true;
        base.OnUnlockingGenerator(ev);
    }
}
