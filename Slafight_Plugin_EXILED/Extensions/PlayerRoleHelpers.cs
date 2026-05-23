using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.Teams;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;

namespace Slafight_Plugin_EXILED.Extensions;

public static class PlayerRoleHelpers
{
    public static CRoleTypeId GetCustomRole(this Player player)
    {
        if (player == null || string.IsNullOrEmpty(player.UniqueRole))
            return CRoleTypeId.None;

        return CRole.GetRoleIdFromUnique(player.UniqueRole);
    }

    public struct PlayerRoleInfo
    {
        public RoleTypeId Vanilla;
        public CRoleTypeId Custom;
    }

    extension(Player player)
    {
        public PlayerRoleInfo GetRoleInfo()
        {
            return new PlayerRoleInfo
            {
                Vanilla = player.Role.Type,
                Custom  = player.GetCustomRole()
            };
        }

        public CTeam GetTeam()
        {
            if (player == null) return CTeam.Null;

            if (CTeamPlayerState.TryGetTeamOverride(player, out var overrideTeam))
                return overrideTeam;

            var info = player.GetRoleInfo();

            // カスタムロールが None のときはバニラ Team から決める
            if (info.Custom == CRoleTypeId.None)
            {
                switch (player.Role.Team)
                {
                    case Team.SCPs:              return CTeam.SCPs;
                    case Team.FoundationForces:  return CTeam.FoundationForces;
                    case Team.ChaosInsurgency:   return CTeam.ChaosInsurgency;
                    case Team.Scientists:        return CTeam.Scientists;
                    case Team.ClassD:            return CTeam.ClassD;
                    case Team.Dead:
                    case Team.OtherAlive:
                    default:
                        return CTeam.Others;
                }
            }

            // カスタムロールがある場合は CRole 側の Team を使う
            return CRole.GetTeamFromUnique(player.UniqueRole);
        }
    }
}
