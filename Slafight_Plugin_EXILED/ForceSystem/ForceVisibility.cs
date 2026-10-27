using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// 「誰にどの隊が見えるか」を決めます。既定は <see cref="ForceBase.Team"/> (無ければ隊の型) で同じ側だけ見せます。
/// </summary>
public static class ForceVisibility
{
    /// <summary>
    /// 見えるかどうかを決める述語です。null で既定に戻ります。ラウンドをまたいで保持されます。
    /// </summary>
    public static Func<Player, ForceBase, bool> Rule
    {
        get => rule;
        set => rule = value ?? Default;
    }

    private static Func<Player, ForceBase, bool> rule = Default;

    /// <summary>
    /// 既定のルールに戻します。
    /// </summary>
    public static void Reset() => rule = Default;

    /// <summary>
    /// このプレイヤーに見える隊です。
    /// </summary>
    public static IEnumerable<ForceBase> VisibleTo(Player viewer)
    {
        if (viewer is null) return [];

        return ForceRegistry.All.Where(force => IsVisible(viewer, force));
    }

    /// <summary>
    /// この隊がこのプレイヤーに見えるかどうか。ルールの例外は握りつぶします。
    /// </summary>
    public static bool IsVisible(Player viewer, ForceBase force)
    {
        if (viewer is null || force is null) return false;

        try
        {
            return rule(viewer, force);
        }
        catch
        {
            return Default(viewer, force);
        }
    }

    /// <summary>
    /// 既定のルールです。
    /// </summary>
    private static bool Default(Player viewer, ForceBase force)
    {
        ForceBase own = viewer.GetForce();

        // 自分の所属は常に見える。
        if (ReferenceEquals(own, force)) return true;

        // 部隊システムの対象外 (SCP など) には何も見せない。
        if (viewer.Role is null || !ForceKinds.IsForceTeam(viewer.Role.Team)) return false;

        // 陣営が違う隊は見せない。索敵情報になる。
        if (viewer.Role.Team.GetFaction() != force.Faction) return false;

        // 隊が CustomTeam を名乗っているなら、それが唯一の正解。
        if (force.Team is { } theirs)
        {
            CustomTeam mine = own?.Team ?? CustomTeam.Of(viewer);

            return mine is not null && mine.IsSameSide(theirs);
        }

        // 名乗っていないなら隊の型で仕分ける。
        // 機動部隊どうし・カオスどうし・ギャングどうしだけが互いに見える。
        if (own is not null)
            return own.GetType() == force.GetType();

        // まだどこにも属していない場合は、自分がこれから入りうる隊だけ見せる。
        return force.GetType() == ForceKinds.For(viewer);
    }
}
