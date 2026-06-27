using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.MainHandlers;

public static class RPNameSetter
{
    public static void SetInputName(Player player, string? input)
    {
        if (player == null)
            return;

        ServerSpecificUserSettings.TrySetText(player, ServerSpecifics.RpNameSettingId, input);

        string customName = BuildCustomName(player, input);

        if (player.HasFlag(SpecificFlagType.RPNameDisabled))
            return;

        ApplyCustomName(player, customName);
    }

    public static void ApplyStoredInputName(Player player)
    {
        if (player == null)
            return;

        ApplyCustomName(player, BuildCustomName(player, ServerSpecificUserSettings.GetRpNameInput(player)));
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

        ServerSpecificUserSettings.TrySetText(player, ServerSpecifics.SecretPasscodeSettingId, passcode);
    }

    public static bool TryGetPasscode(Player player, out string passcode)
        => ServerSpecificUserSettings.TryGetPasscode(player, out passcode);

    public static void Clear(Player player)
    {
        if (player == null)
            return;

        ServerSpecificUserSettings.ClearSetting(player, ServerSpecifics.RpNameSettingId);
        ServerSpecificUserSettings.ClearSetting(player, ServerSpecifics.SecretPasscodeSettingId);
    }

    public static void ClearAll()
    {
        ServerSpecificUserSettings.ClearSettingFromAll(ServerSpecifics.RpNameSettingId);
        ServerSpecificUserSettings.ClearSettingFromAll(ServerSpecifics.SecretPasscodeSettingId);
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
