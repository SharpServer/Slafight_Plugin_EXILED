using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.ForceSystem;

namespace Slafight_Plugin_EXILED.API.Core.Structs;

/// <summary>
/// 脱出しようとしている 1 人ぶんの、役職が変わる前の状況です。
/// </summary>
public readonly struct EscapeContext
{
    /// <summary>
    /// 指定したプレイヤーの現在の状況を写し取ります。
    /// </summary>
    public EscapeContext(Player player)
    {
        Player = player;
        Role = CustomRole.Of(player);
        Team = CustomTeam.Of(player);
        ForceMember = player.GetForceMember();
        Force = ForceMember?.Force;

        // 手錠を掛けられていなければ Cuffer は null になる。
        Escort = player?.Cuffer;
        EscortRole = CustomRole.Of(Escort);
        EscortTeam = CustomTeam.Of(Escort);
    }

    /// <summary>
    /// 脱出しようとしているプレイヤーです。
    /// </summary>
    public Player Player { get; }

    /// <summary>
    /// そのプレイヤーのカスタム役職です。持っていなければ null。
    /// </summary>
    public CustomRole Role { get; }

    /// <summary>
    /// そのプレイヤーの陣営です。属していなければ null。
    /// </summary>
    public CustomTeam Team { get; }

    /// <summary>
    /// 脱出前の隊員状態の写しです。部隊システムの対象外なら null。
    /// </summary>
    public ForceMember ForceMember { get; }

    /// <summary>
    /// 脱出する前に属していた隊です。無所属なら null。
    /// </summary>
    public ForceBase Force { get; }

    /// <summary>
    /// 手錠を掛けて連れてきた相手です。単独で出てきたなら null。
    /// </summary>
    public Player Escort { get; }

    /// <summary>
    /// 護送してきた相手のカスタム役職です。
    /// </summary>
    public CustomRole EscortRole { get; }

    /// <summary>
    /// 護送してきた相手の陣営です。
    /// </summary>
    public CustomTeam EscortTeam { get; }

    /// <summary>
    /// 誰かに護送されているかどうか。
    /// </summary>
    public bool IsEscorted => Escort is not null;

    /// <summary>
    /// <typeparamref name="T"/> に護送されているかどうか。派生した陣営も含みます。
    /// </summary>
    public bool IsEscortedBy<T>() where T : CustomTeam => EscortTeam is T;

    /// <summary>
    /// <typeparamref name="T"/> と同じ側の陣営に護送されているかどうか。
    /// </summary>
    public bool IsEscortedByAllyOf<T>() where T : CustomTeam, new() =>
        EscortTeam is not null && CustomTeam.Get<T>().IsSameSide(EscortTeam);
}
