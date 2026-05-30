using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles.FirstPersonControl.Thirdperson.Subcontrollers.Wearables;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.CustomItems;

/// <summary>
/// NVG ライトの挙動をアイテムごとに定義するプロファイル。
/// </summary>
public readonly struct NvgProfile
{
    /// <summary>1秒あたりの電池消費量（%）。0 以下で無限。</summary>
    public float DrainPerSecond { get; init; }
    public Color LightColor     { get; init; }
    public float LightRange     { get; init; }
    public float LightIntensity { get; init; }
    /// <summary>true のとき、電池切れで Blindness を最大強度で付与する。</summary>
    public bool  UseBlackout    { get; init; }

    public static NvgProfile Default => new()
    {
        DrainPerSecond = 1.85f,
        LightColor     = new Color(0.6f, 1f, 0.6f),
        LightRange     = 30f,
        LightIntensity = 10000f,
        UseBlackout    = true,
    };
}

/// <summary>
/// NVG のライト・視界効果とバッテリー管理を行うマネージャ。
/// NetworkVisibilityManager の Owner / Spectator API を使って可視管理を行う。
/// </summary>
public static class NvgManager
{
    private const float MaxBattery   = 100f;
    private const float TickInterval = 0.1f;

    private static readonly Dictionary<ushort, NvgRuntimeData> ActiveData  = new();
    private static readonly Dictionary<ushort, float>          BatteryData = new();

    // --------------------------------------------------------
    // イベント登録 / 解除
    // --------------------------------------------------------

    public static void Register()
    {
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Player.ChangingRole  += OnChangingRole;
        Exiled.Events.Handlers.Player.Died          += OnDied;
        // Verified / ChangingSpectatedPlayer / Spawned は NetworkVisibilityManager が処理するため不要
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Player.ChangingRole  -= OnChangingRole;
        Exiled.Events.Handlers.Player.Died          -= OnDied;
        ClearRuntimeState(clearBattery: true);
    }

    // --------------------------------------------------------
    // イベントハンドラ
    // --------------------------------------------------------

    private static void OnRoundStarted()
        => ClearRuntimeState(clearBattery: true);

    private static void ClearRuntimeState(bool clearBattery)
    {
        foreach (var data in ActiveData.Values)
            KillRuntimeData(data);
        ActiveData.Clear();
        if (clearBattery)
            BatteryData.Clear();
    }

    private static void OnChangingRole(ChangingRoleEventArgs ev)
    {
        if (ev?.Player == null) return;
        StopNvgByOwner(ev.Player, clearBattery: false);
    }

    private static void OnDied(DiedEventArgs ev)
    {
        if (ev?.Player == null) return;
        StopNvgByOwner(ev.Player, clearBattery: false);
    }

    // --------------------------------------------------------
    // 公開 API
    // --------------------------------------------------------

    /// <summary>NVG を起動する。プロファイル未指定時は NvgProfile.Default を使用。</summary>
    public static void StartNvg(Player player, ushort serial, NvgProfile? profile = null)
    {
        if (player == null) return;

        var prof = profile ?? NvgProfile.Default;
        Log.Debug($"[NvgManager] StartNvg: {player.Nickname} serial={serial} drain={prof.DrainPerSecond}/s");

        bool isInfinite = prof.DrainPerSecond <= 0f;

        // 電池0%なら起動しない（無限電池は常に起動可）
        if (!isInfinite && BatteryData.TryGetValue(serial, out var savedBattery) && savedBattery <= 0f)
        {
            player.ShowHint("このNVGの電池は完全に切れています。", 3f);
            Log.Debug($"[NvgManager] StartNvg拒否: 電池切れ serial={serial}");
            return;
        }

        // 電池を残したまま既存のライトだけ破棄する
        StopNvgBySerial(serial, clearBattery: false);

        float battery = isInfinite ? MaxBattery
                      : BatteryData.TryGetValue(serial, out var saved) ? saved : MaxBattery;

        var light = CreateNvgLight(player, prof);
        if (light == null)
        {
            Log.Error($"[NvgManager] StartNvg: NVGライト生成失敗 ({player.Nickname})");
            return;
        }

        var data = new NvgRuntimeData
        {
            Serial          = serial,
            OwnerId         = player.Id,
            NvgLight        = light,
            Profile         = prof,
            CoroutineHandle = Timing.RunCoroutine(BatteryLoop(player, serial)),
        };

        BatteryData[serial] = battery;
        ActiveData[serial]  = data;

        // InitShowState の CallDelayed(0f) と同フレームになるよう、観戦者同期も遅らせる
        Timing.CallDelayed(0f, () =>
        {
            if (player == null || !player.IsConnected) return;
            SyncSpectatorsForOwner(player, light.Base?.netIdentity, show: true);
        });
    }

    /// <summary>NVG を停止する。電池残量は保持される。</summary>
    public static void StopNvg(Player player, ushort serial)
    {
        if (player == null) return;
        StopNvgBySerial(serial, clearBattery: false);
    }

    // --------------------------------------------------------
    // 内部停止処理
    // --------------------------------------------------------

    /// <param name="clearBattery">true = 電池データも削除 / false = ライトのみ破棄</param>
    private static void StopNvgBySerial(ushort serial, bool clearBattery)
    {
        if (!ActiveData.TryGetValue(serial, out var data)) return;

        // 破棄前に所有者の観戦者へ明示 Hide を送る
        // （SafeDestroy → NetworkServer.Destroy で Mirror が Hide するが、
        //   RemoveShowState で管理から外れた後にタイミング差で再表示される場合の保険）
        if (Player.TryGet(data.OwnerId, out var owner))
        {
            SyncSpectatorsForOwner(owner, data.NvgLight?.Base?.netIdentity, show: false);
        }

        ActiveData.Remove(serial);
        KillRuntimeData(data);

        if (clearBattery)
            BatteryData.Remove(serial);
    }

    private static void StopNvgByOwner(Player player, bool clearBattery)
    {
        if (player == null) return;

        var entry = ActiveData.Values.FirstOrDefault(d => d.OwnerId == player.Id);
        if (entry == null) return;

        StopNvgBySerial(entry.Serial, clearBattery);
    }

    // --------------------------------------------------------
    // 観戦者同期ユーティリティ
    // --------------------------------------------------------

    /// <summary>
    /// owner を現在観戦中のプレイヤー全員に対して identity の表示を同期する。
    /// StartNvg / StopNvg 時の即時補正に使用。
    /// </summary>
    private static void SyncSpectatorsForOwner(Player owner, Mirror.NetworkIdentity? identity, bool show)
    {
        if (owner == null || identity == null) return;

        foreach (var spectator in Player.List)
        {
            if (spectator == null || !spectator.IsConnected || spectator.IsAlive) continue;
            if (!spectator.CurrentSpectatingPlayers.Any(s => s?.Id == owner.Id)) continue;

            if (show) spectator.ShowNetworkIdentity(identity);
            else      spectator.HideNetworkIdentity(identity);
        }
    }

    // --------------------------------------------------------
    // ライト生成
    // --------------------------------------------------------

    private static Light? CreateNvgLight(Player player, NvgProfile prof)
    {
        try
        {
            var light = Light.Create(
                player.CameraTransform.position,
                player.Rotation.eulerAngles,
                null,
                spawn: true);

            if (light?.Base == null || light.Base.netIdentity == null)
            {
                Log.Error("[NvgManager] CreateNvgLight: Light または netIdentity が null");
                return null;
            }

            light.Range     = prof.LightRange;
            light.Intensity = prof.LightIntensity;
            light.Color     = prof.LightColor;
            light.Transform.SetParent(player.Transform, true);

            // Spawn 直後は Mirror の送信がフレームをまたぐため、
            // 1フレーム後に InitShowState を送ることで ShowForConnection が確実に届く
            Timing.CallDelayed(0f, () =>
            {
                if (light?.Base == null) return;
                light.InitShowState(new NetworkShowState
                {
                    OwnerId             = player.Id,
                    ShowToOwner         = true,
                    SpectatorVisibility = SpectatorVisibility.Show,
                });
            });

            return light;
        }
        catch (Exception ex)
        {
            Log.Error($"[NvgManager] CreateNvgLight 例外: {ex}");
            return null;
        }
    }

    // --------------------------------------------------------
    // バッテリーループ
    // --------------------------------------------------------

    private static void KillRuntimeData(NvgRuntimeData data)
    {
        if (data == null) return;

        Timing.KillCoroutines(data.CoroutineHandle);

        data.NvgLight?.SafeDestroy();
        data.NvgLight = null;
    }

    private static IEnumerator<float> BatteryLoop(Player player, ushort serial)
    {
        while (true)
        {
            yield return Timing.WaitForSeconds(TickInterval);

            if (player == null || !player.IsConnected) yield break;
            if (!ActiveData.TryGetValue(serial, out var data)) yield break;

            // インベントリからアイテムが消えていたら強制停止
            // （コマンドによるクリア・その他の異常経路でアイテムが失われた場合の保険）
            if (!player.Items.Any(i => i.Serial == serial))
            {
                Log.Debug($"[NvgManager] BatteryLoop: serial={serial} がインベントリから消えた。強制停止。");
                player.ReferenceHub.DisableWearables(WearableElements.Scp1344Goggles);
                StopNvgBySerial(serial, clearBattery: false);
                yield break;
            }

            var prof = data.Profile;

            // 無限電池はそのまま継続
            if (prof.DrainPerSecond <= 0f) continue;

            float battery = BatteryData.TryGetValue(serial, out var b) ? b : 0f;

            if (battery <= 0f)
            {
                BatteryData[serial] = 0f;

                // 観戦者にライト消灯を明示 Hide してから破棄
                SyncSpectatorsForOwner(player, data.NvgLight?.Base?.netIdentity, show: false);
                data.NvgLight?.SafeDestroy();
                data.NvgLight = null;

                if (prof.UseBlackout)
                    player.EnableEffect<Blindness>(255, 2.5f);

                player.ShowHint("NVGの電池が切れた…視界が真っ暗になった。", 5f);

                StopNvgBySerial(serial, clearBattery: true);
                yield break;
            }

            // 電池消費
            float drain = prof.DrainPerSecond * TickInterval;
            battery = Math.Max(0f, battery - drain);
            BatteryData[serial] = battery;

            // 残量に応じて輝度を落とす
            if (data.NvgLight?.Base != null)
            {
                float ratio = battery / MaxBattery;
                data.NvgLight.Intensity = prof.LightIntensity * (0.4f + 0.6f * ratio);
            }

            player.ShowHint($"NVG電池: {(int)battery}%", 1f);
        }
    }

    private class NvgRuntimeData
    {
        public ushort          Serial          { get; set; }
        public int             OwnerId         { get; set; }
        public Light?          NvgLight        { get; set; }
        public CoroutineHandle CoroutineHandle { get; set; }
        public NvgProfile      Profile         { get; set; }
    }
}
