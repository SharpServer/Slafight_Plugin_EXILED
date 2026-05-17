using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.MainHandlers;

public static class RPNameSetter
{
    public static Dictionary<Player, string> PlayerInputNames = new();
    public static Dictionary<Player, string> Passcodes = new();

    public static void SetInputName(Player player, string? input)
    {
        if (player == null)
            return;

        string customName = BuildCustomName(player, input);
        PlayerInputNames[player] = customName;

        if (player.HasFlag(SpecificFlagType.RPNameDisabled))
            return;

        ApplyCustomName(player, customName);
    }

    public static void ApplyStoredInputName(Player player)
    {
        if (player == null)
            return;

        if (!PlayerInputNames.TryGetValue(player, out string customName))
            customName = BuildCustomName(player, null);

        ApplyCustomName(player, customName);
    }

    public static void SetForcedCustomName(Player player, string? customName)
    {
        if (player == null)
            return;

        ApplyCustomName(player, string.IsNullOrWhiteSpace(customName) ? player.Nickname : customName);
    }

    public static void SetPasscode(Player player, string passcode)
    {
        if (player == null)
            return;

        Passcodes[player] = passcode;
    }

    public static bool TryGetPasscode(Player player, out string passcode)
        => Passcodes.TryGetValue(player, out passcode);

    public static void Clear(Player player)
    {
        if (player == null)
            return;

        PlayerInputNames.Remove(player);
        Passcodes.Remove(player);
    }

    public static void ClearAll()
    {
        PlayerInputNames.Clear();
        Passcodes.Clear();
    }

    private static void ApplyCustomName(Player player, string customName)
    {
        player.CustomName = customName;
        CustomInfoDisplay.Refresh(player);
    }

    private static string BuildCustomName(Player player, string? input)
        => !string.IsNullOrWhiteSpace(input)
            ? $"{input} ({player.Nickname})"
            : player.Nickname;
}
