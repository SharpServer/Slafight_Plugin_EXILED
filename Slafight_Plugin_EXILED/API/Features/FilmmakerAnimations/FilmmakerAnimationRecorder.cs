using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using PlayerRoles.Filmmaker;
using PlayerRoles.FirstPersonControl;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

/// <summary>
/// プレイヤーやダミーの動きを一定間隔で記録し、<see cref="FilmmakerAnimationClip"/> にします。
/// </summary>
/// <remarks>
/// 位置・視点に加えて、姿勢 (しゃがみなど) と操作をダミーアクションとして残すので、ダミーが役者として再現できます。
/// プレイヤーはジャンプ・射撃・リロード・持ち替えを、ダミーは RA やコードで実行したアクションをそのまま記録します。
/// </remarks>
public static class FilmmakerAnimationRecorder
{
    public const float DefaultInterval = 0.04f;
    public const float MinInterval = 0.02f;
    public const float MaxInterval = 1f;
    public const float MaxDuration = 600f;

    // 動きは位置と視点の記録が受け持つので、移動・視点のダミーアクションは残さない。
    private static readonly string[] SkippedModules = ["Motor", "MouseLook"];

    private static readonly Dictionary<uint, RecordingSession> Sessions = new();
    private static bool _registered;

    public static void RegisterEvents()
    {
        if (_registered)
            return;

        PlayerHandlers.Left += OnLeft;
        PlayerHandlers.Jumping += OnJumping;
        PlayerHandlers.Shooting += OnShooting;
        PlayerHandlers.ReloadingWeapon += OnReloading;
        PlayerHandlers.ChangingItem += OnChangingItem;
        DummyActions.Invoked += OnDummyAction;
        ServerHandlers.RestartingRound += CancelAll;
        ServerHandlers.WaitingForPlayers += CancelAll;
        _registered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_registered)
            return;

        PlayerHandlers.Left -= OnLeft;
        PlayerHandlers.Jumping -= OnJumping;
        PlayerHandlers.Shooting -= OnShooting;
        PlayerHandlers.ReloadingWeapon -= OnReloading;
        PlayerHandlers.ChangingItem -= OnChangingItem;
        DummyActions.Invoked -= OnDummyAction;
        ServerHandlers.RestartingRound -= CancelAll;
        ServerHandlers.WaitingForPlayers -= CancelAll;
        CancelAll();
        _registered = false;
    }

    public static bool IsRecording(Player player)
        => TryGetSession(player, out _);

    /// <summary>
    /// 記録を始めます。既に記録中なら失敗します。
    /// </summary>
    public static bool TryStart(Player player, string name, float interval, out string response)
    {
        if (!player.IsSafePlayer())
        {
            response = "対象が存在しません。";
            return false;
        }

        if (!FilmmakerAnimationStorage.TryNormalizeName(name, out string normalizedName, out response))
            return false;

        if (IsRecording(player))
        {
            response = $"{player.Nickname} は既に記録中です。";
            return false;
        }

        if (!FilmmakerAnimationPlayer.TryGetFpc(player, out _, out response))
            return false;

        uint netId = player.GetNetId();
        if (netId == 0)
        {
            response = $"{player.Nickname} の netId が未割り当てです。";
            return false;
        }

        interval = Mathf.Clamp(interval, MinInterval, MaxInterval);
        var session = new RecordingSession(player, netId, normalizedName, interval);
        Sessions[netId] = session;
        session.Start();

        string source = player.IsNPC ? "ダミーの動きと RA で押したアクション" : "動きと操作";
        response = $"{player.Nickname} の{source}を '{normalizedName}' として記録開始 ({interval:0.###}s 間隔、最大 {MaxDuration:0}s)。";
        return true;
    }

    /// <summary>
    /// 記録を止めてクリップを返します。保存はしません。
    /// </summary>
    public static bool TryStop(Player player, out FilmmakerAnimationClip clip, out string response)
    {
        clip = null;

        if (!TryGetSession(player, out RecordingSession session))
        {
            response = $"{player?.Nickname ?? "対象"} は記録中ではありません。";
            return false;
        }

        session.Stop();
        Sessions.Remove(session.NetId);

        clip = session.BuildClip();
        if (!clip.HasPlayableMotion)
        {
            clip = null;
            response = "記録されたキーフレームがありません。";
            return false;
        }

        response = string.Empty;
        return true;
    }

    /// <summary>
    /// 記録中の対象を探します。自分以外 (ダミーなど) を記録している場合は、記録を始めた人からも引けます。
    /// </summary>
    public static Player FindRecordingTarget(Player owner)
    {
        if (owner == null)
            return null;

        if (IsRecording(owner))
            return owner;

        return Sessions.Values.FirstOrDefault(session => ReferenceEquals(session.Owner?.ReferenceHub, owner.ReferenceHub))?.Player;
    }

    public static bool Cancel(Player player)
    {
        if (!TryGetSession(player, out RecordingSession session))
            return false;

        session.Stop();
        Sessions.Remove(session.NetId);
        return true;
    }

    public static void CancelAll()
    {
        foreach (RecordingSession session in Sessions.Values)
            session.Stop();

        Sessions.Clear();
    }

    /// <summary>
    /// <paramref name="owner"/> が他の対象の記録を始めたことにします。<see cref="FindRecordingTarget"/> で引けるようになります。
    /// </summary>
    internal static void SetOwner(Player target, Player owner)
    {
        if (TryGetSession(target, out RecordingSession session))
            session.Owner = owner;
    }

    private static bool TryGetSession(Player player, out RecordingSession session)
    {
        session = null;
        if (player == null || !Sessions.TryGetValue(player.GetNetId(), out session))
            return false;

        return ReferenceEquals(session.Player.ReferenceHub, player.ReferenceHub);
    }

    /// <summary>
    /// プレイヤーの操作を記録します。ダミーは <see cref="OnDummyAction"/> で正確なパスが取れるので、ここでは扱いません。
    /// </summary>
    private static void RecordPlayerAction(Player player, string module, string name)
    {
        if (player?.ReferenceHub is { IsDummy: false } && TryGetSession(player, out RecordingSession session))
            session.AddAction(DummyActions.ToPath(module, name));
    }

    private static void OnLeft(LeftEventArgs ev) => Cancel(ev.Player);

    private static void OnJumping(JumpingEventArgs ev)
    {
        if (ev.IsAllowed)
            RecordPlayerAction(ev.Player, "FpcJumpController", "Jump");
    }

    private static void OnShooting(ShootingEventArgs ev)
    {
        if (ev.IsAllowed && ev.Firearm != null)
            RecordPlayerAction(ev.Player, ItemModule(ev.Firearm.Type), "Shoot->Click");
    }

    private static void OnReloading(ReloadingWeaponEventArgs ev)
    {
        if (ev.IsAllowed && ev.Firearm != null)
            RecordPlayerAction(ev.Player, ItemModule(ev.Firearm.Type), "Reload->Click");
    }

    private static void OnChangingItem(ChangingItemEventArgs ev)
    {
        if (!ev.IsAllowed)
            return;

        if (ev.Item != null)
            RecordPlayerAction(ev.Player, ItemModule(ev.Item.Type), "Equip");
        else if (ev.Player?.CurrentItem is { } current)
            RecordPlayerAction(ev.Player, ItemModule(current.Type), "Holster");
    }

    private static void OnDummyAction(DummyActionInvokedEventArgs ev)
    {
        if (SkippedModules.Contains(ev.Module) || !TryGetSession(ev.Dummy, out RecordingSession session))
            return;

        // 同じ種類のアイテムが複数あっても再生側で当たるよう、個体番号ではなく種類で残す。
        string module = ev.Module.EndsWith(")") && ev.Module.Contains(" (#") ? ev.Module.Substring(0, ev.Module.IndexOf(" (#")) + " (ANY)" : ev.Module;
        session.AddAction(DummyActions.ToPath(module, ev.Name));
    }

    private static string ItemModule(ItemType type) => $"{type} (ANY)";

    private sealed class RecordingSession
    {
        private readonly List<FilmmakerVector3Keyframe> _positions = [];
        private readonly List<FilmmakerQuaternionKeyframe> _rotations = [];
        private readonly List<FilmmakerActionKey> _actions = [];
        private readonly List<ItemType> _items = [];
        private readonly float _interval;
        private readonly string _name;
        private CoroutineHandle _coroutine;
        private float _startedAt;
        private int _lastFrame = -1;
        private PlayerMovementState? _lastState;
        private RoleTypeId _role;

        public RecordingSession(Player player, uint netId, string name, float interval)
        {
            Player = player;
            NetId = netId;
            _name = name;
            _interval = interval;
        }

        public Player Player { get; }
        public uint NetId { get; }

        /// <summary>
        /// 記録を始めた人です。自分自身を記録している場合は <see cref="Player"/> と同じです。
        /// </summary>
        public Player Owner { get; set; }

        public void Start()
        {
            Owner ??= Player;
            _startedAt = Time.time;
            _role = Player.Role.Type;
            _items.AddRange(Player.Items.Select(item => item.Type));

            // 再生側が同じ持ち物を同じ手で始められるよう、最初に持っている物を装備させる。
            if (Player.CurrentItem is { } current)
                AddAction(DummyActions.ToPath(ItemModule(current.Type), "Equip"));

            Sample();
            _coroutine = Timing.RunCoroutine(Run());
        }

        public void Stop()
        {
            if (_coroutine.IsRunning)
                Timing.KillCoroutines(_coroutine);

            // 止めた瞬間の姿勢も残す。
            Sample();
        }

        public void AddAction(string path)
        {
            float elapsed = Time.time - _startedAt;
            if (elapsed > MaxDuration)
                return;

            _actions.Add(FilmmakerActionKey.From(Mathf.RoundToInt(elapsed * FilmmakerTimelineManager.FrameRate), path));
        }

        public FilmmakerAnimationClip BuildClip()
        {
            var clip = new FilmmakerAnimationClip
            {
                Name = _name,
                Source = nameof(FilmmakerAnimationRecorder),
                CreatedBy = Owner is { } owner ? $"{owner.Nickname} ({owner.UserId})" : string.Empty,
                FrameRate = FilmmakerTimelineManager.FrameRate,
                Interpolation = FilmmakerInterpolation.Linear,
                Positions = [.. _positions],
                Rotations = [.. _rotations],
                Actions = [.. _actions],
                ActorRole = _role,
                ActorItems = [.. _items],
            };

            clip.Normalize();
            return clip;
        }

        private IEnumerator<float> Run()
        {
            while (Time.time - _startedAt < MaxDuration)
            {
                yield return Timing.WaitForSeconds(_interval);

                if (!Player.IsSafePlayer())
                    yield break;

                Sample();
            }
        }

        private void Sample()
        {
            float elapsed = Time.time - _startedAt;
            if (elapsed > MaxDuration || !Player.IsSafePlayer() || !FilmmakerAnimationPlayer.TryGetFpc(Player, out _, out _))
                return;

            if (Player.Role is FpcRole fpc && fpc.MoveState != _lastState)
            {
                _lastState = fpc.MoveState;
                AddAction(DummyActions.PosePath(fpc.MoveState));
            }

            int frame = Mathf.RoundToInt(elapsed * FilmmakerTimelineManager.FrameRate);
            if (frame <= _lastFrame)
                return;

            Quaternion rotation = Player.Rotation;

            // 符号が反転した四元数どうしを補間すると途中で破綻するので、前の向きに揃える。
            if (_rotations.Count > 0 && Quaternion.Dot(_rotations[_rotations.Count - 1].Value, rotation) < 0f)
                rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);

            _lastFrame = frame;
            _positions.Add(FilmmakerVector3Keyframe.From(frame, FilmmakerBlendPreset.Linear, Player.Position));
            _rotations.Add(FilmmakerQuaternionKeyframe.From(frame, FilmmakerBlendPreset.Linear, rotation));
        }
    }
}

/// <summary>
/// <see cref="FilmmakerAnimationRecorder"/> の起動と停止を受け持ちます。
/// </summary>
public sealed class FilmmakerAnimationRecorderLifecycle : Slafight_Plugin_EXILED.API.Core.Features.EventHandlerBase
{
    /// <inheritdoc />
    public override void RegisterEvents() => FilmmakerAnimationRecorder.RegisterEvents();

    /// <inheritdoc />
    public override void UnregisterEvents() => FilmmakerAnimationRecorder.UnregisterEvents();
}
