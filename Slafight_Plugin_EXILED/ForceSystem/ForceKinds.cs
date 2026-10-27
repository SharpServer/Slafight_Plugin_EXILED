using System;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.ForceSystem.Forces;

namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// バニラ陣営から隊の種類を決めます。「陣営 → 隊の種類」の対応はここだけに置きます。
/// </summary>
public static class ForceKinds
{
    /// <summary>
    /// このプレイヤーが属する隊の種類です。対象外なら null。
    /// </summary>
    public static Type For(Player player) => For(player?.Role?.Team ?? Team.Dead);

    /// <summary>
    /// この陣営が属する隊の種類です。対象外 (SCP・科学者など) なら null。
    /// </summary>
    public static Type For(Team team) => team switch
    {
        Team.FoundationForces => typeof(MobileTaskForce),
        Team.ChaosInsurgency => typeof(ChaosForce),
        Team.ClassD => typeof(ClassDGang),
        _ => null,
    };

    /// <summary>
    /// この陣営が部隊システムの対象かどうか。
    /// </summary>
    public static bool IsForceTeam(Team team) => For(team) is not null;

    /// <summary>
    /// この陣営に合った隊を作ります。対象外なら null。
    /// </summary>
    public static ForceBase Create(Team team) => team switch
    {
        Team.FoundationForces => new MobileTaskForce(null, null),
        Team.ChaosInsurgency => new ChaosForce(),
        Team.ClassD => new ClassDGang(),
        _ => null,
    };
}
