using System;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Hints;
using Slafight_Plugin_EXILED.ProximityChat;
using UserSettings.ServerSpecific;

namespace Slafight_Plugin_EXILED.MainHandlers;

public static class ServerSpecificsHandler
{
    public static void Register()
    {
        ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSettingValueReceived;
    }

    public static void Unregister()
    {
        ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSettingValueReceived;
        ClearAll();
    }

    public static float GetDocumentHintDuration(Player? player)
        => ServerSpecificUserSettings.GetDocumentHintDuration(player);

    public static bool IsAccessibilityModeEnabled(Player? player)
        => ServerSpecificUserSettings.IsAccessibilityModeEnabled(player);

    public static void RemovePlayer(Player? player)
        => ServerSpecificUserSettings.RemovePlayer(player);

    public static void ClearAll()
    {
        ServerSpecificUserSettings.ClearAll();
    }

    private static void OnSettingValueReceived(ReferenceHub hub, ServerSpecificSettingBase @base)
    {
        if (hub == null || @base == null)
            return;

        var player = Player.Get(hub);
        if (player == null || !player.IsConnected)
            return;

        switch (@base)
        {
            case SSKeybindSetting { SyncIsPressed: true } keybind:
                HandleKeybind(player, keybind.SettingId);
                break;

            case SSPlaintextSetting { SyncInputText: not null } text:
                HandleText(player, text.SettingId, text.SyncInputText);
                break;

            case SSTwoButtonsSetting twoButton when
                twoButton.SettingId == ServerSpecifics.AccessibilityModeSettingId:
                HandleAccessibilityMode(player);
                break;

            case SSTwoButtonsSetting twoButton when
                twoButton.SettingId == ServerSpecifics.DebugModeSettingId:
                HandleDebugMode(player, twoButton.SyncIsA);
                break;
        }
    }

    // =====================
    //  キーバインド (ID: 1, 3, 4, 5)
    // =====================

    private static void HandleKeybind(Player player, int settingId)
    {
        // VCトグルは常に許可
        if (settingId == ServerSpecifics.ProximityChatKeybindSettingId)
        {
            ActivateHandler.ToggleProximityChat(player);
            return;
        }

        // 生きているロールだけ処理
        if (player.Role == null ||
            player.Role.Type is RoleTypeId.None or RoleTypeId.Spectator ||
            player.Role.Team == Team.Dead)
            return;

        if (settingId == ServerSpecifics.ItemModeSwitchKeybindSettingId)
        {
            var item = player.CurrentItem;
            if (item == null)
            {
                Log.Debug($"[Input] G: no CurrentItem for {player.Nickname}");
                return;
            }

            if (!CItem.TryGet(item.Serial, out var ci))
            {
                Log.Debug($"[Input] G: TryGet fail serial={item.Serial} player={player.Nickname}");
                return;
            }

            Log.Debug($"[Input] G: serial={item.Serial} ci={ci.GetType().Name} player={player.Nickname}");

            if (ci is CItemHybrid hybrid)
            {
                hybrid.TrySwitchModeFromInput(item.Serial, player);
            }
            else
            {
                Log.Debug($"[Input] G: ci is not Hybrid ({ci.GetType().Name})");
            }

            return;
        }

        if (!AbilityManager.Loadouts.TryGetValue(player.Id, out var loadout) || loadout == null)
            return;

        try
        {
            if (settingId == ServerSpecifics.AbilityUseKeybindSettingId)
                loadout.ActiveAbility?.TryActivateFromInput(player);
            else if (settingId == ServerSpecifics.AbilitySwitchKeybindSettingId)
            {
                AbilityManager.NextSlot(player);
            }
        }
        catch (Exception e)
        {
            Log.Warn($"[Input] Ability handling error for {player.Nickname}: {e}");
        }
    }

    // =====================
    //  テキスト入力 (ID: 2=RPName, 6=パスコード)
    // =====================

    private static void HandleText(Player player, int settingId, string text)
    {
        if (settingId == ServerSpecifics.RpNameSettingId)
        {
            Log.Debug("nickname updated");
            RPNameSetter.SetInputName(player, text);
        }
        else if (settingId == ServerSpecifics.SecretPasscodeSettingId)
        {
            Log.Debug("passcode updated");
            RPNameSetter.SetPasscode(player, text);
        }
    }

    // =====================
    //  アクセシビリティモード (ID: 8)
    // =====================

    private static void HandleAccessibilityMode(Player player)
    {
        bool enabled = ServerSpecificUserSettings.IsAccessibilityModeEnabled(player);

        NetworkVisibilityManager.RefreshPlayer(player);
        Log.Debug($"[AccessibilityMode] {player.Nickname} => {(enabled ? "ON" : "OFF")}");
    }

    // =====================
    //  デバッグモード (ID: 9)
    // =====================

    private static void HandleDebugMode(Player player, bool isOn)
    {
        DebugModeHandler.SetDebugMode(player, isOn);

        try
        {
            PlayerHUD.Instance.HintSync(SyncType.PHUD_Debug, "", player);
        }
        catch
        {
            // ignored
        }

        Log.Debug($"[DebugMode] {player.Nickname} => {(isOn ? "ON" : "OFF")}");
    }
}
