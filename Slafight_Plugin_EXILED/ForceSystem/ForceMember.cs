using System;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// 部隊に属する 1 人ぶんの状態です。<see cref="NetId"/> で持ち主を識別します。
/// </summary>
public sealed class ForceMember
{
    internal ForceMember(Player player)
    {
        Player = player;
        NetId = player.GetNetId();
        Kind = ForceKinds.For(player);
        JoinedAt = Time.time;
        AloneSince = Time.time;
    }

    /// <summary>
    /// 本人です。<see cref="IsAlive"/> が false なら信用してはいけません。
    /// </summary>
    public Player Player { get; }

    /// <summary>
    /// 本人を指す安定したキーです。
    /// </summary>
    public uint NetId { get; }

    /// <summary>
    /// この隊員状態を作ったときの隊の種類です。今の役職と食い違ったら状態を捨てます。
    /// </summary>
    internal Type Kind { get; }

    /// <summary>
    /// 現在所属している隊です。どこにも属していなければ null。
    /// </summary>
    public ForceBase Force { get; internal set; }

    /// <summary>
    /// 保持している階級です。<see cref="ForceClassLevel.Alone"/> は入りません。
    /// </summary>
    public ForceClassLevel Rank { get; internal set; } = ForceClassLevel.Member;

    /// <summary>
    /// 表示用の階級です。隊に属していない Member だけが <see cref="ForceClassLevel.Alone"/> になります。
    /// </summary>
    public ForceClassLevel Level => Force is null && Rank is ForceClassLevel.Member
        ? ForceClassLevel.Alone
        : Rank;

    /// <summary>
    /// いまの隊に加わった時刻です (<see cref="Time.time"/> 基準)。
    /// </summary>
    public float JoinedAt { get; internal set; }

    /// <summary>
    /// 単独行動になった時刻です。隊に属していれば null。分隊の隊長選びに使います。
    /// </summary>
    public float? AloneSince { get; internal set; }

    /// <summary>
    /// この隊での累計貢献度です。
    /// </summary>
    public int Contribution { get; internal set; }

    /// <summary>
    /// SubLead への昇進条件を満たし始めた時刻です。満たしていなければ null。
    /// </summary>
    internal float? SubLeadHoldSince { get; set; }

    /// <summary>
    /// 昇進条件が緩和 (70%/60 秒 → 60%/40 秒) されているかどうか。
    /// </summary>
    internal bool HasRelaxedPromotion { get; set; }

    /// <summary>
    /// 本人がまだ同一人物 (netId 一致) として生きているかどうか。
    /// </summary>
    public bool IsAlive => Player.IsSafePlayer() && Player.GetNetId() == NetId;

    /// <summary>
    /// この隊に加わってからの経過秒数です。
    /// </summary>
    public float MembershipSeconds => Time.time - JoinedAt;
}
