using System;
using System.Collections.Generic;
using System.Linq;
using CentralAuth;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Permissions.Extensions;
using MEC;
using Slafight_Plugin_EXILED.API.Core.Commands;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.Extensions;
using TMPro;
using UnityEngine;
using UserSettings.ServerSpecific;

using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Server Specifics の定義とページ送りです。キーバインドは全ページに常に載せます。
/// </summary>
public abstract class ServerSpecifics
{
    // 値がそのまま SettingId。クライアントは ID ごとに値を保存するので、既存の番号は変えない。
    private enum SettingKey
    {
        Header = 0,
        ProximityChatKeybind = 3,
        CannedChatKeybind = 4,
        RpName = 5,
        AbilityUseKeybind = 7,
        AbilitySwitchKeybind = 8,
        AbilityOptionPreviousKeybind = 9,
        AbilityOptionNextKeybind = 10,
        ItemModeSwitchKeybind = 12,
        SuicideButtonKeybind = 13,
        DocumentHintDuration = 15,
        AccessibilityMode = 16,
        SecretPasscode = 18,
        DebugMode = 19,
        PageSelector = 20,
        KeybindGroup = 21,
        KeybindSeparator = 22,
        DebugHudLayoutGroup = 23,
        DebugHudX = 24,
        DebugHudY = 25,
        DebugHudTextSize = 26,
        DebugHudCompact = 27,
        DebugHudSectionGroup = 28,
        DebugHudShowRole = 29,
        DebugHudShowPosition = 30,
        DebugHudShowRound = 31,
        DebugHudShowWarhead = 32,
        DebugHudShowItem = 33,
        DebugHudShowInventory = 34,
        DebugHudShowArmor = 35,
        DebugHudShowEffects = 36,
    }

    /// <summary>ページ見出しの SettingId は これ + ページ番号。</summary>
    private const int PageHeaderBaseId = 100;

    private sealed class SettingsPage(string name, Func<Player, bool>? canView, params SettingKey[] keys)
    {
        public string Name { get; } = name;

        public SSGroupHeader CreateHeader() => new(PageHeaderBaseId + Array.IndexOf(Pages, this), Name);

        public SettingKey[] Keys { get; } = keys;

        public bool CanView(Player player) => canView?.Invoke(player) ?? true;
    }

    private static readonly Dictionary<SettingKey, Func<int, ServerSpecificSettingBase>> Factories = new()
    {
        [SettingKey.Header] = id => new SSGroupHeader(
            id,
            "シャープ鯖 機能設定",
            hint: "シャープ鯖特有機能の個人設定です。"),

        [SettingKey.KeybindSeparator] = id => new SSTextArea(
            id,
            "<color=#808080>" + new string('─', 40) + "</color>",
            SSTextArea.FoldoutMode.NotCollapsable,
            null,
            TextAlignmentOptions.Center),

        [SettingKey.KeybindGroup] = id => new SSGroupHeader(
            id,
            "操作キー",
            hint: "どのページを開いていても有効です。"),

        [SettingKey.ProximityChatKeybind] = id => new SSKeybindSetting(
            id,
            "近接チャット",
            KeyCode.V,
            true,
            false,
            hint: "一部の利用可能ロールで、近接チャットを使用するのに必要です。Vを推奨します。"),

        [SettingKey.CannedChatKeybind] = id => new SSKeybindSetting(
            id,
            "定型文通信メニュー",
            KeyCode.Y,
            true,
            false,
            hint: "定型文通信メニューを開閉します。役職によって表示と内容が変わります。Yを推奨します。"),

        [SettingKey.AbilityUseKeybind] = id => new SSKeybindSetting(
            id,
            "アビリティ使用",
            KeyCode.LeftAlt,
            true,
            false,
            hint: "HUDの Ability に表示されている選択中アビリティを発動します。左Altを推奨します。"),

        [SettingKey.AbilitySwitchKeybind] = id => new SSKeybindSetting(
            id,
            "アビリティ切り替え",
            KeyCode.Mouse2,
            true,
            false,
            hint: "HUDに Ability 1/2 のように表示されている場合、次のアビリティへ切り替えます。中マウスボタンを推奨します。"),

        [SettingKey.AbilityOptionPreviousKeybind] = id => new SSKeybindSetting(
            id,
            "アビリティオプション左",
            KeyCode.LeftArrow,
            true,
            false,
            hint: "選択中アビリティに複数のオプションがある場合、前のオプションへ切り替えます。左矢印キーを推奨します。"),

        [SettingKey.AbilityOptionNextKeybind] = id => new SSKeybindSetting(
            id,
            "アビリティオプション右",
            KeyCode.RightArrow,
            true,
            false,
            hint: "選択中アビリティに複数のオプションがある場合、次のオプションへ切り替えます。右矢印キーを推奨します。"),

        [SettingKey.ItemModeSwitchKeybind] = id => new SSKeybindSetting(
            id,
            "アイテムモード切り替え",
            KeyCode.G,
            true,
            false,
            hint: "Hybridアイテムのモードを切り替えます。Gを推奨します。"),

        [SettingKey.SuicideButtonKeybind] = id => new SSKeybindSetting(
            id,
            "自害ボタン",
            KeyCode.K,
            true,
            false,
            hint: "銃器や近接武器を所持しているときに自分に向けて攻撃します。Kを推奨します。"),

        [SettingKey.RpName] = id => new SSPlaintextSetting(
            id,
            "RPキャラクター名",
            "",
            20,
            hint: "RPのキャラ名です。設定した名前の後に本当の名前が表示されます。"),

        [SettingKey.DocumentHintDuration] = id => new SSSliderSetting(
            id,
            "資料表示時間",
            MinDocumentHintDuration,
            MaxDocumentHintDuration,
            DefaultDocumentHintDuration,
            false,
            "0.#",
            "{0}秒",
            hint: "Documentを調べた時に内容を表示する秒数です。"),

        [SettingKey.AccessibilityMode] = id => new SSTwoButtonsSetting(
            id,
            "アクセシビリティモード",
            "OFF",
            "ON",
            hint: "一部のモデルを見やすい表示へ切り替えます。"),

        [SettingKey.SecretPasscode] = id => new SSPlaintextSetting(
            id,
            "シークレットパスコード",
            "00000",
            5,
            hint: "特別な場面で必要となるかもしれません・・・"),

        [SettingKey.DebugMode] = id => new SSTwoButtonsSetting(
            id,
            "デバッグモード",
            "ON",
            "OFF",
            true),

        [SettingKey.DebugHudLayoutGroup] = id => new SSGroupHeader(
            id,
            "配置",
            hint: "デバッグモードを ON にしている間、変更がすぐ反映されます。"),

        [SettingKey.DebugHudX] = id => new SSSliderSetting(
            id,
            "横位置",
            MinDebugHudX,
            MaxDebugHudX,
            DefaultDebugHudX,
            true,
            "0",
            hint: "表示の左端の位置です。0 が画面中央、負で左、正で右です。"),

        [SettingKey.DebugHudY] = id => new SSSliderSetting(
            id,
            "縦位置",
            MinDebugHudY,
            MaxDebugHudY,
            DefaultDebugHudY,
            true,
            "0",
            hint: "表示の 1 行目の位置です。0 が画面上端です。"),

        [SettingKey.DebugHudTextSize] = id => new SSSliderSetting(
            id,
            "文字サイズ",
            MinDebugHudTextSize,
            MaxDebugHudTextSize,
            DefaultDebugHudTextSize,
            true,
            "0"),

        [SettingKey.DebugHudCompact] = id => new SSTwoButtonsSetting(
            id,
            "表示スタイル",
            "詳細",
            "簡易",
            hint: "簡易では字下げされた詳細行を省き、エフェクトを名前だけ 1 行に並べます。"),

        [SettingKey.DebugHudSectionGroup] = id => new SSGroupHeader(id, "表示項目"),

        [SettingKey.DebugHudShowRole] = id => CreateSectionToggle(id, "ロール・チーム"),
        [SettingKey.DebugHudShowPosition] = id => CreateSectionToggle(id, "座標・ルーム"),
        [SettingKey.DebugHudShowRound] = id => CreateSectionToggle(id, "ラウンド"),
        [SettingKey.DebugHudShowWarhead] = id => CreateSectionToggle(id, "核弾頭"),
        [SettingKey.DebugHudShowItem] = id => CreateSectionToggle(id, "装備中アイテム"),
        [SettingKey.DebugHudShowInventory] = id => CreateSectionToggle(id, "インベントリ"),
        [SettingKey.DebugHudShowArmor] = id => CreateSectionToggle(id, "アーマー"),
        [SettingKey.DebugHudShowEffects] = id => CreateSectionToggle(id, "エフェクト"),
    };

    // 並び順がページ選択の並び順。閲覧条件付きのページも途中に置いてよい。
    private static readonly SettingsPage[] Pages =
    [
        new("RP / 表示", null,
            SettingKey.RpName,
            SettingKey.DocumentHintDuration,
            SettingKey.AccessibilityMode),

        new("特殊", null,
            SettingKey.SecretPasscode),

        new("管理者", player => player.CheckPermission(DebugCommand.PermissionNode),
            SettingKey.DebugMode),

        new("デバッグHUD", player => player.CheckPermission(DebugCommand.PermissionNode),
            SettingKey.DebugHudLayoutGroup,
            SettingKey.DebugHudX,
            SettingKey.DebugHudY,
            SettingKey.DebugHudTextSize,
            SettingKey.DebugHudCompact,
            SettingKey.DebugHudSectionGroup,
            SettingKey.DebugHudShowRole,
            SettingKey.DebugHudShowPosition,
            SettingKey.DebugHudShowRound,
            SettingKey.DebugHudShowWarhead,
            SettingKey.DebugHudShowItem,
            SettingKey.DebugHudShowInventory,
            SettingKey.DebugHudShowArmor,
            SettingKey.DebugHudShowEffects),
    ];

    // ページに関係なく常に送る。クライアントは受け取った一覧にあるキーしか監視しないため。
    private static readonly SettingKey[] Keybinds =
    [
        SettingKey.ProximityChatKeybind,
        SettingKey.CannedChatKeybind,
        SettingKey.AbilityUseKeybind,
        SettingKey.AbilitySwitchKeybind,
        SettingKey.AbilityOptionPreviousKeybind,
        SettingKey.AbilityOptionNextKeybind,
        SettingKey.ItemModeSwitchKeybind,
        SettingKey.SuicideButtonKeybind,
    ];

    private static readonly HashSet<int> ManagedSettingIds = [.. Enum.GetValues(typeof(SettingKey)).Cast<int>()];

    /// <summary>netId ごとに最後に送ったページの番号です。</summary>
    private static readonly Dictionary<uint, int> SentPages = [];

    public static readonly int HeaderSettingId = (int)SettingKey.Header;
    public static readonly int PageSelectorSettingId = (int)SettingKey.PageSelector;
    public static readonly int ProximityChatKeybindSettingId = (int)SettingKey.ProximityChatKeybind;
    public static readonly int CannedChatKeybindSettingId = (int)SettingKey.CannedChatKeybind;
    public static readonly int RpNameSettingId = (int)SettingKey.RpName;
    public static readonly int AbilityUseKeybindSettingId = (int)SettingKey.AbilityUseKeybind;
    public static readonly int AbilitySwitchKeybindSettingId = (int)SettingKey.AbilitySwitchKeybind;
    public static readonly int AbilityOptionPreviousKeybindSettingId = (int)SettingKey.AbilityOptionPreviousKeybind;
    public static readonly int AbilityOptionNextKeybindSettingId = (int)SettingKey.AbilityOptionNextKeybind;
    public static readonly int ItemModeSwitchKeybindSettingId = (int)SettingKey.ItemModeSwitchKeybind;
    public static readonly int SuicideButtonKeybindSettingId = (int)SettingKey.SuicideButtonKeybind;
    public static readonly int SecretPasscodeSettingId = (int)SettingKey.SecretPasscode;
    public static readonly int DocumentHintDurationSettingId = (int)SettingKey.DocumentHintDuration;
    public static readonly int AccessibilityModeSettingId = (int)SettingKey.AccessibilityMode;
    public static readonly int DebugModeSettingId = (int)SettingKey.DebugMode;
    public static readonly int DebugHudXSettingId = (int)SettingKey.DebugHudX;
    public static readonly int DebugHudYSettingId = (int)SettingKey.DebugHudY;
    public static readonly int DebugHudTextSizeSettingId = (int)SettingKey.DebugHudTextSize;
    public static readonly int DebugHudCompactSettingId = (int)SettingKey.DebugHudCompact;
    public static readonly int DebugHudShowRoleSettingId = (int)SettingKey.DebugHudShowRole;
    public static readonly int DebugHudShowPositionSettingId = (int)SettingKey.DebugHudShowPosition;
    public static readonly int DebugHudShowRoundSettingId = (int)SettingKey.DebugHudShowRound;
    public static readonly int DebugHudShowWarheadSettingId = (int)SettingKey.DebugHudShowWarhead;
    public static readonly int DebugHudShowItemSettingId = (int)SettingKey.DebugHudShowItem;
    public static readonly int DebugHudShowInventorySettingId = (int)SettingKey.DebugHudShowInventory;
    public static readonly int DebugHudShowArmorSettingId = (int)SettingKey.DebugHudShowArmor;
    public static readonly int DebugHudShowEffectsSettingId = (int)SettingKey.DebugHudShowEffects;
    public const float DefaultDocumentHintDuration = 10f;
    public const float MinDocumentHintDuration = 1f;
    public const float MaxDocumentHintDuration = 60f;
    public const int DefaultDebugHudX = -350;
    public const int MinDebugHudX = -1200;
    public const int MaxDebugHudX = 1200;
    public const int DefaultDebugHudY = 345;
    public const int MinDebugHudY = 0;
    public const int MaxDebugHudY = 1080;
    public const int DefaultDebugHudTextSize = 18;
    public const int MinDebugHudTextSize = 10;
    public const int MaxDebugHudTextSize = 40;

    /// <summary>
    /// 全ページぶんの定義です。<see cref="ServerSpecificSettingsSync.DefinedSettings"/> に入れ、受信値の検証に使われます。
    /// </summary>
    public static ServerSpecificSettingBase[] Settings()
    {
        var settings = new List<ServerSpecificSettingBase>
        {
            Create(SettingKey.Header),
            CreatePageSelector(Pages),
        };

        foreach (var page in Pages)
            settings.AddRange(page.Keys.Select(Create));

        AddKeybinds(settings);

        return [.. settings];
    }

    public static bool IsManagedSettingId(int settingId)
        => ManagedSettingIds.Contains(settingId);

    /// <summary>
    /// 見られる全ページを並べて送ります。クライアントが全項目の値を返し、続くページ選択の値で現在のページへ絞られます。
    /// </summary>
    public static void SendAll(Player player)
    {
        if (!NetGuards.IsReadyClient(player))
            return;

        var pages = VisiblePages(player);
        var settings = new List<ServerSpecificSettingBase>
        {
            Create(SettingKey.Header),
            CreatePageSelector(pages),
        };

        foreach (var page in pages)
        {
            settings.Add(page.CreateHeader());
            settings.AddRange(page.Keys.Select(Create));
        }

        AddKeybinds(settings);

        SentPages.Remove(player.NetId);
        ServerSpecificSettingsSync.SendToPlayer(player.ReferenceHub, [.. settings]);
    }

    /// <summary>
    /// ページ選択の値に対応するページだけを送ります。同じページを続けて送ることはしません。
    /// </summary>
    public static void ShowPage(Player player, int selection)
    {
        if (!NetGuards.IsReadyClient(player))
            return;

        var pages = VisiblePages(player);
        if (pages.Count == 0)
            return;

        int index = Mathf.Clamp(selection, 0, pages.Count - 1);
        if (SentPages.TryGetValue(player.NetId, out int sent) && sent == index)
            return;

        SentPages[player.NetId] = index;

        var page = pages[index];
        var settings = new List<ServerSpecificSettingBase>
        {
            Create(SettingKey.Header),
            CreatePageSelector(pages),
            page.CreateHeader(),
        };

        settings.AddRange(page.Keys.Select(Create));
        AddKeybinds(settings);

        ServerSpecificSettingsSync.SendToPlayer(player.ReferenceHub, [.. settings]);
    }

    /// <summary>
    /// ページ送りの記録を消します。
    /// </summary>
    public static void Forget(Player player)
    {
        if (player?.ReferenceHub != null)
            SentPages.Remove(player.NetId);
    }

    /// <inheritdoc cref="Forget(Player)"/>
    public static void ForgetAll() => SentPages.Clear();

    private static List<SettingsPage> VisiblePages(Player player)
        => Pages.Where(page => page.CanView(player)).ToList();

    private static void AddKeybinds(List<ServerSpecificSettingBase> settings)
    {
        settings.Add(Create(SettingKey.KeybindSeparator));
        settings.Add(Create(SettingKey.KeybindGroup));
        settings.AddRange(Keybinds.Select(Create));
    }

    private static SSDropdownSetting CreatePageSelector(IReadOnlyList<SettingsPage> pages)
    {
        var options = new string[pages.Count];
        for (int i = 0; i < pages.Count; i++)
            options[i] = $"{pages[i].Name} ({i + 1}/{pages.Count})";

        return new SSDropdownSetting(
            (int)SettingKey.PageSelector,
            "ページ",
            options,
            entryType: SSDropdownSetting.DropdownEntryType.HybridLoop,
            hint: "表示する設定のページを切り替えます。");
    }

    private static ServerSpecificSettingBase Create(SettingKey key)
        => Factories[key]((int)key);

    private static SSTwoButtonsSetting CreateSectionToggle(int id, string label)
        => new(id, label, "表示", "非表示");
}

/// <summary>
/// 参加・ページ選択・権限変更に合わせて Server Specifics を送り直します。
/// </summary>
public sealed class ServerSpecificsPageSync : EventHandlerBase
{
    /// <summary>権限変更はイベントの後で反映されるため、少し待ってから送り直す。</summary>
    private const float GroupRefreshDelay = 0.5f;

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        PlayerAuthenticationManager.OnInstanceModeChanged += OnInstanceModeChanged;
        ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSettingValueReceived;
        PlayerHandlers.ChangingGroup += OnChangingGroup;
        PlayerHandlers.Left += OnLeft;
        ServerHandlers.RestartingRound += ServerSpecifics.ForgetAll;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        PlayerAuthenticationManager.OnInstanceModeChanged -= OnInstanceModeChanged;
        ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSettingValueReceived;
        PlayerHandlers.ChangingGroup -= OnChangingGroup;
        PlayerHandlers.Left -= OnLeft;
        ServerHandlers.RestartingRound -= ServerSpecifics.ForgetAll;

        ServerSpecifics.ForgetAll();
    }

    private static void OnInstanceModeChanged(ReferenceHub hub, ClientInstanceMode mode)
    {
        if (mode != ClientInstanceMode.ReadyClient || Player.Get(hub) is not { } player)
            return;

        ServerSpecifics.SendAll(player);
    }

    private static void OnSettingValueReceived(ReferenceHub hub, ServerSpecificSettingBase setting)
    {
        if (setting is not SSDropdownSetting dropdown || dropdown.SettingId != ServerSpecifics.PageSelectorSettingId)
            return;

        if (Player.Get(hub) is { } player)
            ServerSpecifics.ShowPage(player, dropdown.SyncSelectionIndexRaw);
    }

    private static void OnChangingGroup(ChangingGroupEventArgs ev)
    {
        Player player = ev.Player;
        uint netId = player.GetNetId();

        Timing.CallDelayed(GroupRefreshDelay, () =>
        {
            if (player.GetNetId() != netId || !NetGuards.IsReadyClient(player))
                return;

            // 権限を失った人のデバッグ表示は、管理者ページごと消える前に切っておく。
            if (DebugMode.IsEnabled(player) && !player.CheckPermission(DebugCommand.PermissionNode))
                DebugMode.Set(player, false);

            ServerSpecifics.SendAll(player);
        });
    }

    private static void OnLeft(LeftEventArgs ev) => ServerSpecifics.Forget(ev.Player);
}
