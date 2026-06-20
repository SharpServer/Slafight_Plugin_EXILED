using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using ExiledScp106Role = Exiled.API.Features.Roles.Scp106Role;
using HuntRole = Slafight_Plugin_EXILED.CustomRoles.SCPs.Scp106Role;

namespace Slafight_Plugin_EXILED.Abilities;

public sealed class Scp106TargetTeleportAbility : AbilityBase
{
    protected override float DefaultCooldown => 20f;
    protected override int DefaultMaxUses => -1;

    public Scp106TargetTeleportAbility(Player owner)
        : base(owner, HuntRole.HuntSettings.TeleportCooldown) { }

    public Scp106TargetTeleportAbility(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds) { }

    protected override bool CanActivate(Player player, out string failureReason)
    {
        failureReason = string.Empty;
        if (!base.CanActivate(player, out failureReason))
            return false;

        if (player?.Role is not ExiledScp106Role scp106)
        {
            failureReason = "SCP-106でなければ使用できません。";
            return false;
        }

        if (scp106.IsSubmerged || scp106.IsDuringAnimation)
        {
            failureReason = "現在はポータルを使用できません。";
            return false;
        }

        if (!HuntRole.TryGetHuntTarget(player, out var target))
        {
            failureReason = "追跡対象が存在しません。";
            return false;
        }

        if (!TryGetDestination(target, out _))
        {
            failureReason = "対象のいる場所にはテレポートできません。";
            return false;
        }

        if (scp106.Vigor < HuntRole.HuntSettings.TeleportVigorCost)
        {
            failureReason = "Vigorが不足しています。";
            return false;
        }

        return true;
    }

    protected override void ExecuteAbility(Player player)
    {
        if (player.Role is not ExiledScp106Role scp106 ||
            !HuntRole.TryGetHuntTarget(player, out var target) ||
            !TryGetDestination(target, out var destination))
            return;

        if (scp106.UsePortal(destination, HuntRole.HuntSettings.TeleportVigorCost))
        {
            player.ShowHint(
                $"<color=#c50000>{target.Nickname}</color>のいる部屋へ移動を開始します。",
                3f);
        }
    }

    private static bool TryGetDestination(Player target, out Vector3 destination)
    {
        destination = Vector3.zero;
        if (target?.ReferenceHub == null || !target.IsConnected || !target.IsAlive)
            return false;

        var room = target.CurrentRoom ?? Room.Get(target.Position);
        if (room == null ||
            room.Type is RoomType.Surface or RoomType.Pocket or RoomType.Unknown ||
            room.Zone is ZoneType.Surface or ZoneType.Pocket or ZoneType.Unspecified or ZoneType.Other)
            return false;

        destination = target.Position;
        return true;
    }
}
