#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Utilities;
using InventorySystem;
using InventorySystem.Items.Radio;
using PlayerRoles;
using Slafight_Plugin_EXILED.CustomItems.Accesibilities.InventoryChat;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using PlayerTeam = PlayerRoles.Team;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// PlayerHUD 上部の「通信」欄へ短いメッセージを流す公開 API です。
/// 定型文メニュー以外の機能も、インベントリやメニュー実装を知らずに利用できます。
/// </summary>
public static class CommunicationApi
{
    /// <summary>プレイヤー発信の通信が届く既定距離です。</summary>
    public const float DefaultRange = 8f;

    private const int MaxVisibleRows = 10;
    private const int MaxTextLength = 44;
    private const int MaxSenderLength = 18;
    private const int MaxCategoryLength = 10;
    private const int MaxPrefixLength = 12;
    private const int HeaderX = -350;
    private const int HeaderY = 505;
    private const int FirstRowY = 535;
    private const int RowHeight = 27;
    private const string HeaderColor = "#62c7ff";
    private const string SenderColor = "#a9ddff";

    private static readonly Dictionary<uint, List<CommunicationEntry>> EntriesByViewer = new();

    private static readonly Dictionary<PlayerTeam, ResolvedCommunicationPolicy> DefaultTeamPolicies =
        new Dictionary<PlayerTeam, ResolvedCommunicationPolicy>
        {
            [PlayerTeam.FoundationForces] = new(true, "財団"),
            [PlayerTeam.ChaosInsurgency] = new(true, "カオス"),
            [PlayerTeam.Scientists] = new(true, "研究員"),
            [PlayerTeam.ClassD] = new(true, "Dクラス"),
            [PlayerTeam.SCPs] = new(false, "SCP"),
            [PlayerTeam.Dead] = new(false, "死亡者"),
            [PlayerTeam.OtherAlive] = new(false, "その他"),
            [PlayerTeam.Flamingos] = new(false, "フラミンゴ"),
        };

    private static readonly Dictionary<PlayerTeam, ResolvedCommunicationPolicy> TeamPolicies =
        new(DefaultTeamPolicies);

    private static readonly Dictionary<RoleTypeId, CommunicationPolicy> RolePolicies = new();
    private static readonly Dictionary<string, CommunicationPolicy> UserPolicies = new(StringComparer.Ordinal);
    private static readonly Dictionary<uint, (ReferenceHub Hub, CommunicationPolicy Policy)> PlayerPolicies = new();

    /// <summary>通信が正常に少なくとも1人へ配信された経路ごとに発火します。</summary>
    public static event Action<CommunicationEntry>? Sent;

    /// <summary>
    /// 発言者名と陣営 Prefix 付きで、送信者から8m以内の受信者へ通信を送ります。
    /// recipients を指定した場合も、その中から距離内の生存者だけへ送ります。
    /// </summary>
    public static int Send(
        Player sender,
        string text,
        IEnumerable<Player>? recipients = null,
        string category = "通信")
        => SendRouted(sender, text, recipients, category);

    /// <summary>
    /// 近接経路とRadio経路へ1回の発言を配信します。
    /// 近接範囲内の受信者には近接表示を優先し、同じ発言を二重には表示しません。
    /// </summary>
    public static int SendRouted(
        Player sender,
        string text,
        IEnumerable<Player>? recipients = null,
        string category = "通信")
    {
        if (sender is null || !sender.IsSafePlayer())
            return 0;

        ResolvedCommunicationPolicy policy = ResolvePolicy(sender);
        if (!policy.IsAvailable)
            return 0;

        Player[] candidates =
        [
            .. (recipients ?? Player.List)
            .Where(viewer => viewer is not null && viewer.IsSafePlayer() && viewer.IsAlive)
            .Distinct()
        ];

        float proximityRange = Mathf.Max(0f, policy.ProximityRange);
        float squaredProximityRange = proximityRange * proximityRange;
        Player[] nearby =
        [
            .. candidates
                .Where(viewer => CanReceiveNearby(sender, viewer, squaredProximityRange))
        ];
        var nearbyNetIds = new HashSet<uint>(nearby.Select(viewer => viewer.NetId));

        int delivered = SendCore(
            Safe(sender.Nickname),
            text,
            nearby,
            ResolveRouteLabel(category, policy.ProximityLabel),
            policy.ProximityPrefix,
            CommunicationRoute.Proximity);

        if (!policy.IsRadioAvailable || !TryGetUsableRadio(sender, out RadioItem senderRadio))
            return delivered;

        Player[] radioOnly =
        [
            .. candidates
                .Where(viewer => !nearbyNetIds.Contains(viewer.NetId) && CanReceiveRadio(sender, senderRadio, viewer))
        ];

        delivered += SendCore(
            Safe(sender.Nickname),
            text,
            radioOnly,
            ResolveRouteLabel(category, policy.RadioLabel),
            policy.RadioPrefix,
            CommunicationRoute.Radio);

        return delivered;
    }

    /// <summary>
    /// 指定距離内へプレイヤー発信の通信を送ります。
    /// バニラ陣営、CustomTeam、CustomRole の順に利用可否と Prefix を解決します。
    /// </summary>
    public static int SendNearby(
        Player sender,
        string text,
        float range = DefaultRange,
        IEnumerable<Player>? recipients = null,
        string category = "通信")
    {
        if (sender is null || !sender.IsSafePlayer())
            return 0;

        ResolvedCommunicationPolicy policy = ResolvePolicy(sender);
        if (!policy.IsAvailable)
            return 0;

        float clampedRange = Mathf.Max(0f, range);
        float squaredRange = clampedRange * clampedRange;
        IEnumerable<Player> candidates = recipients ?? Player.List;
        Player[] nearby =
        [
            .. candidates
                .Where(viewer => CanReceiveNearby(sender, viewer, squaredRange))
                .Distinct()
        ];

        return SendCore(
            Safe(sender.Nickname),
            text,
            nearby,
            ResolveRouteLabel(category, policy.ProximityLabel),
            policy.ProximityPrefix,
            CommunicationRoute.Proximity);
    }

    /// <summary>
    /// 任意の送信元名で距離制限のない通信を送ります。
    /// サーバー全体通知向けです。位置を持つ装置・ギミックには <see cref="SendAt"/> を使います。
    /// </summary>
    public static int Send(
        string senderName,
        string text,
        IEnumerable<Player>? recipients = null,
        string category = "通信")
        => SendCore(
            senderName,
            text,
            recipients ?? Player.List,
            category,
            string.Empty,
            CommunicationRoute.Direct);

    /// <summary>
    /// マップ装置などの任意座標を発信源として、指定距離内の生存者へ通信を送ります。
    /// プレイヤーの陣営利用可否は適用されません。
    /// </summary>
    public static int SendAt(
        string senderName,
        string text,
        Vector3 origin,
        float range = DefaultRange,
        IEnumerable<Player>? recipients = null,
        string category = "通信",
        string? prefix = null)
    {
        float clampedRange = Mathf.Max(0f, range);
        float squaredRange = clampedRange * clampedRange;
        IEnumerable<Player> candidates = recipients ?? Player.List;
        Player[] nearby =
        [
            .. candidates
                .Where(viewer => CanReceiveAt(origin, viewer, squaredRange))
                .Distinct()
        ];

        return SendCore(senderName, text, nearby, category, prefix, CommunicationRoute.Proximity);
    }

    /// <summary>プレイヤーが現在の陣営・役職で定型文通信を利用できるか返します。</summary>
    public static bool CanUse(Player player) => ResolvePolicy(player).IsAvailable;

    /// <summary>
    /// バニラ陣営 → バニラロール → CustomTeam → CustomRole → ユーザー → プレイヤー の順に上書きを適用した設定を返します。
    /// </summary>
    public static ResolvedCommunicationPolicy ResolvePolicy(Player player)
    {
        if (player is null || !player.IsSafePlayer())
            return new ResolvedCommunicationPolicy(false, string.Empty);

        if (!TeamPolicies.TryGetValue(player.Role.Team, out ResolvedCommunicationPolicy resolved))
            resolved = new ResolvedCommunicationPolicy(false, string.Empty);

        if (RolePolicies.TryGetValue(player.Role.Type, out CommunicationPolicy rolePolicy))
            resolved = resolved.Apply(rolePolicy);

        CustomRole? role = CustomRole.Of(player);
        CustomTeam? team = role?.Team ?? CustomTeam.Of(player);

        if (team is not null)
            resolved = resolved.Apply(team.Communication);

        if (role is not null)
            resolved = resolved.Apply(role.Communication);

        if (!string.IsNullOrEmpty(player.UserId) && UserPolicies.TryGetValue(player.UserId, out CommunicationPolicy userPolicy))
            resolved = resolved.Apply(userPolicy);

        if (PlayerPolicies.TryGetValue(player.NetId, out var playerPolicy) &&
            ReferenceEquals(playerPolicy.Hub, player.ReferenceHub))
            resolved = resolved.Apply(playerPolicy.Policy);

        return resolved;
    }

    /// <summary>バニラロールへの上書きを登録・置換します。CustomRoleは自身のCommunicationで設定できます。</summary>
    public static void SetRolePolicy(RoleTypeId role, CommunicationPolicy policy)
    {
        RolePolicies[role] = policy;
        CannedChatMenuApi.Shutdown();
    }

    public static bool RemoveRolePolicy(RoleTypeId role)
    {
        if (!RolePolicies.Remove(role))
            return false;

        CannedChatMenuApi.Shutdown();
        return true;
    }

    /// <summary>UserId単位の上書きを登録・置換します。再接続・ラウンド変更後も有効で、プラグイン終了時に消えます。</summary>
    public static void SetUserPolicy(string userId, CommunicationPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        UserPolicies[userId] = policy;
        foreach (Player player in Player.List.Where(player => player.UserId == userId).ToArray())
            CannedChatMenuApi.Close(player);
    }

    public static bool RemoveUserPolicy(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !UserPolicies.Remove(userId))
            return false;

        foreach (Player player in Player.List.Where(player => player.UserId == userId).ToArray())
            CannedChatMenuApi.Close(player);
        return true;
    }

    /// <summary>現在のプレイヤーへの最優先上書きを登録・置換します。退出・ラウンドリセットで消えます。</summary>
    public static void SetPlayerPolicy(Player player, CommunicationPolicy policy)
    {
        if (player is null || !player.IsSafePlayer())
            throw new ArgumentException("A safe player is required.", nameof(player));

        PlayerPolicies[player.NetId] = (player.ReferenceHub, policy);
        CannedChatMenuApi.Close(player);
    }

    public static bool RemovePlayerPolicy(Player player)
    {
        if (player is null)
            return false;

        // Leftでは既にNetIdが0の場合があるため、保存済みHubとの同一性で解決します。
        uint[] keys =
        [
            .. PlayerPolicies.Where(pair => ReferenceEquals(pair.Value.Hub, player.ReferenceHub))
                .Select(pair => pair.Key)
        ];
        foreach (uint key in keys)
            PlayerPolicies.Remove(key);

        if (keys.Length > 0)
            CannedChatMenuApi.Close(player);
        return keys.Length > 0;
    }

    /// <summary>指定したバニラ陣営の既定可否と Prefix を変更します。</summary>
    public static void SetTeamPolicy(PlayerTeam team, bool isAvailable, string? prefix = null)
    {
        ResolvedCommunicationPolicy current = GetTeamPolicy(team);
        TeamPolicies[team] = current.Apply(new CommunicationPolicy(isAvailable, prefix));
        CannedChatMenuApi.Shutdown();
    }

    /// <summary>指定したバニラ陣営へ詳細な通信設定を重ねます。</summary>
    public static void SetTeamPolicy(PlayerTeam team, CommunicationPolicy policy)
    {
        ResolvedCommunicationPolicy current = GetTeamPolicy(team);
        TeamPolicies[team] = current.Apply(policy);
        CannedChatMenuApi.Shutdown();
    }

    /// <summary>指定したバニラ陣営の現在の既定設定を返します。</summary>
    public static ResolvedCommunicationPolicy GetTeamPolicy(PlayerTeam team)
        => TeamPolicies.TryGetValue(team, out ResolvedCommunicationPolicy policy)
            ? policy
            : new ResolvedCommunicationPolicy(false, string.Empty);

    /// <summary>バニラ陣営の可否と Prefix を組み込み既定値へ戻します。</summary>
    public static void ResetTeamPolicies()
    {
        TeamPolicies.Clear();
        foreach (KeyValuePair<PlayerTeam, ResolvedCommunicationPolicy> pair in DefaultTeamPolicies)
            TeamPolicies.Add(pair.Key, pair.Value);
        CannedChatMenuApi.Shutdown();
    }

    private static int SendCore(
        string senderName,
        string text,
        IEnumerable<Player> recipients,
        string category,
        string? prefix,
        CommunicationRoute route)
    {
        if (!Round.IsStarted)
            return 0;

        string normalized = Normalize(text);
        if (normalized.Length == 0)
            return 0;

        var entry = new CommunicationEntry(
            NormalizeLabel(senderName, "SYSTEM", MaxSenderLength),
            normalized,
            NormalizeLabel(category, "通信", MaxCategoryLength),
            NormalizeOptionalLabel(prefix, MaxPrefixLength),
            route,
            DateTime.UtcNow);

        int delivered = 0;

        foreach (Player viewer in recipients.Where(x => x is not null).Distinct())
        {
            if (!viewer.IsSafePlayer())
                continue;

            uint netId = viewer.NetId;
            if (!EntriesByViewer.TryGetValue(netId, out List<CommunicationEntry> entries))
            {
                entries = new List<CommunicationEntry>(MaxVisibleRows);
                EntriesByViewer.Add(netId, entries);
            }

            while (entries.Count >= MaxVisibleRows)
                entries.RemoveAt(0);

            entries.Add(entry);
            Render(viewer, entries);
            delivered++;
        }

        if (delivered > 0)
        {
            try
            {
                Sent?.Invoke(entry);
            }
            catch (Exception exception)
            {
                Log.Error($"[CommunicationApi] Sent subscriber failed: {exception}");
            }
        }

        return delivered;
    }

    /// <summary>指定プレイヤーの通信履歴と表示行を消します。見出しは残します。</summary>
    public static void Clear(Player player)
    {
        if (player is null)
            return;

        EntriesByViewer.Remove(player.NetId);
        if (Round.IsStarted)
            Render(player, []);
        else
            RemoveHints(player);
    }

    internal static void Ensure(Player player)
    {
        if (!player.IsSafePlayer())
            return;

        if (!Round.IsStarted)
        {
            RemoveHints(player);
            return;
        }

        EntriesByViewer.TryGetValue(player.NetId, out List<CommunicationEntry>? entries);
        Render(player, entries ?? Enumerable.Empty<CommunicationEntry>());
    }

    internal static void Remove(Player player)
    {
        if (player is null)
            return;

        EntriesByViewer.Remove(player.NetId);
        RemovePlayerPolicy(player);
    }

    internal static void Reset(bool removeHints = false)
    {
        if (removeHints)
        {
            foreach (Player player in Player.List)
                RemoveHints(player);
        }

        EntriesByViewer.Clear();
        PlayerPolicies.Clear();
        CannedChatMenuApi.Shutdown();
    }

    internal static void Shutdown()
    {
        foreach (Player player in Player.List)
            RemoveHints(player);

        EntriesByViewer.Clear();
        Sent = null;
        PlayerPolicies.Clear();
        UserPolicies.Clear();
        RolePolicies.Clear();
        ResetTeamPolicies();
    }

    private static void Render(Player viewer, IEnumerable<CommunicationEntry> source)
    {
        if (!Round.IsStarted)
        {
            RemoveHints(viewer);
            return;
        }

        if (!NetGuards.IsReadyClient(viewer))
            return;

        PlayerDisplay display;
        try
        {
            display = PlayerDisplay.Get(viewer.ReferenceHub);
        }
        catch
        {
            return;
        }

        EnsureHint(
            display,
            HudConstId.PlayerHUD_CommunicationHeader,
            $"<size=20><color={HeaderColor}>━━ 通信 ━━</color></size>",
            20,
            HeaderX,
            HeaderY);

        CommunicationEntry[] entries = [.. source.Take(MaxVisibleRows)];
        for (int index = 0; index < MaxVisibleRows; index++)
        {
            string id = $"{HudConstId.PlayerHUD_CommunicationRows}_{index}";
            string row = index < entries.Length ? Format(entries[index]) : string.Empty;
            EnsureHint(display, id, row, 17, HeaderX, FirstRowY + (index * RowHeight));
        }
    }

    private static void EnsureHint(PlayerDisplay display, string id, string text, int fontSize, int x, int y)
    {
        if (display.GetHint(id) is not Hint hint)
        {
            hint = new Hint
            {
                Id = id,
                Alignment = HintAlignment.Left,
                YCoordinateAlign = HintVerticalAlign.Top,
                SyncSpeed = HintSyncSpeed.Fast,
                ResolutionBasedAlign = true,
                FontSize = fontSize,
                XCoordinate = x,
                YCoordinate = y,
            };
            display.AddHint(hint);
        }

        if (!string.Equals(hint.Text, text, StringComparison.Ordinal))
            hint.Text = text;
    }

    private static void RemoveHints(Player player)
    {
        if (!NetGuards.IsReadyClient(player))
            return;

        try
        {
            PlayerDisplay display = PlayerDisplay.Get(player.ReferenceHub);
            display.RemoveHint(HudConstId.PlayerHUD_CommunicationHeader);
            for (int index = 0; index < MaxVisibleRows; index++)
                display.RemoveHint($"{HudConstId.PlayerHUD_CommunicationRows}_{index}");
        }
        catch
        {
            // 切断・プラグイン停止中はクライアント側表示も失われるため続行する。
        }
    }

    private static string Format(CommunicationEntry entry)
    {
        string prefix = entry.Prefix.Length == 0 ? string.Empty : $"[{entry.Prefix}] ";
        return $"<size=17><color={SenderColor}>[{entry.Category}] {prefix}{entry.Sender}</color>: {entry.Text}</size>";
    }

    private static bool CanReceiveNearby(Player sender, Player? viewer, float squaredRange)
    {
        if (viewer is null || !viewer.IsSafePlayer() || !viewer.IsAlive)
            return false;

        if (ReferenceEquals(sender.ReferenceHub, viewer.ReferenceHub))
            return true;

        return CanReceiveAt(sender.Position, viewer, squaredRange);
    }

    private static bool CanReceiveAt(Vector3 origin, Player? viewer, float squaredRange)
        => viewer is not null && viewer.IsSafePlayer() && viewer.IsAlive &&
           (origin - viewer.Position).sqrMagnitude <= squaredRange;

    /// <summary>プレイヤーが電源ONかつ電池残量ありのRadioを所持しているか返します。</summary>
    public static bool TryGetUsableRadio(Player? player, out RadioItem radio)
    {
        radio = null!;
        if (player is null || !player.IsSafePlayer() ||
            !RadioMessages.GetRadio(player.ReferenceHub, out RadioItem found) || !found.IsUsable)
            return false;

        radio = found;
        return true;
    }

    private static bool CanReceiveRadio(Player sender, RadioItem senderRadio, Player viewer)
    {
        if (!TryGetUsableRadio(viewer, out RadioItem receiverRadio))
            return false;

        int rangeId = Mathf.Max(senderRadio._rangeId, receiverRadio._rangeId);
        RadioItem rangeSource = senderRadio;
        if (InventoryItemLoader.TryGetItem(ItemType.Radio, out RadioItem template))
            rangeSource = template;

        if (rangeSource.Ranges is null || rangeId < 0 || rangeId >= rangeSource.Ranges.Length)
            return false;

        return rangeSource.Ranges[rangeId].CheckRange(sender.Position, viewer.Position, out _);
    }

    private static string ResolveRouteLabel(string requestedCategory, string routeLabel)
        => string.Equals(requestedCategory, "通信", StringComparison.Ordinal)
            ? routeLabel
            : requestedCategory;

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string normalized = string.Join(" ", text!
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Split([' '], StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length > MaxTextLength)
            normalized = normalized.Substring(0, MaxTextLength - 1) + "…";

        return Safe(normalized);
    }

    private static string Safe(string? text)
        => string.IsNullOrEmpty(text)
            ? string.Empty
            : text!.Replace("<", "＜").Replace(">", "＞");

    private static string NormalizeLabel(string? text, string fallback, int maxLength)
    {
        string normalized = Safe(string.IsNullOrWhiteSpace(text) ? fallback : text!.Trim());
        return normalized.Length <= maxLength
            ? normalized
            : normalized.Substring(0, maxLength - 1) + "…";
    }

    private static string NormalizeOptionalLabel(string? text, int maxLength)
    {
        string normalized = Safe(text?.Trim());
        return normalized.Length <= maxLength
            ? normalized
            : normalized.Substring(0, maxLength - 1) + "…";
    }
}

/// <summary>通信欄に保存される、描画に依存しない1件のメッセージです。</summary>
public sealed class CommunicationEntry
{
    public CommunicationEntry(string sender, string text, string category, DateTime sentAtUtc)
        : this(sender, text, category, string.Empty, CommunicationRoute.Direct, sentAtUtc)
    {
    }

    public CommunicationEntry(string sender, string text, string category, string prefix, DateTime sentAtUtc)
        : this(sender, text, category, prefix, CommunicationRoute.Direct, sentAtUtc)
    {
    }

    public CommunicationEntry(
        string sender,
        string text,
        string category,
        string prefix,
        CommunicationRoute route,
        DateTime sentAtUtc)
    {
        Sender = sender;
        Text = text;
        Category = category;
        Prefix = prefix ?? string.Empty;
        Route = route;
        SentAtUtc = sentAtUtc;
    }

    public string Sender { get; }
    public string Text { get; }
    public string Category { get; }
    public string Prefix { get; }
    public CommunicationRoute Route { get; }
    public DateTime SentAtUtc { get; }
}

/// <summary>通信行がどの経路で受信されたか。</summary>
public enum CommunicationRoute
{
    Direct,
    Proximity,
    Radio,
}

/// <summary>通信HUDの生成とラウンド／退出時の後始末を担当します。</summary>
public sealed class CommunicationHudHandler : EventHandlerBase
{
    public override void RegisterEvents()
    {
        PlayerHandlers.Verified += OnVerified;
        PlayerHandlers.Left += OnLeft;
        ServerHandlers.RoundStarted += OnRoundStarted;
        ServerHandlers.WaitingForPlayers += OnRoundReset;
        ServerHandlers.RestartingRound += OnRestartingRound;
    }

    public override void UnregisterEvents()
    {
        PlayerHandlers.Verified -= OnVerified;
        PlayerHandlers.Left -= OnLeft;
        ServerHandlers.RoundStarted -= OnRoundStarted;
        ServerHandlers.WaitingForPlayers -= OnRoundReset;
        ServerHandlers.RestartingRound -= OnRestartingRound;
        CommunicationApi.Shutdown();
    }

    private static void OnVerified(VerifiedEventArgs ev) => CommunicationApi.Ensure(ev.Player);

    private static void OnLeft(LeftEventArgs ev) => CommunicationApi.Remove(ev.Player);

    private static void OnRoundStarted()
    {
        foreach (Player player in Player.List)
            CommunicationApi.Ensure(player);
    }

    private static void OnRoundReset() => CommunicationApi.Reset(removeHints: true);

    private static void OnRestartingRound() => CommunicationApi.Reset(removeHints: true);
}
