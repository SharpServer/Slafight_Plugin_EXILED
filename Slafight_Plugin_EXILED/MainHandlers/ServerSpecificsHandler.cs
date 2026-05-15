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
    }

    private static void OnSettingValueReceived(ReferenceHub hub, ServerSpecificSettingBase @base)
    {
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

            case SSTwoButtonsSetting { SettingId: 7 } twoButton:
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
        if (settingId == 1)
        {
            ActivateHandler.ToggleProximityChat(player);
            return;
        }

        // 生きているロールだけ処理
        if (player.Role == null ||
            player.Role.Type is RoleTypeId.None or RoleTypeId.Spectator ||
            player.Role.Team == Team.Dead)
            return;

        if (settingId == 5)
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
                hybrid.SwitchMode(item.Serial, player);
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
            if (settingId == 3)
                loadout.ActiveAbility?.TryActivateFromInput(player);
            else if (settingId == 4)
            {
                loadout.CycleNext();
                AbilityManager.UpdateAbilityHint(player, loadout);
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
        if (settingId == 2)
        {
            Log.Debug("nickname updated");

            var flags = player.Get();
            if (flags == null)
                Log.Debug($"[RPNameSetter] Flags null for {player.Nickname}, skipping.");

            if (!(flags != null && flags.Contains(SpecificFlagType.RPNameDisabled)))
            {
                player.CustomName = !string.IsNullOrEmpty(text)
                    ? $"{text} ({player.Nickname})"
                    : player.Nickname;

                CustomInfoDisplay.Refresh(player);
            }

            RPNameSetter.PlayerInputNames[player] = player.CustomName;
        }
        else if (settingId == 6)
        {
            Log.Debug("passcode updated");
            RPNameSetter.Passcodes[player] = text;
        }
    }

    // =====================
    //  デバッグモード (ID: 7)
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
