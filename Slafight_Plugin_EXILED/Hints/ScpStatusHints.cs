#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Utilities;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.CustomMaps.Core;
using Slafight_Plugin_EXILED.CustomMaps.Features.FacilityControlRoomFunctions;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using AbstractHint = HintServiceMeow.Core.Models.Hints.AbstractHint;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.Hints;

public class ScpStatusHints : IBootstrapHandler
{
    private const string HintIdPrefix = "ScpStatusHints_Status_";
    private const float UpdateInterval = 0.5f;
    private const float GeneratorStartupBlinkSeconds = 3f;
    private const float GeneratorStartupBlinkInterval = 0.8f;

    private static readonly Dictionary<string, StatusHintChannel> Channels = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, TrackedHint> TrackingHints = new(StringComparer.OrdinalIgnoreCase);

    private static CoroutineHandle _coroutineHandle;
    private static int _updateVersion;
    private static bool _registered;

    private sealed class TrackedHint
    {
        public TrackedHint(string key, int slot, int playerId, string hintId, AbstractHint hint)
        {
            Key = key;
            Slot = slot;
            PlayerId = playerId;
            HintId = hintId;
            Hint = hint;
        }

        public string Key { get; }
        public int Slot { get; }
        public int PlayerId { get; }
        public string HintId { get; }
        public AbstractHint Hint { get; }
    }

    /// <summary>
    /// Hint を共有できるレイアウトかどうかの判定キー。
    /// ここが一致するチャンネルは 1 つの Hint に縦積みで結合されるため、互いに重ならない。
    /// </summary>
    private readonly struct HintLayoutKey : IEquatable<HintLayoutKey>
    {
        private readonly HintAlignment _alignment;
        private readonly HintVerticalAlign _verticalAlign;
        private readonly HintSyncSpeed _syncSpeed;
        private readonly bool _resolutionBasedAlign;
        private readonly float _x;
        private readonly float _y;
        private readonly float _lineHeight;

        public HintLayoutKey(StatusHintLayout layout, float resolvedX)
        {
            _alignment = layout.Alignment;
            _verticalAlign = layout.VerticalAlign;
            _syncSpeed = layout.SyncSpeed;
            _resolutionBasedAlign = layout.ResolutionBasedAlign;
            _x = Quantize(resolvedX);
            _y = Quantize(layout.YCoordinate);
            _lineHeight = Quantize(layout.LineHeight);
        }

        public HintAlignment Alignment => _alignment;
        public HintVerticalAlign VerticalAlign => _verticalAlign;
        public HintSyncSpeed SyncSpeed => _syncSpeed;
        public bool ResolutionBasedAlign => _resolutionBasedAlign;
        public float X => _x;
        public float Y => _y;
        public float LineHeight => _lineHeight;

        // 浮動小数の微小な揺れで別グループに分かれないよう 0.1 単位に丸める。
        private static float Quantize(float value)
        {
            return Mathf.Round(value * 10f) / 10f;
        }

        public bool Equals(HintLayoutKey other)
        {
            return _alignment == other._alignment &&
                   _verticalAlign == other._verticalAlign &&
                   _syncSpeed == other._syncSpeed &&
                   _resolutionBasedAlign == other._resolutionBasedAlign &&
                   _x.Equals(other._x) &&
                   _y.Equals(other._y) &&
                   _lineHeight.Equals(other._lineHeight);
        }

        public override bool Equals(object? obj)
        {
            return obj is HintLayoutKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)_alignment;
                hash = (hash * 397) ^ (int)_verticalAlign;
                hash = (hash * 397) ^ (int)_syncSpeed;
                hash = (hash * 397) ^ _resolutionBasedAlign.GetHashCode();
                hash = (hash * 397) ^ _x.GetHashCode();
                hash = (hash * 397) ^ _y.GetHashCode();
                hash = (hash * 397) ^ _lineHeight.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>1 チャンネル分の描画済みブロック。ヘッダーは共有有無が確定してから生成する。</summary>
    private sealed class ChannelSegment
    {
        public ChannelSegment(StatusHintBuildContext context, string body)
        {
            Context = context;
            Body = body;
        }

        public StatusHintBuildContext Context { get; }
        public StatusHintChannel Channel => Context.Channel;
        public string Body { get; }
        public bool WantsGeneratorStatus => Channel.IncludeGeneratorStatus;

        public string Compose(bool shared)
        {
            Context.IsSharedHint = shared;

            var header = BuildHeader(Context);

            if (string.IsNullOrEmpty(header))
                return Body;

            return string.IsNullOrEmpty(Body) ? header : header + "\n" + Body;
        }
    }

    /// <summary>同一レイアウトのチャンネルをまとめて描画する 1 つの Hint 分の情報。</summary>
    private sealed class ViewerHintGroup
    {
        public ViewerHintGroup(HintLayoutKey key, bool allowMerge)
        {
            Key = key;
            AllowMerge = allowMerge;
        }

        public HintLayoutKey Key { get; }
        public bool AllowMerge { get; }
        public List<ChannelSegment> Segments { get; } = new();

        public int FontSize => Segments.Count == 0
            ? 24
            : Segments.Max(segment => segment.Channel.Layout.FontSize);

        public string BuildText()
        {
            var fontSize = FontSize;
            var shared = Segments.Count > 1;
            var parts = new List<string>(Segments.Count);

            foreach (var segment in Segments)
            {
                var text = segment.Compose(shared);
                if (string.IsNullOrEmpty(text))
                    continue;

                var segmentFontSize = segment.Channel.Layout.FontSize;

                // グループの基準サイズと違うチャンネルはタグでサイズを上書きする。
                if (segmentFontSize != fontSize)
                    text = $"<size={segmentFontSize.ToString(CultureInfo.InvariantCulture)}>{text}</size>";

                parts.Add(text);
            }

            // 発電機の状態はチャンネル固有の情報ではないので、
            // どのチャンネルのブロックにも埋めずに Hint 全体の末尾へ 1 回だけ置く。
            if (parts.Count > 0 && Segments.Any(segment => segment.WantsGeneratorStatus))
            {
                var generatorText = BuildGeneratorText().TrimEnd('\r', '\n');

                if (!string.IsNullOrEmpty(generatorText))
                    parts.Add(generatorText);
            }

            return string.Join("\n", parts);
        }
    }

    public static IReadOnlyCollection<StatusHintChannel> RegisteredChannels => Channels.Values.ToList();

    public static void Register()
    {
        Unregister();

        RegisterDefaultChannels();

        _registered = true;
        _updateVersion++;

        Server.RoundStarted += OnRoundStarted;
        Server.RestartingRound += OnRestartingRound;
        Exiled.Events.Handlers.Player.Verified += OnVerified;
        Exiled.Events.Handlers.Player.Left += OnLeft;
        Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;

        if (!Round.IsLobby)
            StartUpdateCoroutine();
    }

    public static void Unregister()
    {
        _registered = false;
        _updateVersion++;

        Server.RoundStarted -= OnRoundStarted;
        Server.RestartingRound -= OnRestartingRound;
        Exiled.Events.Handlers.Player.Verified -= OnVerified;
        Exiled.Events.Handlers.Player.Left -= OnLeft;
        Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;

        Timing.KillCoroutines(_coroutineHandle);
        ClearAll();
        Channels.Clear();
    }

    public static bool RegisterChannel(StatusHintChannel? channel)
    {
        if (channel == null || string.IsNullOrWhiteSpace(channel.Id))
            return false;

        Channels[channel.Id] = channel;
        _updateVersion++;

        if (_registered)
            RefreshSoon();

        return true;
    }

    public static bool TryGetChannel(string? channelId, out StatusHintChannel? channel)
    {
        channel = null;

        return !string.IsNullOrWhiteSpace(channelId) &&
               Channels.TryGetValue(channelId!, out channel);
    }

    public static bool UnregisterChannel(string? channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
            return false;

        var id = channelId!;

        if (!Channels.Remove(id))
            return false;

        _updateVersion++;

        // Hint はチャンネル単位ではなくレイアウト単位で束ねているため、
        // 個別に消さず全て破棄して次のリフレッシュで作り直す。
        ClearAll();

        if (_registered)
            RefreshSoon();

        return true;
    }

    public static void RequestRefresh()
    {
        if (_registered)
            RefreshAll();
    }

    public static void ResetDefaultChannels()
    {
        Channels.Clear();
        ClearAll();
        RegisterDefaultChannels();
        _updateVersion++;

        if (_registered)
            RefreshSoon();
    }

    private static void RegisterDefaultChannels()
    {
        RegisterChannel(CreateScpChannel());
        RegisterChannel(CreateFifthistsChannel());
        RegisterChannel(CreateWarriorsChannel());
    }

    private static StatusHintChannel CreateScpChannel()
    {
        return new StatusHintChannel("scp", IsScpStatusMember)
        {
            Title = "SCP",
            Color = CTeam.SCPs.GetTeamColor(),
            Priority = 0,
            IncludeGeneratorStatus = true,
            CanReceive = IsScpStatusRecipient,
        };
    }

    private static StatusHintChannel CreateFifthistsChannel()
    {
        return new StatusHintChannel("fifthists", IsFifthistStatusMember)
        {
            Title = "第五教会",
            Color = CTeam.Fifthists.GetTeamColor(),
            Priority = 10,
            CanReceive = IsFifthistStatusMember,
            FooterBuilder = BuildFifthistsFooter,
        };
    }

    private static StatusHintChannel CreateWarriorsChannel()
    {
        return new StatusHintChannel("warriors", player => player.GetTeam() == CTeam.Warriors)
        {
            Title = "Warriors",
            Color = CTeam.Warriors.GetTeamColor(),
            Priority = 20,
            CanReceive = player => player.GetTeam() == CTeam.Warriors,
            FooterBuilder = BuildWarriorsFooter,
        };
    }

    private static bool IsScpStatusMember(Player player)
    {
        return (player.GetTeam() == CTeam.SCPs ||
                player.GetCustomRole() == CRoleTypeId.Scp3005) &&
               !CRole.IsTeamNpc(player) && player.IsSafePlayer();
    }

    private static bool IsScpStatusRecipient(Player player)
    {
        return IsScpStatusMember(player) && !player.IsNPC;
    }

    private static bool IsFifthistStatusMember(Player player)
    {
        return (player.GetTeam() == CTeam.Fifthists ||
                player.GetCustomRole() == CRoleTypeId.Scp3005) &&
               !CRole.IsTeamNpc(player) && player.IsSafePlayer();
    }

    private static void OnRoundStarted()
    {
        if (!_registered)
            return;

        StartUpdateCoroutine();
    }

    private static void StartUpdateCoroutine()
    {
        Timing.KillCoroutines(_coroutineHandle);
        _coroutineHandle = Timing.RunCoroutine(UpdateCoroutine());
    }

    private static void OnRestartingRound()
    {
        _updateVersion++;
        Timing.KillCoroutines(_coroutineHandle);
        ClearAll();
    }

    private static void OnVerified(VerifiedEventArgs? ev)
    {
        if (ev?.Player == null)
            return;

        RefreshSoon(0.75f);
    }

    private static void OnLeft(LeftEventArgs? ev)
    {
        if (ev?.Player == null)
            return;

        RemoveHint(ev.Player);
    }

    private static void OnChangingRole(ChangingRoleEventArgs ev)
    {
        if (!ev.IsAllowed || ev.Player is null)
            return;

        RefreshSoon(0.25f);
        RefreshSoon(0.75f);
        RefreshSoon(1.5f);
    }

    private static void RefreshSoon(float delay = 0.05f)
    {
        var version = _updateVersion;

        Timing.CallDelayed(delay, () =>
        {
            if (IsCurrent(version))
                RefreshAll();
        });
    }

    private static IEnumerator<float> UpdateCoroutine()
    {
        while (true)
        {
            if (!_registered)
                yield break;

            if (Round.IsLobby)
            {
                ClearAll();
                yield break;
            }

            RefreshAll();

            yield return Timing.WaitForSeconds(UpdateInterval);
        }
    }

    private static void RefreshAll()
    {
        if (!_registered)
            return;

        // StatusHintBuildContext が IReadOnlyList (インデックスアクセス) を要求するため ToList() が必要。
        var players = Player.List.ToList();
        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var orderedChannels = Channels.Values
            .OrderBy(channel => channel.Priority)
            .ThenBy(channel => channel.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // メンバー一覧は受信者に依存しないので 1 回だけ計算する。
        var membersByChannel = new Dictionary<string, List<Player>>(StringComparer.OrdinalIgnoreCase);
        foreach (var channel in orderedChannels)
            membersByChannel[channel.Id] = GetChannelMembers(channel, players);

        foreach (var viewer in players)
        {
            if (!IsPlayerValid(viewer) || viewer!.IsNPC)
                continue;

            var display = TryGetDisplay(viewer);
            if (display == null)
                continue;

            var groups = BuildViewerGroups(viewer, orderedChannels, membersByChannel, players);

            for (var slot = 0; slot < groups.Count; slot++)
            {
                if (ApplyGroupHint(viewer, display, groups[slot], slot))
                    activeKeys.Add(GetTrackingKey(slot, viewer.Id));
            }
        }

        foreach (var tracked in TrackingHints.Values.ToList())
        {
            if (activeKeys.Contains(tracked.Key))
                continue;

            RemoveTrackedHint(tracked);
        }
    }

    /// <summary>
    /// 受信者 1 人が見る全チャンネルを、レイアウトが一致するものごとに 1 つの Hint へまとめる。
    /// これにより行数が動的に変わっても Hint 同士が重ならない。
    /// </summary>
    private static List<ViewerHintGroup> BuildViewerGroups(
        Player viewer,
        IReadOnlyList<StatusHintChannel> orderedChannels,
        IReadOnlyDictionary<string, List<Player>> membersByChannel,
        IReadOnlyList<Player> players)
    {
        var groups = new List<ViewerHintGroup>();

        foreach (var channel in orderedChannels)
        {
            if (!SafeInvoke(channel.CanReceive, viewer, false))
                continue;

            if (!membersByChannel.TryGetValue(channel.Id, out var members))
                continue;

            var segment = BuildSegment(new StatusHintBuildContext(channel, viewer, members, players));
            if (segment == null)
                continue;

            var layout = channel.Layout;
            var key = new HintLayoutKey(layout, layout.ResolveX(viewer));

            var group = layout.AllowMerge
                ? groups.FirstOrDefault(candidate => candidate.AllowMerge && candidate.Key.Equals(key))
                : null;

            if (group == null)
            {
                group = new ViewerHintGroup(key, layout.AllowMerge);
                groups.Add(group);
            }

            group.Segments.Add(segment);
        }

        return groups;
    }

    private static bool ApplyGroupHint(Player viewer, PlayerDisplay display, ViewerHintGroup group, int slot)
    {
        var text = group.BuildText();
        var key = GetTrackingKey(slot, viewer.Id);

        if (string.IsNullOrEmpty(text))
        {
            if (TrackingHints.TryGetValue(key, out var empty))
                RemoveTrackedHint(empty, viewer);

            return false;
        }

        var hintId = GetHintId(slot);

        if (display.GetHint(hintId) is not Hint hint)
        {
            hint = new Hint
            {
                Id = hintId,
                Text = string.Empty,
            };

            display.AddHint(hint);
        }

        hint.Alignment = group.Key.Alignment;
        hint.YCoordinateAlign = group.Key.VerticalAlign;
        hint.ResolutionBasedAlign = group.Key.ResolutionBasedAlign;
        hint.SyncSpeed = group.Key.SyncSpeed;
        hint.XCoordinate = group.Key.X;
        hint.YCoordinate = group.Key.Y;
        hint.FontSize = group.FontSize;
        hint.LineHeight = group.Key.LineHeight;

        if (hint.Text != text)
            hint.Text = text;

        TrackingHints[key] = new TrackedHint(key, slot, viewer.Id, hintId, hint);
        return true;
    }

    private static List<Player> GetChannelMembers(StatusHintChannel channel, IEnumerable<Player> players)
    {
        var members = players
            .Where(player => IsPlayerValid(player))
            .Where(player => channel.IncludeNpcMembers || !player.IsNPC)
            .Where(player => !CRole.IsTeamNpc(player))
            .Where(player => SafeInvoke(channel.IncludesMember, player, false))
            .ToList();

        try
        {
            return channel.SortMembers(members)
                .Where(player => player != null)
                .ToList();
        }
        catch (Exception e)
        {
            Log.Warn($"[ScpStatusHints] SortMembers failed for channel {channel.Id}: {e.Message}");
            return members.OrderBy(player => player.Id).ToList();
        }
    }

    /// <summary>
    /// 1 チャンネル分の本文を組み立てる。ヘッダーは Hint 共有の有無が決まってから
    /// <see cref="ChannelSegment.Compose"/> 側で付与するため、ここには含めない。
    /// </summary>
    private static ChannelSegment? BuildSegment(StatusHintBuildContext context)
    {
        var channel = context.Channel;

        var visibleMembers = context.Members
            .Where(member => SafeInvoke(channel.CanViewerSeeMember, context.Viewer, member, false))
            .ToList();

        var hiddenMemberCount = 0;

        if (channel.MaxVisibleMembers > 0 && visibleMembers.Count > channel.MaxVisibleMembers)
        {
            hiddenMemberCount = visibleMembers.Count - channel.MaxVisibleMembers;
            visibleMembers = visibleMembers.Take(channel.MaxVisibleMembers).ToList();
        }

        if (visibleMembers.Count == 0 && channel.HideWhenNoVisibleMembers)
            return null;

        var sb = new StringBuilder();

        foreach (var member in visibleMembers)
        {
            var line = BuildMemberLine(context, member);
            if (!string.IsNullOrEmpty(line))
                sb.AppendLine(line);
        }

        if (hiddenMemberCount > 0)
            sb.AppendLine($"... +{hiddenMemberCount}");

        // 発電機の状態は ViewerHintGroup 側で Hint 末尾にまとめて出す。
        var footer = BuildFooter(context);
        if (!string.IsNullOrEmpty(footer))
            sb.Append(footer);

        // 末尾の改行を落とさないと結合時にチャンネル間へ空行が入る。
        var body = sb.ToString().TrimEnd('\r', '\n');

        return new ChannelSegment(context, body);
    }

    private static string BuildHeader(StatusHintBuildContext context)
    {
        if (context.Channel.HeaderBuilder != null)
            return SafeInvoke(context.Channel.HeaderBuilder, context, string.Empty);

        if (string.IsNullOrEmpty(context.Channel.Title))
            return string.Empty;

        // 単独表示ならヘッダー無しでよいが、他チャンネルと同じ Hint に並ぶ場合は
        // どこからどこまでが同じチャンネルか分かるようタイトルを区切りとして出す。
        var showHeader = context.Channel.ShowHeader ||
                         (context.IsSharedHint && context.Channel.ShowHeaderWhenShared);

        if (!showHeader)
            return string.Empty;

        return $"<b><color={context.Channel.Color}>{context.Channel.Title}</color></b>";
    }

    private static string BuildFooter(StatusHintBuildContext context)
    {
        return context.Channel.FooterBuilder == null
            ? string.Empty
            : SafeInvoke(context.Channel.FooterBuilder, context, string.Empty);
    }

    public static string BuildDefaultMemberLine(StatusHintLineContext context)
    {
        var player = context.Subject;
        var sb = new StringBuilder();
        var isScp079 = player.Role.Type is RoleTypeId.Scp079;
        var scp079Role = player.Role as Scp079Role;

        var displayName = ResolveSubjectName(context).RemoveUnityRichTextTag();
        var color = ResolveSubjectColor(context);

        sb.Append("<color=")
            .Append(color)
            .Append(">")
            .Append(displayName)
            .Append("</color> ");

        if (isScp079 && scp079Role != null)
            AppendScp079Status(sb, scp079Role);
        else
            AppendHealthStatus(sb, player);

        if (context.Channel.ShowDistance && player != context.Viewer)
            AppendDistance(sb, context.Viewer, player);

        return sb.ToString();
    }

    private static string BuildMemberLine(StatusHintBuildContext context, Player member)
    {
        var lineContext = new StatusHintLineContext(context, member);

        return context.Channel.LineBuilder == null
            ? BuildDefaultMemberLine(lineContext)
            : SafeInvoke(context.Channel.LineBuilder, lineContext, string.Empty);
    }

    private static string ResolveSubjectName(StatusHintLineContext context)
    {
        if (context.Channel.SubjectNameBuilder != null)
            return SafeInvoke(context.Channel.SubjectNameBuilder, context, string.Empty);

        var customRole = context.Subject.GetCustomRole();

        if (customRole is not CRoleTypeId.None &&
            CRole.TryGet(customRole, out var cRole) &&
            cRole != null)
            return cRole.RoleDisplayName;

        return context.Subject.Role?.Name ?? "Unknown";
    }

    private static string ResolveSubjectColor(StatusHintLineContext context)
    {
        if (context.Channel.SubjectColorBuilder != null)
            return SafeInvoke(context.Channel.SubjectColorBuilder, context, context.Channel.Color);

        return context.Channel.Color;
    }

    private static void AppendScp079Status(StringBuilder sb, Scp079Role role)
    {
        var energyPercentage = role.MaxEnergy > 0f ? role.Energy / role.MaxEnergy : 0f;
        var energyColor = StaticUtils.ToGradientColor(energyPercentage).ToHex();

        sb.Append("[ENERGY: <color=")
            .Append(energyColor)
            .Append(">")
            .Append(role.Energy.ToString("F0", CultureInfo.InvariantCulture))
            .Append("</color>/")
            .Append(role.MaxEnergy.ToString("F0", CultureInfo.InvariantCulture))
            .Append("] (LEVEL: ")
            .Append(role.Level)
            .Append(")");
    }

    private static void AppendHealthStatus(StringBuilder sb, Player player)
    {
        sb.Append("[");

        var healthPercentage = player.MaxHealth > 0f ? player.Health / player.MaxHealth : 0f;
        var healthColor = StaticUtils.ToGradientColor(healthPercentage).ToHex();

        sb.Append("<color=")
            .Append(healthColor)
            .Append(">")
            .Append(player.Health.ToString("F0", CultureInfo.InvariantCulture))
            .Append("</color>/")
            .Append(player.MaxHealth.ToString("F0", CultureInfo.InvariantCulture))
            .Append(" HP")
            .Append("] ");

        if (player.MaxHumeShield <= 0f)
            return;

        sb.Append("(");

        var hsPercentage = player.HumeShield / player.MaxHumeShield;
        var hsColor = StaticUtils.ToGradientColor(hsPercentage).ToHex();

        sb.Append("<color=")
            .Append(hsColor)
            .Append(">")
            .Append(player.HumeShield.ToString("F0", CultureInfo.InvariantCulture))
            .Append("</color>/")
            .Append(player.MaxHumeShield.ToString("F0", CultureInfo.InvariantCulture))
            .Append(" HS")
            .Append(") ");
    }

    private static void AppendDistance(StringBuilder sb, Player viewer, Player subject)
    {
        var distance = (int)Vector3.Distance(GetStatusPosition(viewer), GetStatusPosition(subject));
        sb.Append("距離: ")
            .Append(distance)
            .Append("m");
    }

    private static Vector3 GetStatusPosition(Player player)
    {
        return player.Role is Scp079Role scp079Role
            ? scp079Role.CameraPosition
            : player.Position;
    }

    private static string BuildFifthistsFooter(StatusHintBuildContext context)
    {
        var sb = new StringBuilder();
        var marion = context.AllPlayers.FirstOrDefault(player =>
            IsPlayerValid(player) &&
            player.IsAlive &&
            player.GetCustomRole() == CRoleTypeId.MarionWheeler);

        if (marion != null)
        {
            sb.Append("<color=")
                .Append(context.Channel.Color)
                .Append(">第五目標:</color> Marion Wheeler / ")
                .Append(marion.Zone);

            if (marion != context.Viewer)
            {
                var distance = (int)Vector3.Distance(GetStatusPosition(context.Viewer), marion.Position);
                sb.Append(" / ")
                    .Append(distance)
                    .Append("m");
            }

            sb.AppendLine();
        }

        if (FacilityControlRoom.IsAntiMemeProtocolActive)
            sb.AppendLine("<color=red>反ミームプロトコル: 起動中</color>");
        else if (FacilityControlRoom.HasAntiMemeProtocolActivatedInPast)
            sb.AppendLine("<color=orange>反ミームプロトコル: 起動履歴あり</color>");

        return sb.ToString();
    }

    private static string BuildWarriorsFooter(StatusHintBuildContext context)
    {
        var operation = MapFlags.GetSeason() switch
        {
            SeasonTypeId.Christmas => "SNOW DIVISION",
            SeasonTypeId.April => "CANDY DIVISION",
            SeasonTypeId.Halloween => "HALLOWEEN CANDY DIVISION",
            _ => "DIVISION COMMAND",
        };

        var sb = new StringBuilder();
        sb.Append("<color=")
            .Append(context.Channel.Color)
            .Append(">COMMAND:</color> ")
            .Append(operation)
            .AppendLine();

        if (Warhead.IsInProgress)
        {
            sb.Append("<color=red>ALPHA WARHEAD:</color> T-")
                .Append(Warhead.DetonationTimer.ToString("F0", CultureInfo.InvariantCulture))
                .AppendLine("s");
        }

        return sb.ToString();
    }

    public static string BuildGeneratorText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("発電機の状態：");

        foreach (var generator in Generator.List)
        {
            if (generator is null)
                continue;

            float progress = generator.ActivationTime > 0f
                ? 1f - generator.CurrentTime / generator.ActivationTime
                : 0f;

            progress = Mathf.Clamp01(progress);

            string color;
            string statusText;
            var startupElapsed = Mathf.Max(0f, generator.ActivationTime - generator.CurrentTime);

            if (generator.IsEngaged || progress >= 1f)
            {
                color = "red";
                statusText = "起動済み";
            }
            else if (generator.IsActivating && startupElapsed <= GeneratorStartupBlinkSeconds)
            {
                color = GetGeneratorStartupBlinkColor(startupElapsed);
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:F0}秒)";
            }
            else if (progress == 0f)
            {
                color = "white";
                statusText = "未起動";
            }
            else if (progress < 0.5f)
            {
                color = "yellow";
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:F0}秒)";
            }
            else if (progress < 0.8f)
            {
                color = "orange";
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:F0}秒)";
            }
            else
            {
                color = "red";
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:F0}秒)";
            }

            sb.Append("<color=")
                .Append(color)
                .Append("><b>")
                .Append(generator.Room.Type.TranslateRoomName())
                .Append(": </b>")
                .Append(statusText)
                .Append("</color>")
                .AppendLine();
        }

        return sb.ToString();
    }

    private static void RemoveHint(Player? player)
    {
        if (player == null)
            return;

        foreach (var tracked in TrackingHints.Values
                     .Where(tracked => tracked.PlayerId == player.Id)
                     .ToList())
        {
            RemoveTrackedHint(tracked, player);
        }
    }

    private static void RemoveTrackedHint(TrackedHint tracked, Player? player = null)
    {
        try
        {
            player ??= Player.Get(tracked.PlayerId);

            if (player != null)
            {
                player.RemoveHint(tracked.Hint);

                var display = TryGetDisplay(player);
                var displayHint = display?.GetHint(tracked.HintId);

                if (displayHint != null)
                    player.RemoveHint(displayHint);
            }
        }
        catch (Exception e)
        {
            Log.Debug($"[ScpStatusHints] Failed to remove hint {tracked.HintId}: {e.Message}");
        }
        finally
        {
            TrackingHints.Remove(tracked.Key);
        }
    }

    private static void ClearAll()
    {
        foreach (var tracked in TrackingHints.Values.ToList())
            RemoveTrackedHint(tracked);

        TrackingHints.Clear();
    }

    private static bool IsCurrent(int version)
    {
        return _registered && _updateVersion == version;
    }

    private static bool IsPlayerValid(Player? player)
    {
        try
        {
            return player != null && player.IsConnected && player.ReferenceHub != null && player.IsSafePlayer();
        }
        catch
        {
            return false;
        }
    }

    private static PlayerDisplay? TryGetDisplay(Player player)
    {
        try
        {
            return PlayerDisplay.Get(player.ReferenceHub);
        }
        catch
        {
            return null;
        }
    }

    private static string GetTrackingKey(int slot, int playerId)
    {
        return slot.ToString(CultureInfo.InvariantCulture) + ":" + playerId.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetHintId(int slot)
    {
        return HintIdPrefix + slot.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetGeneratorStartupBlinkColor(float startupElapsed)
    {
        var blinkIndex = Mathf.FloorToInt(startupElapsed / GeneratorStartupBlinkInterval);
        return blinkIndex % 2 == 0 ? "red" : "yellow";
    }

    private static TResult SafeInvoke<TArg, TResult>(Func<TArg, TResult> func, TArg arg, TResult fallback)
    {
        try
        {
            return func(arg);
        }
        catch (Exception e)
        {
            Log.Debug($"[ScpStatusHints] Channel callback failed: {e.Message}");
            return fallback;
        }
    }

    private static TResult SafeInvoke<TArg1, TArg2, TResult>(Func<TArg1, TArg2, TResult> func, TArg1 arg1, TArg2 arg2, TResult fallback)
    {
        try
        {
            return func(arg1, arg2);
        }
        catch (Exception e)
        {
            Log.Debug($"[ScpStatusHints] Channel callback failed: {e.Message}");
            return fallback;
        }
    }
}
