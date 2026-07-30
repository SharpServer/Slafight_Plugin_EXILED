using InventorySystem.Items.Firearms.Attachments;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunSuppressiver : CItemWeapon
{
    public override string DisplayName => "Suppressiver";
    public override string Description => "カオスが開発した工作員向けの特殊なサブマシンガン。\n" +
                                          "射撃時に妨害電波とノイズを発し、射撃音の補足を妨害する。\n" +
                                          "消音性能には優れているものの、安定性はイマイチ。";
    protected override string UniqueKey => "GunSuppressiver";
    protected override ItemType BaseItem => ItemType.GunFSP9;
    protected override float Damage => 30f;
    protected override byte MagazineSize => 42;
    protected override Vector3 Scale => new(1.16f, 1f, 0.96f);
    protected override AttachmentName[] Attachments =>
    [
        AttachmentName.DotSight,
        AttachmentName.SoundSuppressor,
        AttachmentName.AmmoCounter,
        AttachmentName.None,
        AttachmentName.RetractedStock
    ];
    protected override bool AllowAttachmentChanges => false;
    protected override string? OverrideAudio => "GunSuppressiver.ogg";
    protected override float OverrideAudioRange => 15f;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => CustomColor.ChaoticGreen.ToUnityColor();
}
