using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Extensions;

/// <summary>
/// カスタム役職をプレイヤー側から引くための糖衣です。
/// </summary>
/// <remarks>
/// <para>
/// 役職の判定は <see cref="CustomRole"/>、所属と色は <see cref="CustomTeam"/> に委譲します。
/// </para>
/// <para>
/// 旧 API の <c>GetCustomRole</c> は <c>CRoleTypeId</c> という enum を返していました。
/// こちらは<b>インスタンスそのもの</b>を返すので、名前も所属陣営も per-player 状態も、
/// 返り値から直接読めます。役職の同一性は型なので、比較は
/// <see cref="IsCustomRole{T}(Player)"/> か <c>is</c> パターンで書いてください。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// if (player.TryGetCustomRole&lt;ChaosSniper&gt;(out ChaosSniper sniper))
///     sniper.ShowStatus("残弾 " + sniper.Shots, 2f);
///
/// if (attacker.IsCustomRole&lt;Scp173&gt;()) { }
/// </code>
/// </example>
public static class CustomRoleExtensions
{
    /// <summary>
    /// このプレイヤーの現在のカスタム役職です。持っていなければ null。
    /// </summary>
    public static CustomRole GetCustomRole(this Player player) => CustomRole.Of(player);

    /// <summary>
    /// このプレイヤーのカスタム役職を <typeparamref name="T"/> として取ります。
    /// 別の役職なら null。
    /// </summary>
    public static T GetCustomRole<T>(this Player player) where T : CustomRole => CustomRole.Of(player) as T;

    /// <summary>
    /// カスタム役職を持っていれば取り出します。
    /// </summary>
    public static bool TryGetCustomRole(this Player player, out CustomRole role)
    {
        role = CustomRole.Of(player);

        return role is not null;
    }

    /// <summary>
    /// <typeparamref name="T"/> の役職であれば取り出します。
    /// </summary>
    public static bool TryGetCustomRole<T>(this Player player, out T role) where T : CustomRole
    {
        role = CustomRole.Of(player) as T;

        return role is not null;
    }

    /// <summary>
    /// 何らかのカスタム役職を持っているかどうか。
    /// </summary>
    public static bool HasCustomRole(this Player player) => CustomRole.Of(player) is not null;

    /// <summary>
    /// <typeparamref name="T"/> の役職かどうか。派生役職も真になります。
    /// </summary>
    public static bool IsCustomRole<T>(this Player player) where T : CustomRole => CustomRole.Of(player) is T;

    /// <summary>
    /// 実行時に決まった型の役職かどうか。コマンドや設定から来た型を突き合わせる用です。
    /// </summary>
    public static bool IsCustomRole(this Player player, Type roleType) =>
        roleType is not null && CustomRole.Of(player) is { } role && roleType.IsInstanceOfType(role);

    /// <summary>
    /// このプレイヤーに <typeparamref name="T"/> の役職を付与します。失敗したら null。
    /// </summary>
    public static T SetCustomRole<T>(this Player player) where T : CustomRole, new() => CustomRole.Spawn<T>(player);

    /// <summary>
    /// 実行時に決まった型の役職を付与します。失敗したら null。
    /// </summary>
    public static CustomRole SetCustomRole(this Player player, Type roleType) => CustomRole.Spawn(roleType, player);

    /// <summary>
    /// このプレイヤーのカスタム役職を解除します。バニラ役職はそのままです。
    /// </summary>
    public static void RemoveCustomRole(this Player player) => CustomRole.Remove(player);

    /// <summary>
    /// 表示に使う役職名です。カスタム役職があればその名前、無ければバニラ役職の正式名。
    /// </summary>
    public static string GetRoleName(this Player player)
    {
        if (!player.IsSafePlayer()) return string.Empty;

        return CustomRole.Of(player)?.Name ?? player.Role.Type.GetFullName();
    }

    /// <summary>
    /// 陣営定義の色を巻いた役職名です。陣営未定義ならバニラ色を使います。カスタム役職があれば <see cref="CustomRole.HudLabel"/> をそのまま使います。
    /// </summary>
    public static string GetColoredRoleName(this Player player)
    {
        if (!player.IsSafePlayer()) return string.Empty;

        if (CustomRole.Of(player) is { } role)
            return role.HudLabel;

        string name = player.Role.Type.GetFullName();
        if (CustomTeam.Of(player) is { } team)
            return team.Colorize(name);

        string color = ColorUtility.ToHtmlStringRGB(player.Role.Type.GetColor());
        return $"<color=#{color}>{name}</color>";
    }

    /// <summary>
    /// 役職インスタンスの持ち主を、まだ有効なものだけ返します。
    /// </summary>
    public static IEnumerable<Player> Owners(this IEnumerable<CustomRole> roles) =>
        roles is null
            ? []
            : roles.Select(role => role.Player).Where(player => player.IsSafePlayer());
}
