using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Features;
using InventorySystem.Items.Firearms.Attachments;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomTeams;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.Weapons;

/// <summary>
/// 被弾した SCP の移動速度を一時的に下げる対異常兵装です。
/// </summary>
public sealed class GunAnomalyDetainer : CustomFirearm
{
    private static readonly IReadOnlyList<AttachmentName> FixedAttachments =
    [
        AttachmentName.RifleBody,
        AttachmentName.NightVisionSight,
        AttachmentName.StandardStock,
        AttachmentName.Foregrip,
        AttachmentName.LowcapMagJHP,
        AttachmentName.FlashHider,
    ];

    private const byte SlownessIntensity = 35;
    private const float SlownessDuration = 7f;

    public override string Name => "XE-11 ANOMALY DETAINER";
    public override string Description => "被弾したSCPに強力な鈍足を付与する試作対異常兵装";
    public override ItemType BaseType => ItemType.GunE11SR;
    public override Rarity Rarity => Rarity.Rare;
    protected override float? Damage => 20f;
    protected override int? MagazineCapacity => 5;
    protected override Vector3 PickupScale => new(1f, 1f, 1.1f);
    protected override IReadOnlyList<AttachmentName> Attachments => FixedAttachments;
    protected override bool AllowAttachmentChanges => false;

    protected override void OnHurtingPlayer(PlayerHurtingEventArgs ev)
    {
        Player target = ev.Player is { } labTarget ? Player.Get(labTarget.ReferenceHub) : null;
        if (ev.IsAllowed && target is not null &&
            (target.Role.Team == PlayerRoles.Team.SCPs || target.IsInTeam<ScpTeam>()))
        {
            ev.Player.EnableEffect<Slowness>(SlownessIntensity, SlownessDuration);
        }

        base.OnHurtingPlayer(ev);
    }
}
