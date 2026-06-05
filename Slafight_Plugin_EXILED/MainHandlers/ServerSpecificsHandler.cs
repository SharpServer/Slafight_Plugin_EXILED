using System;
using System.Collections.Generic;
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
    private static readonly Dictionary<int, float> DocumentHintDurations = new();

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
    {
        if (player == null)
            return ServerSpecifics.DefaultDocumentHintDuration;

        return DocumentHintDurations.TryGetValue(player.Id, out var duration)
            ? ClampDocumentHintDuration(duration)
            : ServerSpecifics.DefaultDocumentHintDuration;
    }

    public static void RemovePlayer(Player? player)
    {
        if (player == null)
            return;

        DocumentHintDurations.Remove(player.Id);
    }

    public static void ClearAll()
    {
        DocumentHintDurations.Clear();
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

            case SSSliderSetting slider:
                HandleSlider(player, slider.SettingId, slider.SyncFloatValue);
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
            if (settingId == 3)
                loadout.ActiveAbility?.TryActivateFromInput(player);
            else if (settingId == 4)
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
        if (settingId == 2)
        {
            Log.Debug("nickname updated");
            RPNameSetter.SetInputName(player, text);
        }
        else if (settingId == 6)
        {
            Log.Debug("passcode updated");
            RPNameSetter.SetPasscode(player, text);
        }
    }

    // =====================
    //  スライダー (ID: 8=Document表示時間)
    // =====================

    private static void HandleSlider(Player player, int settingId, float value)
    {
        if (player == null)
            return;

        if (settingId == ServerSpecifics.DocumentHintDurationSettingId)
        {
            DocumentHintDurations[player.Id] = ClampDocumentHintDuration(value);
        }
    }

    private static float ClampDocumentHintDuration(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return ServerSpecifics.DefaultDocumentHintDuration;

        return Math.Max(
            ServerSpecifics.MinDocumentHintDuration,
            Math.Min(ServerSpecifics.MaxDocumentHintDuration, value));
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
