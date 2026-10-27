using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Interfaces;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

public sealed class FilmmakerCutsceneOptions
{
    /// <summary>再生中の役職です。</summary>
    public RoleTypeId Role { get; set; } = RoleTypeId.Tutorial;

    public float SpeedScale { get; set; } = 1f;

    /// <summary>再生中にラウンドを終わらせないかどうか。</summary>
    public bool LockRound { get; set; } = true;

    /// <summary>
    /// 動き出す前に最初の構図で待つ秒数です。<see cref="FilmmakerAnimation.PrepareSeconds"/> と大きい方を使います。
    /// </summary>
    public float PrepareSeconds { get; set; }
}

/// <summary>
/// 対象を一時的に別役職へ差し替えてクリップを再生し、終わったら <see cref="RoleHold"/> で元の状態へ戻す演出です。
/// </summary>
/// <remarks>
/// 再生中の対象は無敵・全員から不可視・Noclip になり、アイテムや設備に触れません。
/// 小道具は <see cref="Preparing"/> / <see cref="Started"/> / <see cref="CueReached"/> で出し、
/// <c>SpawnLight</c> などの小道具 API か <see cref="OnEnd"/> で片付けを預けてください。
/// </remarks>
public sealed partial class FilmmakerCutscene
{
    /// <summary>
    /// 再生中に見ている人の状態 (Noclip・不可視・無敵) を確かめ直す間隔です。
    /// </summary>
    private const float GuardInterval = 0.25f;

    private static readonly List<FilmmakerCutscene> Running = [];
    private static readonly Dictionary<uint, Member> Watchers = new();
    private static bool _registered;
    private static bool _lockedRound;

    private readonly List<Member> _members = [];
    private readonly List<Action> _cleanups = [];
    private readonly List<CoroutineHandle> _tracked = [];
    private readonly List<Action> _startedCallbacks = [];
    private readonly FilmmakerCutsceneOptions _options;
    private readonly Action _onFinished;
    private CoroutineHandle _coroutine;
    private float _startedAt = -1f;

    private FilmmakerCutscene(
        FilmmakerAnimationClip clip,
        FilmmakerAnimation animation,
        FilmmakerCutsceneOptions options,
        Action onFinished)
    {
        Clip = clip;
        Animation = animation;
        _options = options;
        _onFinished = onFinished;
        PrepareSeconds = Mathf.Max(0f, Mathf.Max(options.PrepareSeconds, animation?.PrepareSeconds ?? 0f));
    }

    /// <summary>
    /// 全員の差し替えが済み、最初の構図で待っている間に起きます。小道具はここで出します。
    /// </summary>
    public static event Action<FilmmakerCutscenePreparingEventArgs> Preparing;

    /// <summary>
    /// 準備が終わり、カメラが動き出した直後に起きます。
    /// </summary>
    public static event Action<FilmmakerCutsceneStartedEventArgs> Started;

    /// <summary>
    /// クリップに置いた合図 (<see cref="FilmmakerAnimationBuilder.Cue(string)"/>) の時刻に起きます。
    /// </summary>
    public static event Action<FilmmakerCutsceneCueEventArgs> CueReached;

    /// <summary>
    /// 全員を戻し、片付けが済んだ後に起きます。ラウンド再開で捨てたときは起きません。
    /// </summary>
    public static event Action<FilmmakerCutsceneFinishedEventArgs> Finished;

    public FilmmakerAnimationClip Clip { get; }

    /// <summary>
    /// コード定義のアニメーションです。保存済みファイルからの再生なら null。
    /// </summary>
    public FilmmakerAnimation Animation { get; }

    public bool IsFinished { get; private set; }

    public float SpeedScale => _options.SpeedScale;

    /// <summary>
    /// 動き出す前に最初の構図で待つ秒数です。
    /// </summary>
    public float PrepareSeconds { get; }

    /// <summary>
    /// カメラが動いている部分の実時間での長さ (秒) です。
    /// </summary>
    public float Duration => Clip.DurationSeconds / _options.SpeedScale;

    /// <summary>
    /// クリップ上の経過秒です。準備中は 0。
    /// </summary>
    public float Elapsed => _startedAt < 0f
        ? 0f
        : Mathf.Min((Time.time - _startedAt) * _options.SpeedScale, Clip.DurationSeconds);

    public IReadOnlyList<Player> Players => [.. _members.Select(member => member.Player)];

    public static IReadOnlyList<FilmmakerCutscene> Active => Running;

    /// <summary>
    /// 指定プレイヤーがいま演出を見ているかどうか。
    /// </summary>
    public static bool IsWatching(Player player)
        => player != null &&
           Watchers.TryGetValue(player.GetNetId(), out Member member) &&
           ReferenceEquals(member.Player.ReferenceHub, player.ReferenceHub) &&
           !member.Hold.IsReleased;

    /// <summary>
    /// 既定の対象です。観戦者を含む全員で、NPC・Overwatch・演出中の人は除きます。
    /// </summary>
    public static IReadOnlyList<Player> DefaultTargets()
        =>
        [
            .. Player.List.Where(player =>
                player.IsSafePlayer() &&
                !player.IsNPC &&
                player.Role.Type is not (RoleTypeId.Overwatch or RoleTypeId.None) &&
                !RoleHold.IsHeld(player))
        ];

    public static FilmmakerCutscene Play(
        string animationName,
        IEnumerable<Player> targets,
        FilmmakerCutsceneOptions options,
        Action onFinished,
        out string response)
    {
        if (!FilmmakerAnimation.TryResolve(
                animationName,
                out FilmmakerAnimationClip clip,
                out FilmmakerAnimation animation,
                out response))
            return null;

        return Create(clip, animation, targets, options, onFinished, out response);
    }

    /// <summary>
    /// コード定義のアニメーションで演出を始めます。
    /// </summary>
    public static FilmmakerCutscene Play<TAnimation>(
        IEnumerable<Player> targets,
        FilmmakerCutsceneOptions options,
        Action onFinished,
        out string response)
        where TAnimation : FilmmakerAnimation, new()
        => Play(new TAnimation(), targets, options, onFinished, out response);

    /// <inheritdoc cref="Play{TAnimation}"/>
    public static FilmmakerCutscene Play(
        FilmmakerAnimation animation,
        IEnumerable<Player> targets,
        FilmmakerCutsceneOptions options,
        Action onFinished,
        out string response)
    {
        if (animation == null)
        {
            response = "Animation is null.";
            return null;
        }

        if (!animation.TryCreateClip(out FilmmakerAnimationClip clip, out response))
            return null;

        return Create(clip, animation, targets, options, onFinished, out response);
    }

    /// <summary>
    /// 演出を始めます。1 人も差し替えられなければ null を返し、<paramref name="onFinished"/> は呼びません。
    /// </summary>
    public static FilmmakerCutscene Play(
        FilmmakerAnimationClip clip,
        IEnumerable<Player> targets,
        FilmmakerCutsceneOptions options,
        Action onFinished,
        out string response)
        => Create(clip, null, targets, options, onFinished, out response);

    /// <summary>
    /// 途中で打ち切り、全員を元に戻します。
    /// </summary>
    public void Stop() => Finish(interrupted: true);

    public static void StopAll()
    {
        foreach (FilmmakerCutscene cutscene in Running.ToArray())
            cutscene.Finish(interrupted: true);
    }

    /// <summary>
    /// 演出が終わるときの片付けを預けます。打ち切り・ラウンド再開でも、登録の逆順で必ず呼ばれます。
    /// </summary>
    public void OnEnd(Action cleanup)
    {
        if (cleanup == null)
            throw new ArgumentNullException(nameof(cleanup));

        if (IsFinished)
        {
            RunSafely(cleanup, "片付け");
            return;
        }

        _cleanups.Add(cleanup);
    }

    /// <summary>
    /// 自前のコルーチンを預けます。演出が終わると止まります。
    /// </summary>
    public CoroutineHandle Track(CoroutineHandle handle)
    {
        if (IsFinished)
            Timing.KillCoroutines(handle);
        else
            _tracked.Add(handle);

        return handle;
    }

    /// <summary>
    /// カメラが動き出したときに一度だけ実行します。既に動き出していればすぐ実行します。
    /// </summary>
    public void WhenStarted(Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        if (IsFinished)
            return;

        if (_startedAt >= 0f)
            RunSafely(action, "WhenStarted");
        else
            _startedCallbacks.Add(action);
    }

    /// <summary>
    /// 実時間で <paramref name="seconds"/> 秒後に一度だけ実行します。演出が先に終われば実行しません。
    /// </summary>
    public CoroutineHandle Delay(float seconds, Action action)
        => Track(Timing.CallDelayed(seconds, () =>
        {
            if (!IsFinished)
                RunSafely(action, "遅延処理");
        }));

    internal static void RegisterEvents()
    {
        if (_registered)
            return;

        PlayerHandlers.Left += OnLeft;
        PlayerHandlers.Hurting += OnHurting;
        PlayerHandlers.Handcuffing += OnHandcuffing;
        PlayerHandlers.ChangingItem += Deny;
        PlayerHandlers.DroppingItem += Deny;
        PlayerHandlers.DroppingAmmo += Deny;
        PlayerHandlers.SearchingPickup += Deny;
        PlayerHandlers.InteractingDoor += Deny;
        PlayerHandlers.InteractingElevator += Deny;
        PlayerHandlers.InteractingLocker += Deny;
        PlayerHandlers.TogglingNoClip += Deny;
        ServerHandlers.RestartingRound += Abandon;
        _registered = true;
    }

    internal static void UnregisterEvents()
    {
        if (!_registered)
            return;

        StopAll();

        PlayerHandlers.Left -= OnLeft;
        PlayerHandlers.Hurting -= OnHurting;
        PlayerHandlers.Handcuffing -= OnHandcuffing;
        PlayerHandlers.ChangingItem -= Deny;
        PlayerHandlers.DroppingItem -= Deny;
        PlayerHandlers.DroppingAmmo -= Deny;
        PlayerHandlers.SearchingPickup -= Deny;
        PlayerHandlers.InteractingDoor -= Deny;
        PlayerHandlers.InteractingElevator -= Deny;
        PlayerHandlers.InteractingLocker -= Deny;
        PlayerHandlers.TogglingNoClip -= Deny;
        ServerHandlers.RestartingRound -= Abandon;
        _registered = false;
    }

    private static FilmmakerCutscene Create(
        FilmmakerAnimationClip clip,
        FilmmakerAnimation animation,
        IEnumerable<Player> targets,
        FilmmakerCutsceneOptions options,
        Action onFinished,
        out string response)
    {
        options ??= new FilmmakerCutsceneOptions();
        options.SpeedScale = Mathf.Clamp(options.SpeedScale, 0.05f, 8f);

        if (clip == null)
        {
            response = "Animation clip is null.";
            return null;
        }

        clip.Normalize();
        if (!clip.HasPlayableMotion)
        {
            response = $"Animation '{clip.Name}' has no position or rotation keyframes.";
            return null;
        }

        var cutscene = new FilmmakerCutscene(clip, animation, options, onFinished);
        var failures = new List<string>();

        foreach (Player player in (targets ?? DefaultTargets()).Distinct())
        {
            if (!cutscene.TryAdd(player, out string failure))
                failures.Add(failure);
        }

        if (cutscene._members.Count == 0)
        {
            response = failures.Count > 0
                ? "演出を始められませんでした:\n  " + string.Join("\n  ", failures)
                : "演出の対象がいません。";
            return null;
        }

        Running.Add(cutscene);

        if (options.LockRound && !Round.IsLocked)
        {
            Round.IsLocked = true;
            _lockedRound = true;
        }

        cutscene._coroutine = Timing.RunCoroutine(cutscene.Run());

        response =
            $"演出 '{clip.Name}' を {cutscene._members.Count} 人に再生します " +
            $"(準備 {cutscene.PrepareSeconds:0.##}s + {cutscene.Duration:0.##}s)。" +
            (failures.Count > 0 ? "\n除外:\n  " + string.Join("\n  ", failures) : string.Empty);
        return cutscene;
    }

    private bool TryAdd(Player player, out string failure)
    {
        failure = null;

        if (player == null || IsWatching(player))
        {
            failure = $"{player?.Nickname ?? "null"}: 既に演出中です。";
            return false;
        }

        bool godMode = player.IsGodModeEnabled;
        bool noclip = player.IsNoclipEnabled;
        RoleHold hold = RoleHold.Begin(player, _options.Role, out string holdFailure);
        if (hold == null)
        {
            failure = $"{player.Nickname}: {holdFailure}";
            return false;
        }

        var member = new Member(player, hold, godMode, noclip);
        _members.Add(member);
        Watchers[hold.NetId] = member;

        player.CurrentItem = null;

        // 最初の構図へすぐ移しておく。移動中の数フレームを見せない。
        if (Clip.TryEvaluate(0f, out FilmmakerAnimationSample first))
        {
            if (first.HasPosition)
                player.Position = first.Position;
            if (first.HasRotation)
                player.Rotation = first.Rotation;
        }

        ApplyWatcherState(member);
        return true;
    }

    /// <summary>
    /// 見ている人を無敵・Noclip・全員から不可視にします。何かに戻されても再生中は付け直します。
    /// </summary>
    private static void ApplyWatcherState(Member member)
    {
        Player player = member.Player;
        if (!player.IsSafePlayer() || member.Hold.IsReleased)
            return;

        if (!player.IsGodModeEnabled)
            player.IsGodModeEnabled = true;

        if (!player.IsNoclipEnabled)
            player.IsNoclipEnabled = true;

        // 一時役職の FpcRole ごと捨てられるので、復元時に戻す必要はない。
        if (player.Role is FpcRole { IsInvisible: false })
            PlayerVisibilitySyncProvider.SetInvisible(player, true);
    }

    private IEnumerator<float> Run()
    {
        // 役職の切り替えが行き渡ってから動かす。
        yield return Timing.WaitForOneFrame;

        Track(Timing.RunCoroutine(GuardWatchers()));

        var playback = new FilmmakerPlaybackOptions
        {
            Mode = FilmmakerPlaybackMode.Absolute,
            SpeedScale = _options.SpeedScale,
            LockMovement = true,
            StartDelay = PrepareSeconds,
        };

        foreach (Member member in _members)
        {
            if (member.Hold.IsReleased)
                continue;

            ApplyWatcherState(member);
            if (!FilmmakerAnimationPlayer.TryPlay(member.Player, Clip, playback, out string error))
                Log.Warn($"[FilmmakerCutscene] {member.Player.Nickname} で再生できませんでした: {error}");
        }

        var preparing = new FilmmakerCutscenePreparingEventArgs(this);
        RunSafely(() => Animation?.OnPreparing(preparing), $"{Animation?.GetType().Name}.OnPreparing");
        Raise(Preparing, preparing);

        if (IsFinished)
            yield break;

        if (PrepareSeconds > 0f)
            yield return Timing.WaitForSeconds(PrepareSeconds);

        if (IsFinished)
            yield break;

        _startedAt = Time.time;

        foreach (Action callback in _startedCallbacks)
            RunSafely(callback, "WhenStarted");

        _startedCallbacks.Clear();

        var started = new FilmmakerCutsceneStartedEventArgs(this);
        RunSafely(() => Animation?.OnStarted(started), $"{Animation?.GetType().Name}.OnStarted");
        Raise(Started, started);

        foreach (FilmmakerCue cue in Clip.Cues)
        {
            if (IsFinished)
                yield break;

            float wait = cue.TimeFrames / Clip.EffectiveFrameRate / _options.SpeedScale - (Time.time - _startedAt);
            if (wait > 0f)
                yield return Timing.WaitForSeconds(wait);

            if (IsFinished)
                yield break;

            var reached = new FilmmakerCutsceneCueEventArgs(this, cue);
            RunSafely(() => Animation?.OnCue(reached), $"{Animation?.GetType().Name}.OnCue({cue.Name})");
            Raise(CueReached, reached);
        }

        if (IsFinished)
            yield break;

        float remaining = Duration - (Time.time - _startedAt);
        if (remaining > 0f)
            yield return Timing.WaitForSeconds(remaining);

        Finish(interrupted: false);
    }

    private IEnumerator<float> GuardWatchers()
    {
        while (!IsFinished)
        {
            foreach (Member member in _members)
                ApplyWatcherState(member);

            yield return Timing.WaitForSeconds(GuardInterval);
        }
    }

    private void Finish(bool interrupted)
    {
        if (IsFinished)
            return;

        IsFinished = true;
        Running.Remove(this);

        // 自分のコルーチンの中から呼ばれた場合は、この後の yield で止まる。
        if (_coroutine.IsRunning)
            Timing.KillCoroutines(_coroutine);

        foreach (Member member in _members)
        {
            if (Watchers.TryGetValue(member.Hold.NetId, out Member current) && ReferenceEquals(current, member))
                Watchers.Remove(member.Hold.NetId);

            if (!member.Player.IsSafePlayer())
            {
                member.Hold.Release();
                continue;
            }

            FilmmakerAnimationPlayer.Stop(member.Player);

            // 外から役職を変えられた人は、そのまま残す。
            if (!member.Hold.IsReleased && !member.Hold.Restore(out string failure))
                Log.Warn($"[FilmmakerCutscene] {member.Player.Nickname} を元に戻せませんでした: {failure}");

            // 役職を戻すと Noclip は切れるので、元の値を付け直す。
            member.Player.IsGodModeEnabled = member.GodMode;
            member.Player.IsNoclipEnabled = member.Noclip;
        }

        RunCleanups();
        UnlockRoundIfIdle();

        Log.Debug($"[FilmmakerCutscene] '{Clip.Name}' を終了しました (打ち切り: {interrupted})。");

        var finished = new FilmmakerCutsceneFinishedEventArgs(this, interrupted);
        RunSafely(() => Animation?.OnFinished(finished), $"{Animation?.GetType().Name}.OnFinished");
        Raise(Finished, finished);

        if (_onFinished != null)
            RunSafely(_onFinished, "終了時の処理");
    }

    private void RunCleanups()
    {
        foreach (CoroutineHandle handle in _tracked)
            Timing.KillCoroutines(handle);

        _tracked.Clear();

        for (int i = _cleanups.Count - 1; i >= 0; i--)
            RunSafely(_cleanups[i], "片付け");

        _cleanups.Clear();
    }

    private static void Raise<TArgs>(Action<TArgs> handlers, TArgs args)
    {
        if (handlers == null)
            return;

        // 1 つの購読者が投げても残りは呼ぶ。
        foreach (Action<TArgs> handler in handlers.GetInvocationList())
            RunSafely(() => handler(args), handler.Method.Name);
    }

    private static void RunSafely(Action action, string label)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Log.Error($"[FilmmakerCutscene] {label} で例外が発生しました: {exception}");
        }
    }

    private static void UnlockRoundIfIdle()
    {
        if (!_lockedRound || Running.Count > 0)
            return;

        Round.IsLocked = false;
        _lockedRound = false;
    }

    /// <summary>
    /// ラウンド再開時は戻さずに捨てます。役職はどのみち作り直されます。片付けだけは行います。
    /// </summary>
    private static void Abandon()
    {
        foreach (FilmmakerCutscene cutscene in Running)
        {
            cutscene.IsFinished = true;
            if (cutscene._coroutine.IsRunning)
                Timing.KillCoroutines(cutscene._coroutine);

            foreach (Member member in cutscene._members)
                member.Hold.Release();

            cutscene.RunCleanups();
        }

        Running.Clear();
        Watchers.Clear();

        if (_lockedRound)
        {
            Round.IsLocked = false;
            _lockedRound = false;
        }
    }

    private static void OnLeft(LeftEventArgs ev)
    {
        if (ev.Player != null && Watchers.TryGetValue(ev.Player.GetNetId(), out Member member) &&
            ReferenceEquals(member.Player.ReferenceHub, ev.Player.ReferenceHub))
            Watchers.Remove(member.Hold.NetId);
    }

    private static void OnHurting(HurtingEventArgs ev)
    {
        if (IsWatching(ev.Player) || IsWatching(ev.Attacker))
            ev.IsAllowed = false;
    }

    private static void OnHandcuffing(HandcuffingEventArgs ev)
    {
        if (IsWatching(ev.Player) || IsWatching(ev.Target))
            ev.IsAllowed = false;
    }

    private static void Deny<T>(T ev) where T : IPlayerEvent, IDeniableEvent
    {
        if (IsWatching(ev.Player))
            ev.IsAllowed = false;
    }

    private sealed class Member(Player player, RoleHold hold, bool godMode, bool noclip)
    {
        public Player Player { get; } = player;
        public RoleHold Hold { get; } = hold;
        public bool GodMode { get; } = godMode;
        public bool Noclip { get; } = noclip;
    }
}

/// <summary>
/// <see cref="FilmmakerCutscene"/> の後始末を受け持ちます。
/// </summary>
public sealed class FilmmakerCutsceneLifecycle : Slafight_Plugin_EXILED.API.Core.Features.EventHandlerBase
{
    /// <inheritdoc />
    public override void RegisterEvents() => FilmmakerCutscene.RegisterEvents();

    /// <inheritdoc />
    public override void UnregisterEvents() => FilmmakerCutscene.UnregisterEvents();
}
