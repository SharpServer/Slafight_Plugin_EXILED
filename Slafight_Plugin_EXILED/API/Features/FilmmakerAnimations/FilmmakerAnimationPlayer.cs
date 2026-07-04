using System;
using System.Collections.Generic;
using CursorManagement;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using UnityEngine;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

public enum FilmmakerPlaybackMode
{
    Absolute,
    Relative,
}

public sealed class FilmmakerPlaybackOptions
{
    public FilmmakerPlaybackMode Mode { get; set; } = FilmmakerPlaybackMode.Absolute;
    public float SpeedScale { get; set; } = 1f;
    public bool LockMovement { get; set; } = true;
    public bool Loop { get; set; }
}

public static class FilmmakerAnimationPlayer
{
    private static readonly Dictionary<int, PlaybackSession> ActiveSessions = new();
    private static bool _registered;

    public static void RegisterEvents()
    {
        if (_registered)
            return;

        PlayerHandlers.Left += OnLeft;
        PlayerHandlers.Died += OnDied;
        PlayerHandlers.ChangingRole += OnChangingRole;
        ServerHandlers.RestartingRound += StopAll;
        ServerHandlers.WaitingForPlayers += StopAll;
        _registered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_registered)
            return;

        PlayerHandlers.Left -= OnLeft;
        PlayerHandlers.Died -= OnDied;
        PlayerHandlers.ChangingRole -= OnChangingRole;
        ServerHandlers.RestartingRound -= StopAll;
        ServerHandlers.WaitingForPlayers -= StopAll;
        StopAll();
        _registered = false;
    }

    public static bool TryPlay(
        Player target,
        string animationName,
        FilmmakerPlaybackOptions options,
        out string response)
    {
        if (!FilmmakerAnimationStorage.TryLoad(animationName, out FilmmakerAnimationClip clip, out response))
            return false;

        return TryPlay(target, clip, options, out response);
    }

    public static bool TryPlay(
        Player target,
        FilmmakerAnimationClip clip,
        FilmmakerPlaybackOptions options,
        out string response)
    {
        options ??= new FilmmakerPlaybackOptions();
        options.SpeedScale = Mathf.Clamp(options.SpeedScale, 0.05f, 8f);

        if (target == null || !target.IsConnected)
        {
            response = "Target player is not connected.";
            return false;
        }

        if (clip == null)
        {
            response = "Animation clip is null.";
            return false;
        }

        clip.Normalize();
        if (!clip.HasPlayableMotion)
        {
            response = $"Animation '{clip.Name}' has no position or rotation keyframes.";
            return false;
        }

        if (!TryGetFpc(target, out _, out response))
            return false;

        Stop(target);

        var session = new PlaybackSession(target, clip, options);
        if (!session.TryStart(out response))
            return false;

        ActiveSessions[target.Id] = session;
        response =
            $"Playing animation '{clip.Name}' on {target.Nickname}. " +
            $"Duration={clip.DurationSeconds:0.##}s, Mode={options.Mode}, Speed={options.SpeedScale:0.##}x, Loop={options.Loop}.";
        return true;
    }

    public static bool Stop(Player target)
    {
        if (target == null)
            return false;

        if (!ActiveSessions.TryGetValue(target.Id, out PlaybackSession session))
            return false;

        session.Stop();
        ActiveSessions.Remove(target.Id);
        return true;
    }

    public static bool Stop(Player target, out string response)
    {
        if (Stop(target))
        {
            response = $"Stopped animation playback on {target.Nickname}.";
            return true;
        }

        response = $"No active animation playback on {target?.Nickname ?? "target"}.";
        return false;
    }

    public static void StopAll()
    {
        var sessions = new List<PlaybackSession>(ActiveSessions.Values);
        foreach (PlaybackSession session in sessions)
            session.Stop();

        ActiveSessions.Clear();
    }

    internal static bool TryGetFpc(Player player, out IFpcRole fpcRole, out string response)
    {
        fpcRole = player?.ReferenceHub?.roleManager?.CurrentRole as IFpcRole;
        if (fpcRole?.FpcModule?.ModuleReady == true)
        {
            response = string.Empty;
            return true;
        }

        response = $"{player?.Nickname ?? "Target"} is not an active first-person role.";
        return false;
    }

    private static void RemoveSession(int playerId, PlaybackSession session)
    {
        if (ActiveSessions.TryGetValue(playerId, out PlaybackSession active) && ReferenceEquals(active, session))
            ActiveSessions.Remove(playerId);
    }

    private static void OnLeft(LeftEventArgs ev) => Stop(ev.Player);

    private static void OnDied(DiedEventArgs ev) => Stop(ev.Player);

    private static void OnChangingRole(ChangingRoleEventArgs ev) => Stop(ev.Player);

    private sealed class PlaybackSession
    {
        private readonly Player _target;
        private readonly FilmmakerAnimationClip _clip;
        private readonly FilmmakerPlaybackOptions _options;
        private readonly LockMovementOverride _movementLock = new();
        private readonly int _targetId;
        private IFpcRole _lockedFpc;
        private CoroutineHandle _coroutine;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private Vector3 _sourcePosition;
        private Quaternion _sourceRotation;
        private Quaternion _relativeRotationOffset = Quaternion.identity;
        private bool _hasSourcePosition;
        private bool _hasSourceRotation;
        private bool _cleaned;

        public PlaybackSession(Player target, FilmmakerAnimationClip clip, FilmmakerPlaybackOptions options)
        {
            _target = target;
            _targetId = target.Id;
            _clip = clip;
            _options = options;
        }

        public bool TryStart(out string response)
        {
            if (!TryGetFpc(_target, out IFpcRole fpcRole, out response))
                return false;

            _basePosition = _target.Position;
            _baseRotation = _target.Rotation;
            _hasSourcePosition = _clip.TryGetFirstPosition(out _sourcePosition);
            _hasSourceRotation = _clip.TryGetFirstRotation(out _sourceRotation);

            if (_hasSourceRotation)
                _relativeRotationOffset = _baseRotation * Quaternion.Inverse(_sourceRotation);

            if (_options.LockMovement)
            {
                fpcRole.FpcModule.Motor.RegisterMovementLock(_movementLock);
                _lockedFpc = fpcRole;
            }

            if (!ApplyFrame(0f, out response))
            {
                Cleanup();
                return false;
            }

            _coroutine = Timing.RunCoroutine(Run());
            return true;
        }

        public void Stop()
        {
            if (_coroutine.IsRunning)
                Timing.KillCoroutines(_coroutine);

            Cleanup();
        }

        private IEnumerator<float> Run()
        {
            float frameDuration = 1f / _clip.EffectiveFrameRate;
            float duration = _clip.DurationSeconds;
            float elapsed = 0f;

            try
            {
                if (duration <= 0f)
                    yield break;

                while (true)
                {
                    yield return Timing.WaitForSeconds(frameDuration);

                    elapsed += frameDuration * _options.SpeedScale;
                    if (elapsed >= duration)
                    {
                        if (_options.Loop)
                        {
                            elapsed %= duration;
                        }
                        else
                        {
                            elapsed = duration;
                            ApplyFrame(elapsed, out _);
                            yield break;
                        }
                    }

                    if (!ApplyFrame(elapsed, out string error))
                    {
                        Log.Debug($"[FilmmakerAnimationPlayer] Playback stopped for {_target?.Nickname}: {error}");
                        yield break;
                    }
                }
            }
            finally
            {
                Cleanup();
                RemoveSession(_targetId, this);
            }
        }

        private bool ApplyFrame(float seconds, out string response)
        {
            if (!TryGetFpc(_target, out IFpcRole fpcRole, out response))
                return false;

            if (!_clip.TryEvaluate(seconds, out FilmmakerAnimationSample sample))
            {
                response = $"Animation '{_clip.Name}' has no evaluatable keyframes.";
                return false;
            }

            if (sample.HasPosition)
            {
                Vector3 position = sample.Position;
                if (_options.Mode == FilmmakerPlaybackMode.Relative && _hasSourcePosition)
                    position = _basePosition + _relativeRotationOffset * (sample.Position - _sourcePosition);

                if (!IsFinite(position))
                {
                    response = "Animation produced a non-finite position.";
                    return false;
                }

                WaypointChunkStreamer.EnsureCoverage(position);
                _target.Position = position;
                fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(position);
            }

            if (sample.HasRotation)
            {
                Quaternion rotation = sample.Rotation;
                if (_options.Mode == FilmmakerPlaybackMode.Relative && _hasSourceRotation)
                    rotation = _relativeRotationOffset * sample.Rotation;

                _target.Rotation = rotation;
            }

            fpcRole.FpcModule.Motor.Velocity = Vector3.zero;
            fpcRole.FpcModule.Motor.ResetFallDamageCooldown();
            response = string.Empty;
            return true;
        }

        private void Cleanup()
        {
            if (_cleaned)
                return;

            _cleaned = true;
            try
            {
                _lockedFpc?.FpcModule?.Motor?.UnregisterMovementLock(_movementLock);
            }
            catch (Exception exception)
            {
                Log.Debug($"[FilmmakerAnimationPlayer] Failed to unregister movement lock: {exception.Message}");
            }
        }

        private static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private sealed class LockMovementOverride : ICursorOverride
    {
        public CursorOverrideMode CursorOverride => CursorOverrideMode.NoOverride;
        public bool LockMovement => true;
    }
}
