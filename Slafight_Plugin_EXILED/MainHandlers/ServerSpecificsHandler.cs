using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
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
        try
        {
            ProcessSettingValueReceived(hub, @base);
        }
        catch (Exception e)
        {
            Log.Warn($"[ServerSpecifics] Failed to process setting value. Hub={DescribeHub(hub)}, Setting={DescribeSetting(@base)}\n{e}");
        }
    }

    private static void ProcessSettingValueReceived(ReferenceHub hub, ServerSpecificSettingBase @base)
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
    //  キーバインド
    // =====================

    private static void HandleKeybind(Player player, int settingId)
    {
        if (!IsConnectedPlayer(player))
            return;

        // VCトグルは常に許可
        if (settingId == ServerSpecifics.ProximityChatKeybindSettingId)
        {
            ActivateHandler.ToggleProximityChat(player);
            return;
        }

        // 生きているロールだけ処理
        if (!CanHandleAlivePlayer(player))
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

        if (settingId == ServerSpecifics.SuicideButtonKeybindSettingId)
        {
            HandleSuicideKeybind(player);
            return;
        }

        if (!AbilityManager.Loadouts.TryGetValue(player.Id, out var loadout) || loadout == null)
            return;

        try
        {
            if (settingId == ServerSpecifics.AbilityUseKeybindSettingId)
            {
                loadout.ActiveAbility?.TryActivateFromInput(player);
            }
            else if (settingId == ServerSpecifics.AbilitySwitchKeybindSettingId)
            {
                AbilityManager.NextSlot(player);
            }
            else if (settingId == ServerSpecifics.AbilityOptionPreviousKeybindSettingId)
            {
                loadout.ActiveAbility?.TrySwitchOptionFromInput(player, AbilityOptionDirection.Previous);
            }
            else if (settingId == ServerSpecifics.AbilityOptionNextKeybindSettingId)
            {
                loadout.ActiveAbility?.TrySwitchOptionFromInput(player, AbilityOptionDirection.Next);
            }
        }
        catch (Exception e)
        {
            Log.Warn($"[Input] Ability handling error for {player.Nickname}: {e}");
        }
    }

    // =====================
    //  テキスト入力
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
    //  アクセシビリティモード
    // =====================

    private static void HandleAccessibilityMode(Player player)
    {
        bool enabled = ServerSpecificUserSettings.IsAccessibilityModeEnabled(player);

        NetworkVisibilityManager.RefreshPlayer(player);
        Log.Debug($"[AccessibilityMode] {player.Nickname} => {(enabled ? "ON" : "OFF")}");
    }

    // =====================
    //  デバッグモード
    // =====================

    private static void HandleDebugMode(Player player, bool isOn)
    {
        DebugModeHandler.SetDebugMode(player, isOn);

        try
        {
            PlayerHUD.Instance?.HintSync(SyncType.PHUD_Debug, "", player);
        }
        catch
        {
            // ignored
        }

        Log.Debug($"[DebugMode] {player.Nickname} => {(isOn ? "ON" : "OFF")}");
    }

    private static void HandleSuicideKeybind(Player player)
    {
        try
        {
            if (!CanHandleAlivePlayer(player))
                return;

            var item = player.CurrentItem;
            if (item is null)
            {
                Log.Debug($"[Input] K: no CurrentItem for {DescribePlayer(player)}");
                return;
            }

            if (item is not Firearm firearm)
            {
                Log.Debug($"[Input] K: CurrentItem is not Firearm ({DescribeItem(item)}) for {DescribePlayer(player)}");
                return;
            }

            TryPlaySuicideGunSound(player, firearm);
            TryKillBySuicide(player);
        }
        catch (Exception e)
        {
            Log.Warn($"[Input] K suicide handling error for {DescribePlayer(player)}: {e}");
        }
    }

    private static void TryPlaySuicideGunSound(Player player, Firearm firearm)
    {
        try
        {
            var firearmType = firearm.FirearmType;
            if (firearmType is FirearmType.None or FirearmType.ParticleDisruptor)
                return;

            player.PlayGunSound(firearmType);
            SpeakerApi.Play("suicide_shot.ogg", $"{player.NetId}_suicideShotSound", player.Position, true);
        }
        catch (Exception e)
        {
            Log.Warn($"[Input] K: failed to play suicide gun sound for {DescribePlayer(player)} with {DescribeItem(firearm)}: {e.GetType().Name}: {e.Message}");
        }
    }

    private static void TryKillBySuicide(Player player)
    {
        try
        {
            if (!CanHandleAlivePlayer(player))
                return;

            player.Kill("自害した");
        }
        catch (Exception e)
        {
            Log.Warn($"[Input] K: failed to kill suicide player {DescribePlayer(player)}: {e}");
        }
    }

    private static bool IsConnectedPlayer(Player player)
    {
        try
        {
            return player != null &&
                   player.ReferenceHub != null &&
                   player.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanHandleAlivePlayer(Player player)
    {
        try
        {
            return IsConnectedPlayer(player) &&
                   player.Role != null &&
                   player.Role.Type is not (RoleTypeId.None or RoleTypeId.Spectator or RoleTypeId.Destroyed) &&
                   player.Role.Team != Team.Dead;
        }
        catch (Exception e)
        {
            Log.Debug($"[Input] Invalid player state for {DescribePlayer(player)}: {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    private static string DescribeHub(ReferenceHub hub)
    {
        try
        {
            if (hub == null)
                return "null";

            var player = Player.Get(hub);
            return player == null
                ? $"ReferenceHub#{hub.PlayerId}"
                : DescribePlayer(player);
        }
        catch
        {
            return "unknown";
        }
    }

    private static string DescribePlayer(Player player)
    {
        try
        {
            if (player == null)
                return "null";

            return $"{player.Nickname}({player.Id})";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string DescribeSetting(ServerSpecificSettingBase setting)
    {
        try
        {
            return setting == null
                ? "null"
                : $"{setting.GetType().Name}#{setting.SettingId}";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string DescribeItem(Item item)
    {
        try
        {
            return item == null
                ? "null"
                : $"{item.GetType().Name}/{item.Type}/{item.Serial}";
        }
        catch
        {
            return "unknown";
        }
    }
}
