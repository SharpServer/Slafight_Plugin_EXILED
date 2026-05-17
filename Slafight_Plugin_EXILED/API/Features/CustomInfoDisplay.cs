using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;

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

    public static readonly CustomInfoDisplayOptions Default = new();

    public string Order { get; set; } = CustomNameToken + RoleNameToken;
    public bool ShowCustomName { get; set; } = true;
    public bool ShowRoleName { get; set; } = true;
    public string? RoleNameOverride { get; set; }
    public CustomInfoUnitNameMode UnitNameMode { get; set; } = CustomInfoUnitNameMode.Native;

    public CustomInfoDisplayOptions Clone()
        => new()
        {
            Order = Order,
            ShowCustomName = ShowCustomName,
            ShowRoleName = ShowRoleName,
            RoleNameOverride = RoleNameOverride,
            UnitNameMode = UnitNameMode
        };
}

public static class CustomInfoDisplay
{
    private const string EmptyColorTag = "<color=#FFFFFF></color>";
    private static readonly Dictionary<int, DisplayState> States = new();

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
        string roleReplacement = ProcessText(options.RoleNameOverride ?? customInfo ?? GetRoleName(player));
        string customName = ProcessText(GetCustomName(player));
        string unitName = ProcessText(player.UnitName);

        var replacements = new Dictionary<string, string>
        {
            [CustomInfoDisplayOptions.CustomInfoToken] = BuildLine(roleReplacement),
            [CustomInfoDisplayOptions.CustomNameToken] = options.ShowCustomName ? BuildLine(customName) : string.Empty,
            [CustomInfoDisplayOptions.RoleNameToken] = options.ShowRoleName ? BuildLine(roleReplacement) : string.Empty,
            [CustomInfoDisplayOptions.UnitNameToken] = options.UnitNameMode == CustomInfoUnitNameMode.Inline ? BuildLine(unitName) : string.Empty
        };

        string rendered = replacements.Aggregate(
            EmptyColorTag + options.Order,
            (current, kvp) => current.Replace(kvp.Key, kvp.Value));

        player.CustomInfo = rendered.TrimEnd('\n', '\r');
        ApplyInfoArea(player, options.UnitNameMode);
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

    private static void ApplyInfoArea(Player player, CustomInfoUnitNameMode unitNameMode)
    {
        player.InfoArea |= PlayerInfoArea.CustomInfo;
        player.InfoArea &= ~PlayerInfoArea.Nickname;
        player.InfoArea &= ~PlayerInfoArea.Role;

        if (unitNameMode == CustomInfoUnitNameMode.Native)
            player.InfoArea |= PlayerInfoArea.UnitName;
        else
            player.InfoArea &= ~PlayerInfoArea.UnitName;
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
