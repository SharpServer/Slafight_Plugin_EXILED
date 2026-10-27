using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Core.Extensions;

/// <summary>
/// プレイヤーから陣営を引く拡張です。陣営インスタンスが null でも落ちません。
/// </summary>
public static class CustomTeamExtensions
{
    private const string DefaultColor = "#FFFFFF";

    /// <summary>
    /// このプレイヤーが属している陣営です。どこにも属していなければ null。
    /// </summary>
    public static CustomTeam GetTeam(this Player player) => CustomTeam.Of(player);

    /// <summary>
    /// このプレイヤーの陣営を <typeparamref name="T"/> として取ります。別の陣営なら null。
    /// </summary>
    public static T GetTeam<T>(this Player player) where T : CustomTeam => CustomTeam.Of(player) as T;

    /// <summary>
    /// 陣営に属していれば取り出します。
    /// </summary>
    public static bool TryGetTeam(this Player player, out CustomTeam team)
    {
        team = CustomTeam.Of(player);

        return team is not null;
    }

    /// <summary>
    /// <typeparamref name="T"/> に属していれば取り出します。
    /// </summary>
    public static bool TryGetTeam<T>(this Player player, out T team) where T : CustomTeam
    {
        team = CustomTeam.Of(player) as T;

        return team is not null;
    }

    /// <summary>
    /// 何らかの陣営に属しているかどうか。
    /// </summary>
    public static bool HasTeam(this Player player) => CustomTeam.Of(player) is not null;

    /// <summary>
    /// <typeparamref name="T"/> に属しているかどうか。
    /// </summary>
    public static bool IsInTeam<T>(this Player player) where T : CustomTeam, new() =>
        CustomTeam.Get<T>().Includes(player);

    /// <summary>
    /// 指定した陣営に属しているかどうか。<paramref name="team"/> が null なら false。
    /// </summary>
    public static bool IsInTeam(this Player player, CustomTeam team) => team is not null && team.Includes(player);

    /// <summary>
    /// 2 人が勝敗判定上おなじ側かどうか。どちらかが陣営を持たなければゲーム本体の陣営で判定します。
    /// </summary>
    public static bool IsAllyOf(this Player player, Player other) => CustomTeam.AreAllies(player, other);

    /// <summary>
    /// 2 人が敵対しているかどうか。自分自身は敵になりません。
    /// </summary>
    public static bool IsEnemyOf(this Player player, Player other)
    {
        if (!player.IsSafePlayer() || !other.IsSafePlayer()) return false;
        if (ReferenceEquals(player, other)) return false;

        return !CustomTeam.AreAllies(player, other);
    }

    /// <summary>
    /// 勝敗判定上おなじ側にいる生存プレイヤーです。既定では自分を含みません。
    /// </summary>
    public static IEnumerable<Player> GetAllies(this Player player, bool includeSelf = false)
    {
        if (!player.IsSafePlayer()) return [];

        return Player.List.Where(other =>
            other.IsSafePlayer() &&
            other.IsAlive &&
            (includeSelf || !ReferenceEquals(other, player)) &&
            CustomTeam.AreAllies(player, other));
    }

    /// <summary>
    /// 敵対している生存プレイヤーです。
    /// </summary>
    public static IEnumerable<Player> GetEnemies(this Player player)
    {
        if (!player.IsSafePlayer()) return [];

        return Player.List.Where(other => other.IsAlive && player.IsEnemyOf(other));
    }

    /// <summary>
    /// 表示に使う陣営名です。陣営を持たなければ空文字。
    /// </summary>
    public static string GetTeamName(this Player player) => CustomTeam.Of(player)?.HudName ?? string.Empty;

    /// <summary>
    /// 表示に使う陣営色です。陣営を持たなければ白。
    /// </summary>
    public static string GetTeamColor(this Player player) => CustomTeam.Of(player).ColorOrDefault();

    /// <summary>
    /// 陣営の表示色です。陣営が null でも白を返します。
    /// </summary>
    public static string ColorOrDefault(this CustomTeam team) => team?.Color ?? DefaultColor;

    /// <summary>
    /// 陣営色で文字列を巻きます。陣営が null なら白で巻きます。
    /// </summary>
    public static string Colorize(this CustomTeam team, string text) =>
        $"<color={team.ColorOrDefault()}>{text}</color>";

    /// <summary>
    /// このプレイヤーがこの陣営に属するかどうか。陣営が null なら false。
    /// </summary>
    public static bool Contains(this CustomTeam team, Player player) => team is not null && team.Includes(player);

    /// <summary>
    /// 陣営の生存者数です。陣営が null なら 0。
    /// </summary>
    public static int CountAlive(this CustomTeam team) => team?.Members.Count() ?? 0;

    /// <summary>
    /// 陣営に生存者が残っているかどうか。
    /// </summary>
    public static bool HasMembers(this CustomTeam team) => team is not null && team.Members.Any();

    /// <summary>
    /// この陣営の生存者が持っているカスタム役職です。バニラ役職の生存者は含みません。
    /// </summary>
    public static IEnumerable<CustomRole> GetRoles(this CustomTeam team) =>
        team is null
            ? []
            : team.Members.Select(CustomRole.Of).Where(role => role is not null);

    /// <summary>
    /// この陣営の生存者のうち <typeparamref name="T"/> の役職を持っている者です。
    /// </summary>
    public static IEnumerable<T> GetRoles<T>(this CustomTeam team) where T : CustomRole =>
        team.GetRoles().OfType<T>();

    /// <summary>
    /// 勝敗判定上おなじ側かどうか。どちらかが null なら false。
    /// </summary>
    public static bool IsSameSideAs(this CustomTeam team, CustomTeam other) =>
        team is not null && team.IsSameSide(other);
}
