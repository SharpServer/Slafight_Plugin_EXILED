using Exiled.API.Enums;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardChaosIntruder : CItemKeycard
{
    public override string DisplayName => "Chaos Intruder Device";
    public override string Description => "カオスの権限1,1,1の侵入部隊用デバイス。";

    protected override string UniqueKey => "KeycardChaosIntruder";
    protected override ItemType BaseItem => ItemType.KeycardChaosInsurgency;

    protected override string KeycardLabel => "INTRUDER";
    protected override Color32? KeycardLabelColor => new Color32(218, 255, 218, 255);

    protected override string KeycardName => "Chaos Intruder";
    protected override Color32? TintColor => new Color32(20, 90, 38, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(4, 32, 12, 255);

    protected override KeycardPermissions Permissions =>
        KeycardPermissions.ContainmentLevelOne |
        KeycardPermissions.ArmoryLevelOne |
        KeycardPermissions.Intercom |
        KeycardPermissions.Checkpoints;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => CustomColor.ChaoticGreen.ToUnityColor();

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        ev.Item.Cast<Keycard>()?.Permissions = Permissions;
        base.OnAcquired(ev, displayMessage);
    }
}
