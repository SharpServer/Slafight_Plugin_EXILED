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
    /// 表示順です。既定は 名前 → 役職 → 追加情報 です。
    /// </summary>
    public string Order { get; set; } = CustomNameToken + RoleNameToken + ExtraInfoToken;
    public bool ShowCustomName { get; set; } = true;
    public bool ShowRoleName { get; set; } = true;
    public string? RoleNameOverride { get; set; }
    public CustomInfoUnitNameMode UnitNameMode { get; set; } = CustomInfoUnitNameMode.Native;

    /// <summary>
    /// バニラの階級表示 (GiveOrders / FollowOrders / SameRank) を出すかどうか。既定は true。
    /// </summary>
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
    /// <see cref="CustomInfoDisplayOptions.ExtraInfoToken"/> の中身を返します。null ならその行は出ません。
    /// </summary>
    public static Func<Player, string>? ExtraInfoProvider { get; set; }

    /// <summary>
    /// まだ名札を管理していないプレイヤーを既定の見た目で管理下に置きます。管理済みなら何もしません。
    /// </summary>
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
