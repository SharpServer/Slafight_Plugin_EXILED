using System;
using System.Collections.Generic;
using MEC;
using Exiled.API.Features;
using UnityEngine;
using Slafight_Plugin_EXILED.API.Features;
using PlayerRoles;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// プレイヤーが誰かを「見た」ときにホラースティンガー / チェイステーマを再生する API。
/// Audio 再生は SpeakerApi を使用し、AudioClip には依存しない。
/// </summary>
public static class LookSoundApi
{
    private class LookSession
    {
        public Player Owner;                 // 視線を飛ばす側 (SCP など)
        public Player Target;                // 監視対象
        public bool StingerPlayed;
        public bool ChasePlaying;
        public CoroutineHandle Coroutine;
        public SpeakerApi.Playback? CurrentChasePlayback;
    }

    private static readonly Dictionary<int, LookSession> Sessions = new();
    private static bool _enabled;

    // パラメータ（必要に応じて Config へ）
    public static float RayDistance = 40f;
    public static float RayRadius = 0.5f;        // 少し甘く判定したいとき
    public static LayerMask RayMask = Physics.DefaultRaycastLayers;
    public static float CheckInterval = 0.05f;   // 監視間隔
    public static float MinDotForLook = 0.8f;    // 視線方向のどれくらい前を見ているか

    /// <summary>
    /// LookSound システムを有効化。
    /// </summary>
    public static void Enable()
    {
        if (_enabled) return;
        _enabled = true;
    }

    /// <summary>
    /// LookSound システムを無効化し、全セッションを停止。
    /// </summary>
    public static void Disable()
    {
        if (!_enabled) return;
        _enabled = false;

        foreach (var s in Sessions.Values)
        {
            if (s.Coroutine.IsRunning)
                Timing.KillCoroutines(s.Coroutine);

            StopChaseInternal(s);
        }

        Sessions.Clear();
    }

    /// <summary>
    /// owner が target を見たときに stingerFile / chaseFile を鳴らすセッションを開始。
    /// file 名は SpeakerApi.AudioDirectory 基準の相対パス（例: "scp3114_stinger.ogg"）。
    /// </summary>
    public static void StartLookSession(
        Player owner,
        Player target,
        string stingerFile,
        string chaseFile,
        bool loopChase = true)
    {
        if (!_enabled || owner == null || target == null)
            return;

        if (string.IsNullOrWhiteSpace(stingerFile) || string.IsNullOrWhiteSpace(chaseFile))
            throw new ArgumentException("Stinger and chase file names must not be empty.");

        // すでに owner にセッションがあるなら止める
        if (Sessions.TryGetValue(owner.Id, out var existing))
        {
            StopSession(owner);
        }

        var session = new LookSession
        {
            Owner = owner,
            Target = target,
            StingerPlayed = false,
            ChasePlaying = false
        };

        session.Coroutine = Timing.RunCoroutine(
            LookCoroutine(session, stingerFile, chaseFile, loopChase));

        Sessions[owner.Id] = session;

        Log.Debug($"[LookSoundApi] StartLookSession: {owner.Nickname} watching {target.Nickname}");
    }

    /// <summary>
    /// owner の LookSession を停止。
    /// </summary>
    public static void StopSession(Player owner)
    {
        if (owner == null)
            return;

        if (!Sessions.TryGetValue(owner.Id, out var s))
            return;

        if (s.Coroutine.IsRunning)
            Timing.KillCoroutines(s.Coroutine);

        StopChaseInternal(s);
        Sessions.Remove(owner.Id);

        Log.Debug($"[LookSoundApi] StopSession: {owner.Nickname}");
    }

    /// <summary>
    /// プレイヤーが死亡 / 離脱した際に呼び出すクリーンアップ。
    /// </summary>
    public static void OnPlayerLeftOrDied(Player player)
    {
        if (player == null)
            return;

        // owner としてのセッション
        StopSession(player);

        // target として参照されているセッションも止める
        foreach (var s in new List<LookSession>(Sessions.Values))
        {
            if (s.Target?.Id == player.Id)
                StopSession(s.Owner);
        }
    }

    // ======== コルーチン本体 ========

    private static IEnumerator<float> LookCoroutine(
        LookSession session,
        string stingerFile,
        string chaseFile,
        bool loopChase)
    {
        var owner = session.Owner;
        var target = session.Target;

        while (_enabled)
        {
            bool shouldContinue;
            try
            {
                shouldContinue =
                    owner != null && owner.ReferenceHub != null && owner.IsAlive &&
                    target != null && target.ReferenceHub != null && target.IsAlive;
            }
            catch
            {
                shouldContinue = false;
            }

            if (!shouldContinue)
                break;

            bool isLooking = IsLookingAt(owner, target, RayDistance, RayRadius, MinDotForLook);

            if (isLooking)
            {
                if (!session.StingerPlayed)
                {
                    PlayStinger(owner, stingerFile);
                    session.StingerPlayed = true;
                }

                if (!session.ChasePlaying)
                {
                    StartChase(session, chaseFile, loopChase);
                    session.ChasePlaying = true;
                }
            }
            else
            {
                if (session.ChasePlaying)
                {
                    StopChaseInternal(session);
                    session.ChasePlaying = false;
                }
            }

            yield return Timing.WaitForSeconds(CheckInterval);
        }

        StopChaseInternal(session);
        if (owner != null &&
            Sessions.TryGetValue(owner.Id, out var currentSession) &&
            ReferenceEquals(currentSession, session))
        {
            Sessions.Remove(owner.Id);
        }

        Log.Debug($"[LookSoundApi] LookCoroutine stopped for {owner?.Nickname ?? "Unknown"}");
    }

    // ======== Raycast 判定 ========

    /// <summary>
    /// owner が target を「見ている」か判定。
    /// Raycast + 視線角度で判断する。
    /// </summary>
    private static bool IsLookingAt(Player owner, Player target, float maxDistance, float radius, float minDot)
    {
        // CameraTransform が取れる想定（Exiled.Player に存在）
        var cam = owner.CameraTransform;
        Vector3 origin = cam.position;
        Vector3 forward = cam.forward;

        // 視線方向とターゲット方向の角度チェック
        Vector3 toTarget = (target.Position - origin).normalized;
        float dot = Vector3.Dot(forward, toTarget);
        if (dot < minDot)
            return false;

        // SphereCast で多少の誤差を許容
        if (Physics.SphereCast(origin, radius, forward, out var hit, maxDistance, RayMask))
        {
            var hub = hit.collider.GetComponentInParent<ReferenceHub>();
            if (hub != null && hub == target.ReferenceHub)
                return true;
        }

        return false;
    }

    // ======== 再生制御 (SpeakerApi 利用) ========

    private static void PlayStinger(Player owner, string oggFileName)
    {
        try
        {
            // 音源位置はプレイヤーの足元あたり
            var pos = owner.Position;

            // 1 回だけ鳴らすので Play() を使用
            SpeakerApi.Play(
                fileName: oggFileName,
                audioPlayerName: $"LookStinger_{owner.Id}",
                position: pos,
                destroyOnEnd: true,
                parent: null,
                isSpatial: false, // 全員同じ音量にしたいなら false
                maxDistance: 50f,
                minDistance: 0f,
                loadClip: true);

            Log.Debug($"[LookSoundApi] PlayStinger: {owner.Nickname} -> {oggFileName}");
        }
        catch (Exception ex)
        {
            Log.Error($"[LookSoundApi] PlayStinger error: {ex}");
        }
    }

    private static void StartChase(LookSession session, string oggFileName, bool loop)
    {
        var owner = session.Owner;
        try
        {
            var pos = owner.Position;

            // チェイステーマはプレイヤーに追従させたいので、SpeakerApi 側で親をプレイヤーにするのもあり
            var playback = SpeakerApi.PlayLoop(
                fileName: oggFileName,
                audioPlayerName: $"LookChase_{owner.Id}",
                position: pos,
                parent: null,
                isSpatial: false,
                maxDistance: 60f,
                minDistance: 0f,
                loadClip: true,
                speakerName: "LookChaseTheme",
                clipName: null,
                restartIfAlreadyPlaying: true);

            session.CurrentChasePlayback = playback;

            Log.Debug($"[LookSoundApi] StartChase: {owner.Nickname} -> {oggFileName}");
        }
        catch (Exception ex)
        {
            Log.Error($"[LookSoundApi] StartChase error: {ex}");
        }
    }

    private static void StopChaseInternal(LookSession session)
    {
        try
        {
            if (session.CurrentChasePlayback is { } pb && pb.IsValid)
            {
                SpeakerApi.Stop(pb);
                session.CurrentChasePlayback = null;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[LookSoundApi] StopChaseInternal error: {ex}");
        }
    }
}
