#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Utilities;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using AbstractHint = HintServiceMeow.Core.Models.Hints.AbstractHint;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

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
        public TrackedHint(string key, string channelId, int playerId, string hintId, AbstractHint hint)
        {
            Key = key;
            ChannelId = channelId;
            PlayerId = playerId;
            HintId = hintId;
            Hint = hint;
        }

        public string Key { get; }
        public string ChannelId { get; }
        public int PlayerId { get; }
        public string HintId { get; }
        public AbstractHint Hint { get; }
    }

    public static IReadOnlyCollection<StatusHintChannel> RegisteredChannels => Channels.Values.ToList();

    public static void Register()
    {
        Unregister();

        RegisterDefaultChannels();

        _registered = true;
        _updateVersion++;

        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound += OnRestartingRound;
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

        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRestartingRound;
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
        ClearChannel(id);
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

        // UpdateHint が IReadOnlyList (インデックスアクセス) を要求するため ToList() が必要。
        var players = Player.List.ToList();
        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var channel in Channels.Values.OrderBy(channel => channel.Priority).ThenBy(channel => channel.Id))
        {
            var members = GetChannelMembers(channel, players);
            var recipients = GetChannelRecipients(channel, players);

            foreach (var recipient in recipients)
            {
                if (UpdateHint(recipient, channel, members, players))
                    activeKeys.Add(GetTrackingKey(channel.Id, recipient.Id));
            }
        }

        foreach (var tracked in TrackingHints.Values.ToList())
        {
            if (activeKeys.Contains(tracked.Key))
                continue;

            RemoveTrackedHint(tracked);
        }
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

    private static List<Player> GetChannelRecipients(StatusHintChannel channel, IEnumerable<Player> players)
    {
        return players
            .Where(player => IsPlayerValid(player))
            .Where(player => !player.IsNPC)
            .Where(player => SafeInvoke(channel.CanReceive, player, false))
            .ToList();
    }

    private static bool UpdateHint(
        Player? player,
        StatusHintChannel channel,
        IReadOnlyList<Player> members,
        IReadOnlyList<Player> allPlayers)
    {
        if (!_registered || !IsPlayerValid(player))
            return false;

        if (player!.IsNPC || !SafeInvoke(channel.CanReceive, player, false))
        {
            RemoveHint(player, channel.Id);
            return false;
        }

        var display = TryGetDisplay(player);
        if (display == null)
            return false;

        var context = new StatusHintBuildContext(channel, player, members, allPlayers);
        var text = BuildStatusText(context);

        if (string.IsNullOrEmpty(text) && channel.HideWhenNoVisibleMembers)
        {
            RemoveHint(player, channel.Id);
            return false;
        }

        var hint = EnsureHint(player, display, channel);

        if (hint.Text != text)
            hint.Text = text;

        return true;
    }

    private static AbstractHint EnsureHint(Player player, PlayerDisplay display, StatusHintChannel channel)
    {
        var hintId = GetHintId(channel.Id);

        if (display.GetHint(hintId) is not Hint hint)
        {
            hint = new Hint()
            {
                Id = hintId,
                Text = string.Empty,
            };

            display.AddHint(hint);
        }

        hint.Alignment = channel.Layout.Alignment;
        hint.ResolutionBasedAlign = channel.Layout.ResolutionBasedAlign;
        hint.SyncSpeed = channel.Layout.SyncSpeed;
        hint.XCoordinate = channel.Layout.ResolveX(player);
        hint.YCoordinate = channel.Layout.YCoordinate;
        hint.FontSize = channel.Layout.FontSize;

        var key = GetTrackingKey(channel.Id, player.Id);
        TrackingHints[key] = new TrackedHint(key, channel.Id, player.Id, hintId, hint);
        return hint;
    }

    private static string BuildStatusText(StatusHintBuildContext context)
    {
        var sb = new StringBuilder();

        var visibleMembers = context.Members
            .Where(member => SafeInvoke(context.Channel.CanViewerSeeMember, context.Viewer, member, false))
            .ToList();

        if (context.Channel.MaxVisibleMembers > 0)
            visibleMembers = visibleMembers.Take(context.Channel.MaxVisibleMembers).ToList();

        if (visibleMembers.Count == 0 && context.Channel.HideWhenNoVisibleMembers)
            return string.Empty;

        var header = BuildHeader(context);
        if (!string.IsNullOrEmpty(header))
            sb.AppendLine(header);

        foreach (var member in visibleMembers)
        {
            var line = BuildMemberLine(context, member);
            if (!string.IsNullOrEmpty(line))
                sb.AppendLine(line);
        }

        if (context.Channel.MaxVisibleMembers > 0 && context.Members.Count > visibleMembers.Count)
            sb.AppendLine($"... +{context.Members.Count - visibleMembers.Count}");

        if (context.Channel.IncludeGeneratorStatus)
            sb.Append(BuildGeneratorText());

        var footer = BuildFooter(context);
        if (!string.IsNullOrEmpty(footer))
            sb.Append(footer);

        return sb.ToString();
    }

    private static string BuildHeader(StatusHintBuildContext context)
    {
        if (context.Channel.HeaderBuilder != null)
            return SafeInvoke(context.Channel.HeaderBuilder, context, string.Empty);

        if (!context.Channel.ShowHeader || string.IsNullOrEmpty(context.Channel.Title))
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

    private static void RemoveHint(Player? player, string? channelId = null)
    {
        if (player == null)
            return;

        foreach (var tracked in TrackingHints.Values
                     .Where(tracked => tracked.PlayerId == player.Id &&
                                       (channelId == null || string.Equals(tracked.ChannelId, channelId, StringComparison.OrdinalIgnoreCase)))
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

    private static void ClearChannel(string channelId)
    {
        foreach (var tracked in TrackingHints.Values
                     .Where(tracked => string.Equals(tracked.ChannelId, channelId, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            RemoveTrackedHint(tracked);
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

    private static string GetTrackingKey(string channelId, int playerId)
    {
        return channelId + ":" + playerId.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetHintId(string channelId)
    {
        return HintIdPrefix + SanitizeId(channelId);
    }

    private static string SanitizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "default";

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c) || c is '_' or '-')
                sb.Append(c);
            else
                sb.Append('_');
        }

        return sb.ToString();
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
