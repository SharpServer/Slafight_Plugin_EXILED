using System.Collections.Generic;
using InventorySystem.Items.Firearms.Attachments;
using LabApi.Events.Arguments.PlayerEvents;
using PlayerRoles;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.API.Core.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.Weapons;

/// <summary>
/// Epsilon-11 が運用する対物ライフルです。
/// </summary>
public sealed class GunM82 : CustomFirearm
{
    private static readonly IReadOnlyList<AttachmentName> FixedAttachments =
    [
        AttachmentName.ScopeSight,
        AttachmentName.LowcapMagAP,
        AttachmentName.RifleBody,
        AttachmentName.RecoilReducingStock,
        AttachmentName.SoundSuppressor,
    ];

    public override string Name => "M-82-APR";
    public override string Description => "E11が所持する対物ライフル";
    public override ItemType BaseType => ItemType.GunE11SR;
    protected override float? Damage => 35f;
    protected override int? MagazineCapacity => 30;
    protected override Vector3 PickupScale => new(1f, 1f, 2.25f);
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.cyan;
    protected override int AmmoDrain => 30;
    protected override IReadOnlyList<AttachmentName> Attachments => FixedAttachments;
    protected override bool AllowAttachmentChanges => false;

    protected override void OnHurtingPlayer(PlayerHurtingEventArgs ev)
    {
        StandardDamageHandler standard = ev.DamageHandler as StandardDamageHandler;
        if (standard is not null)
        {
            standard.StartVelocity *= 4f;
            if (standard.Hitbox == HitboxType.Headshot)
                standard.Damage += 30f;
        }

        if (ev.Player?.Role == RoleTypeId.Scp173 && standard is not null)
            standard.Damage *= 2.5f;

        base.OnHurtingPlayer(ev);
    }
}
