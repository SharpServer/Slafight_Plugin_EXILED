using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles.Voice;
using Slafight_Plugin_EXILED.API.Interface;
using ExiledIntercom = Exiled.API.Features.Intercom;
using GameIntercom = PlayerRoles.Voice.Intercom;
using PlayerHandler = Exiled.Events.Handlers.Player;
using ServerHandler = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features;

[Flags]
public enum IntercomControlFlags
{
    None = 0,
    BlockNormalUse = 1 << 0,
    ForceTimeout = 1 << 1,
    ForceInfiniteCooldown = 1 << 2,
    Unavailable = BlockNormalUse | ForceTimeout | ForceInfiniteCooldown,
}

[Flags]
public enum IntercomAvailabilityFlags
{
    None = 0,
    NotFound = 1 << 0,
    ApiBlocked = 1 << 1,
    NotReady = 1 << 2,
    Starting = 1 << 3,
    InUse = 1 << 4,
    Cooldown = 1 << 5,
    PlayerMissing = 1 << 6,
    PlayerInvalid = 1 << 7,
    PlayerDead = 1 << 8,
    PlayerNotHuman = 1 << 9,
    PlayerIntercomMuted = 1 << 10,
    PlayerOutOfRange = 1 << 11,
}

public sealed class IntercomStatus
{
    public bool Exists { get; init; }
    public IntercomState State { get; init; }
    public bool InUse { get; init; }
    public Player? Speaker { get; init; }
    public double RemainingCooldown { get; init; }
    public float SpeechRemainingTime { get; init; }
    public string DisplayText { get; init; } = string.Empty;
    public IntercomControlFlags ControlFlags { get; init; }
    public IntercomAvailabilityFlags AvailabilityFlags { get; init; }
    public IReadOnlyCollection<string> ControlOwners { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> DisplayOwners { get; init; } = Array.Empty<string>();

    public bool IsAvailable => AvailabilityFlags == IntercomAvailabilityFlags.None;
}

public static class IntercomApi
{
    public const string DefaultOwner = "Slafight.IntercomApi";

    private static readonly Dictionary<string, IntercomControlFlags> ControlFlagsByOwner = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CoroutineHandle> ControlFlagTimers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DisplayOverride> DisplayOverrides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CoroutineHandle> DisplayTimers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, PlayerOverrideState> OverrideOwnersByPlayer = new();
    private static readonly Dictionary<string, CoroutineHandle> OverrideTimers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, PlayerMuteState> MuteOwnersByPlayer = new();
    private static readonly Dictionary<string, CoroutineHandle> MuteTimers = new(StringComparer.OrdinalIgnoreCase);

    private static long _displaySequence;
    private static bool _forcedUnavailableApplied;
    private static bool _displayOverrideApplied;

    public static bool Exists => GameIntercom._singletonSet && GameIntercom._singleton != null;

    public static IntercomState State => Exists ? ExiledIntercom.State : IntercomState.NotFound;

    public static bool InUse => Exists && ExiledIntercom.InUse;

    public static Player? Speaker => Exists ? ExiledIntercom.Speaker : null;

    public static double RemainingCooldown => Exists ? Math.Max(0d, ExiledIntercom.RemainingCooldown) : 0d;

    public static float SpeechRemainingTime => Exists ? ExiledIntercom.SpeechRemainingTime : 0f;

    public static string DisplayText => Exists ? ExiledIntercom.DisplayText ?? string.Empty : string.Empty;

    public static IntercomControlFlags EffectiveControlFlags =>
        ControlFlagsByOwner.Values.Aggregate(IntercomControlFlags.None, (current, flags) => current | flags);

    public static bool IsAvailable => GetAvailability() == IntercomAvailabilityFlags.None;

    public static bool CanUse(Player? player, out IntercomAvailabilityFlags flags)
    {
        flags = GetAvailability(player, requirePlayer: true);
        return flags == IntercomAvailabilityFlags.None;
    }

    public static bool CanUse(Player? player = null)
        => GetAvailability(player, player != null) == IntercomAvailabilityFlags.None;

    public static IntercomAvailabilityFlags GetAvailability(Player? player = null, bool requirePlayer = false)
    {
        var flags = IntercomAvailabilityFlags.None;

        if (!Exists)
            flags |= IntercomAvailabilityFlags.NotFound;

        if (HasControlFlag(IntercomControlFlags.BlockNormalUse))
            flags |= IntercomAvailabilityFlags.ApiBlocked;

        switch (State)
        {
            case IntercomState.Ready:
                if (RemainingCooldown > 0.05d)
                    flags |= IntercomAvailabilityFlags.Cooldown;
                break;
            case IntercomState.Starting:
                flags |= IntercomAvailabilityFlags.Starting;
                break;
            case IntercomState.InUse:
                flags |= IntercomAvailabilityFlags.InUse;
                break;
            case IntercomState.Cooldown:
                flags |= IntercomAvailabilityFlags.Cooldown;
                break;
            case IntercomState.NotFound:
                flags |= IntercomAvailabilityFlags.NotFound;
                break;
            default:
                flags |= IntercomAvailabilityFlags.NotReady;
                break;
        }

        if (player == null)
            return requirePlayer ? flags | IntercomAvailabilityFlags.PlayerMissing : flags;

        if (player.ReferenceHub == null || !player.IsConnected)
            flags |= IntercomAvailabilityFlags.PlayerInvalid;
        else
        {
            if (!player.IsAlive)
                flags |= IntercomAvailabilityFlags.PlayerDead;

            if (!player.IsHuman)
                flags |= IntercomAvailabilityFlags.PlayerNotHuman;

            if (player.IsIntercomMuted)
                flags |= IntercomAvailabilityFlags.PlayerIntercomMuted;

            if (Exists && !IsPlayerInRange(player))
                flags |= IntercomAvailabilityFlags.PlayerOutOfRange;
        }

        return flags;
    }

    public static IntercomStatus GetStatus(Player? player = null)
    {
        return new IntercomStatus
        {
            Exists = Exists,
            State = State,
            InUse = InUse,
            Speaker = Speaker,
            RemainingCooldown = RemainingCooldown,
            SpeechRemainingTime = SpeechRemainingTime,
            DisplayText = DisplayText,
            ControlFlags = EffectiveControlFlags,
            AvailabilityFlags = GetAvailability(player, player != null),
            ControlOwners = GetControlOwners(),
            DisplayOwners = GetDisplayOwners(),
        };
    }

    public static bool IsPlayerInRange(Player? player)
    {
        if (!Exists || player?.ReferenceHub == null)
            return false;

        return GameIntercom._singleton.CheckRange(player.ReferenceHub);
    }

    public static bool SetDisplayText(string? text)
    {
        if (!Exists)
            return false;

        ExiledIntercom.DisplayText = text ?? string.Empty;
        return true;
    }

    public static bool SetDisplayOverride(string owner, string? text, int priority = 0, float? durationSeconds = null)
    {
        owner = NormalizeOwner(owner);
        DisplayOverrides[owner] = new DisplayOverride(text ?? string.Empty, priority, ++_displaySequence);
        ScheduleOwnerTimer(DisplayTimers, owner, durationSeconds, () => ClearDisplayOverride(owner));
        ApplyDisplayOverride();
        return Exists;
    }

    public static bool ClearDisplayOverride(string owner)
    {
        owner = NormalizeOwner(owner);
        KillOwnerTimer(DisplayTimers, owner);
        bool removed = DisplayOverrides.Remove(owner);
        ApplyDisplayOverride();
        return removed;
    }

    public static IReadOnlyCollection<string> GetDisplayOwners()
        => DisplayOverrides.Keys.OrderBy(owner => owner, StringComparer.OrdinalIgnoreCase).ToArray();

    public static bool SetCooldown(double seconds)
    {
        if (!Exists)
            return false;

        ExiledIntercom.RemainingCooldown = Math.Max(0d, seconds);
        return true;
    }

    public static bool ResetCooldown()
        => SetCooldown(0d);

    public static bool SetSpeechRemainingTime(float seconds)
    {
        if (!Exists)
            return false;

        ExiledIntercom.SpeechRemainingTime = Math.Max(0f, seconds);
        return true;
    }

    public static bool Reset(bool clearDisplay = false)
    {
        if (!Exists)
            return false;

        ExiledIntercom.RemainingCooldown = 0d;
        ExiledIntercom.Reset();

        if (clearDisplay)
            SetDisplayText(string.Empty);

        _forcedUnavailableApplied = false;
        return true;
    }

    public static bool Timeout(string? displayText = null, double? cooldownSeconds = null)
    {
        if (!Exists)
            return false;

        ExiledIntercom.Timeout();

        if (cooldownSeconds.HasValue)
            ExiledIntercom.RemainingCooldown = Math.Max(0d, cooldownSeconds.Value);

        if (displayText != null)
            ExiledIntercom.DisplayText = displayText;

        return true;
    }

    public static bool SetControlFlags(string owner, IntercomControlFlags flags, float? durationSeconds = null)
    {
        owner = NormalizeOwner(owner);

        if (flags == IntercomControlFlags.None)
            return ClearControlFlags(owner);

        ControlFlagsByOwner[owner] = flags;
        ScheduleOwnerTimer(ControlFlagTimers, owner, durationSeconds, () => ClearControlFlags(owner));
        ApplyControlFlags();
        return Exists;
    }

    public static bool AddControlFlags(string owner, IntercomControlFlags flags, float? durationSeconds = null)
    {
        owner = NormalizeOwner(owner);
        ControlFlagsByOwner.TryGetValue(owner, out var current);
        return SetControlFlags(owner, current | flags, durationSeconds);
    }

    public static bool RemoveControlFlags(string owner, IntercomControlFlags flags)
    {
        owner = NormalizeOwner(owner);

        if (!ControlFlagsByOwner.TryGetValue(owner, out var current))
            return false;

        current &= ~flags;

        if (current == IntercomControlFlags.None)
            return ClearControlFlags(owner);

        ControlFlagsByOwner[owner] = current;
        ApplyControlFlags();
        return true;
    }

    public static bool ClearControlFlags(string owner)
    {
        owner = NormalizeOwner(owner);
        KillOwnerTimer(ControlFlagTimers, owner);
        bool removed = ControlFlagsByOwner.Remove(owner);
        ApplyControlFlags();
        return removed;
    }

    public static bool SetUnavailable(
        string owner,
        string? displayText = null,
        bool infiniteCooldown = true,
        float? durationSeconds = null)
    {
        var flags = IntercomControlFlags.BlockNormalUse | IntercomControlFlags.ForceTimeout;

        if (infiniteCooldown)
            flags |= IntercomControlFlags.ForceInfiniteCooldown;

        bool result = SetControlFlags(owner, flags, durationSeconds);

        if (displayText != null)
            SetDisplayOverride(owner, displayText, durationSeconds: durationSeconds);

        return result;
    }

    public static void ClearOwner(string owner)
    {
        owner = NormalizeOwner(owner);
        ClearControlFlags(owner);
        ClearDisplayOverride(owner);
        ReleaseAllOverrides(owner);
        ReleaseAllMutes(owner);
    }

    public static bool HasControlFlag(IntercomControlFlags flag)
        => (EffectiveControlFlags & flag) != 0;

    public static IReadOnlyCollection<string> GetControlOwners(IntercomControlFlags flag = IntercomControlFlags.None)
    {
        return ControlFlagsByOwner
            .Where(pair => flag == IntercomControlFlags.None || (pair.Value & flag) != 0)
            .Select(pair => pair.Key)
            .OrderBy(owner => owner, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool SetOverride(Player? player, bool enabled, string? owner = null, float? durationSeconds = null)
        => enabled
            ? GrantOverride(player, owner, durationSeconds)
            : ReleaseOverride(player, owner);

    public static bool GrantOverride(Player? player, string? owner = null, float? durationSeconds = null)
    {
        if (!IsValidPlayer(player) || !Exists)
            return false;

        owner = NormalizeOwner(owner);

        bool wasOverriddenBeforeApi = ExiledIntercom.HasOverride(player!);

        if (!ExiledIntercom.TrySetOverride(player!, true))
            return false;

        if (!OverrideOwnersByPlayer.TryGetValue(player!.Id, out var state))
        {
            state = new PlayerOverrideState(wasOverriddenBeforeApi);
            OverrideOwnersByPlayer[player.Id] = state;
        }

        state.Owners.Add(owner);
        SchedulePlayerOwnerTimer(OverrideTimers, player.Id, owner, durationSeconds, () => ReleaseOverride(player.Id, owner));
        return true;
    }

    public static bool ReleaseOverride(Player? player, string? owner = null)
    {
        if (player == null)
            return false;

        return ReleaseOverride(player.Id, NormalizeOwner(owner));
    }

    public static bool ReleaseOverride(int playerId, string? owner = null)
    {
        owner = NormalizeOwner(owner);
        KillPlayerOwnerTimer(OverrideTimers, playerId, owner);

        if (!OverrideOwnersByPlayer.TryGetValue(playerId, out var state))
            return false;

        bool removed = state.Owners.Remove(owner);

        if (state.Owners.Count > 0)
            return removed;

        OverrideOwnersByPlayer.Remove(playerId);

        var player = FindPlayer(playerId);
        if (player?.ReferenceHub != null && Exists && !state.WasOverriddenBeforeApi)
            ExiledIntercom.TrySetOverride(player, false);

        return removed;
    }

    public static void ReleaseAllOverrides(string owner)
    {
        owner = NormalizeOwner(owner);

        foreach (var playerId in OverrideOwnersByPlayer
                     .Where(pair => pair.Value.Owners.Contains(owner))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            ReleaseOverride(playerId, owner);
        }
    }

    public static bool HasOverride(Player? player, string? owner = null)
    {
        if (!IsValidPlayer(player))
            return false;

        if (!OverrideOwnersByPlayer.TryGetValue(player!.Id, out var state))
            return Exists && ExiledIntercom.HasOverride(player);

        if (owner == null)
            return state.Owners.Count > 0 || Exists && ExiledIntercom.HasOverride(player);

        return state.Owners.Contains(NormalizeOwner(owner));
    }

    public static IReadOnlyCollection<string> GetOverrideOwners(Player? player)
    {
        if (!IsValidPlayer(player) || !OverrideOwnersByPlayer.TryGetValue(player!.Id, out var state))
            return Array.Empty<string>();

        return state.Owners.OrderBy(owner => owner, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool SetMuted(Player? player, bool muted, string? owner = null, float? durationSeconds = null)
        => muted
            ? AddMute(player, owner, durationSeconds)
            : ReleaseMute(player, owner);

    public static bool AddMute(Player? player, string? owner = null, float? durationSeconds = null)
    {
        if (!IsValidPlayer(player))
            return false;

        owner = NormalizeOwner(owner);

        if (!MuteOwnersByPlayer.TryGetValue(player!.Id, out var state))
        {
            state = new PlayerMuteState(player.IsIntercomMuted);
            MuteOwnersByPlayer[player.Id] = state;
        }

        state.Owners.Add(owner);
        player.IsIntercomMuted = true;
        SchedulePlayerOwnerTimer(MuteTimers, player.Id, owner, durationSeconds, () => ReleaseMute(player.Id, owner));
        return true;
    }

    public static bool ReleaseMute(Player? player, string? owner = null)
    {
        if (player == null)
            return false;

        return ReleaseMute(player.Id, NormalizeOwner(owner));
    }

    public static bool ReleaseMute(int playerId, string? owner = null)
    {
        owner = NormalizeOwner(owner);
        KillPlayerOwnerTimer(MuteTimers, playerId, owner);

        if (!MuteOwnersByPlayer.TryGetValue(playerId, out var state))
            return false;

        bool removed = state.Owners.Remove(owner);

        if (state.Owners.Count > 0)
            return removed;

        MuteOwnersByPlayer.Remove(playerId);

        var player = FindPlayer(playerId);
        if (player?.ReferenceHub != null && !state.WasMutedBeforeApi)
            player.IsIntercomMuted = false;

        return removed;
    }

    public static void ReleaseAllMutes(string owner)
    {
        owner = NormalizeOwner(owner);

        foreach (var playerId in MuteOwnersByPlayer
                     .Where(pair => pair.Value.Owners.Contains(owner))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            ReleaseMute(playerId, owner);
        }
    }

    public static bool IsMutedByApi(Player? player, string? owner = null)
    {
        if (!IsValidPlayer(player) || !MuteOwnersByPlayer.TryGetValue(player!.Id, out var state))
            return false;

        return owner == null || state.Owners.Contains(NormalizeOwner(owner));
    }

    public static IReadOnlyCollection<string> GetMuteOwners(Player? player)
    {
        if (!IsValidPlayer(player) || !MuteOwnersByPlayer.TryGetValue(player!.Id, out var state))
            return Array.Empty<string>();

        return state.Owners.OrderBy(owner => owner, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IDisposable OverrideScope(Player player, string owner)
    {
        GrantOverride(player, owner);
        return new ActionScope(() => ReleaseOverride(player, owner));
    }

    public static IDisposable UnavailableScope(string owner, string? displayText = null, bool infiniteCooldown = true)
    {
        SetUnavailable(owner, displayText, infiniteCooldown);
        return new ActionScope(() => ClearOwner(owner));
    }

    public static void CleanupPlayer(Player? player)
    {
        if (player == null)
            return;

        CleanupPlayer(player.Id, restoreMute: false);
    }

    public static void ClearAll(bool resetIntercom = false)
    {
        foreach (var playerId in MuteOwnersByPlayer.Keys.ToArray())
            CleanupMutePlayer(playerId, restoreMute: true);

        foreach (var playerId in OverrideOwnersByPlayer.Keys.ToArray())
            CleanupOverridePlayer(playerId);

        KillAllTimers(ControlFlagTimers);
        KillAllTimers(DisplayTimers);
        ControlFlagsByOwner.Clear();
        DisplayOverrides.Clear();

        if (resetIntercom)
        {
            _forcedUnavailableApplied = false;
            _displayOverrideApplied = false;

            if (Exists)
            {
                ExiledIntercom.RemainingCooldown = 0d;
                ExiledIntercom.Reset();
                ExiledIntercom.DisplayText = string.Empty;
            }
        }
        else
        {
            ApplyControlFlags();
            ApplyDisplayOverride();
        }
    }

    internal static void HandleIntercomSpeaking(IntercomSpeakingEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        if (HasControlFlag(IntercomControlFlags.BlockNormalUse))
            ev.IsAllowed = false;
    }

    private static void CleanupPlayer(int playerId, bool restoreMute)
    {
        CleanupOverridePlayer(playerId);
        CleanupMutePlayer(playerId, restoreMute);
    }

    private static void CleanupOverridePlayer(int playerId)
    {
        if (!OverrideOwnersByPlayer.TryGetValue(playerId, out var state))
            return;

        foreach (var owner in state.Owners.ToArray())
            KillPlayerOwnerTimer(OverrideTimers, playerId, owner);

        OverrideOwnersByPlayer.Remove(playerId);

        var player = FindPlayer(playerId);
        if (player?.ReferenceHub != null && Exists && !state.WasOverriddenBeforeApi)
            ExiledIntercom.TrySetOverride(player, false);
    }

    private static void CleanupMutePlayer(int playerId, bool restoreMute)
    {
        if (!MuteOwnersByPlayer.TryGetValue(playerId, out var state))
            return;

        foreach (var owner in state.Owners.ToArray())
            KillPlayerOwnerTimer(MuteTimers, playerId, owner);

        MuteOwnersByPlayer.Remove(playerId);

        var player = FindPlayer(playerId);
        if (restoreMute && player?.ReferenceHub != null && !state.WasMutedBeforeApi)
            player.IsIntercomMuted = false;
    }

    private static void ApplyControlFlags()
    {
        if (!Exists)
        {
            if (EffectiveControlFlags == IntercomControlFlags.None)
                _forcedUnavailableApplied = false;

            return;
        }

        var flags = EffectiveControlFlags;
        bool shouldForceTimeout = (flags & IntercomControlFlags.ForceTimeout) != 0;
        bool shouldForceInfiniteCooldown = (flags & IntercomControlFlags.ForceInfiniteCooldown) != 0;

        if (shouldForceTimeout || shouldForceInfiniteCooldown)
        {
            ExiledIntercom.Timeout();
            _forcedUnavailableApplied = true;

            if (shouldForceInfiniteCooldown)
                ExiledIntercom.RemainingCooldown = double.MaxValue;

            return;
        }

        if (!_forcedUnavailableApplied)
            return;

        _forcedUnavailableApplied = false;

        if (ExiledIntercom.State == IntercomState.Cooldown)
            Reset();
    }

    private static void ApplyDisplayOverride()
    {
        if (!Exists)
        {
            if (DisplayOverrides.Count == 0)
                _displayOverrideApplied = false;

            return;
        }

        var top = DisplayOverrides.Values
            .OrderByDescending(entry => entry.Priority)
            .ThenByDescending(entry => entry.Sequence)
            .FirstOrDefault();

        if (top == null)
        {
            if (_displayOverrideApplied)
                ExiledIntercom.DisplayText = string.Empty;

            _displayOverrideApplied = false;
            return;
        }

        ExiledIntercom.DisplayText = top.Text;
        _displayOverrideApplied = true;
    }

    private static bool IsValidPlayer(Player? player)
        => player?.ReferenceHub != null && player.IsConnected;

    private static Player? FindPlayer(int playerId)
        => Player.List.FirstOrDefault(player => player?.Id == playerId);

    private static string NormalizeOwner(string? owner)
        => string.IsNullOrWhiteSpace(owner) ? DefaultOwner : owner.Trim();

    private static void ScheduleOwnerTimer(
        Dictionary<string, CoroutineHandle> timers,
        string owner,
        float? durationSeconds,
        Action action)
    {
        KillOwnerTimer(timers, owner);

        if (!durationSeconds.HasValue || durationSeconds.Value <= 0f)
            return;

        timers[owner] = Timing.CallDelayed(durationSeconds.Value, () =>
        {
            timers.Remove(owner);
            action();
        });
    }

    private static void KillOwnerTimer(Dictionary<string, CoroutineHandle> timers, string owner)
    {
        if (!timers.TryGetValue(owner, out var handle))
            return;

        if (handle.IsRunning)
            Timing.KillCoroutines(handle);

        timers.Remove(owner);
    }

    private static void SchedulePlayerOwnerTimer(
        Dictionary<string, CoroutineHandle> timers,
        int playerId,
        string owner,
        float? durationSeconds,
        Action action)
    {
        string key = PlayerOwnerKey(playerId, owner);
        KillOwnerTimer(timers, key);

        if (!durationSeconds.HasValue || durationSeconds.Value <= 0f)
            return;

        timers[key] = Timing.CallDelayed(durationSeconds.Value, () =>
        {
            timers.Remove(key);
            action();
        });
    }

    private static void KillPlayerOwnerTimer(Dictionary<string, CoroutineHandle> timers, int playerId, string owner)
        => KillOwnerTimer(timers, PlayerOwnerKey(playerId, owner));

    private static string PlayerOwnerKey(int playerId, string owner)
        => $"{playerId}:{owner}";

    private static void KillAllTimers(Dictionary<string, CoroutineHandle> timers)
    {
        foreach (var handle in timers.Values.ToArray())
        {
            if (handle.IsRunning)
                Timing.KillCoroutines(handle);
        }

        timers.Clear();
    }

    private sealed class DisplayOverride
    {
        public DisplayOverride(string text, int priority, long sequence)
        {
            Text = text;
            Priority = priority;
            Sequence = sequence;
        }

        public string Text { get; }
        public int Priority { get; }
        public long Sequence { get; }
    }

    private sealed class PlayerMuteState
    {
        public PlayerMuteState(bool wasMutedBeforeApi)
        {
            WasMutedBeforeApi = wasMutedBeforeApi;
        }

        public bool WasMutedBeforeApi { get; }
        public HashSet<string> Owners { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PlayerOverrideState
    {
        public PlayerOverrideState(bool wasOverriddenBeforeApi)
        {
            WasOverriddenBeforeApi = wasOverriddenBeforeApi;
        }

        public bool WasOverriddenBeforeApi { get; }
        public HashSet<string> Owners { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ActionScope : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public ActionScope(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _dispose();
        }
    }
}

public sealed class IntercomApiHandler : IBootstrapHandler
{
    public static void Register()
    {
        PlayerHandler.IntercomSpeaking += IntercomApi.HandleIntercomSpeaking;
        PlayerHandler.Left += OnPlayerLeft;
        ServerHandler.WaitingForPlayers += OnRoundReset;
        ServerHandler.RestartingRound += OnRoundReset;
        ServerHandler.RoundStarted += OnRoundStarted;
    }

    public static void Unregister()
    {
        PlayerHandler.IntercomSpeaking -= IntercomApi.HandleIntercomSpeaking;
        PlayerHandler.Left -= OnPlayerLeft;
        ServerHandler.WaitingForPlayers -= OnRoundReset;
        ServerHandler.RestartingRound -= OnRoundReset;
        ServerHandler.RoundStarted -= OnRoundStarted;
        IntercomApi.ClearAll(resetIntercom: true);
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
        => IntercomApi.CleanupPlayer(ev.Player);

    private static void OnRoundReset()
        => IntercomApi.ClearAll(resetIntercom: true);

    private static void OnRoundStarted()
        => IntercomApi.ClearAll(resetIntercom: true);
}
