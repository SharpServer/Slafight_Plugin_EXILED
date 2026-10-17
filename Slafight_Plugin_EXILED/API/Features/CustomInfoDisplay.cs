using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Features;

public enum CustomInfoUnitNameMode
{
    Native,
    Inline,
    Hidden
}

public sealed class CustomInfoDisplayOptions
{
    public const string CustomInfoToken = "%custominfo%";
    public const string CustomNameToken = "%customname%";
    public const string RoleNameToken = "%rolename%";
    public const string UnitNameToken = "%unitname%";

    /// <summary>
    /// 部隊システムが差し込む行です。<see cref="CustomInfoDisplay.ExtraInfoProvider"/> が中身を返します。
    /// </summary>
    public const string ExtraInfoToken = "%extrainfo%";

    public static readonly CustomInfoDisplayOptions Default = new();

    /// <summary>
    /// 既定では 名前 → 役職 → 追加情報 の順に積みます。
    /// </summary>
    /// <remarks>
    /// <see cref="ExtraInfoToken"/> を既定に入れてあるので、
    /// 順序を上書きしていない役職の名札には部隊情報が自動で出ます。
    /// 独自の <see cref="Order"/> を持つ役職に出したい場合は、自分でこのトークンを並べてください。
    /// </remarks>
    public string Order { get; set; } = CustomNameToken + RoleNameToken + ExtraInfoToken;
    public bool ShowCustomName { get; set; } = true;
    public bool ShowRoleName { get; set; } = true;
    public string? RoleNameOverride { get; set; }
    public CustomInfoUnitNameMode UnitNameMode { get; set; } = CustomInfoUnitNameMode.Native;

    /// <summary>
    /// バニラの階級表示 (GiveOrders / FollowOrders / SameRank) を出すかどうか。
    /// </summary>
    /// <remarks>
    /// バニラは <c>UnitNamingRule.AppendName</c> が <see cref="PlayerInfoArea.PowerStatus"/> を見て
    /// 自分と相手の役職を比べた文言をクライアント側で描きます。
    /// 部隊システムは独自の階級 (TopLead / SubLead / …) を持つので、
    /// 併記すると 2 種類の階級が名札に並んでしまいます。
    /// false にするとサーバー側でフラグを落とし、バニラ側の表示だけを消せます。
    ///
    /// 既定は true です。<b>これまでの見え方を変えないため</b>で、
    /// 消したい側が明示的に false にします。
    /// </remarks>
    public bool ShowPowerStatus { get; set; } = true;

    public CustomInfoDisplayOptions Clone()
        => new()
        {
            Order = Order,
            ShowCustomName = ShowCustomName,
            ShowRoleName = ShowRoleName,
            RoleNameOverride = RoleNameOverride,
            UnitNameMode = UnitNameMode,
            ShowPowerStatus = ShowPowerStatus
        };
}

public static class CustomInfoDisplay
{
    private const string EmptyColorTag = "<color=#FFFFFF></color>";
    private static readonly Dictionary<int, DisplayState> States = new();

    /// <summary>
    /// <see cref="CustomInfoDisplayOptions.ExtraInfoToken"/> の中身を返すものです。
    /// </summary>
    /// <remarks>
    /// 部隊システムがここに自分を差します。<see cref="Func{T, TResult}"/> で注入するのは、
    /// 表示層が <c>ForceSystem</c> を直接知らずに済ませるためです。
    /// null を返せばその行は出ません。
    /// </remarks>
    public static Func<Player, string>? ExtraInfoProvider { get; set; }

    /// <summary>
    /// まだ名札を管理していないプレイヤーを、既定の見た目で管理下に置きます。
    /// </summary>
    /// <remarks>
    /// <see cref="Apply"/> と違い、<b>既に管理下にある名札は書き換えません</b>。
    /// カスタム役職が自分で <see cref="Apply"/> した名札を、
    /// 部隊システムが横から潰さないようにするためです。
    /// </remarks>
    public static void EnsureTracked(Player player, CustomInfoDisplayOptions? options = null)
    {
        if (player == null || States.ContainsKey(player.Id))
            return;

        Apply(player, null, options);
    }

    public static void Apply(Player player, string? customInfo, CustomInfoDisplayOptions? options = null)
    {
        if (player == null)
            return;

        options ??= CustomInfoDisplayOptions.Default;
        States[player.Id] = new DisplayState(customInfo, options.Clone());
        Render(player, customInfo, options);
    }

    public static void Refresh(Player player)
    {
        if (player == null)
            return;

        if (States.TryGetValue(player.Id, out var state))
            Render(player, state.CustomInfo, state.Options);
    }

    public static string? GetAssignedCustomInfo(Player player)
        => player != null && States.TryGetValue(player.Id, out var state)
            ? state.CustomInfo
            : player?.CustomInfo;

    private static void Render(Player player, string? customInfo, CustomInfoDisplayOptions options)
    {
        string roleReplacement = ProcessText(
            CustomSeason.Current.LocalizeScpText(options.RoleNameOverride ?? customInfo ?? GetRoleName(player)));
        string customName = ProcessCustomNameText(GetCustomName(player));
        string unitName = ProcessText(player.UnitName);

        var replacements = new Dictionary<string, string>
        {
            [CustomInfoDisplayOptions.CustomInfoToken] = BuildLine(roleReplacement),
            [CustomInfoDisplayOptions.CustomNameToken] = options.ShowCustomName ? BuildLine(customName) : string.Empty,
            [CustomInfoDisplayOptions.RoleNameToken] = options.ShowRoleName ? BuildLine(roleReplacement) : string.Empty,
            [CustomInfoDisplayOptions.UnitNameToken] = options.UnitNameMode == CustomInfoUnitNameMode.Inline ? BuildLine(unitName) : string.Empty,
            [CustomInfoDisplayOptions.ExtraInfoToken] = BuildLine(ProcessText(ResolveExtraInfo(player)))
        };

        string rendered = replacements.Aggregate(
            EmptyColorTag + options.Order,
            (current, kvp) => current.Replace(kvp.Key, kvp.Value));

        player.CustomInfo = rendered.TrimEnd('\n', '\r');
        ApplyInfoArea(player, options);
    }

    /// <summary>
    /// 追加情報を解決します。提供側が落ちても名札全体を巻き添えにしません。
    /// </summary>
    private static string? ResolveExtraInfo(Player player)
    {
        if (ExtraInfoProvider is not { } provider)
            return null;

        try
        {
            return provider(player);
        }
        catch
        {
            return null;
        }
    }

    public static void Clear(Player player)
    {
        if (player == null)
            return;

        player.CustomInfo = null;
        States.Remove(player.Id);
        player.InfoArea |= PlayerInfoArea.Nickname;
        player.InfoArea |= PlayerInfoArea.Badge;
        player.InfoArea |= PlayerInfoArea.CustomInfo;
        player.InfoArea |= PlayerInfoArea.UnitName;
        player.InfoArea |= PlayerInfoArea.PowerStatus;
        player.InfoArea |= PlayerInfoArea.Role;
    }

    private static void ApplyInfoArea(Player player, CustomInfoDisplayOptions options)
    {
        player.InfoArea |= PlayerInfoArea.CustomInfo;
        player.InfoArea &= ~PlayerInfoArea.Nickname;
        player.InfoArea &= ~PlayerInfoArea.Role;

        if (options.UnitNameMode == CustomInfoUnitNameMode.Native)
            player.InfoArea |= PlayerInfoArea.UnitName;
        else
            player.InfoArea &= ~PlayerInfoArea.UnitName;

        if (options.ShowPowerStatus)
            player.InfoArea |= PlayerInfoArea.PowerStatus;
        else
            player.InfoArea &= ~PlayerInfoArea.PowerStatus;
    }

    private static string GetCustomName(Player player)
    {
        if (!string.IsNullOrWhiteSpace(player.CustomName))
            return player.CustomName;

        return player.Nickname ?? string.Empty;
    }

    private static string GetRoleName(Player player)
    {
        if (player.Role == null)
            return string.Empty;

        return player.Role.Name ?? player.Role.Type.ToString();
    }

    private static string ProcessText(string? text)
        => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("[br]", "\n");

    private static string ProcessCustomNameText(string? text)
        => ProcessText(text).Replace('[', '|').Replace(']', '|');

    private static string BuildLine(string text)
        => string.IsNullOrEmpty(text) ? string.Empty : text + "\n";

    private sealed class DisplayState
    {
        public DisplayState(string? customInfo, CustomInfoDisplayOptions options)
        {
            CustomInfo = customInfo;
            Options = options;
        }

        public string? CustomInfo { get; }
        public CustomInfoDisplayOptions Options { get; }
    }
}
