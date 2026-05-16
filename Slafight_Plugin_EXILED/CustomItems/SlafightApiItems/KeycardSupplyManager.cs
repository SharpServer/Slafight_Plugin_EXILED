using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class KeycardSupplyManager : CItemKeycard
{
    public override string DisplayName => "供給管理官キーカード";
    public override string Description => "サイト-02の供給管理課の職員が持つキーカード。";

    protected override string UniqueKey => "KeycardSupplyManager";
    protected override ItemType BaseItem => ItemType.KeycardCustomManagement;

    protected override string KeycardLabel => "SUPPLY MANAGER";
    protected override Color32? KeycardLabelColor => new Color32(255, 255, 255, 255);

    protected override string KeycardName => "Supply Manager";
    protected override Color32? TintColor => new Color32(54, 120, 140, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(0, 107, 137, 255);
    protected override string SerialNumber => "SP-07";

    protected override KeycardPermissions Permissions =>
        KeycardPermissions.Checkpoints |
        KeycardPermissions.ExitGates |
        KeycardPermissions.Intercom;

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new Color32(0, 107, 137, 255);
}
