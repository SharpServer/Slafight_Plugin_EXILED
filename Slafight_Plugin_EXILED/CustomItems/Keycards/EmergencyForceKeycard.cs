using Exiled.API.Enums;
using Exiled.API.Features.Items.Keycards;
using Slafight_Plugin_EXILED.API.Core.Features;
using Item = LabApi.Features.Wrappers.Item;

namespace Slafight_Plugin_EXILED.CustomItems.Keycards;

public class EmergencyForceKeycard : CustomKeycard
{
    public override string Name => "緊急部隊キーカード";
    public override string Description => "緊急で発行される、ゲートの行き来が可能なキーカード。\n" +
                                          "主に緊急時に派遣されてくる警備隊の指揮官などに支給される。";
    public override ItemType BaseType => ItemType.SurfaceAccessPass;
    protected override KeycardPermissions Permissions => KeycardPermissions.ExitGates;

    protected override void Customize(Item item)
    {
        var keycard = Exiled.API.Features.Items.Item.Get<SingleUseKeycard>(item.Base);
        if (keycard != null)
        {
            keycard.AllowClosingDoors = true;
            keycard.Uses = 114514;
        }
        base.Customize(item);
    }

}
