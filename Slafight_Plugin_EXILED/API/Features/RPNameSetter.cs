using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using PlayerHandlers = Exiled.Events.Handlers.Player;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// SSS のテキスト欄で決めた RP 名を、ネームプレートの表示名に反映します。
/// </summary>
public static class RPNameSetter
{
    /// <summary>
    /// 入力された RP 名を記録し、表示へ反映します。
    /// </summary>
    public static void SetInputName(Player player, string? input)
    {
        if (player == null)
            return;

        ServerSpecificUserSettings.TrySetText(player, ServerSpecifics.RpNameSettingId, input);

        string customName = BuildCustomName(player, input);

        // 役職側が RP 名を禁じている場合は記録だけして表示は変えない。
        if (player.HasFlag(SpecificFlagType.RPNameDisabled))
            return;

        ApplyCustomName(player, customName);
    }

    /// <summary>
    /// 記録済みの RP 名を表示へ反映し直します。
    /// </summary>
    public static void ApplyStoredInputName(Player player)
    {
        if (player == null)
            return;

        ApplyCustomName(player, BuildCustomName(player, ServerSpecificUserSettings.GetRpNameInput(player)));
    }

    /// <summary>
    /// RP 名を無視して、指定した表示名を強制します。
    /// 変装や特殊役職が名乗りを差し替えるときに使います。
    /// </summary>
    public static void SetForcedCustomName(Player player, string? customName)
    {
        if (player == null)
            return;

        ApplyCustomName(player, string.IsNullOrWhiteSpace(customName) ? player.Nickname : customName);
    }

    /// <summary>
    /// 合言葉を記録します。
    /// </summary>
    public static void SetPasscode(Player player, string passcode)
    {
        if (player == null)
            return;

        ServerSpecificUserSettings.TrySetText(player, ServerSpecifics.SecretPasscodeSettingId, passcode);
    }

    /// <inheritdoc cref="ServerSpecificUserSettings.TryGetPasscode"/>
    public static bool TryGetPasscode(Player player, out string passcode)
        => ServerSpecificUserSettings.TryGetPasscode(player, out passcode);

    /// <summary>
    /// このプレイヤーの記録を消します。
    /// </summary>
    public static void Clear(Player player)
    {
        if (player == null)
            return;

        ServerSpecificUserSettings.ClearSetting(player, ServerSpecifics.RpNameSettingId);
        ServerSpecificUserSettings.ClearSetting(player, ServerSpecifics.SecretPasscodeSettingId);
    }

    /// <summary>
    /// 全員ぶんの記録を消します。
    /// </summary>
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

/// <summary>
/// 参加時に記録済みの RP 名を貼り直し、退出時に記録を捨てます。
/// </summary>
public sealed class RPNameLifecycle : EventHandlerBase
{
    /// <inheritdoc />
    public override void RegisterEvents()
    {
        PlayerHandlers.Verified += OnVerified;
        PlayerHandlers.Left += OnLeft;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        PlayerHandlers.Verified -= OnVerified;
        PlayerHandlers.Left -= OnLeft;

        RPNameSetter.ClearAll();
    }

    private static void OnVerified(VerifiedEventArgs ev) => RPNameSetter.ApplyStoredInputName(ev.Player);

    private static void OnLeft(LeftEventArgs ev) => RPNameSetter.Clear(ev.Player);
}
