using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// 名札に部隊名と階級を出します。<c>%extrainfo%</c> の中身を返し、バニラの階級表示は消します。
/// </summary>
public static class ForceNameplate
{
    /// <summary>
    /// 部隊システムに乗っている人の名札設定です。
    /// </summary>
    private static readonly CustomInfoDisplayOptions Options = new()
    {
        // バニラの階級表示をやめて、こちらの TopLead / SubLead 表記に一本化する。
        ShowPowerStatus = false,
    };

    /// <summary>
    /// 名札に差し込む行を返します。部隊システムの対象外なら null。
    /// </summary>
    /// <remarks>色は <see cref="ServerColors"/> だけを使ってください。それ以外は名札ごと弾かれます。</remarks>
    public static string Text(Player player)
    {
        // SCP や科学者は部隊システムの対象外。役職が変わった後も
        // 隊員状態は残るので、陣営を毎回見て判断する。
        if (player?.Role is null || !ForceKinds.IsForceTeam(player.Role.Team)) return null;

        if (player.GetForceMember() is not { } member) return null;

        ForceBase force = member.Force;
        string rank = member.Level.NameOf(force);

        if (force is null)
            return $"<size=20><color={ServerColors.Nickel}>{rank}</color></size>";

        string color = force.IsMainForce ? ServerColors.Yellow : ServerColors.Cyan;

        return $"<size=20><color={color}>{force.Name}</color> " +
               $"<color={ServerColors.Silver}>{force.KindName}・{rank}</color></size>";
    }

    /// <summary>
    /// この人の名札を部隊システムの管理下に置いて描き直します。
    /// </summary>
    public static void Refresh(Player player)
    {
        if (player is null) return;

        CustomInfoDisplay.EnsureTracked(player, Options);
        CustomInfoDisplay.Refresh(player);
    }
}
