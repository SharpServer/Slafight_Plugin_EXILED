using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.Abilities;

public class TeleportRandomAbility : AbilityBase
{
    private static readonly HashSet<RoomType> ExcludedRoomTypes =
    [
        RoomType.Lcz173,
        RoomType.LczClassDSpawn,
        RoomType.Surface,
        RoomType.EzCollapsedTunnel,
        RoomType.Lcz330,
        RoomType.LczArmory,
        RoomType.HczArmory,
        RoomType.LczCheckpointA,
        RoomType.LczCheckpointB,
        RoomType.LczToilets,
        RoomType.Hcz049,
        RoomType.Hcz939,
        RoomType.HczCrossRoomWater,
        RoomType.HczEzCheckpointA,
        RoomType.HczEzCheckpointB,
        RoomType.Hcz096,
        RoomType.Hcz106,
        RoomType.HczTestRoom
    ];

    private Vector3? _preparedDestination;

    // AbilityBase の抽象プロパティを実装
    protected override float DefaultCooldown => 180f;
    protected override int DefaultMaxUses => -1;

    protected override bool CanActivate(Player player, out string failureReason)
    {
        _preparedDestination = null;
        if (!base.CanActivate(player, out failureReason))
            return false;

        var candidates = Room.List
            .Where(room =>
                room != null &&
                room.Zone == player.Zone &&
                IsValidTeleportTarget(room.WorldPosition(Vector3.zero)))
            .Select(room => room.WorldPosition(Vector3.zero))
            .Concat(Player.List
                .Where(target =>
                    target != null &&
                    target != player &&
                    target.IsAlive &&
                    IsValidTeleportTarget(target.Position))
                .Select(target => target.Position))
            .ToList();

        if (candidates.Count == 0)
        {
            failureReason = "安全なテレポート位置が見つかりませんでした。";
            return false;
        }

        _preparedDestination = candidates[Random.Range(0, candidates.Count)];
        failureReason = string.Empty;
        return true;
    }

    protected override void ExecuteAbility(Player player)
    {
        if (!_preparedDestination.HasValue)
            return;

        var targetPos = _preparedDestination.Value;
        _preparedDestination = null;
        player.CurrentRoom?.TurnOffLights(2.5f);
        player.Position = targetPos + new Vector3(0f, 1.05f, 0f);
        player.CurrentRoom?.TurnOffLights(2.5f);
    }

    private static bool IsValidTeleportTarget(Vector3 pos)
    {
        var room = Room.Get(pos);
        if (room == null || ExcludedRoomTypes.Contains(room.Type)) return false;

        foreach (var occupant in room.Players)
        {
            if (occupant.Role.Type != RoleTypeId.Spectator && occupant.GetTeam() != CTeam.SCPs)
                return false;
        }

        return true;
    }
}
