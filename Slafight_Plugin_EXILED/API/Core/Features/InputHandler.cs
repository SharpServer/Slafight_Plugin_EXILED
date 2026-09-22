using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.API.Core.Commands;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ProximityChat;
using UserSettings.ServerSpecific;

using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// Server Specific Settings のキーバインドを、対応する処理へ渡します。
///
/// <b>能力そのものは入力を知りません。</b>
/// <see cref="AbilityBase"/> は「使えるか」「効果は何か」だけを持ち、
/// どのキーで撃つかはここが決めます。入力方法を変えても能力側は無変更で済みます。
/// </summary>
/// <remarks>
/// このクラスはどこからも登録されていません。<see cref="EventHandlerBase"/> を
/// 継承しているだけで <see cref="EventHandlerRegistry"/> が購読させます。
/// </remarks>
public sealed class InputHandler : EventHandlerBase
{
    /// <inheritdoc />
    public override void RegisterEvents()
    {
        ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSettingValueReceived;
        ServerHandlers.WaitingForPlayers += RestoreDebugMode;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSettingValueReceived;
        ServerHandlers.WaitingForPlayers -= RestoreDebugMode;
    }

    /// <summary>
    /// ラウンドを跨いだあと、設定画面の値からデバッグ表示を入れ直します。
    /// </summary>
    /// <remarks>
    /// <see cref="DebugMode"/> の寿命は <see cref="PlayerScope"/> なので、ラウンド再開で
    /// 全員ぶん消えます。設定画面の値はサーバー側に残っているので、ここで入れ直さないと
    /// 「設定は ON なのに出ない」ままになります。設定は値が変わったときにしか飛んでこないため、
    /// その状態では ON を押し直しても戻せません。
    /// </remarks>
    private static void RestoreDebugMode()
    {
        foreach (Player player in Player.List)
        {
            if (!player.IsSafePlayer()) continue;

            if (!ServerSpecificUserSettings.IsDebugModeSelected(player)) continue;

            if (!player.CheckPermission(DebugCommand.PermissionNode)) continue;

            DebugMode.Set(player, true);
        }
    }

    private static void OnSettingValueReceived(ReferenceHub hub, ServerSpecificSettingBase setting)
    {
        if (Player.Get(hub) is not { } player || !player.IsSafePlayer()) return;

        // テキスト欄は押下ではなく入力確定で届く。キーバインドとは別に拾う。
        if (setting is SSPlaintextSetting { SyncInputText: not null } text)
        {
            HandleText(player, text.SettingId, text.SyncInputText);

            return;
        }

        // 2 択のトグルも押下ではなく値の変化で届く。
        if (setting is SSTwoButtonsSetting twoButtons)
        {
            HandleTwoButtons(player, twoButtons);

            return;
        }

        if (setting is not SSKeybindSetting { SyncIsPressed: true } keybind) return;

        // ボイスの切り替えは死んでいても許可する。
        if (keybind.SettingId == ServerSpecifics.ProximityChatKeybindSettingId)
        {
            ActivateHandler.ToggleProximityChat(player);

            return;
        }

        if (keybind.SettingId == ServerSpecifics.CannedChatKeybindSettingId)
        {
            CannedChatMenuApi.Toggle(player);

            return;
        }

        // インベントリを持たない役職向け文字メニューを開いている間だけ、
        // 既存の能力操作キーを選択操作として消費する。閉じれば通常処理へ戻る。
        if (CannedChatMenuApi.TryHandleTextInput(player, keybind.SettingId))
            return;

        if (!player.IsAlive) return;

        if (keybind.SettingId == ServerSpecifics.ItemModeSwitchKeybindSettingId)
        {
            if (player.CurrentItem is { } currentItem &&
                CustomItem.Of(currentItem.Serial) is CustomHybrid hybrid)
                hybrid.TrySwitchModeFromInput(currentItem.Serial, player);

            return;
        }

        if (keybind.SettingId == ServerSpecifics.AbilityUseKeybindSettingId)
        {
            UseActiveAbility(player);

            return;
        }

        if (keybind.SettingId == ServerSpecifics.AbilitySwitchKeybindSettingId)
        {
            AbilityBase.SelectNext(player);

            return;
        }

        if (keybind.SettingId == ServerSpecifics.AbilityOptionPreviousKeybindSettingId)
        {
            SwitchOption(player, AbilityOptionDirection.Previous);

            return;
        }

        if (keybind.SettingId == ServerSpecifics.AbilityOptionNextKeybindSettingId)
        {
            SwitchOption(player, AbilityOptionDirection.Next);

            return;
        }

        if (keybind.SettingId == ServerSpecifics.SuicideButtonKeybindSettingId)
        {
            Suicide(player);
        }
    }

    /// <summary>
    /// 銃を構えている間だけ、自害できます。
    /// </summary>
    private static void Suicide(Player player)
    {
        if (player.CurrentItem is not Firearm firearm)
            return;

        try
        {
            if (firearm.FirearmType is not (FirearmType.None or FirearmType.ParticleDisruptor))
            {
                player.PlayGunSound(firearm.FirearmType);
                SpeakerApi.Play("suicide_shot.ogg", $"{player.NetId}_suicideShotSound", player.Position, true);
            }
        }
        catch (Exception exception)
        {
            // 音が出せなくても自害そのものは通す。
            Log.Warn($"[Slafight] 自害音の再生に失敗しました ({player.Nickname}): {exception.Message}");
        }

        player.Kill("自害した");
    }

    /// <summary>
    /// いま選んでいる能力の選択肢を送ります。
    /// </summary>
    private static void SwitchOption(Player player, AbilityOptionDirection direction)
    {
        if (AbilityBase.Active(player) is not { } ability) return;

        if (!ability.TrySwitchOption(direction)) return;

        player.ShowHint(
            $"<size=22>{ability.DisplayName}: <color=#8fdcff>{ability.SelectedOption?.Name}</color></size>",
            2f);
    }

    /// <summary>
    /// 2 択設定の切り替えを受け取ります。
    /// </summary>
    /// <remarks>
    /// デバッグ表示は管理者向けなので、設定画面から押されても
    /// <see cref="DebugCommand.PermissionNode"/> を持っていなければ有効になりません。
    /// クライアント側の見た目は ON のままになりますが、サーバーが持つ値は OFF に戻すので
    /// <see cref="ServerSpecificUserSettings.IsDebugModeSelected"/> は実際の状態と食い違いません。
    /// </remarks>
    private static void HandleTwoButtons(Player player, SSTwoButtonsSetting setting)
    {
        if (setting.SettingId != ServerSpecifics.DebugModeSettingId) return;

        // A が ON。定義 (ServerSpecifics の SSTwoButtonsSetting) の並びと合わせている。
        bool enabled = !setting.SyncIsB;

        if (enabled && !player.CheckPermission(DebugCommand.PermissionNode))
        {
            setting.SyncIsB = true;
            DebugMode.Set(player, false);
            player.ShowHint("<size=22>デバッグモードは管理者専用です。</size>", 3f);

            return;
        }

        DebugMode.Set(player, enabled);
    }

    /// <summary>
    /// テキスト設定の入力を受け取ります。
    /// </summary>
    private static void HandleText(Player player, int settingId, string text)
    {
        if (settingId == ServerSpecifics.RpNameSettingId)
        {
            RPNameSetter.SetInputName(player, text);

            return;
        }

        if (settingId == ServerSpecifics.SecretPasscodeSettingId)
        {
            RPNameSetter.SetPasscode(player, text);
        }
    }

    /// <summary>
    /// いま選んでいる能力を使います。使えなかった理由は本人にだけ返します。
    /// </summary>
    private static void UseActiveAbility(Player player)
    {
        if (AbilityBase.Active(player) is not { } ability)
            return;

        if (!ability.TryUse(out string failureReason) && failureReason is { Length: > 0 })
            player.ShowHint($"<size=22>{ability.DisplayName}: {failureReason}</size>", 2f);
    }
}
