using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.CustomMaps.Core;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Features;

/// <summary>
/// SCP-012 (Theta Primed) 周辺にいるプレイヤーに「ラジオ使用不可」効果を付与するハンドラ（CustomMap 連動版）。
/// </summary>
public static class Scp012_033
{
    // 012 の影響を受けているプレイヤー一覧
    public static readonly Dictionary<Player, bool> Effecteds = new();

    // コルーチンハンドル（MoreEffectiveCoroutinesのstruct）
    private static CoroutineHandle _coroutineHandle;

    // イベント登録済みフラグ
    private static bool _registered;

    /// <summary>
    /// Plugin.OnEnabled などで 1 回だけ呼ぶ。イベント登録のみ行う。
    /// </summary>
    public static void Register()
    {
        if (_registered)
            return;

        _registered = true;

        Exiled.Events.Handlers.Player.Left += OnLeft;
        Exiled.Events.Handlers.Player.UsingRadioBattery += ThetaPrimeEffect;
    }

    /// <summary>
    /// Plugin.OnDisabled などで呼ぶ。イベント解除と状態クリア。
    /// </summary>
    public static void Unregister()
    {
        if (!_registered)
            return;

        _registered = false;

        Exiled.Events.Handlers.Player.Left -= OnLeft;
        Exiled.Events.Handlers.Player.UsingRadioBattery -= ThetaPrimeEffect;

        Stop();
        Effecteds.Clear();
    }

    /// <summary>
    /// CustomMapMainHandler 側から、Scp012_t が取得できたタイミングで呼び出すスタート処理。
    /// </summary>
    public static void Start()
    {
        if (!_registered)
            Register();

        // 既存のコルーチンがあれば止める
        if (_coroutineHandle.IsRunning)
            Timing.KillCoroutines(_coroutineHandle);

        // 012 の Schematic がまだ無いなら起動しない
        if (CustomMapMainHandler.Scp012_t == null)
        {
            Log.Error("[Scp012_033] Start() が呼ばれたが CustomMapMainHandler.Scp012_t が null です。");
            return;
        }

        _coroutineHandle = Timing.RunCoroutine(ThetaPrimeCoroutine());
    }

    /// <summary>
    /// ラウンド終了・マップリセットなどで外から安全に止めたいとき用。
    /// </summary>
    public static void Stop()
    {
        if (_coroutineHandle.IsRunning)
            Timing.KillCoroutines(_coroutineHandle);

        _coroutineHandle = default;
    }

    // =====================
    // イベントハンドラ
    // =====================

    private static void OnLeft(LeftEventArgs ev)
    {
        Effecteds.Remove(ev.Player);
    }

    private static void ThetaPrimeEffect(UsingRadioBatteryEventArgs ev)
    {
        if (Effecteds.TryGetValue(ev.Player, out var effected) && effected)
        {
            ev.IsAllowed = false;
            ev.Radio.IsEnabled = false;
            ev.Player.ShowHint("...?", 1.5f);
        }
    }

    // =====================
    // コルーチン本体
    // =====================

    private static IEnumerator<float> ThetaPrimeCoroutine()
    {
        // Start() 時点では null じゃない前提だが、念のため再チェック
        if (CustomMapMainHandler.Scp012_t == null)
        {
            Log.Error("[Scp012_033] ThetaPrimeCoroutine 開始時に Scp012_t が null でした。中止します。");
            yield break;
        }

        var scp012Obj = CustomMapMainHandler.Scp012_t;

        while (true)
        {
            // ラウンドが終わっていたら終了
            if (!Round.InProgress)
            {
                Effecteds.Clear();
                yield break;
            }

            // オブジェクトが破棄されていないか確認
            if (scp012Obj == null || scp012Obj.gameObject == null)
            {
                Log.Error("[Scp012_033] Scp012 オブジェクトが破棄されたためコルーチンを終了します。");
                Effecteds.Clear();
                yield break;
            }

            var scp012Pos = scp012Obj.Position;

            var alivePlayers = Player.List
                .Where(p => p.ReferenceHub != null && !p.IsHost && !p.ReferenceHub.IsHost && p.IsAlive)
                .ToList();

            foreach (var player in alivePlayers)
            {
                bool inRange = Vector3.Distance(scp012Pos, player.Position) <= 5.5f;
                Effecteds[player] = inRange;
            }

            // 切断済みプレイヤーを掃除
            foreach (var kvp in Effecteds.Where(kvp => kvp.Key?.ReferenceHub == null).ToList())
                Effecteds.Remove(kvp.Key);

            yield return Timing.WaitForSeconds(0.5f);
        }
    }
}

