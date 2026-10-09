#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Utilities;
using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using Mirror;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.CustomItems.Accesibilities.InventoryChat;

/// <summary>
/// SCP-330 の6分割セレクターを一時的な階層型定型文メニューとして使う API です。
/// 通常所持品は置換せず、8枠上限を越える専用バッグを一時的に追加します。
/// バッグは通常ホイールには並べず、サーバーから直接装備させてセレクターだけを使います。
/// </summary>
public static class CannedChatMenuApi
{
    private const int MaxOptions = 6;
    private const float EquipDelay = 0.08f;
    private const int TextMenuY = 720;

    private static readonly CandyKindID[] SlotCandies =
    [
        CandyKindID.Blue,
        CandyKindID.Green,
        CandyKindID.Yellow,
        CandyKindID.Red,
        CandyKindID.Purple,
        CandyKindID.Pink,
    ];

    // 6分割のキャンディースロットの内周寄り。スクリーン座標は HSM の 1920x1080 基準。
    private static readonly (int X, int Y)[] SlotLabelPositions =
    [
        (155, 315),
        (300, 535),
        (155, 755),
        (-155, 755),
        (-300, 535),
        (-155, 315),
    ];

    private static readonly Dictionary<uint, MenuSession> Sessions = new();
    private static readonly Dictionary<uint, TextMenuSession> TextSessions = new();
    private static IReadOnlyList<CannedChatNode> rootOptions = BuildDefaultMenu();
    private static int nextSessionToken;

    /// <summary>現在使われるルート選択肢です。最大6件です。</summary>
    public static IReadOnlyList<CannedChatNode> RootOptions => rootOptions;

    /// <summary>別機能が独自の定型文ツリーへ差し替えるための登録口です。</summary>
    public static void SetRootOptions(IEnumerable<CannedChatNode> options)
    {
        CannedChatNode[] next = options?.ToArray() ?? throw new ArgumentNullException(nameof(options));
        ValidateOptions(next, "root");
        Shutdown();
        rootOptions = next;
    }

    /// <summary>対象プレイヤーのメニューを開閉します。</summary>
    public static bool Toggle(Player player)
        => IsOpen(player) ? Close(player) : Open(player);

    /// <summary>対象プレイヤーが定型文メニューを開いているか返します。</summary>
    public static bool IsOpen(Player? player)
        => TryFindSession(player, out _) || TryFindTextSession(player, out _);

    /// <summary>定型文メニューを開きます。</summary>
    public static bool Open(Player player)
    {
        if (!Round.IsStarted || !player.IsSafePlayer() || !NetGuards.IsReadyClient(player) || !player.IsAlive)
        {
            player?.ShowHint("<size=20>定型文通信は生存中のみ使用できます。</size>", 2f);
            return false;
        }

        if (IsOpen(player))
            return true;

        if (rootOptions.Count == 0)
        {
            player.ShowHint("<size=20>定型文が登録されていません。</size>", 2f);
            return false;
        }

        ResolvedCommunicationPolicy policy = CommunicationApi.ResolvePolicy(player);
        if (!policy.IsAvailable)
        {
            player.ShowHint("<size=20>現在の陣営・役職では定型文通信を利用できません。</size>", 2f);
            return false;
        }

        if (policy.MenuMode == CommunicationMenuMode.Text)
            return OpenTextMenu(player);

        ReferenceHub hub = player.ReferenceHub;
        Inventory inventory = hub.inventory;
        ushort serial = NextUnusedSerial(inventory);
        var identifier = new ItemIdentifier(ItemType.SCP330, serial);

        if (inventory.CreateItemInstance(identifier, inventory.isLocalPlayer) is not Scp330Bag bag)
        {
            player.ShowHint("<size=20>通信メニューを作成できませんでした。</size>", 2f);
            return false;
        }

        // ServerAddItem は Inventory.MaxSlots (8) で拒否されるため、この試験用バッグだけを
        // 直接登録する。既存8枠はクライアントにも残り、9個目は通常ホイールへ表示されない。
        bag.ServerAddReason = ItemAddReason.AdminCommand;
        bag.Candies = BuildCandySlots(rootOptions.Count);
        inventory.UserInventory.Items.Add(serial, bag);
        inventory.SendItemsNextFrame = true;

        var session = new MenuSession(
            ++nextSessionToken,
            hub,
            serial,
            inventory.CurItem.SerialNumber,
            bag);
        Sessions.Add(hub.netId, session);

        RenderMenu(player, session);
        inventory.ServerSendItems();
        bag.ServerRefreshBag();

        int token = session.Token;
        uint netId = hub.netId;
        Timing.CallDelayed(EquipDelay, () => EquipMenu(netId, serial, token));
        return true;
    }

    /// <summary>対象プレイヤーのメニューを閉じ、開く前の装備へ戻します。</summary>
    public static bool Close(Player player)
    {
        if (TryFindTextSession(player, out TextMenuSession? textSession))
        {
            CloseTextSession(textSession);
            return true;
        }

        if (!TryFindSession(player, out MenuSession? session))
            return false;

        CloseSession(session, restoreHeldItem: true);
        return true;
    }

    /// <summary>
    /// 文字メニュー表示中だけ、能力操作用Server Specific Settingsキーを選択操作として消費します。
    /// </summary>
    internal static bool TryHandleTextInput(Player player, int settingId)
    {
        if (!TryFindTextSession(player, out TextMenuSession? session))
            return false;

        bool isPrevious = settingId == ServerSpecifics.AbilityOptionPreviousKeybindSettingId;
        bool isNext = settingId == ServerSpecifics.AbilityOptionNextKeybindSettingId;
        bool isSelect = settingId == ServerSpecifics.AbilityUseKeybindSettingId;
        bool isBack = settingId == ServerSpecifics.AbilitySwitchKeybindSettingId;
        if (!isPrevious && !isNext && !isSelect && !isBack)
            return false;

        if (!CommunicationApi.CanUse(player))
        {
            CloseTextSession(session);
            player.ShowHint("<size=20>現在の陣営・役職では定型文通信を利用できません。</size>", 2f);
            return true;
        }

        IReadOnlyList<CannedChatNode> options = CurrentOptions(session);
        if (options.Count == 0)
        {
            CloseTextSession(session);
            return true;
        }

        if (isPrevious || isNext)
        {
            int delta = isPrevious ? -1 : 1;
            session.SelectedIndex = (session.SelectedIndex + delta + options.Count) % options.Count;
            RenderTextMenu(player, session);
            return true;
        }

        if (isBack)
        {
            if (session.Path.Count == 0)
                CloseTextSession(session);
            else
            {
                session.Path.RemoveAt(session.Path.Count - 1);
                session.SelectedIndex = 0;
                RenderTextMenu(player, session);
            }

            return true;
        }

        CannedChatNode selected = options[session.SelectedIndex];
        if (selected.Children.Count > 0)
        {
            session.Path.Add(selected);
            session.SelectedIndex = 0;
            RenderTextMenu(player, session);
            return true;
        }

        CloseTextSession(session);
        ExecuteNode(player, selected);
        return true;
    }

    /// <summary>
    /// Harmony パッチから呼ばれます。専用バッグの選択だけを消費し、通常のSCP-330は触りません。
    /// </summary>
    internal static bool TryHandleSelection(NetworkConnection connection, SelectScp330Message message)
    {
        if (connection?.identity is null ||
            !ReferenceHub.TryGetHubNetID(connection.identity.netId, out ReferenceHub hub) ||
            !Sessions.TryGetValue(hub.netId, out MenuSession? session) ||
            !ReferenceEquals(session.Hub, hub) ||
            session.BagSerial != message.Serial)
            return false;

        Player player = Player.Get(hub);
        if (player is null || !player.IsSafePlayer())
        {
            CloseSession(session, restoreHeldItem: false);
            return true;
        }

        if (!CommunicationApi.CanUse(player))
        {
            CloseSession(session, restoreHeldItem: true);
            player.ShowHint("<size=20>現在の陣営・役職では定型文通信を利用できません。</size>", 2f);
            return true;
        }

        if (message.Drop)
        {
            if (session.Path.Count == 0)
                CloseSession(session, restoreHeldItem: true);
            else
            {
                session.Path.RemoveAt(session.Path.Count - 1);
                RefreshMenu(player, session);
            }

            return true;
        }

        IReadOnlyList<CannedChatNode> options = CurrentOptions(session);
        if (message.CandyID < 0 || message.CandyID >= options.Count)
            return true;

        CannedChatNode selected = options[message.CandyID];
        if (selected.Children.Count > 0)
        {
            session.Path.Add(selected);
            RefreshMenu(player, session);
            return true;
        }

        CloseSession(session, restoreHeldItem: true);
        ExecuteNode(player, selected);

        return true;
    }

    internal static void Shutdown()
    {
        foreach (MenuSession session in Sessions.Values.ToArray())
            CloseSession(session, restoreHeldItem: true);

        foreach (TextMenuSession session in TextSessions.Values.ToArray())
            CloseTextSession(session);

        Sessions.Clear();
        TextSessions.Clear();
    }

    private static bool OpenTextMenu(Player player)
    {
        ReferenceHub hub = player.ReferenceHub;
        var session = new TextMenuSession(++nextSessionToken, hub);
        TextSessions.Add(hub.netId, session);
        RenderTextMenu(player, session);
        return true;
    }

    private static void ExecuteNode(Player player, CannedChatNode selected)
    {
        try
        {
            if (selected.Action is not null)
                selected.Action(player);
            else if (!string.IsNullOrWhiteSpace(selected.Message))
                CommunicationApi.Send(player, selected.Message!, category: selected.Channel);
        }
        catch (Exception exception)
        {
            Log.Error($"[CannedChatMenu] '{selected.Label}' action failed for {player.Nickname}: {exception}");
        }
    }

    private static void EquipMenu(uint netId, ushort bagSerial, int token)
    {
        if (!ReferenceHub.TryGetHubNetID(netId, out ReferenceHub hub) ||
            !Sessions.TryGetValue(netId, out MenuSession? session) ||
            session.Token != token || session.BagSerial != bagSerial ||
            !ReferenceEquals(session.Hub, hub) || !NetGuards.IsReadyClient(hub) ||
            !hub.inventory.UserInventory.Items.ContainsKey(bagSerial))
            return;

        hub.inventory.ServerSelectItem(bagSerial);
        session.Transitioning = false;
    }

    private static void RefreshMenu(Player player, MenuSession session)
    {
        IReadOnlyList<CannedChatNode> options = CurrentOptions(session);
        session.Bag.Candies = BuildCandySlots(options.Count);
        session.Bag.SelectedCandyId = Scp330Bag.NoSelectionIndex;
        RenderMenu(player, session);

        session.Transitioning = true;
        Inventory inventory = session.Hub.inventory;
        inventory.ServerSelectItem(0);
        session.Bag.ServerRefreshBag();

        int token = session.Token;
        uint netId = session.NetId;
        ushort serial = session.BagSerial;
        Timing.CallDelayed(EquipDelay, () => EquipMenu(netId, serial, token));
    }

    private static void CloseSession(MenuSession session, bool restoreHeldItem)
    {
        uint netId = session.NetId;
        if (!Sessions.TryGetValue(netId, out MenuSession? current) || !ReferenceEquals(current, session))
            return;

        Sessions.Remove(netId);
        RemoveMenuHints(session.Hub);

        if (!ReferenceHub.TryGetHubNetID(netId, out ReferenceHub liveHub) ||
            !ReferenceEquals(liveHub, session.Hub))
            return;

        Inventory inventory = liveHub.inventory;
        if (inventory.CurItem.SerialNumber == session.BagSerial)
            inventory.ServerSelectItem(0);

        if (inventory.UserInventory.Items.ContainsKey(session.BagSerial))
            inventory.ServerRemoveItem(session.BagSerial, null);

        if (NetGuards.IsReadyClient(liveHub))
            inventory.ServerSendItems();

        if (restoreHeldItem && session.PreviousHeldSerial != 0 &&
            inventory.UserInventory.Items.ContainsKey(session.PreviousHeldSerial))
            inventory.ServerSelectItem(session.PreviousHeldSerial);
    }

    private static IReadOnlyList<CannedChatNode> CurrentOptions(MenuSession session)
        => session.Path.Count == 0 ? rootOptions : session.Path[session.Path.Count - 1].Children;

    private static IReadOnlyList<CannedChatNode> CurrentOptions(TextMenuSession session)
        => session.Path.Count == 0 ? rootOptions : session.Path[session.Path.Count - 1].Children;

    private static List<CandyKindID> BuildCandySlots(int count)
        => SlotCandies.Take(Math.Min(count, MaxOptions)).ToList();

    private static ushort NextUnusedSerial(Inventory inventory)
    {
        ushort serial;

        do
            serial = ItemSerialGenerator.GenerateNext();
        while (inventory.UserInventory.Items.ContainsKey(serial));

        return serial;
    }

    private static void RenderMenu(Player player, MenuSession session)
    {
        if (!NetGuards.IsReadyClient(player))
            return;

        PlayerDisplay display;
        try
        {
            display = PlayerDisplay.Get(player.ReferenceHub);
        }
        catch
        {
            return;
        }

        string path = session.Path.Count == 0
            ? "メイン"
            : string.Join(" › ", session.Path.Select(x => Safe(x.Label)));

        EnsureMenuHint(
            display,
            HudConstId.CannedChatMenuHeader,
            $"<size=23><mark=#11151ddd><color=#7fd6ff><b> 定型文通信 </b></color></mark></size>\n" +
            $"<size=18><mark=#11151ddd> {path} </mark></size>\n" +
            "<size=15><mark=#11151ddd><color=#b8b8b8> 左クリック: 選択　右クリック: 戻る　Esc: 閉じる </color></mark></size>",
            0,
            455,
            23);

        IReadOnlyList<CannedChatNode> options = CurrentOptions(session);
        for (int index = 0; index < MaxOptions; index++)
        {
            string id = $"{HudConstId.CannedChatMenuOptions}_{index}";
            string text = index < options.Count
                ? $"<size=18><mark=#11151ddd> {Safe(options[index].Label)} </mark></size>"
                : string.Empty;
            (int x, int y) = SlotLabelPositions[index];
            EnsureMenuHint(display, id, text, x, y, 18);
        }
    }

    private static void RenderTextMenu(Player player, TextMenuSession session)
    {
        if (!NetGuards.IsReadyClient(player))
            return;

        PlayerDisplay display;
        try
        {
            display = PlayerDisplay.Get(player.ReferenceHub);
        }
        catch
        {
            return;
        }

        IReadOnlyList<CannedChatNode> options = CurrentOptions(session);
        if (options.Count == 0)
            return;

        session.SelectedIndex = Math.Min(session.SelectedIndex, options.Count - 1);
        string path = session.Path.Count == 0
            ? "メイン"
            : string.Join(" › ", session.Path.Select(x => Safe(x.Label)));

        var choices = new StringBuilder();
        for (int index = 0; index < options.Count; index++)
        {
            if (index == 3)
                choices.AppendLine();
            else if (index > 0)
                choices.Append("　");

            string label = Safe(options[index].Label);
            choices.Append(index == session.SelectedIndex
                ? $"<color=#7fd6ff><b>▶ {label} ◀</b></color>"
                : $"<color=#d8d8d8>{label}</color>");
        }

        string text =
            $"<size=20><mark=#11151ddd><color=#7fd6ff><b> 定型文通信 </b></color> {path} </mark></size>\n" +
            $"<size=18><mark=#11151ddd> {choices} </mark></size>\n" +
            "<size=14><mark=#11151ddd> " +
            "<color=#aaffaa>{0}</color>/<color=#aaffaa>{1}</color>:選択　" +
            "<color=#aaffaa>{2}</color>:決定　" +
            "<color=#aaffaa>{3}</color>:戻る　" +
            "<color=#aaffaa>{4}</color>:閉じる </mark></size>";

        if (display.GetHint(HudConstId.CannedChatTextMenu) is not Hint hint)
        {
            hint = new Hint
            {
                Id = HudConstId.CannedChatTextMenu,
                Alignment = HintAlignment.Center,
                YCoordinateAlign = HintVerticalAlign.Top,
                SyncSpeed = HintSyncSpeed.Fastest,
                ResolutionBasedAlign = true,
                XCoordinate = 0,
                YCoordinate = TextMenuY,
                FontSize = 20,
            };
            display.AddHint(hint);
        }

        hint.Text = text;
        hint.Parameters =
        [
            new global::Hints.SSKeybindHintParameter(ServerSpecifics.AbilityOptionPreviousKeybindSettingId),
            new global::Hints.SSKeybindHintParameter(ServerSpecifics.AbilityOptionNextKeybindSettingId),
            new global::Hints.SSKeybindHintParameter(ServerSpecifics.AbilityUseKeybindSettingId),
            new global::Hints.SSKeybindHintParameter(ServerSpecifics.AbilitySwitchKeybindSettingId),
            new global::Hints.SSKeybindHintParameter(ServerSpecifics.CannedChatKeybindSettingId),
        ];
    }

    private static void EnsureMenuHint(PlayerDisplay display, string id, string text, int x, int y, int fontSize)
    {
        if (display.GetHint(id) is not Hint hint)
        {
            hint = new Hint
            {
                Id = id,
                Alignment = HintAlignment.Center,
                YCoordinateAlign = HintVerticalAlign.Middle,
                SyncSpeed = HintSyncSpeed.Fastest,
                ResolutionBasedAlign = true,
                XCoordinate = x,
                YCoordinate = y,
                FontSize = fontSize,
            };
            display.AddHint(hint);
        }

        if (!string.Equals(hint.Text, text, StringComparison.Ordinal))
            hint.Text = text;
    }

    private static void RemoveMenuHints(ReferenceHub hub)
    {
        if (!NetGuards.IsReadyClient(hub))
            return;

        try
        {
            PlayerDisplay display = PlayerDisplay.Get(hub);
            display.RemoveHint(HudConstId.CannedChatMenuHeader);
            for (int index = 0; index < MaxOptions; index++)
                display.RemoveHint($"{HudConstId.CannedChatMenuOptions}_{index}");
        }
        catch
        {
            // 切断・ラウンド破棄中は表示自体も失われるため、後処理を続行する。
        }
    }

    private static void CloseTextSession(TextMenuSession session)
    {
        if (!TextSessions.TryGetValue(session.NetId, out TextMenuSession? current) ||
            !ReferenceEquals(current, session))
            return;

        TextSessions.Remove(session.NetId);
        RemoveTextMenuHint(session.Hub);
    }

    private static void RemoveTextMenuHint(ReferenceHub hub)
    {
        if (!NetGuards.IsReadyClient(hub))
            return;

        try
        {
            PlayerDisplay.Get(hub).RemoveHint(HudConstId.CannedChatTextMenu);
        }
        catch
        {
            // 切断・ラウンド破棄中は表示自体も失われるため、後処理を続行する。
        }
    }

    private static void ValidateOptions(IReadOnlyList<CannedChatNode> options, string path)
    {
        if (options.Count > MaxOptions)
            throw new ArgumentException($"{path} has {options.Count} options; SCP-330 supports at most {MaxOptions}.");

        foreach (CannedChatNode node in options)
        {
            if (node is null)
                throw new ArgumentException($"{path} contains a null option.");

            if (string.IsNullOrWhiteSpace(node.Label))
                throw new ArgumentException($"{path} contains an option without a label.");

            if (node.Children.Count > 0 && (node.Message is not null || node.Action is not null))
                throw new ArgumentException($"{path}/{node.Label} cannot be both a category and an action.");

            if (node.Children.Count == 0 && string.IsNullOrWhiteSpace(node.Message) && node.Action is null)
                throw new ArgumentException($"{path}/{node.Label} has no children or action.");

            ValidateOptions(node.Children, $"{path}/{node.Label}");
        }
    }

    private static IReadOnlyList<CannedChatNode> BuildDefaultMenu()
        =>
        [
            CannedChatNode.Category("状況報告",
                CannedChatNode.Category("敵を発見",
                    CannedChatNode.Phrase("SCP", "SCPを発見しました。"),
                    CannedChatNode.Phrase("財団部隊", "財団部隊を発見しました。"),
                    CannedChatNode.Phrase("武装勢力", "武装した敵対勢力を発見しました。")),
                CannedChatNode.Category("エリア状況",
                    CannedChatNode.Phrase("安全", "このエリアは安全です。"),
                    CannedChatNode.Phrase("危険", "このエリアは危険です。"),
                    CannedChatNode.Phrase("未確認", "この先は未確認です。"))),
            CannedChatNode.Category("要請",
                CannedChatNode.Phrase("援護", "援護を要請します。"),
                CannedChatNode.Phrase("救護", "救護を要請します。"),
                CannedChatNode.Phrase("弾薬", "弾薬が必要です。"),
                CannedChatNode.Phrase("集合", "こちらへ集合してください。")),
            CannedChatNode.Category("指示",
                CannedChatNode.Phrase("前進", "前進してください。"),
                CannedChatNode.Phrase("後退", "後退してください。"),
                CannedChatNode.Phrase("待機", "その場で待機してください。"),
                CannedChatNode.Phrase("追従", "私について来てください。")),
            CannedChatNode.Category("応答",
                CannedChatNode.Phrase("了解", "了解しました。"),
                CannedChatNode.Phrase("拒否", "対応できません。"),
                CannedChatNode.Phrase("感謝", "ありがとうございます。"),
                CannedChatNode.Phrase("謝罪", "申し訳ありません。")),
            CannedChatNode.Category("移動",
                CannedChatNode.Phrase("入口", "入口へ向かいます。"),
                CannedChatNode.Phrase("チェックポイント", "チェックポイントへ向かいます。"),
                CannedChatNode.Phrase("地上", "地上へ向かいます。"),
                CannedChatNode.Phrase("退避", "この場から退避します。")),
            CannedChatNode.Category("その他",
                CannedChatNode.Phrase("はい", "はい。"),
                CannedChatNode.Phrase("いいえ", "いいえ。"),
                CannedChatNode.Phrase("不明", "分かりません。"),
                CannedChatNode.Phrase("もう一度", "もう一度お願いします。")),
        ];

    private static string Safe(string? text)
        => string.IsNullOrEmpty(text) ? string.Empty : text!.Replace("<", "＜").Replace(">", "＞");

    private sealed class MenuSession
    {
        public MenuSession(
            int token,
            ReferenceHub hub,
            ushort bagSerial,
            ushort previousHeldSerial,
            Scp330Bag bag)
        {
            Token = token;
            NetId = hub.netId;
            Hub = hub;
            BagSerial = bagSerial;
            PreviousHeldSerial = previousHeldSerial;
            Bag = bag;
        }

        public int Token { get; }
        public uint NetId { get; }
        public ReferenceHub Hub { get; }
        public ushort BagSerial { get; }
        public ushort PreviousHeldSerial { get; }
        public Scp330Bag Bag { get; }
        public List<CannedChatNode> Path { get; } = new();
        public bool Transitioning { get; set; } = true;
    }

    private sealed class TextMenuSession
    {
        public TextMenuSession(int token, ReferenceHub hub)
        {
            Token = token;
            NetId = hub.netId;
            Hub = hub;
        }

        public int Token { get; }
        public uint NetId { get; }
        public ReferenceHub Hub { get; }
        public List<CannedChatNode> Path { get; } = new();
        public int SelectedIndex { get; set; }
    }

    internal static bool TryGetSession(Player player, out ushort bagSerial, out bool transitioning)
    {
        if (TryFindSession(player, out MenuSession? session))
        {
            bagSerial = session.BagSerial;
            transitioning = session.Transitioning;
            return true;
        }

        bagSerial = 0;
        transitioning = false;
        return false;
    }

    internal static void CloseWithoutRestore(Player player)
    {
        if (TryFindTextSession(player, out TextMenuSession? textSession))
            CloseTextSession(textSession);

        if (TryFindSession(player, out MenuSession? session))
            CloseSession(session, restoreHeldItem: false);
    }

    private static bool TryFindTextSession(Player? player, out TextMenuSession session)
    {
        session = null!;
        if (player?.ReferenceHub is not { } hub)
            return false;

        if (TextSessions.TryGetValue(player.NetId, out TextMenuSession? direct) &&
            ReferenceEquals(direct.Hub, hub))
        {
            session = direct;
            return true;
        }

        session = TextSessions.Values.FirstOrDefault(candidate => ReferenceEquals(candidate.Hub, hub))!;
        return session is not null;
    }

    private static bool TryFindSession(Player? player, out MenuSession session)
    {
        session = null!;
        if (player?.ReferenceHub is not { } hub)
            return false;

        if (Sessions.TryGetValue(player.NetId, out MenuSession? direct) && ReferenceEquals(direct.Hub, hub))
        {
            session = direct;
            return true;
        }

        // Left の発火タイミングによっては Player.NetId が既に 0 のことがあるため、
        // 辞書キーにはせず、保存済み Hub との同一性で一度だけ引き直す。
        session = Sessions.Values.FirstOrDefault(candidate => ReferenceEquals(candidate.Hub, hub))!;
        return session is not null;
    }
}

/// <summary>階層型メニューのカテゴリまたは末端アクションです。</summary>
public sealed class CannedChatNode
{
    private CannedChatNode(
        string label,
        IReadOnlyList<CannedChatNode>? children,
        string? message,
        string category,
        Action<Player>? action)
    {
        Label = label;
        Children = children ?? Array.Empty<CannedChatNode>();
        Message = message;
        Channel = category;
        Action = action;
    }

    public string Label { get; }
    public IReadOnlyList<CannedChatNode> Children { get; }
    public string? Message { get; }
    public string Channel { get; }
    public Action<Player>? Action { get; }

    public static CannedChatNode Category(string label, params CannedChatNode[] children)
        => new(label, children, null, "通信", null);

    public static CannedChatNode Phrase(string label, string message, string category = "通信")
        => new(label, null, message, category, null);

    public static CannedChatNode Command(string label, Action<Player> action)
        => new(label, null, null, "通信", action ?? throw new ArgumentNullException(nameof(action)));
}

/// <summary>メニューの終了条件とイベント購読の対称性を管理します。</summary>
public sealed class CannedChatMenuHandler : EventHandlerBase
{
    public override void RegisterEvents()
    {
        PlayerHandlers.ChangedItem += OnChangedItem;
        PlayerHandlers.ChangingRole += OnChangingRole;
        PlayerHandlers.Left += OnLeft;
        ServerHandlers.WaitingForPlayers += OnWaitingForPlayers;
        ServerHandlers.RestartingRound += OnRestartingRound;
    }

    public override void UnregisterEvents()
    {
        PlayerHandlers.ChangedItem -= OnChangedItem;
        PlayerHandlers.ChangingRole -= OnChangingRole;
        PlayerHandlers.Left -= OnLeft;
        ServerHandlers.WaitingForPlayers -= OnWaitingForPlayers;
        ServerHandlers.RestartingRound -= OnRestartingRound;
        CannedChatMenuApi.Shutdown();
    }

    private static void OnChangedItem(ChangedItemEventArgs ev)
    {
        if (!CannedChatMenuApi.TryGetSession(ev.Player, out ushort bagSerial, out bool transitioning) || transitioning)
            return;

        if (ev.Item?.Serial != bagSerial)
            CannedChatMenuApi.Close(ev.Player);
    }

    private static void OnChangingRole(ChangingRoleEventArgs ev)
        => CannedChatMenuApi.CloseWithoutRestore(ev.Player);

    private static void OnLeft(LeftEventArgs ev)
        => CannedChatMenuApi.CloseWithoutRestore(ev.Player);

    private static void OnWaitingForPlayers() => CannedChatMenuApi.Shutdown();

    private static void OnRestartingRound() => CannedChatMenuApi.Shutdown();
}
