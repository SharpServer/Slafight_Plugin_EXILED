using Exiled.API.Enums;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardChaosIntruder : CItem
{
    public override string DisplayName => "Chaos Intruder Device";
    public override string Description => "カオスの権限1,1,1の侵入部隊用デバイス。";
    protected override string UniqueKey => "KeycardChaosIntruder";
    protected override ItemType BaseItem => ItemType.KeycardChaosInsurgency;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => CustomColor.ChaoticGreen.ToUnityColor();

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        ev.Item.Cast<Keycard>()?.Permissions =
            KeycardPermissions.ContainmentLevelOne |
            KeycardPermissions.ArmoryLevelOne |
            KeycardPermissions.Intercom |
            KeycardPermissions.Checkpoints;
        base.OnAcquired(ev, displayMessage);
    }
}
