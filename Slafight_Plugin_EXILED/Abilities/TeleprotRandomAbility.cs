using System.Collections.Generic;
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
    // AbilityBase の抽象プロパティを実装
    protected override float DefaultCooldown => 180f;
    protected override int DefaultMaxUses => -1;

    // 完全デフォルト
    public TeleportRandomAbility(Player owner)
        : base(owner) { }

    // コマンドなどから上書きしたいとき用
    public TeleportRandomAbility(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds) { }

    public TeleportRandomAbility(Player owner, float cooldownSeconds, int maxUses)
        : base(owner, cooldownSeconds, maxUses) { }

    protected override void ExecuteAbility(Player player)
    {
        var excludeTypes = new HashSet<RoomType>
        {
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
        };

        var candidates = new List<Vector3>(2);
        var randomRoom = Room.Random(player.Zone);
        if (randomRoom != null)
            candidates.Add(randomRoom.WorldPosition(Vector3.zero));

        var randomPlayer = Player.Get(Random.Range(0, Player.List.Count));
        if (randomPlayer != null)
            candidates.Add(randomPlayer.Position);

        if (candidates.Count == 0)
        {
            player.ShowHint("有効な位置が見つかりませんでした", 3f);
            return;
        }

        Vector3 targetPos;
        int attempts = 0;
        do
        {
            int randomIndex = Random.Range(0, candidates.Count);
            targetPos = candidates[randomIndex];
        }
        while (!IsValidTeleportTarget(targetPos, excludeTypes) && attempts++ < candidates.Count * 2);

        if (attempts < candidates.Count * 2)
        {
            player.CurrentRoom.TurnOffLights(2.5f);
            player.Position = targetPos + new Vector3(0f, 1.05f, 0f);
            player.CurrentRoom.TurnOffLights(2.5f);
        }
        else
            player.ShowHint("安全なテレポート位置が見つかりませんでした", 5f);
    }

    private static bool IsValidTeleportTarget(Vector3 pos, HashSet<RoomType> excludeTypes)
    {
        var room = Room.Get(pos);
        if (room == null || excludeTypes.Contains(room.Type)) return false;

        foreach (var occupant in room.Players)
        {
            if (occupant.Role.Type != RoleTypeId.Spectator && occupant.GetTeam() != CTeam.SCPs)
                return false;
        }

        return true;
    }
}
